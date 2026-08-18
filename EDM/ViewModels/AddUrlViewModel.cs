using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;
using EDM.Views;

namespace EDM.ViewModels
{
    public enum AddUrlAnalysisState
    {
        Idle,
        Analyzing,
        Detected,
        NoMediaDetected,
        Unsupported,
        Failed,
        Timeout
    }

    /// <summary>
    /// ViewModel for the Add URL dialog window.
    /// Manages URL validation, real media & direct-file stream analysis,
    /// dynamic quality/format resolution, save path selection, double-download prevention,
    /// and unified DownloadRequest generation.
    /// </summary>
    public partial class AddUrlViewModel : ViewModelBase
    {
        public event Action<bool>? RequestClose;

        [ObservableProperty]
        private string? url = string.Empty;

        [ObservableProperty]
        private string? savePath;

        [ObservableProperty]
        private string? selectedFormat;

        [ObservableProperty]
        private string? selectedQuality;

        [ObservableProperty]
        private string? selectedCategory = "General";

        [ObservableProperty]
        private bool autoStartDownload = true;

        [ObservableProperty]
        private bool autoRouteCategoryFolder = true;

        [ObservableProperty]
        private bool isAnalyzing = false;

        [ObservableProperty]
        private bool isSubmitting = false;

        [ObservableProperty]
        private AddUrlAnalysisState analysisState = AddUrlAnalysisState.Idle;

        [ObservableProperty]
        private string? analysisStatus;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private string? resourceTitle;

        [ObservableProperty]
        private string? resourceSizeText;

        [ObservableProperty]
        private bool isMediaResource = false;

        [ObservableProperty]
        private bool resumeSupported = true;

        public ObservableCollection<string> AvailableFormats { get; } = new();
        public ObservableCollection<string> AvailableQualities { get; } = new();
        public ObservableCollection<string> AvailableCategories { get; } = new(new[]
        {
            "General", "Video", "Audio", "Documents", "Programs", "Compressed", "Others"
        });

        public DownloadItem? CreatedDownloadItem { get; private set; }

        private readonly IFileDialogService _fileDialogService;
        private readonly MediaVariantResolver _mediaResolver;
        private readonly HttpProbeService _probeService;
        private List<MediaVariantOption> _detectedVariants = new();
        private CancellationTokenSource? _analysisCts;

        public AddUrlViewModel(IFileDialogService? fileDialogService = null, MediaVariantResolver? mediaResolver = null, HttpProbeService? probeService = null)
        {
            _fileDialogService = fileDialogService ?? new FileDialogService();
            _mediaResolver = mediaResolver ?? new MediaVariantResolver();
            _probeService = probeService ?? new HttpProbeService();

            try
            {
                SavePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads"
                );
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[AddUrlViewModel] Failed to initialize save path", ex);
            }

            ResetFormatsAndQualities();
        }

        public void ResetFormatsAndQualities()
        {
            AvailableFormats.Clear();
            AvailableFormats.Add("Auto Detect");
            SelectedFormat = AvailableFormats[0];

            AvailableQualities.Clear();
            AvailableQualities.Add("Original / Default Stream");
            SelectedQuality = AvailableQualities[0];

            ResourceTitle = null;
            ResourceSizeText = null;
            IsMediaResource = false;
            AnalysisState = AddUrlAnalysisState.Idle;
        }

        /// <summary>
        /// Asynchronously analyzes media streams or direct HTTP/FTP files from URL and populates real available formats and qualities.
        /// </summary>
        [RelayCommand]
        public async Task AnalyzeMediaAsync()
        {
            if (IsAnalyzing) return;

            ErrorMessage = null;
            if (!AddUrlWindow.ValidateUrlInput(Url, out string normalizedUrl, out string error))
            {
                ErrorMessage = error;
                AnalysisState = AddUrlAnalysisState.Unsupported;
                return;
            }

            IsAnalyzing = true;
            AnalysisState = AddUrlAnalysisState.Analyzing;
            AnalysisStatus = "Inspecting stream & remote server capabilities...";

            _analysisCts?.Cancel();
            _analysisCts?.Dispose();
            _analysisCts = new CancellationTokenSource(TimeSpan.FromSeconds(12));

            try
            {
                bool isStreaming = IsStreamingOrManifestUrl(normalizedUrl);

                if (isStreaming)
                {
                    SelectedCategory = "Video";
                    var result = await _mediaResolver.ResolveVariantsAsync(normalizedUrl, cancellationToken: _analysisCts.Token).ConfigureAwait(true);

                    if (result.Success && result.Variants != null && result.Variants.Count > 0)
                    {
                        _detectedVariants = result.Variants;
                        IsMediaResource = true;
                        ResourceTitle = result.Title;
                        AnalysisState = AddUrlAnalysisState.Detected;

                        AvailableQualities.Clear();
                        foreach (var v in result.Variants)
                        {
                            AvailableQualities.Add(v.FormattedDetails);
                        }
                        SelectedQuality = AvailableQualities[0];

                        AvailableFormats.Clear();
                        var containers = result.Variants.Select(v => v.Container.ToUpperInvariant()).Distinct().ToList();
                        if (containers.Count == 0) containers.Add("MP4");
                        foreach (var c in containers) AvailableFormats.Add($"{c} Container");
                        SelectedFormat = AvailableFormats[0];

                        SelectedCategory = "Video";
                        AnalysisStatus = $"✓ Verified {result.Variants.Count} media stream(s): {result.Title}";
                    }
                    else
                    {
                        AnalysisState = AddUrlAnalysisState.NoMediaDetected;
                        SetDirectStreamFallback(normalizedUrl, "No specialized media streams found. Ready for direct file download.");
                    }
                }
                else
                {
                    // Direct file probe
                    try
                    {
                        var probeResult = await _probeService.ProbeUrlAsync(normalizedUrl, string.Empty, cancellationToken: _analysisCts.Token).ConfigureAwait(true);
                        ResumeSupported = probeResult.ServerSupportsResume;
                        ResourceTitle = probeResult.InferredFileName;
                        
                        if (probeResult.TotalBytes.HasValue && probeResult.TotalBytes.Value > 0)
                        {
                            ResourceSizeText = FormatBytes(probeResult.TotalBytes.Value);
                        }
                        else
                        {
                            ResourceSizeText = "Size: Unknown";
                        }

                        // Determine category
                        var catRule = DownloadCategoryRouter.Instance.DetermineCategory(probeResult.InferredFileName, probeResult.ContentType, normalizedUrl);
                        if (catRule != null)
                        {
                            SelectedCategory = catRule.Name;
                        }

                        AvailableFormats.Clear();
                        string ext = Path.GetExtension(probeResult.InferredFileName).TrimStart('.').ToUpperInvariant();
                        AvailableFormats.Add(!string.IsNullOrEmpty(ext) ? $"{ext} File" : "Binary File");
                        SelectedFormat = AvailableFormats[0];

                        AvailableQualities.Clear();
                        AvailableQualities.Add($"Direct Download ({ResourceSizeText})");
                        SelectedQuality = AvailableQualities[0];

                        AnalysisState = AddUrlAnalysisState.Detected;
                        AnalysisStatus = $"✓ Direct file verified: {probeResult.InferredFileName} ({ResourceSizeText})";
                    }
                    catch (Exception probeEx)
                    {
                        LoggingService.Log($"[AddUrlViewModel] Probe fallback: {probeEx.Message}");
                        SetDirectStreamFallback(normalizedUrl, "Direct file stream ready.");
                        AnalysisState = AddUrlAnalysisState.Detected;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AnalysisState = AddUrlAnalysisState.Timeout;
                AnalysisStatus = "Analysis timed out. You can still start direct download.";
                SetDirectStreamFallback(normalizedUrl, "Direct stream (Default)");
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[AddUrlViewModel] Analysis error: {ex.Message}");
                AnalysisState = AddUrlAnalysisState.Failed;
                AnalysisStatus = $"Stream verification: {ex.Message}";
                SetDirectStreamFallback(normalizedUrl, "Direct stream fallback ready.");
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        private void SetDirectStreamFallback(string url, string statusMsg)
        {
            AvailableQualities.Clear();
            AvailableQualities.Add("Direct Stream (Original)");
            SelectedQuality = AvailableQualities[0];

            AvailableFormats.Clear();
            AvailableFormats.Add("Direct File");
            SelectedFormat = AvailableFormats[0];

            AnalysisStatus = statusMsg;
        }

        private static bool IsStreamingOrManifestUrl(string url)
        {
            return Regex.IsMatch(url, @"youtube\.com|youtu\.be|vimeo\.com|dailymotion\.com|twitch\.tv|twitter\.com|x\.com|tiktok\.com|instagram\.com|\.m3u8|\.mpd", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Command to validate and start the download immediately, creating authoritative DownloadItem and preventing double submissions.
        /// </summary>
        [RelayCommand]
        public void StartDownload()
        {
            if (IsSubmitting) return;

            ErrorMessage = null;
            if (!AddUrlWindow.ValidateUrlInput(Url, out string normalizedUrl, out string error))
            {
                ErrorMessage = error;
                return;
            }

            IsSubmitting = true;

            // Generate filename and save path
            string targetFolder = !string.IsNullOrWhiteSpace(SavePath) ? SavePath : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string fileName = !string.IsNullOrWhiteSpace(ResourceTitle) ? ResourceTitle : ("EDM_Download_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            try
            {
                var uri = new Uri(normalizedUrl);
                string pathFileName = Path.GetFileName(uri.AbsolutePath);
                if (string.IsNullOrWhiteSpace(ResourceTitle))
                {
                    if (!string.IsNullOrWhiteSpace(pathFileName) && pathFileName.Contains("."))
                    {
                        fileName = pathFileName;
                    }
                    else if (normalizedUrl.Contains("youtube.com") || normalizedUrl.Contains("youtu.be"))
                    {
                        fileName = $"YouTube_Video_{Guid.NewGuid():N}.mp4";
                    }
                    else
                    {
                        fileName = "download.dat";
                    }
                }
            }
            catch
            {
                if (string.IsNullOrWhiteSpace(fileName)) fileName = "download.dat";
            }

            // Route category folder if selected
            if (AutoRouteCategoryFolder && !string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "General")
            {
                targetFolder = Path.Combine(targetFolder, SelectedCategory);
            }

            string fullPath = Path.Combine(targetFolder, fileName);

            CreatedDownloadItem = new DownloadItem
            {
                FileName = fileName,
                Url = normalizedUrl,
                SavePath = fullPath,
                Category = SelectedCategory ?? "General",
                Status = AutoStartDownload ? "Downloading" : "Queued",
                Progress = 0,
                LastTryDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Size = ResourceSizeText ?? "0 B",
                TransferRate = "0 B/s"
            };

            // Attach detected variant if available
            if (_detectedVariants != null && _detectedVariants.Count > 0)
            {
                var selectedVar = _detectedVariants.FirstOrDefault(v => v.FormattedDetails == SelectedQuality) ?? _detectedVariants[0];
                CreatedDownloadItem.VideoUrl = !string.IsNullOrWhiteSpace(selectedVar.DirectUrl) ? selectedVar.DirectUrl : normalizedUrl;
                CreatedDownloadItem.AudioUrl = selectedVar.AudioStreamUrl ?? string.Empty;
                CreatedDownloadItem.RequiresFfmpegMerge = selectedVar.RequiresFfmpegMerge;
                CreatedDownloadItem.Quality = selectedVar.QualityLabel;
                CreatedDownloadItem.EstimatedSizeBytes = selectedVar.EstimatedSizeBytes;
                CreatedDownloadItem.Container = selectedVar.Container;
                CreatedDownloadItem.Codec = selectedVar.Codec;
                CreatedDownloadItem.AudioCodec = selectedVar.AudioCodec;
                CreatedDownloadItem.IsAudioOnly = selectedVar.IsAudioOnly;
                if (selectedVar.EstimatedSizeBytes > 0)
                {
                    CreatedDownloadItem.Size = FormatBytes(selectedVar.EstimatedSizeBytes);
                }
            }

            RequestClose?.Invoke(true);
        }

        [RelayCommand]
        public void DownloadLater()
        {
            AutoStartDownload = false;
            StartDownload();
        }

        [RelayCommand]
        public void Cancel()
        {
            _analysisCts?.Cancel();
            RequestClose?.Invoke(false);
        }

        [RelayCommand]
        public void Browse()
        {
            var selectedFile = _fileDialogService.OpenFile("All Files (*.*)|*.*", SavePath);
            if (!string.IsNullOrWhiteSpace(selectedFile))
            {
                SavePath = Path.GetDirectoryName(selectedFile) ?? selectedFile;
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

