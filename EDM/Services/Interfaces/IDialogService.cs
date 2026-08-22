using System;

namespace EDM.Services.Interfaces
{
    public interface IDialogService
    {
        /// <summary>
        /// Shows an informational message to the user.
        /// </summary>
        void ShowMessage(string title, string message);

        /// <summary>
        /// Shows a confirmation dialog with Yes/No and returns true if the user confirmed (Yes).
        /// </summary>
        bool Confirm(string title, string message);
    }
}
