using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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

            bool launchGui = ExtractGuiFlag(ref args);
            args = CliProgram.ExtractDebugLogFlag(args, out string debugLogFile);

            if (launchGui || args.Length == 0)
            {
                using (debugLogFile != null ? CliProgram.DebugLogSession.Create(debugLogFile) : null)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Form1());
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
