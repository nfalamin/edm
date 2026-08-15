using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using EDM.Services;

namespace EDM.ViewModels
{
    /// <summary>
    /// ViewModel for the Add URL dialog window.
    /// Manages URL input, save path selection, format/quality selection, and dialog actions.
    /// Uses CommunityToolkit MVVM for property change notification.
    /// </summary>
    public partial class AddUrlViewModel : ViewModelBase
    {
        /// <summary>
        /// Raised when the dialog should close with a specific result.
        /// </summary>
        public event Action<bool>? RequestClose;

        /// <summary>
        /// The URL entered by the user.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Available file formats for download.
        /// </summary>
        public ObservableCollection<string> AvailableFormats { get; } = new ObservableCollection<string>(new[] { "MP4", "MP3", "ZIP", "EXE", "DOCX" });

        /// <summary>
        /// Available quality options for downloads.
        /// </summary>
        public ObservableCollection<string> AvailableQualities { get; } = new ObservableCollection<string>(new[] { "Best Available" });

        /// <summary>
        /// Path where the downloaded file will be saved.
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string? savePath;

        /// <summary>
        /// Currently selected file format.
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string? selectedFormat;

        /// <summary>
        /// Currently selected quality option.
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string? selectedQuality;

        private readonly EDM.Services.Interfaces.IFileDialogService _fileDialogService;

        public AddUrlViewModel(EDM.Services.Interfaces.IFileDialogService? fileDialogService = null)
        {
            _fileDialogService = fileDialogService ?? new FileDialogService();

            try
            {
                SavePath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads",
                    "EDM"
                );
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[AddUrlViewModel] Failed to initialize save path", ex);
            }

            // Default selections
            SelectedFormat = AvailableFormats.Count > 0 ? AvailableFormats[0] : null;
            SelectedQuality = AvailableQualities.Count > 0 ? AvailableQualities[0] : null;
        }

        /// <summary>
        /// Command to start the download immediately.
        /// </summary>
        [RelayCommand]
        private void StartDownload()
        {
            RequestClose?.Invoke(true);
        }

        /// <summary>
        /// Command to schedule the download for later.
        /// </summary>
        [RelayCommand]
        private void DownloadLater()
        {
            RequestClose?.Invoke(true);
        }

        /// <summary>
        /// Command to cancel the download dialog.
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }

        /// <summary>
        /// Command to browse for a download location.
        /// </summary>
        [RelayCommand]
        private void Browse()
        {
            var selectedFile = _fileDialogService.OpenFile("All Files (*.*)|*.*", SavePath);
            if (!string.IsNullOrWhiteSpace(selectedFile))
            {
                SavePath = selectedFile;
            }
        }
    }
}
