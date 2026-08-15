using System;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace EDM.Services
{
    /// <summary>
    /// Service for displaying user-friendly error messages while logging detailed exception information.
    /// Ensures exceptions are never silently swallowed - always logged with full context.
    /// </summary>
    public static class ErrorDialogService
    {
        /// <summary>
        /// Shows an error dialog to the user with a friendly message while logging the full exception.
        /// </summary>
        public static void ShowError(string context, Exception exception, string userMessage, string title = "Error", bool showDetails = false)
        {
            if (exception == null)
                exception = new InvalidOperationException("Unknown error occurred");

            // Always log the full exception with context
            LoggingService.LogException(context, exception);

            // Prepare user-friendly message
            string displayMessage = userMessage;
            if (showDetails && !string.IsNullOrEmpty(exception.Message))
            {
                displayMessage += $"\n\nDetails: {exception.Message}";
            }

            // Show to user
            try
            {
                MessageBox.Show(
                    displayMessage,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ErrorDialogService] MessageBox failed: {ex}"); }
        }

        /// <summary>
        /// Shows a warning dialog with optional logging.
        /// </summary>
        public static void ShowWarning(string context, string userMessage, string title = "Warning")
        {
            LoggingService.LogWarning($"[{context}] {userMessage}");

            try
            {
                MessageBox.Show(
                    userMessage,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ErrorDialogService] MessageBox failed: {ex}"); }
        }

        /// <summary>
        /// Shows an info dialog with optional logging.
        /// </summary>
        public static void ShowInfo(string context, string userMessage, string title = "Information")
        {
            LoggingService.Log($"[{context}] {userMessage}");

            try
            {
                MessageBox.Show(
                    userMessage,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ErrorDialogService] MessageBox failed: {ex}"); }
        }

        /// <summary>
        /// Shows a confirmation dialog and returns the result.
        /// </summary>
        public static bool? ShowConfirm(string context, string userMessage, string title = "Confirm", MessageBoxButton buttons = MessageBoxButton.YesNo)
        {
            try
            {
                var result = MessageBox.Show(
                    userMessage,
                    title,
                    buttons,
                    MessageBoxImage.Question);

                LoggingService.Log($"[{context}] User confirmed: {result}");
                return (bool)(result == MessageBoxResult.Yes || result == MessageBoxResult.OK);
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"{context}.ShowConfirm", ex);
                return null;
            }
        }

        /// <summary>
        /// Executes an action and displays errors if they occur.
        /// </summary>
        public static void TryExecute(string context, Action action, string errorMessage = "An error occurred")
        {
            try
            {
                action?.Invoke();
            }
            catch (OperationCanceledException)
            {
                LoggingService.LogWarning($"[{context}] Operation cancelled");
            }
            catch (Exception ex)
            {
                ShowError(context, ex, errorMessage);
            }
        }

        /// <summary>
        /// Executes an async action and displays errors if they occur.
        /// </summary>
        public static async System.Threading.Tasks.Task TryExecuteAsync(string context, Func<System.Threading.Tasks.Task> action, string errorMessage = "An error occurred")
        {
            try
            {
                if (action != null)
                    await action().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                LoggingService.LogWarning($"[{context}] Operation cancelled");
            }
            catch (Exception ex)
            {
                ShowError(context, ex, errorMessage);
            }
        }

        /// <summary>
        /// Executes an async action with return value and displays errors if they occur.
        /// </summary>
        public static async System.Threading.Tasks.Task<T?> TryExecuteAsync<T>(string context, Func<System.Threading.Tasks.Task<T>> action, string errorMessage = "An error occurred") where T : class
        {
            try
            {
                if (action != null)
                    return await action().ConfigureAwait(false);
                return null;
            }
            catch (OperationCanceledException)
            {
                LoggingService.LogWarning($"[{context}] Operation cancelled");
                return null;
            }
            catch (Exception ex)
            {
                ShowError(context, ex, errorMessage);
                return null;
            }
        }
    }
}
