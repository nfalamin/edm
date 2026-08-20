using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Threading;
using EDM.Services;

namespace EDM.Models
{
    public class DownloadItem : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Backing fields
        private string _fileName = string.Empty;
        private string _url = string.Empty;
        private string _savePath = string.Empty;
        private string _category = "General";
        private string _desiredFormat = string.Empty;
        private string _quality = string.Empty;
        private string _status = string.Empty;
        private string _size = string.Empty;
        private string _timeLeft = string.Empty;
        private string _transferRate = string.Empty;
        private string _lastTryDate = string.Empty;
        private string _description = string.Empty;
        private double _progress = 0.0;
        private string _authUsername = string.Empty;
        private string _authPassword = string.Empty;
        private string _cookies = string.Empty;
        private bool _isSelected = false;

        private long _downloadedBytes = 0;
        private long _totalBytes = 0;

        // MediaDownloadJob properties
        private string _title = string.Empty;
        private string _manifestUrl = string.Empty;
        private string _audioCodec = string.Empty;
        private string _videoUrl = string.Empty;
        private string _audioUrl = string.Empty;
        private bool _requiresFfmpegMerge = false;
        private string _formatArg = string.Empty;
        private string _downloadIdentity = string.Empty;
        private long _estimatedSizeBytes = 0;
        private string _codec = string.Empty;
        private string _container = string.Empty;
        private bool _isAudioOnly = false;
        private string _pageUrl = string.Empty;
        private string _referer = string.Empty;
        private string _userAgent = string.Empty;
        private string _authHeader = string.Empty;
        private string _postData = string.Empty;

        public string FileName { get => _fileName; set { _fileName = value ?? string.Empty; OnPropertyChanged(nameof(FileName)); } }
        public string Title { get => _title; set { _title = value ?? string.Empty; OnPropertyChanged(nameof(Title)); } }
        public string Url { get => _url; set { _url = value ?? string.Empty; OnPropertyChanged(nameof(Url)); } }
        public string SavePath { get => _savePath; set { _savePath = value ?? string.Empty; OnPropertyChanged(nameof(SavePath)); } }
        public string Category { get => _category; set { _category = value ?? "General"; OnPropertyChanged(nameof(Category)); } }
        public string DesiredFormat { get => _desiredFormat; set { _desiredFormat = value ?? string.Empty; OnPropertyChanged(nameof(DesiredFormat)); } }
        public string Quality { get => _quality; set { _quality = value ?? string.Empty; OnPropertyChanged(nameof(Quality)); } }
        public string Status { get => _status; set { _status = value ?? string.Empty; OnPropertyChanged(nameof(Status)); } }
        public string Size { get => _size; set { _size = value ?? string.Empty; OnPropertyChanged(nameof(Size)); } }
        public long DownloadedBytes { get => _downloadedBytes; set { _downloadedBytes = value; OnPropertyChanged(nameof(DownloadedBytes)); } }
        public long TotalBytes { get => _totalBytes; set { _totalBytes = value; OnPropertyChanged(nameof(TotalBytes)); } }
        public string TimeLeft { get => _timeLeft; set { _timeLeft = value ?? string.Empty; OnPropertyChanged(nameof(TimeLeft)); } }
        public string TransferRate { get => _transferRate; set { _transferRate = value ?? string.Empty; OnPropertyChanged(nameof(TransferRate)); } }
        public string LastTryDate { get => _lastTryDate; set { _lastTryDate = value ?? string.Empty; OnPropertyChanged(nameof(LastTryDate)); } }
        public string Description { get => _description; set { _description = value ?? string.Empty; OnPropertyChanged(nameof(Description)); } }

        public string ManifestUrl { get => _manifestUrl; set { _manifestUrl = value ?? string.Empty; OnPropertyChanged(nameof(ManifestUrl)); } }
        public string AudioCodec { get => _audioCodec; set { _audioCodec = value ?? string.Empty; OnPropertyChanged(nameof(AudioCodec)); } }
        public string VideoUrl { get => _videoUrl; set { _videoUrl = value ?? string.Empty; OnPropertyChanged(nameof(VideoUrl)); } }
        public string AudioUrl { get => _audioUrl; set { _audioUrl = value ?? string.Empty; OnPropertyChanged(nameof(AudioUrl)); } }
        public bool RequiresFfmpegMerge { get => _requiresFfmpegMerge; set { _requiresFfmpegMerge = value; OnPropertyChanged(nameof(RequiresFfmpegMerge)); } }
        public string FormatArg { get => _formatArg; set { _formatArg = value ?? string.Empty; OnPropertyChanged(nameof(FormatArg)); } }
        public string DownloadIdentity { get => _downloadIdentity; set { _downloadIdentity = value ?? string.Empty; OnPropertyChanged(nameof(DownloadIdentity)); } }
        public long EstimatedSizeBytes { get => _estimatedSizeBytes; set { _estimatedSizeBytes = value; OnPropertyChanged(nameof(EstimatedSizeBytes)); } }
        public string Codec { get => _codec; set { _codec = value ?? string.Empty; OnPropertyChanged(nameof(Codec)); } }
        public string Container { get => _container; set { _container = value ?? string.Empty; OnPropertyChanged(nameof(Container)); } }
        public bool IsAudioOnly { get => _isAudioOnly; set { _isAudioOnly = value; OnPropertyChanged(nameof(IsAudioOnly)); } }
        public string PageUrl { get => _pageUrl; set { _pageUrl = value ?? string.Empty; OnPropertyChanged(nameof(PageUrl)); } }
        public string Referer { get => _referer; set { _referer = value ?? string.Empty; OnPropertyChanged(nameof(Referer)); } }
        public string UserAgent { get => _userAgent; set { _userAgent = value ?? string.Empty; OnPropertyChanged(nameof(UserAgent)); } }
        [JsonIgnore]
        public string AuthHeader { get => _authHeader; set { _authHeader = value ?? string.Empty; OnPropertyChanged(nameof(AuthHeader)); } }

        /// <summary>DPAPI encrypted authorization header for storage at rest.</summary>
        public string EncryptedAuthHeader
        {
            get => string.IsNullOrWhiteSpace(_authHeader) ? string.Empty : EDM.Services.ProxyService.EncryptPassword(_authHeader);
            set => _authHeader = string.IsNullOrWhiteSpace(value) ? string.Empty : EDM.Services.ProxyService.DecryptPassword(value);
        }

        [JsonIgnore]
        public string PostData { get => _postData; set { _postData = value ?? string.Empty; OnPropertyChanged(nameof(PostData)); } }

        /// <summary>DPAPI encrypted POST data for storage at rest.</summary>
        public string EncryptedPostData
        {
            get => string.IsNullOrWhiteSpace(_postData) ? string.Empty : EDM.Services.ProxyService.EncryptPassword(_postData);
            set => _postData = string.IsNullOrWhiteSpace(value) ? string.Empty : EDM.Services.ProxyService.DecryptPassword(value);
        }

        /// <summary>Optional session cookies captured from browser extension for authenticated downloads.</summary>
        [JsonIgnore]
        public string Cookies { get => _cookies; set { _cookies = value ?? string.Empty; OnPropertyChanged(nameof(Cookies)); } }

        /// <summary>DPAPI encrypted cookie representation for storage at rest.</summary>
        public string EncryptedCookies
        {
            get => string.IsNullOrWhiteSpace(_cookies) ? string.Empty : EDM.Services.ProxyService.EncryptPassword(_cookies);
            set => _cookies = string.IsNullOrWhiteSpace(value) ? string.Empty : EDM.Services.ProxyService.DecryptPassword(value);
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(nameof(Progress)); }
        }

        /// <summary>
        /// UI-specific: Whether this download row is selected in the table
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        /// <summary>Optional HTTP Basic-auth username for links behind a login wall (IDM calls this "Authorization").</summary>
        public string AuthUsername { get => _authUsername; set { _authUsername = value ?? string.Empty; OnPropertyChanged(nameof(AuthUsername)); } }
        /// <summary>Optional HTTP Basic-auth password. Kept in memory only for the lifetime of this item; not persisted to history in plain text.</summary>
        [JsonIgnore]
        public string AuthPassword { get => _authPassword; set { _authPassword = value ?? string.Empty; OnPropertyChanged(nameof(AuthPassword)); } }

        /// <summary>Builds a DownloadCredentials instance from AuthUsername/AuthPassword, or null when no username was set.</summary>
        public DownloadCredentials? BuildCredentials() => DownloadCredentials.FromInput(AuthUsername, AuthPassword);

        // ==================== RUNTIME CONTROL TOKENS (not persisted) ====================

        /// <summary>
        /// Live pause/resume controller for this download's active task.
        /// Lazily created — never null after first access.
        /// [JsonIgnore] so it is not serialised to disk or sent over the wire.
        /// </summary>
        [JsonIgnore]
        public PauseTokenSource PauseSource { get; } = new PauseTokenSource();

        /// <summary>
        /// Cancellation source used to hard-stop this download's active task.
        /// Replace with a fresh instance before re-starting after a Stop.
        /// [JsonIgnore] so it is not serialised to disk.
        /// </summary>
        [JsonIgnore]
        private CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>Gets the current CancellationToken for this download's active task.</summary>
        [JsonIgnore]
        public CancellationToken CancellationToken => _cts.Token;

        /// <summary>
        /// Active background execution Task (if running).
        /// </summary>
        [JsonIgnore]
        public Task? ActiveDownloadTask { get; set; }

        /// <summary>Cancels the current token and replaces with a fresh one for the next start.</summary>
        public void CancelAndReset()
        {
            var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
            try { old.Cancel(); } catch { /* already disposed */ }
            old.Dispose();
        }

        /// <summary>
        /// Gracefully waits for any active download task to exit.
        /// </summary>
        public async Task WaitForCompletionAsync(TimeSpan timeout)
        {
            var task = ActiveDownloadTask;
            if (task == null || task.IsCompleted) return;

            try
            {
                using var timeoutCts = new CancellationTokenSource(timeout);
                await task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch { /* task cancelled or timed out */ }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        // Verification metadata
        private VerificationState _verificationState = VerificationState.Pending;
        private string? _verificationAlgorithm;
        private string? _trustedVerificationHash;
        private string? _computedVerificationHash;
        private string? _verificationMessage;
        private DateTime? _verificationTimestamp;

        public VerificationState VerificationState { get => _verificationState; set { _verificationState = value; OnPropertyChanged(nameof(VerificationState)); } }
        public string? VerificationAlgorithm { get => _verificationAlgorithm; set { _verificationAlgorithm = value; OnPropertyChanged(nameof(VerificationAlgorithm)); } }
        public string? TrustedVerificationHash { get => _trustedVerificationHash; set { _trustedVerificationHash = value; OnPropertyChanged(nameof(TrustedVerificationHash)); } }
        public string? ComputedVerificationHash { get => _computedVerificationHash; set { _computedVerificationHash = value; OnPropertyChanged(nameof(ComputedVerificationHash)); } }
        public string? VerificationMessage { get => _verificationMessage; set { _verificationMessage = value; OnPropertyChanged(nameof(VerificationMessage)); } }
        public DateTime? VerificationTimestamp { get => _verificationTimestamp; set { _verificationTimestamp = value; OnPropertyChanged(nameof(VerificationTimestamp)); } }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
