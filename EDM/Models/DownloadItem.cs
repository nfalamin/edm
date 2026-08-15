using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Threading;
using EDM.Services;

namespace EDM.Models
{
    public class DownloadItem : INotifyPropertyChanged
    {
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

        public string FileName { get => _fileName; set { _fileName = value ?? string.Empty; OnPropertyChanged(nameof(FileName)); } }
        public string Url { get => _url; set { _url = value ?? string.Empty; OnPropertyChanged(nameof(Url)); } }
        public string SavePath { get => _savePath; set { _savePath = value ?? string.Empty; OnPropertyChanged(nameof(SavePath)); } }
        public string Category { get => _category; set { _category = value ?? "General"; OnPropertyChanged(nameof(Category)); } }
        public string DesiredFormat { get => _desiredFormat; set { _desiredFormat = value ?? string.Empty; OnPropertyChanged(nameof(DesiredFormat)); } }
        public string Quality { get => _quality; set { _quality = value ?? string.Empty; OnPropertyChanged(nameof(Quality)); } }
        public string Status { get => _status; set { _status = value ?? string.Empty; OnPropertyChanged(nameof(Status)); } }
        public string Size { get => _size; set { _size = value ?? string.Empty; OnPropertyChanged(nameof(Size)); } }
        public string TimeLeft { get => _timeLeft; set { _timeLeft = value ?? string.Empty; OnPropertyChanged(nameof(TimeLeft)); } }
        public string TransferRate { get => _transferRate; set { _transferRate = value ?? string.Empty; OnPropertyChanged(nameof(TransferRate)); } }
        public string LastTryDate { get => _lastTryDate; set { _lastTryDate = value ?? string.Empty; OnPropertyChanged(nameof(LastTryDate)); } }
        public string Description { get => _description; set { _description = value ?? string.Empty; OnPropertyChanged(nameof(Description)); } }

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

        /// <summary>Cancels the current token and replaces with a fresh one for the next start.</summary>
        public void CancelAndReset()
        {
            var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
            try { old.Cancel(); } catch { /* already disposed */ }
            old.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        // Verification metadata
        private VerificationState _verificationState = VerificationState.Pending;
        private string? _verificationAlgorithm;
        private string? _trustedVerificationHash;
        private string? _computedVerificationHash;
        private DateTime? _verificationTimestamp;

        public VerificationState VerificationState { get => _verificationState; set { _verificationState = value; OnPropertyChanged(nameof(VerificationState)); } }
        public string? VerificationAlgorithm { get => _verificationAlgorithm; set { _verificationAlgorithm = value; OnPropertyChanged(nameof(VerificationAlgorithm)); } }
        public string? TrustedVerificationHash { get => _trustedVerificationHash; set { _trustedVerificationHash = value; OnPropertyChanged(nameof(TrustedVerificationHash)); } }
        public string? ComputedVerificationHash { get => _computedVerificationHash; set { _computedVerificationHash = value; OnPropertyChanged(nameof(ComputedVerificationHash)); } }
        public DateTime? VerificationTimestamp { get => _verificationTimestamp; set { _verificationTimestamp = value; OnPropertyChanged(nameof(VerificationTimestamp)); } }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
