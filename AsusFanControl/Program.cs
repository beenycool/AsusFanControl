using System;
using AsusFanControl.Core;

namespace AsusFanControl
{
    internal static class Program
    {
        static bool skipResetOnExit = false;
        static bool isDisposed = false;

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

            IFanController asusControl = new AsusControl();

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                if (!isDisposed && !skipResetOnExit)
                {
                    try { asusControl.ResetToDefault(); }
                    catch (Exception ex) { Console.Error.WriteLine($"[ProcessExit] Reset error: {ex.Message}"); }
                }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (!skipResetOnExit)
                {
                    try { asusControl.ResetToDefault(); }
                    catch (Exception ex) { Console.Error.WriteLine($"[UnhandledException] Reset error: {ex.Message}"); }
                }
                try { asusControl.Dispose(); }
                catch (Exception ex) { Console.Error.WriteLine($"[UnhandledException] Dispose error: {ex.Message}"); }
            };

            try
            {
                foreach (var arg in args)
                {
                    if (arg == "--get-fan-speeds")
                    {
                        var fanSpeeds = asusControl.GetFanSpeeds();
                        Console.WriteLine($"Current fan speeds: {string.Join(" ", fanSpeeds)} RPM");
                    }
                    else if (arg.StartsWith("--set-fan-speeds="))
                    {
                        var parts = arg.Split('=');
                        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
                        {
                            Console.WriteLine("Error: Invalid format for --set-fan-speeds. Usage: --set-fan-speeds=50");
                            continue;
                        }

                        if (int.TryParse(parts[1], out int newSpeed))
                        {
                            asusControl.SetFanSpeeds(newSpeed);
                            skipResetOnExit = true;

                            if (newSpeed == 0)
                                Console.WriteLine("Test mode turned off");
                            else
                                Console.WriteLine($"New fan speeds: {newSpeed}%");
                        }
                        else
                        {
                            Console.WriteLine($"Error: Invalid number format for speed: {parts[1]}");
                        }
                    }
                    else if (arg.StartsWith("--get-fan-speed="))
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
                    else if (arg == "--get-fan-count")
                    {
                        var fanCount = asusControl.HealthyTable_FanCounts();
                        Console.WriteLine($"Fan count: {fanCount}");
                    }
                    else if (arg.StartsWith("--set-fan-speed="))
                    {
                        var parts = arg.Split('=');
                        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
                        {
                             Console.WriteLine("Error: Invalid format. Usage: --set-fan-speed=0:50,1:100");
                             continue;
                        }

                        var fanSettings = parts[1].Split(',');
                        bool anyValid = false;
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
                                    anyValid = true;

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
                        if (anyValid)
                            skipResetOnExit = true;
                    }
                    else if (arg == "--get-cpu-temp")
                    {
                        var cpuTemp = asusControl.Thermal_Read_Cpu_Temperature();
                        Console.WriteLine($"Current CPU temp: {cpuTemp}");
                    }
                    else
                    {
                        Console.WriteLine($"Error: Unknown argument: {arg}");
                    }
                }
            }
            finally
            {
                if (!skipResetOnExit)
                {
                    try { asusControl.ResetToDefault(); }
                    catch (Exception ex) { Console.Error.WriteLine($"[Finally] Reset error: {ex.Message}"); }
                }

                isDisposed = true;
                asusControl.Dispose();
            }

            return 0;
        }
    }
}
