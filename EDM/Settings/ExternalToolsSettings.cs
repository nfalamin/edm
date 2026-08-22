using System;
using System.IO;

namespace EDM.Settings
{
    public class ExternalToolsSettings
    {
        public string? Aria2Path { get; set; }
        public string? YtDlpPath { get; set; }
        public string? FfmpegPath { get; set; }
        public string? DefaultFormatArgs { get; set; }

        public bool IsAria2Available() => !string.IsNullOrWhiteSpace(Aria2Path) && File.Exists(Aria2Path);
        public bool IsYtDlpAvailable() => !string.IsNullOrWhiteSpace(YtDlpPath) && File.Exists(YtDlpPath);
        public bool IsFfmpegAvailable() => !string.IsNullOrWhiteSpace(FfmpegPath) && File.Exists(FfmpegPath);

        public void EnsureDirectories()
        {
            // placeholder in case we want to ensure folders for output, config, etc.
        }

        public string GetSafeFormatArg() => DefaultFormatArgs ?? string.Empty;
    }
}
