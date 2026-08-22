using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using EDM.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfCursors = System.Windows.Input.Cursors;
using WpfClipboard = System.Windows.Clipboard;
using WpfMessageBox = System.Windows.MessageBox;

namespace EDM.Views
{
    public partial class PrivacyPolicyWindow : Window
    {
        private readonly PrivacyPolicyContent _policy;
        private readonly Dictionary<string, FrameworkElement> _sectionElements = new();
        private string? _selectedSectionId = null;

        public PrivacyPolicyWindow()
        {
            InitializeComponent();
            _policy = PrivacyPolicyContent.Instance;

            PolicyVersionText.Text = $"Policy Version {_policy.PolicyVersion}";
            LastUpdatedText.Text = $"Last Updated: {_policy.LastUpdatedDate}";

            PopulateTOC();
            DisplaySections(_policy.GetSections());
        }

        private void PopulateTOC()
        {
            TOCPanel.Children.Clear();

            // All Sections
            var allBtn = CreateTOCButton("all", "All Sections (Full Document)", "📜");
            TOCPanel.Children.Add(allBtn);

            foreach (var sec in _policy.GetSections())
            {
                var btn = CreateTOCButton(sec.Id, sec.Title, sec.Icon);
                TOCPanel.Children.Add(btn);
            }
        }

        private WpfButton CreateTOCButton(string sectionId, string title, string icon)
        {
            var btn = new WpfButton
            {
                Tag = sectionId,
                Background = WpfBrushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = WpfCursors.Hand,
                Margin = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(0)
            };

            bool isSelected = string.Equals(_selectedSectionId, sectionId, StringComparison.OrdinalIgnoreCase) ||
                              (_selectedSectionId == null && sectionId == "all");

            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 7, 10, 7),
                Background = isSelected ? (WpfBrush)FindResource("NavHoverBg") : WpfBrushes.Transparent
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

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
                Text = title,
                FontSize = 11.5,
                Foreground = isSelected ? (WpfBrush)FindResource("AccentBrush") : (WpfBrush)FindResource("SidebarTextBrush"),
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(labelText, 1);
            grid.Children.Add(labelText);

            border.Child = grid;
            btn.Content = border;

            btn.Click += (s, e) =>
            {
                _selectedSectionId = sectionId;
                PopulateTOC();

                if (sectionId == "all")
                {
                    PolicyScrollViewer.ScrollToTop();
                }
                else if (_sectionElements.TryGetValue(sectionId, out var targetEl))
                {
                    targetEl.BringIntoView();
                }
            };

            return btn;
        }

        private void DisplaySections(IEnumerable<PolicySection> sections)
        {
            PolicySectionsPanel.Children.Clear();
            _sectionElements.Clear();

            foreach (var sec in sections)
            {
                var card = new Border
                {
                    Background = (WpfBrush)FindResource("CardInputBg"),
                    BorderBrush = (WpfBrush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(18, 16, 18, 16),
                    Margin = new Thickness(0, 0, 0, 14)
                };

                var stack = new StackPanel();

                // Header
                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var iconBlock = new TextBlock
                {
                    Text = sec.Icon,
                    FontSize = 16,
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(iconBlock, 0);
                headerGrid.Children.Add(iconBlock);

                var titleBlock = new TextBlock
                {
                    Text = sec.Title,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = (WpfBrush)FindResource("PrimaryTextBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(titleBlock, 1);
                headerGrid.Children.Add(titleBlock);

                stack.Children.Add(headerGrid);

                // Content
                var contentBlock = new TextBlock
                {
                    Text = sec.Content,
                    FontSize = 12.5,
                    Foreground = (WpfBrush)FindResource("SecondaryTextBrush"),
                    Margin = new Thickness(0, 10, 0, 10),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 19
                };
                stack.Children.Add(contentBlock);

                // Key Points
                if (sec.KeyPoints != null && sec.KeyPoints.Count > 0)
                {
                    var pointsPanel = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
                    foreach (var pt in sec.KeyPoints)
                    {
                        var ptRow = new TextBlock
                        {
                            Text = $"• {pt}",
                            FontSize = 12,
                            Foreground = (WpfBrush)FindResource("PrimaryTextBrush"),
                            Margin = new Thickness(0, 2, 0, 2),
                            TextWrapping = TextWrapping.Wrap
                        };
                        pointsPanel.Children.Add(ptRow);
                    }
                    stack.Children.Add(pointsPanel);
                }

                card.Child = stack;
                _sectionElements[sec.Id] = card;
                PolicySectionsPanel.Children.Add(card);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.Trim();
            SearchWatermark.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
            ClearSearchBtn.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;

            if (string.IsNullOrEmpty(query))
            {
                DisplaySections(_policy.GetSections());
            }
            else
            {
                var matches = _policy.Search(query);
                DisplaySections(matches);
            }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
        }

        private void CopyPolicy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Exclusive Download Manager (EDM) Privacy Policy & Legal Agreements ===");
                sb.AppendLine($"Policy Version: {_policy.PolicyVersion}");
                sb.AppendLine($"Last Updated: {_policy.LastUpdatedDate}");
                sb.AppendLine();

                foreach (var s in _policy.GetSections())
                {
                    sb.AppendLine($"--- {s.Title} ---");
                    sb.AppendLine(s.Content);
                    if (s.KeyPoints.Count > 0)
                    {
                        foreach (var k in s.KeyPoints)
                        {
                            sb.AppendLine($"  * {k}");
                        }
                    }
                    sb.AppendLine();
                }

                WpfClipboard.SetText(sb.ToString());
                WpfMessageBox.Show("Complete privacy policy and terms copied to clipboard.", "EDM Privacy Center", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Failed to copy policy: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
