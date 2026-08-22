using System;
using System.Collections.Generic;

namespace EDM.Services
{
    public class ParsedCommandLineOptions
    {
        public string? DownloadUrl { get; set; }
        public string? SavePath { get; set; }
        public string? LocalFileName { get; set; }
        public bool SilentMode { get; set; }
        public bool AddToQueueOnly { get; set; }
        public bool StartQueueImmediately { get; set; }
        public bool ShowHelp { get; set; }
        public bool ShowVersion { get; set; }
    }

    /// <summary>
    /// Standard Universal Command Line Arguments Parser for EDM.
    /// Supports standard download execution switches:
    ///   /d "URL"       - Download URL
    ///   /p "LocalPath" - Local save directory path
    ///   /f "FileName"  - Local file name
    ///   /q             - Add to queue silently without showing start download dialog
    ///   /n             - Quiet mode (no dialogs)
    ///   /a             - Add to queue in paused state without starting immediately
    ///   /s             - Start download queue processing
    /// </summary>
    public static class CommandLineArgsParser
    {
        public static ParsedCommandLineOptions Parse(string[] args)
        {
            var options = new ParsedCommandLineOptions();
            if (args == null || args.Length == 0) return options;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].Trim();
                if (arg.Equals("/d", StringComparison.OrdinalIgnoreCase) || arg.Equals("-d", StringComparison.OrdinalIgnoreCase) || arg.Equals("--download", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        options.DownloadUrl = args[++i].Trim('\"');
                    }
                }
                else if (arg.Equals("/p", StringComparison.OrdinalIgnoreCase) || arg.Equals("-p", StringComparison.OrdinalIgnoreCase) || arg.Equals("--path", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        options.SavePath = args[++i].Trim('\"');
                    }
                }
                else if (arg.Equals("/f", StringComparison.OrdinalIgnoreCase) || arg.Equals("-f", StringComparison.OrdinalIgnoreCase) || arg.Equals("--file", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        options.LocalFileName = args[++i].Trim('\"');
                    }
                }
                else if (arg.Equals("/q", StringComparison.OrdinalIgnoreCase) || arg.Equals("-q", StringComparison.OrdinalIgnoreCase) || arg.Equals("--queue", StringComparison.OrdinalIgnoreCase))
                {
                    options.AddToQueueOnly = true;
                }
                else if (arg.Equals("/n", StringComparison.OrdinalIgnoreCase) || arg.Equals("-n", StringComparison.OrdinalIgnoreCase) || arg.Equals("--silent", StringComparison.OrdinalIgnoreCase))
                {
                    options.SilentMode = true;
                }
                else if (arg.Equals("/a", StringComparison.OrdinalIgnoreCase) || arg.Equals("-a", StringComparison.OrdinalIgnoreCase))
                {
                    options.AddToQueueOnly = true;
                }
                else if (arg.Equals("/s", StringComparison.OrdinalIgnoreCase) || arg.Equals("-s", StringComparison.OrdinalIgnoreCase) || arg.Equals("--start-queue", StringComparison.OrdinalIgnoreCase))
                {
                    options.StartQueueImmediately = true;
                }
                else if (arg.Equals("/h", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase) || arg.Equals("/?", StringComparison.OrdinalIgnoreCase) || arg.Equals("--help", StringComparison.OrdinalIgnoreCase))
                {
                    options.ShowHelp = true;
                }
                else if (arg.Equals("/v", StringComparison.OrdinalIgnoreCase) || arg.Equals("-v", StringComparison.OrdinalIgnoreCase) || arg.Equals("--version", StringComparison.OrdinalIgnoreCase))
                {
                    options.ShowVersion = true;
                }
                else if ((arg.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)) && string.IsNullOrEmpty(options.DownloadUrl))
                {
                    options.DownloadUrl = arg.Trim('\"');
                }
            }

            return options;
        }
    }
}
