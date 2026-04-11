using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics; // required for Debug.WriteLine calls

namespace AsusFanControlGUI
{
    internal static class Program
    {
        const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole();

        static void EnsureConsoleForCli()
        {
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
                AllocConsole();
        }

        static bool ExtractGuiFlag(ref string[] args)
        {
            bool found = false;
            var kept = new System.Collections.Generic.List<string>(args.Length);
            foreach (var arg in args)
            {
                if (arg == "--gui")
                {
                    found = true;
                    continue;
                }
                kept.Add(arg);
            }
            args = kept.ToArray();
            return found;
        }

        /// <summary>
        /// GUI when launched with no arguments; CLI when any arguments are present (same binary as releases).
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            if (args == null)
                args = Array.Empty<string>();

            try
            {
                #region agent log
                System.IO.File.AppendAllText("/home/ubuntu/projects/AsusFanControl/.cursor/debug-4df631.log",
                    "{\"sessionId\":\"4df631\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H1\",\"location\":\"Program.cs:50\",\"message\":\"Main entry\",\"data\":{\"argCount\":" + args.Length + ",\"args\":\"" + string.Join(" ", args).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"},\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n");
                #endregion
            }
            catch
            {
            }

            bool launchGui = ExtractGuiFlag(ref args);
            args = CliProgram.ExtractDebugLogFlag(args, out string debugLogFile);

            try
            {
                #region agent log
                System.IO.File.AppendAllText("/home/ubuntu/projects/AsusFanControl/.cursor/debug-4df631.log",
                    "{\"sessionId\":\"4df631\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H1\",\"location\":\"Program.cs:62\",\"message\":\"Arguments parsed\",\"data\":{\"launchGui\":" + (launchGui ? "true" : "false") + ",\"remainingArgCount\":" + args.Length + ",\"debugLogEnabled\":" + (debugLogFile != null ? "true" : "false") + "},\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n");
                #endregion
            }
            catch
            {
            }

        if (launchGui || args.Length == 0)
        {
            CliProgram.DebugLogSession debugSession = null;
            try
            {
                if (debugLogFile != null)
                    debugSession = CliProgram.DebugLogSession.Create(debugLogFile);

                try
                {
                    #region agent log
                    Console.Error.WriteLine("[startup] Entering GUI branch");
                    Debug.WriteLine("[startup] Entering GUI branch");
                    System.IO.File.AppendAllText("/home/ubuntu/projects/AsusFanControl/.cursor/debug-4df631.log",
                        "{\"sessionId\":\"4df631\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H2\",\"location\":\"Program.cs:78\",\"message\":\"Entering GUI branch\",\"data\":{\"debugLogFile\":\"" + (debugLogFile ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"},\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n");
                    #endregion
                }
                catch
                {
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    #region agent log
                    Console.Error.WriteLine("[startup] Before Form1 construction");
                    Debug.WriteLine("[startup] Before Form1 construction");
                    System.IO.File.AppendAllText("/home/ubuntu/projects/AsusFanControl/.cursor/debug-4df631.log",
                        "{\"sessionId\":\"4df631\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H3\",\"location\":\"Program.cs:90\",\"message\":\"Before Form1 construction\",\"data\":{},\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n");
                    #endregion
                }
                catch
                {
                }

                var form = new Form1();

                try
                {
                    #region agent log
                    Console.Error.WriteLine("[startup] Form1 constructed");
                    Debug.WriteLine("[startup] Form1 constructed");
                    System.IO.File.AppendAllText("/home/ubuntu/projects/AsusFanControl/.cursor/debug-4df631.log",
                        "{\"sessionId\":\"4df631\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H3\",\"location\":\"Program.cs:102\",\"message\":\"Form1 constructed\",\"data\":{},\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n");
                    #endregion
                }
                catch
                {
                }

                Application.Run(form);
            }
            catch (Exception ex)
            {
                try
                {
                    #region agent log
                    Console.Error.WriteLine("[startup] GUI startup exception: " + ex);
                    Debug.WriteLine("[startup] GUI startup exception: " + ex);
                    System.IO.File.AppendAllText("/home/ubuntu/projects/AsusFanControl/.cursor/debug-4df631.log",
                        "{\"sessionId\":\"4df631\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H4\",\"location\":\"Program.cs:115\",\"message\":\"GUI startup exception\",\"data\":{\"type\":\"" + ex.GetType().FullName.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"message\":\"" + (ex.Message ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"},\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n");
                    #endregion
                }
                catch
                {
                }
                throw;
            }
            finally
            {
                debugSession?.Dispose();
            }
            return 0;
        }

            EnsureConsoleForCli();
            using (debugLogFile != null ? CliProgram.DebugLogSession.Create(debugLogFile) : null)
            {
                if (debugLogFile != null)
                    Console.WriteLine("[debug-log] Writing diagnostics to: " + System.IO.Path.GetFullPath(debugLogFile));

                EnsureConsoleForCli();
                return CliProgram.Run(args);
            }
        }
    }
}
