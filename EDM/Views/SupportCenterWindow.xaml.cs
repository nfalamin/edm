using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EDM.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfCursors = System.Windows.Input.Cursors;
using WpfMessageBox = System.Windows.MessageBox;

namespace EDM.Views
{
    public partial class SupportCenterWindow : Window
    {
        private readonly SupportKnowledgeBase _kb;
        private int _selectedCategoryId = 0; // 0 = All

        public SupportCenterWindow()
        {
            InitializeComponent();
            _kb = SupportKnowledgeBase.Instance;

            PopulateCategorySidebar();
            DisplayArticles(_kb.GetAllArticles());
        }

        private void PopulateCategorySidebar()
        {
            CategoryListPanel.Children.Clear();

            // "All Topics" item
            var allBtn = CreateCategoryButton(0, "All Help Topics", "📚", _kb.GetAllArticles().Count);
            CategoryListPanel.Children.Add(allBtn);

            foreach (var cat in _kb.GetCategories())
            {
                var btn = CreateCategoryButton(cat.Id, cat.Name, cat.Icon, cat.ArticleCount);
                CategoryListPanel.Children.Add(btn);
            }
        }

        private WpfButton CreateCategoryButton(int categoryId, string name, string icon, int count)
        {
            var btn = new WpfButton
            {
                Tag = categoryId,
                Background = WpfBrushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = WpfCursors.Hand,
                Margin = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(0)
            };

            bool isSelected = _selectedCategoryId == categoryId;

            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Background = isSelected ? (WpfBrush)FindResource("NavHoverBg") : WpfBrushes.Transparent
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 13,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconText, 0);
            grid.Children.Add(iconText);

            var labelText = new TextBlock
            {
                Text = name,
                FontSize = 12,
                Foreground = isSelected ? (WpfBrush)FindResource("AccentBrush") : (WpfBrush)FindResource("SidebarTextBrush"),
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(labelText, 1);
            grid.Children.Add(labelText);

            var countBadge = new Border
            {
                Background = (WpfBrush)FindResource("CardInputBg"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            var countText = new TextBlock
            {
                Text = count.ToString(),
                FontSize = 10,
                Foreground = (WpfBrush)FindResource("SecondaryTextBrush"),
                FontWeight = FontWeights.SemiBold
            };
            countBadge.Child = countText;
            Grid.SetColumn(countBadge, 2);
            grid.Children.Add(countBadge);

            border.Child = grid;
            btn.Content = border;

            btn.Click += (s, e) =>
            {
                _selectedCategoryId = categoryId;
                PopulateCategorySidebar();

                if (categoryId == 0)
                {
                    CurrentCategoryTitle.Text = "All Troubleshooting Guides";
                    CurrentCategoryDesc.Text = "Displaying all 32 EDM troubleshooting and diagnostic topics.";
                    DisplayArticles(_kb.GetAllArticles());
                }
                else
                {
                    var cat = _kb.GetCategories().FirstOrDefault(c => c.Id == categoryId);
                    CurrentCategoryTitle.Text = cat?.Name ?? "Troubleshooting Guide";
                    CurrentCategoryDesc.Text = cat?.Description ?? string.Empty;
                    DisplayArticles(_kb.GetArticlesByCategory(categoryId));
                }

                ShowListView();
            };

            return btn;
        }

        private void DisplayArticles(IEnumerable<SupportArticle> articles)
        {
            ArticleCardsPanel.Children.Clear();
            var list = articles.ToList();

            if (list.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = "No troubleshooting articles matched your search query.",
                    Foreground = (WpfBrush)FindResource("SecondaryTextBrush"),
                    FontSize = 13,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0)
                };
                ArticleCardsPanel.Children.Add(empty);
                return;
            }

            foreach (var art in list)
            {
                var card = new Border
                {
                    Background = (WpfBrush)FindResource("CardInputBg"),
                    BorderBrush = (WpfBrush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14),
                    Margin = new Thickness(0, 0, 0, 10),
                    Cursor = WpfCursors.Hand
                };

                var stack = new StackPanel();

                var topGrid = new Grid();
                topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var title = new TextBlock
                {
                    Text = art.Title,
                    FontWeight = FontWeights.Bold,
                    FontSize = 13.5,
                    Foreground = (WpfBrush)FindResource("PrimaryTextBrush"),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetColumn(title, 0);
                topGrid.Children.Add(title);

                var badge = new Border
                {
                    Background = (WpfBrush)FindResource("NavHoverBg"),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };
                badge.Child = new TextBlock
                {
                    Text = art.CategoryName,
                    FontSize = 10,
                    Foreground = (WpfBrush)FindResource("AccentBrush"),
                    FontWeight = FontWeights.SemiBold
                };
                Grid.SetColumn(badge, 1);
                topGrid.Children.Add(badge);

                stack.Children.Add(topGrid);

                var summary = new TextBlock
                {
                    Text = art.Summary,
                    FontSize = 12,
                    Foreground = (WpfBrush)FindResource("SecondaryTextBrush"),
                    Margin = new Thickness(0, 6, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                stack.Children.Add(summary);

                card.Child = stack;

                card.MouseLeftButtonUp += (s, e) => OpenArticleDetail(art);

                ArticleCardsPanel.Children.Add(card);
            }
        }

        private void OpenArticleDetail(SupportArticle art)
        {
            DetailTitleText.Text = art.Title;
            DetailSummaryText.Text = art.Summary;
            DetailCategoryBadgeText.Text = art.CategoryName;

            // Causes
            DetailCausesPanel.Children.Clear();
            foreach (var cause in art.PossibleCauses)
            {
                var row = new TextBlock
                {
                    Text = $"• {cause}",
                    FontSize = 12,
                    Foreground = (WpfBrush)FindResource("SecondaryTextBrush"),
                    Margin = new Thickness(4, 2, 0, 2),
                    TextWrapping = TextWrapping.Wrap
                };
                DetailCausesPanel.Children.Add(row);
            }

            // Solutions
            DetailSolutionsPanel.Children.Clear();
            foreach (var step in art.StepByStepSolution)
            {
                var row = new TextBlock
                {
                    Text = step,
                    FontSize = 12.5,
                    Foreground = (WpfBrush)FindResource("PrimaryTextBrush"),
                    Margin = new Thickness(4, 3, 0, 3),
                    TextWrapping = TextWrapping.Wrap
                };
                DetailSolutionsPanel.Children.Add(row);
            }

            // What to Check
            DetailCheckPanel.Children.Clear();
            foreach (var chk in art.WhatToCheck)
            {
                var row = new TextBlock
                {
                    Text = $"✓ {chk}",
                    FontSize = 12,
                    Foreground = (WpfBrush)FindResource("SecondaryTextBrush"),
                    Margin = new Thickness(4, 2, 0, 2),
                    TextWrapping = TextWrapping.Wrap
                };
                DetailCheckPanel.Children.Add(row);
            }

            DetailWhenContactText.Text = art.WhenToContactSupport;

            // Related Articles
            RelatedArticlesPanel.Children.Clear();
            foreach (var relId in art.RelatedArticleIds)
            {
                var relArt = _kb.GetArticleById(relId);
                if (relArt != null)
                {
                    var relBtn = new WpfButton
                    {
                        Content = $"🔗 {relArt.Title}",
                        Padding = new Thickness(10, 5, 10, 5),
                        Background = (WpfBrush)FindResource("NavHoverBg"),
                        Foreground = (WpfBrush)FindResource("AccentBrush"),
                        BorderBrush = (WpfBrush)FindResource("BorderBrush"),
                        BorderThickness = new Thickness(1),
                        Cursor = WpfCursors.Hand,
                        Margin = new Thickness(0, 0, 8, 8)
                    };
                    relBtn.Click += (s, e) => OpenArticleDetail(relArt);
                    RelatedArticlesPanel.Children.Add(relBtn);
                }
            }

            ArticleListView.Visibility = Visibility.Collapsed;
            ArticleDetailView.Visibility = Visibility.Visible;
        }

        private void ShowListView()
        {
            ArticleDetailView.Visibility = Visibility.Collapsed;
            ArticleListView.Visibility = Visibility.Visible;
        }

        private void BackToList_Click(object sender, RoutedEventArgs e)
        {
            ShowListView();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.Trim();
            SearchWatermark.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
            ClearSearchBtn.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;

            if (string.IsNullOrEmpty(query))
            {
                if (_selectedCategoryId == 0)
                    DisplayArticles(_kb.GetAllArticles());
                else
                    DisplayArticles(_kb.GetArticlesByCategory(_selectedCategoryId));
            }
            else
            {
                var matches = _kb.Search(query);
                CurrentCategoryTitle.Text = $"Search Results for \"{query}\"";
                CurrentCategoryDesc.Text = $"Found {matches.Count} matching troubleshooting articles.";
                DisplayArticles(matches);
                ShowListView();
            }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
        }

        private void ContactSupport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "mailto:support@exclusive-download-manager.com?subject=EDM%20Technical%20Support%20Request",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch
            {
                WpfMessageBox.Show("Please email our engineering support team at:\nsupport@exclusive-download-manager.com", "Contact EDM Support", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
