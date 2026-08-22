using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EDM.Services;
using EDM.Services.AI;
using EDM.ViewModels;
using WpfButton = System.Windows.Controls.Button;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfMessageBox = System.Windows.MessageBox;

namespace EDM.Views
{
    public partial class AiChatbotWindow : Window
    {
        private readonly OfflineAiChatEngine _aiEngine;
        private readonly AiChatHistoryService _historyService;
        private DownloadManagerViewModel? _viewModel;

        public AiChatbotWindow(DownloadManagerViewModel? vm = null)
        {
            InitializeComponent();
            _viewModel = vm;
            _aiEngine = OfflineAiChatEngine.Instance;
            _historyService = AiChatHistoryService.Instance;

            PopulateQuickPrompts();
            Loaded += async (s, e) => await LoadChatHistoryAsync().ConfigureAwait(true);
        }

        public void SetViewModel(DownloadManagerViewModel vm)
        {
            _viewModel = vm;
        }

        private void PopulateQuickPrompts()
        {
            QuickPromptsPanel.Children.Clear();
            var prompts = _aiEngine.GetDefaultQuickPrompts();

            foreach (var p in prompts)
            {
                var chip = new WpfButton
                {
                    Content = p,
                    Padding = new Thickness(10, 5, 10, 5),
                    Background = (WpfBrush)FindResource("CardInputBg"),
                    Foreground = (WpfBrush)FindResource("PrimaryTextBrush"),
                    BorderBrush = (WpfBrush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(0, 0, 8, 0),
                    FontSize = 11.5
                };
                chip.Resources.Add(typeof(Border), new Style(typeof(Border))
                {
                    Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(14)) }
                });

                string currentPrompt = p;
                chip.Click += (s, e) =>
                {
                    UserInputTextBox.Text = currentPrompt;
                    SendMessage();
                };

                QuickPromptsPanel.Children.Add(chip);
            }
        }

        private async Task LoadChatHistoryAsync()
        {
            MessagesPanel.Children.Clear();
            var history = _historyService.GetHistory();

            if (history.Count == 0)
            {
                // Send initial greeting asynchronously without UI freeze
                var initialResponse = await _aiEngine.ProcessUserPromptAsync(string.Empty, _viewModel).ConfigureAwait(true);
                var botMsg = new AiChatMessage
                {
                    Sender = "Assistant",
                    Content = initialResponse.ReplyText,
                    Timestamp = DateTime.Now,
                    SuggestedFollowUps = initialResponse.SuggestedFollowUps
                };
                _historyService.AddMessage(botMsg);
                AppendMessageBubble(botMsg);
            }
            else
            {
                foreach (var msg in history)
                {
                    AppendMessageBubble(msg);
                }
            }

            ScrollToBottom();
        }

        private void AppendMessageBubble(AiChatMessage msg)
        {
            bool isUser = string.Equals(msg.Sender, "User", StringComparison.OrdinalIgnoreCase);

            var outerGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 12),
                HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left
            };

            var bubbleBorder = new Border
            {
                CornerRadius = isUser ? new CornerRadius(14, 14, 2, 14) : new CornerRadius(14, 14, 14, 2),
                Padding = new Thickness(14, 10, 14, 10),
                MaxWidth = 560
            };

            if (isUser)
            {
                bubbleBorder.Background = new LinearGradientBrush(
                    WpfColor.FromRgb(124, 58, 237),
                    WpfColor.FromRgb(59, 130, 246),
                    new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
            }
            else
            {
                bubbleBorder.Background = (WpfBrush)FindResource("CardInputBg");
                bubbleBorder.BorderBrush = (WpfBrush)FindResource("BorderBrush");
                bubbleBorder.BorderThickness = new Thickness(1);
            }

            var stack = new StackPanel();

            // Header (Icon & Timestamp)
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var senderText = new TextBlock
            {
                Text = isUser ? "You" : "🤖 EDM AI",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = isUser ? WpfBrushes.White : (WpfBrush)FindResource("AccentBrush")
            };
            Grid.SetColumn(senderText, 0);
            headerGrid.Children.Add(senderText);

            var timeText = new TextBlock
            {
                Text = msg.Timestamp.ToString("HH:mm"),
                FontSize = 9.5,
                Foreground = isUser ? new SolidColorBrush(WpfColor.FromArgb(200, 255, 255, 255)) : (WpfBrush)FindResource("SecondaryTextBrush"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            Grid.SetColumn(timeText, 1);
            headerGrid.Children.Add(timeText);

            stack.Children.Add(headerGrid);

            // Message Body Content
            var bodyText = new TextBlock
            {
                Text = msg.Content,
                FontSize = 12.5,
                Foreground = isUser ? WpfBrushes.White : (WpfBrush)FindResource("PrimaryTextBrush"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            };
            stack.Children.Add(bodyText);

            // Interactive Action Button
            if (!string.IsNullOrEmpty(msg.ActionCommand) && !string.IsNullOrEmpty(msg.ActionLabel))
            {
                var actionBtn = new WpfButton
                {
                    Content = msg.ActionLabel,
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = new SolidColorBrush(WpfColor.FromRgb(139, 77, 255)),
                    Foreground = WpfBrushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11.5,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(0, 8, 0, 2),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left
                };
                actionBtn.Resources.Add(typeof(Border), new Style(typeof(Border))
                {
                    Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(8)) }
                });

                string cmd = msg.ActionCommand;
                actionBtn.Click += (s, e) => ExecuteActionCommand(cmd);

                stack.Children.Add(actionBtn);
            }

            bubbleBorder.Child = stack;
            outerGrid.Children.Add(bubbleBorder);
            MessagesPanel.Children.Add(outerGrid);
        }

        private async void SendMessage()
        {
            string prompt = UserInputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(prompt)) return;

            UserInputTextBox.Text = string.Empty;

            // 1. Add User Message
            var userMsg = new AiChatMessage
            {
                Sender = "User",
                Content = prompt,
                Timestamp = DateTime.Now
            };
            _historyService.AddMessage(userMsg);
            AppendMessageBubble(userMsg);
            ScrollToBottom();

            // 2. Process via AI Engine
            SendBtn.IsEnabled = false;
            try
            {
                var reply = await _aiEngine.ProcessUserPromptAsync(prompt, _viewModel).ConfigureAwait(true);

                var botMsg = new AiChatMessage
                {
                    Sender = "Assistant",
                    Content = reply.ReplyText,
                    Timestamp = DateTime.Now,
                    ActionCommand = reply.ActionCommand,
                    ActionLabel = reply.ActionLabel,
                    SuggestedFollowUps = reply.SuggestedFollowUps
                };
                _historyService.AddMessage(botMsg);
                AppendMessageBubble(botMsg);
                ScrollToBottom();
            }
            catch (Exception ex)
            {
                var errorMsg = new AiChatMessage
                {
                    Sender = "Assistant",
                    Content = $"An error occurred while processing: {ex.Message}",
                    Timestamp = DateTime.Now
                };
                AppendMessageBubble(errorMsg);
            }
            finally
            {
                SendBtn.IsEnabled = true;
                UserInputTextBox.Focus();
            }
        }

        private void ExecuteActionCommand(string command)
        {
            try
            {
                switch (command)
                {
                    case "ACTION_RESUME_ALL":
                        _viewModel?.ResumeAll();
                        WpfMessageBox.Show("All paused downloads have been resumed.", "EDM Action", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    case "ACTION_PAUSE_ALL":
                        _viewModel?.PauseAll();
                        WpfMessageBox.Show("All active downloads have been paused.", "EDM Action", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    case "ACTION_OPEN_SETTINGS":
                        var setWin = new SettingsWindow();
                        setWin.Owner = this.Owner ?? this;
                        setWin.ShowDialog();
                        break;
                    case "ACTION_ADD_URL":
                        var addWin = new AddUrlWindow();
                        if (_viewModel != null) addWin.Initialize(_viewModel);
                        addWin.Owner = this.Owner ?? this;
                        addWin.ShowDialog();
                        break;
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Failed to execute action: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ScrollToBottom()
        {
            ChatScrollViewer.ScrollToEnd();
        }

        private void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void UserInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            InputWatermark.Visibility = string.IsNullOrEmpty(UserInputTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UserInputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
            {
                e.Handled = true;
                SendMessage();
            }
        }

        private async void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            _historyService.ClearHistory();
            await LoadChatHistoryAsync().ConfigureAwait(true);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
