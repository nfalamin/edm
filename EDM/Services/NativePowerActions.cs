using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;

namespace EDM.Services
{
    public static class NativePowerActions
    {
        [DllImport("PowrProf.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

        public static void SleepMachine()
        {
            try { SetSuspendState(false, true, true); } catch (Exception ex) { LoggingService.LogException("[NativePowerActions] Sleep failed", ex); }
        }

        public static void HibernateMachine()
        {
            try { SetSuspendState(true, true, true); } catch (Exception ex) { LoggingService.LogException("[NativePowerActions] Hibernate failed", ex); }
        }

        public static void ShutdownMachine()
        {
            try
            {
                const uint EWX_SHUTDOWN = 0x00000008;
                const uint EWX_FORCE = 0x00000004;
                ExitWindowsEx(EWX_SHUTDOWN | EWX_FORCE, 0);
            }
            catch (Exception ex) { LoggingService.LogException("[NativePowerActions] Shutdown failed", ex); }
        }

        public static void RestartMachine()
        {
            try
            {
                const uint EWX_REBOOT = 0x00000002;
                const uint EWX_FORCE = 0x00000004;
                ExitWindowsEx(EWX_REBOOT | EWX_FORCE, 0);
            }
            catch (Exception ex) { LoggingService.LogException("[NativePowerActions] Restart failed", ex); }
        }

        public static void OpenFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
            }
            catch (Exception ex) { LoggingService.LogException("[NativePowerActions] OpenFile failed", ex); }
        }

        public static void OpenFolder(string filePath)
        {
            try
            {
                string? dir = File.Exists(filePath) ? Path.GetDirectoryName(filePath) : filePath;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
            }
            catch (Exception ex) { LoggingService.LogException("[NativePowerActions] OpenFolder failed", ex); }
        }

        public static void PlaySoundNotification()
        {
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch (Exception ex) { LoggingService.LogException("[NativePowerActions] PlaySound failed", ex); }
        }

        public static void ExecuteApplication(string exePath, string args = "")
        {
            try
            {
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo(exePath, args) { UseShellExecute = true });
                }
            }
            catch (Exception ex) { LoggingService.LogException("[NativePowerActions] ExecuteApplication failed", ex); }
        }
    }
}
