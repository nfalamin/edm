using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EDM.Services
{
    /// <summary>
    /// Enterprise Universal File Types & Domain Interception Subsystem.
    /// Manages the registry of intercepted extensions (e.g. 3GP 7Z AAC AVI EXE ISO MP4 ZIP)
    /// and domain exclusion blacklist (e.g. *.update.microsoft.com, *.adobe.com).
    /// </summary>
    public class FileTypesInterceptionManager
    {
        private static readonly Lazy<FileTypesInterceptionManager> _instance = new(() => new FileTypesInterceptionManager());
        public static FileTypesInterceptionManager Instance => _instance.Value;

        private readonly HashSet<string> _interceptedExtensions = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _excludedDomains = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public const string DefaultExtensionsString = "3GP 7Z AAC ACE AIF ARJ ASF AVI BIN BZ2 EXE GZ GZIP IMG ISO LZH M4A M4V MKV MOV MP3 MP4 MPA MPE MPEG MPG MSI MSU OGG OGV PDF PLJ PPS PPT PPTX QT R0* R1* RA RAR RM RMVB SEA SIT SITX TAR TIF TIFF TS WAV WMA WMV Z ZIP";

        public FileTypesInterceptionManager()
        {
            ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            lock (_lock)
            {
                _interceptedExtensions.Clear();
                SetExtensionsFromString(DefaultExtensionsString);

                _excludedDomains.Clear();
                AddExcludedDomain("*.update.microsoft.com");
                AddExcludedDomain("*.windowsupdate.com");
                AddExcludedDomain("*.adobe.com");
                AddExcludedDomain("*.google.com/recaptcha");
                AddExcludedDomain("*.gstatic.com");
            }
        }

        public void SetExtensionsFromString(string rawSpaceDelimited)
        {
            lock (_lock)
            {
                _interceptedExtensions.Clear();
                if (string.IsNullOrWhiteSpace(rawSpaceDelimited)) return;

                var parts = rawSpaceDelimited.Split(new[] { ' ', ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    string ext = part.Trim().TrimStart('.');
                    if (!string.IsNullOrEmpty(ext))
                    {
                        _interceptedExtensions.Add(ext);
                    }
                }
            }
        }

        public string GetExtensionsAsString()
        {
            lock (_lock)
            {
                return string.Join(" ", _interceptedExtensions.OrderBy(x => x));
            }
        }

        public void AddExcludedDomain(string domainPattern)
        {
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(domainPattern))
                {
                    _excludedDomains.Add(domainPattern.Trim());
                }
            }
        }

        public void RemoveExcludedDomain(string domainPattern)
        {
            lock (_lock)
            {
                _excludedDomains.Remove(domainPattern.Trim());
            }
        }

        public IReadOnlyCollection<string> GetExcludedDomains()
        {
            lock (_lock)
            {
                return _excludedDomains.ToList();
            }
        }

        public bool ShouldIntercept(string url, string? fileNameOrExtension = null)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            // 1. Check domain exclusion blacklist
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                string host = uri.Host;
                lock (_lock)
                {
                    foreach (var exc in _excludedDomains)
                    {
                        if (exc.StartsWith("*."))
                        {
                            string root = exc.Substring(2);
                            if (host.EndsWith(root, StringComparison.OrdinalIgnoreCase) || host.Equals(root, StringComparison.OrdinalIgnoreCase))
                            {
                                return false;
                            }
                        }
                        else if (host.Equals(exc, StringComparison.OrdinalIgnoreCase) || url.Contains(exc, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                }
            }

            // 2. Check file extension
            string ext = string.Empty;
            if (!string.IsNullOrWhiteSpace(fileNameOrExtension))
            {
                ext = Path.GetExtension(fileNameOrExtension).TrimStart('.');
            }

            if (string.IsNullOrEmpty(ext) && uri != null)
            {
                try
                {
                    ext = Path.GetExtension(uri.LocalPath).TrimStart('.');
                }
                catch { }
            }

            if (string.IsNullOrEmpty(ext)) return false;

            lock (_lock)
            {
                // Exact extension match
                if (_interceptedExtensions.Contains(ext)) return true;

                // Wildcard match (e.g. R0*, R1*)
                foreach (var pattern in _interceptedExtensions)
                {
                    if (pattern.EndsWith("*") && ext.StartsWith(pattern.TrimEnd('*'), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
