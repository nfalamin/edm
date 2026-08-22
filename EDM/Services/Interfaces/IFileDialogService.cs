using System;

namespace EDM.Services.Interfaces
{
    public interface IFileDialogService
    {
        /// <summary>
        /// Opens a file open dialog and returns the selected file path, or null if cancelled.
        /// </summary>
        /// <param name="filter">The file filter string (e.g. "All files|*.*").</param>
        /// <param name="initialDirectory">Optional initial directory.</param>
        /// <returns>Selected file path or null.</returns>
        string? OpenFile(string? filter = null, string? initialDirectory = null);
    }
}
