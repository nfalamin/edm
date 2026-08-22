using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace EDM.Services
{
    /// <summary>
    /// Manages Windows Explorer context menu registration for "Download with EDM".
    /// Allows runtime enable/disable of context menu entries without reinstalling.
    /// Requires administrative privileges to modify registry.
    /// </summary>
    public class ContextMenuService
    {
        private const string REGISTRY_PATH_STAR = @"Software\Classes\*\shell\DownloadWithEDM";
        private const string REGISTRY_PATH_HTTP = @"Software\Classes\http\shell\DownloadWithEDM";
        private const string REGISTRY_PATH_HTTPS = @"Software\Classes\https\shell\DownloadWithEDM";
        private const string MENU_LABEL = "Download with EDM";

        /// <summary>
        /// Result of a context menu operation.
        /// </summary>
        public class ContextMenuResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public Exception? Exception { get; set; }
        }

        /// <summary>
        /// Gets the full path to the EDM executable.
        /// </summary>
        private static string GetEdmExecutablePath()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().Location;
        }

        /// <summary>
        /// Checks if the context menu is currently registered.
        /// </summary>
        public static bool IsContextMenuActive()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH_STAR))
                {
                    return key != null;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[ContextMenuService.IsContextMenuActive] Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Registers the context menu entries for Files (*), HTTP, and HTTPS.
        /// Requires administrative privileges.
        /// </summary>
        public static ContextMenuResult RegisterContextMenu()
        {
            try
            {
                if (!IsRunningAsAdmin())
                {
                    return new ContextMenuResult
                    {
                        Success = false,
                        Message = "Administrative privileges required. Please run as Administrator."
                    };
                }

                string edmPath = GetEdmExecutablePath();
                if (string.IsNullOrEmpty(edmPath))
                {
                    return new ContextMenuResult
                    {
                        Success = false,
                        Message = "Could not determine EDM executable path."
                    };
                }

                // Register for all files (*)
                RegisterRegistryEntry(REGISTRY_PATH_STAR, edmPath);

                // Register for HTTP URLs
                RegisterRegistryEntry(REGISTRY_PATH_HTTP, edmPath);

                // Register for HTTPS URLs
                RegisterRegistryEntry(REGISTRY_PATH_HTTPS, edmPath);

                LoggingService.Log("[ContextMenuService] Context menu registered successfully.");
                return new ContextMenuResult
                {
                    Success = true,
                    Message = "Context menu registered successfully. Right-click menu updated."
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ContextMenuService.RegisterContextMenu]", ex);
                return new ContextMenuResult
                {
                    Success = false,
                    Message = $"Failed to register context menu: {ex.Message}",
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Unregisters all context menu entries.
        /// Requires administrative privileges.
        /// </summary>
        public static ContextMenuResult UnregisterContextMenu()
        {
            try
            {
                if (!IsRunningAsAdmin())
                {
                    return new ContextMenuResult
                    {
                        Success = false,
                        Message = "Administrative privileges required. Please run as Administrator."
                    };
                }

                // Remove all files (*) entry
                UnregisterRegistryEntry(REGISTRY_PATH_STAR);

                // Remove HTTP entry
                UnregisterRegistryEntry(REGISTRY_PATH_HTTP);

                // Remove HTTPS entry
                UnregisterRegistryEntry(REGISTRY_PATH_HTTPS);

                LoggingService.Log("[ContextMenuService] Context menu unregistered successfully.");
                return new ContextMenuResult
                {
                    Success = true,
                    Message = "Context menu unregistered successfully. Right-click menu updated."
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ContextMenuService.UnregisterContextMenu]", ex);
                return new ContextMenuResult
                {
                    Success = false,
                    Message = $"Failed to unregister context menu: {ex.Message}",
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Toggles context menu registration (on if off, off if on).
        /// </summary>
        public static ContextMenuResult ToggleContextMenu()
        {
            if (IsContextMenuActive())
            {
                return UnregisterContextMenu();
            }
            else
            {
                return RegisterContextMenu();
            }
        }

        /// <summary>
        /// Helper: Register a single registry path for context menu.
        /// </summary>
        private static void RegisterRegistryEntry(string registryPath, string edmPath)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(registryPath))
            {
                if (key != null)
                {
                    // Set menu label
                    key.SetValue("", MENU_LABEL);

                    // Set icon
                    key.SetValue("Icon", $"\"{edmPath}\",0");
                }
            }

            // Set command
            string commandPath = registryPath + @"\command";
            using (var key = Registry.CurrentUser.CreateSubKey(commandPath))
            {
                if (key != null)
                {
                    key.SetValue("", $"\"{edmPath}\" \"%1\"");
                }
            }
        }

        /// <summary>
        /// Helper: Unregister a single registry path.
        /// </summary>
        private static void UnregisterRegistryEntry(string registryPath)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(registryPath, false);
            }
            catch (System.ArgumentException)
            {
                // Key doesn't exist - that's fine
            }
        }

        /// <summary>
        /// Checks if application is running with administrative privileges.
        /// </summary>
        private static bool IsRunningAsAdmin()
        {
            try
            {
                System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Elevates the application to run as Administrator and run a callback.
        /// This should be called when non-admin user tries to modify context menu.
        /// </summary>
        public static bool ElevateAndExecute(Action callback)
        {
            try
            {
                if (IsRunningAsAdmin())
                {
                    callback?.Invoke();
                    return true;
                }

                // Restart application with admin privileges
                var processInfo = new ProcessStartInfo
                {
                    FileName = System.Reflection.Assembly.GetExecutingAssembly().Location,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(processInfo);
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[ContextMenuService.ElevateAndExecute] Failed to elevate: {ex.Message}");
                return false;
            }
        }
    }
}
