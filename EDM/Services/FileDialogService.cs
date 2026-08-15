using Microsoft.Win32;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    public class FileDialogService : IFileDialogService
    {
        public string? OpenFile(string? filter = null, string? initialDirectory = null)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            if (!string.IsNullOrWhiteSpace(filter)) dialog.Filter = filter;
            if (!string.IsNullOrWhiteSpace(initialDirectory)) dialog.InitialDirectory = initialDirectory;

            bool? result = dialog.ShowDialog();
            if (result == true) return dialog.FileName;
            return null;
        }
    }
}
