using System;
using AsusFanControl.Core;

namespace AsusFanControl
{
    internal static class Program
    {
        // Shared flag to control watchdog behavior
        static bool skipResetOnExit = false;
        // Shared flag to indicate if manual disposal happened
        static bool isDisposed = false;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: AsusFanControl <args>");
                Console.WriteLine("\t--get-fan-speeds");
                Console.WriteLine("\t--set-fan-speeds=0-100 (percent value, 0 for turning off test mode)");
                Console.WriteLine("\t--get-fan-count");
                Console.WriteLine("\t--get-fan-speed=fanId (comma separated)");
                Console.WriteLine("\t--set-fan-speed=fanId:0-100 (comma separated, percent value, 0 for turning off test mode)");
                Console.WriteLine("\t--get-cpu-temp");
                return 1;
            }

            // Using Interface type if possible, but strict dependency is fine too.
            IFanController asusControl = new AsusControl();

            // Watchdog: Ensure fans are reset to default on exit or crash
            // UNLESS the user explicitly requested to set the fan speed and exit.
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                // Only reset if we haven't already disposed (which implies we finished main normally)
                // and if we haven't set the flag to skip reset.
                if (!isDisposed && !skipResetOnExit)
                {
                    try { asusControl.ResetToDefault(); } catch {}
                }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (!skipResetOnExit)
                {
                    try { asusControl.ResetToDefault(); } catch {}
                }
                // We might crash hard after this, but try to clean up.
                try { asusControl.Dispose(); } catch {}
            };

            try
            {
                foreach (var arg in args)
                {
                    if (arg.StartsWith("--get-fan-speeds"))
                    {
                        var fanSpeeds = asusControl.GetFanSpeeds();
                        Console.WriteLine($"Current fan speeds: {string.Join(" ", fanSpeeds)} RPM");
                    }

                    if (arg.StartsWith("--set-fan-speeds"))
                    {
                        skipResetOnExit = true; // User wants this speed to stick

                        var parts = arg.Split('=');
                        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
                        {
                            Console.WriteLine("Error: Invalid format for --set-fan-speeds. Usage: --set-fan-speeds=50");
                            continue;
                        }

                        if (int.TryParse(parts[1], out int newSpeed))
                        {
                            asusControl.SetFanSpeeds(newSpeed);

                            if(newSpeed == 0)
                                Console.WriteLine("Test mode turned off");
                            else
                                Console.WriteLine($"New fan speeds: {newSpeed}%");
                        }
                        else
                        {
                            Console.WriteLine($"Error: Invalid number format for speed: {parts[1]}");
                        }
                    }

                    if (arg.StartsWith("--get-fan-speed="))
                    {
                        var parts = arg.Split('=');
                        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
                        {
                             Console.WriteLine("Error: Invalid format. Usage: --get-fan-speed=0,1");
                             continue;
                        }

                        var fanIds = parts[1].Split(',');
                        foreach (var fanIdStr in fanIds)
                        {
                            if (int.TryParse(fanIdStr, out int fanId))
                            {
                                if (fanId >= 0 && fanId <= 255)
                                {
                                    var fanSpeed = asusControl.GetFanSpeed((byte)fanId);
                                    Console.WriteLine($"Current fan speed for fan {fanId}: {fanSpeed} RPM");
                                }
                                else
                                {
                                    Console.WriteLine($"Error: fan id must be between 0 and 255: {fanId}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Error: Invalid fan ID: {fanIdStr}");
                            }
                        }
                    }

                    if (arg.StartsWith("--get-fan-count"))
                    {
                        var fanCount = asusControl.HealthyTable_FanCounts();
                        Console.WriteLine($"Fan count: {fanCount}");
                    }

                    if (arg.StartsWith("--set-fan-speed="))
                    {
                        skipResetOnExit = true; // User wants this speed to stick

                        var parts = arg.Split('=');
                        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
                        {
                             Console.WriteLine("Error: Invalid format. Usage: --set-fan-speed=0:50,1:100");
                             continue;
                        }

                        var fanSettings = parts[1].Split(',');
                        foreach (var fanSetting in fanSettings)
                        {
                            var settingParts = fanSetting.Split(':');
                            if (settingParts.Length == 2 &&
                                int.TryParse(settingParts[0], out int fanId) &&
                                int.TryParse(settingParts[1], out int fanSpeed))
                            {
                                if (fanId >= 0 && fanId <= 255)
                                {
                                    asusControl.SetFanSpeed(fanSpeed, (byte)fanId);

                                    if (fanSpeed == 0)
                                        Console.WriteLine($"Test mode turned off for fan {fanId}");
                                    else
                                        Console.WriteLine($"New fan speed for fan {fanId}: {fanSpeed}%");
                                }
                                else
                                {
                                    Console.WriteLine($"Error: fan id must be between 0 and 255: {fanId}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Error: Invalid fan setting format: {fanSetting}");
                            }
                        }
                    }

                    if (arg.StartsWith("--get-cpu-temp"))
                    {
                        var cpuTemp = asusControl.Thermal_Read_Cpu_Temperature();
                        Console.WriteLine($"Current CPU temp: {cpuTemp}");
                    }
                }
            }
            finally
            {
                // Normal exit cleanup
                if (!skipResetOnExit) asusControl.ResetToDefault();

                // Mark as disposed so ProcessExit doesn't try to use it
                isDisposed = true;
                asusControl.Dispose();
            }

            return 0;
        }
    }
}
