using System;
using System.Collections.Concurrent;
using System.IO;
using System.Media;

namespace EDM.Services
{
    public enum SoundEvent
    {
        DownloadCompleted = 1,
        DownloadFailed = 2,
        QueueStarted = 3,
        QueueCompleted = 4,
        DownloadPaused = 5,
        DownloadResumed = 6,
        ConnectionLost = 7,
        ConnectionRestored = 8,
        DownloadError = 9,
        SchedulerStarted = 10,
        SchedulerFinished = 11
    }

    public class SoundConfig
    {
        public bool IsEnabled { get; set; } = true;
        public string? CustomWavPath { get; set; }
    }

    /// <summary>
    /// Audio Notification & Sound Event Mapping Subsystem.
    /// Supports per-event custom .WAV playback, master mute, preview testing, and reset to defaults.
    /// </summary>
    public class SoundNotificationService
    {
        private static readonly Lazy<SoundNotificationService> _instance = new(() => new SoundNotificationService());
        public static SoundNotificationService Instance => _instance.Value;

        private readonly ConcurrentDictionary<SoundEvent, SoundConfig> _configs = new();
        public bool MasterSoundEnabled { get; set; } = true;

        public SoundNotificationService()
        {
            ResetToDefaults();
        }

        public void PlayEvent(SoundEvent evt)
        {
            if (!MasterSoundEnabled) return;

            if (_configs.TryGetValue(evt, out var config) && config.IsEnabled)
            {
                PlaySoundFile(config.CustomWavPath);
            }
        }

        public void PreviewSound(string? wavPath)
        {
            PlaySoundFile(wavPath);
        }

        public void SetCustomSound(SoundEvent evt, string? wavPath, bool enabled = true)
        {
            _configs[evt] = new SoundConfig { CustomWavPath = wavPath, IsEnabled = enabled };
        }

        public void ResetToDefaults()
        {
            foreach (SoundEvent evt in Enum.GetValues(typeof(SoundEvent)))
            {
                _configs[evt] = new SoundConfig { IsEnabled = true, CustomWavPath = null };
            }
        }

        private static void PlaySoundFile(string? path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    using var player = new SoundPlayer(path);
                    player.Play();
                }
                else
                {
                    // Fallback to system sound
                    SystemSounds.Asterisk.Play();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SoundNotificationService] Audio playback error", ex);
            }
        }
    }
}
