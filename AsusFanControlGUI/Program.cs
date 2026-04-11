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

        /// <summary>
        /// GUI when launched with no arguments; CLI when any arguments are present (same binary as releases).
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            if (args == null)
                args = Array.Empty<string>();

            if (args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
                return 0;
            }

            args = CliProgram.ExtractDebugLogFlag(args, out string debugLogFile);
            EnsureConsoleForCli();
            using (debugLogFile != null ? CliProgram.DebugLogSession.Create(debugLogFile) : null)
            {
                if (debugLogFile != null)
                    Console.WriteLine("[debug-log] Writing diagnostics to: " + System.IO.Path.GetFullPath(debugLogFile));

                if (args.Length < 1)
                {
                    EnsureConsoleForCli();
                    CliProgram.PrintUsage();
                    return 1;
                }

                EnsureConsoleForCli();
                return CliProgram.Run(args);
            }
        }
    }
}
