using System;
using System.Diagnostics;
using System.IO;

namespace AsusFanControl.Core
{
    public static class TaskSchedulerHelper
    {
        private const string TaskName = "AsusFanControl_AutoStart";

        private static bool WaitForExitSafely(Process proc, int timeoutMs, out int exitCode)
        {
            if (!proc.WaitForExit(timeoutMs))
            {
                try { proc.Kill(); } catch { }
                try { proc.WaitForExit(1000); } catch { }
                exitCode = -1;
                return false;
            }
            exitCode = proc.ExitCode;
            return true;
        }

        public static bool IsTaskRegistered()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Query /TN \"{TaskName}\" /FO CSV /NH",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    if (!WaitForExitSafely(proc, 5000, out int exitCode))
                        return false;
                    return exitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool RegisterTask(string exePath)
        {
            try
            {
                var safePath = Path.GetFullPath(exePath);
                if (!File.Exists(safePath))
                    return false;

                var args = exePath.IndexOfAny(new[] { '"', '\n', '\r', ';', '&', '|', '>', '<' }) >= 0;
                if (args)
                    return false;

                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Create /TN \"{TaskName}\" /TR \"\\\"{safePath}\\\"\" /SC ONLOGON /RL HIGHEST /F",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    if (!WaitForExitSafely(proc, 10000, out int exitCode))
                        return false;
                    return exitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool UnregisterTask()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Delete /TN \"{TaskName}\" /F",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    if (!WaitForExitSafely(proc, 10000, out int exitCode))
                        return false;
                    return exitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
