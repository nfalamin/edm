using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EDM.Services.AI
{
    /// <summary>
    /// Local Chat History & Conversation Storage Service.
    /// Safely persists conversation logs locally in user's AppData directory.
    /// </summary>
    public class AiChatHistoryService
    {
        private static readonly Lazy<AiChatHistoryService> _instance = new(() => new AiChatHistoryService());
        public static AiChatHistoryService Instance => _instance.Value;

        private readonly string _historyFilePath;
        private readonly List<AiChatMessage> _history = new();
        private readonly object _lock = new();
        private const int MaxHistoryLimit = 60;

        public AiChatHistoryService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string edmDir = Path.Combine(appData, "EDM");
            Directory.CreateDirectory(edmDir);
            _historyFilePath = Path.Combine(edmDir, "ai_chat_history.json");

            LoadHistory();
        }

        public IReadOnlyList<AiChatMessage> GetHistory()
        {
            lock (_lock)
            {
                return _history.ToList();
            }
        }

        public void AddMessage(AiChatMessage message)
        {
            lock (_lock)
            {
                _history.Add(message);
                while (_history.Count > MaxHistoryLimit)
                {
                    _history.RemoveAt(0);
                }
                SaveHistory();
            }
        }

        public void ClearHistory()
        {
            lock (_lock)
            {
                _history.Clear();
                SaveHistory();
            }
        }

        private void LoadHistory()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_historyFilePath))
                    {
                        string json = File.ReadAllText(_historyFilePath);
                        var items = JsonSerializer.Deserialize<List<AiChatMessage>>(json);
                        if (items != null)
                        {
                            _history.Clear();
                            _history.AddRange(items);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[AiChatHistoryService] Failed to load history", ex);
                }
            }
        }

        private void SaveHistory()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_history, options);
                File.WriteAllText(_historyFilePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[AiChatHistoryService] Failed to save history", ex);
            }
        }
    }
}
