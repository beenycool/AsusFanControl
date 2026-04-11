using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AsusFanControl.Core;

namespace AsusFanControlGUI
{
    /// <summary>Command-line mode (formerly AsusFanControl.exe).</summary>
    internal static class CliProgram
    {
        /// <summary>Writes to two writers (typically console + file). Does not dispose the primary writer.</summary>
        sealed class TeeTextWriter : TextWriter
        {
            readonly TextWriter _primary;
            readonly TextWriter _secondary;

            public TeeTextWriter(TextWriter primary, TextWriter secondary)
            {
                _primary = primary;
                _secondary = secondary;
            }

            public override Encoding Encoding => _primary.Encoding;

            public override void Write(char value)
            {
                _primary.Write(value);
                _secondary.Write(value);
            }

            public override void Write(string value)
            {
                _primary.Write(value);
                _secondary.Write(value);
            }

            public override void WriteLine(string value)
            {
                _primary.WriteLine(value);
                _secondary.WriteLine(value);
            }

            public override void Flush()
            {
                _primary.Flush();
                _secondary.Flush();
            }
        }

        sealed class ForwardingTraceListener : TraceListener
        {
            readonly TextWriter _target;

            public ForwardingTraceListener(TextWriter target)
            {
                _target = target;
            }

            public override void Write(string message)
            {
                if (message != null)
                    _target.Write(message);
            }

            public override void WriteLine(string message)
            {
                _target.WriteLine(message ?? string.Empty);
            }
        }

        internal sealed class DebugLogSession : IDisposable
        {
            readonly TextWriter _originalOut;
            readonly TextWriter _originalErr;
            readonly StreamWriter _fileWriter;
            readonly TeeTextWriter _teeOut;
            readonly TeeTextWriter _teeErr;
            readonly TraceListener _traceListener;
            readonly TraceListener _debugListener;

            DebugLogSession(
                TextWriter originalOut,
                TextWriter originalErr,
                StreamWriter fileWriter,
                TeeTextWriter teeOut,
                TeeTextWriter teeErr,
                TraceListener traceListener,
                TraceListener debugListener)
            {
                _originalOut = originalOut;
                _originalErr = originalErr;
                _fileWriter = fileWriter;
                _teeOut = teeOut;
                _teeErr = teeErr;
                _traceListener = traceListener;
                _debugListener = debugListener;
            }

            public static DebugLogSession Create(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    path = DefaultDebugLogPath();

                var dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var originalOut = Console.Out;
                var originalErr = Console.Error;
                var fileWriter = new StreamWriter(path, append: true) { AutoFlush = true };
                var syncFile = TextWriter.Synchronized(fileWriter);
                var teeOut = new TeeTextWriter(originalOut, syncFile);
                var teeErr = new TeeTextWriter(originalErr, syncFile);
                Console.SetOut(teeOut);
                Console.SetError(teeErr);

                var traceListener = new ForwardingTraceListener(syncFile) { Name = "debug-log-trace" };
                var debugListener = new ForwardingTraceListener(syncFile) { Name = "debug-log-debug" };
                Trace.Listeners.Add(traceListener);
                Debug.Listeners.Add(debugListener);
                Trace.AutoFlush = true;
                Debug.AutoFlush = true;

                return new DebugLogSession(originalOut, originalErr, fileWriter, teeOut, teeErr, traceListener, debugListener);
            }

            public void Dispose()
            {
                Trace.Listeners.Remove(_traceListener);
                Debug.Listeners.Remove(_debugListener);
                _traceListener.Dispose();
                _debugListener.Dispose();
                Trace.Flush();
                Debug.Flush();

                Console.SetOut(_originalOut);
                Console.SetError(_originalErr);
                _teeOut.Dispose();
                _teeErr.Dispose();
                _fileWriter.Dispose();
            }
        }

        static string DefaultDebugLogPath()
        {
            return Path.Combine(
                Environment.CurrentDirectory,
                "AsusFanControl-debug-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
        }

        /// <summary>Removes --debug-log and --debug-log=path from args; returns log file path or null.</summary>
        internal static string[] ExtractDebugLogFlag(string[] args, out string debugLogPath)
        {
            debugLogPath = null;
            var kept = new List<string>(args.Length);
            foreach (var arg in args)
            {
                if (arg == "--debug-log")
                {
                    debugLogPath = DefaultDebugLogPath();
                    continue;
                }

                if (arg.StartsWith("--debug-log=", StringComparison.Ordinal))
                {
                    var p = arg.Substring("--debug-log=".Length).Trim();
                    debugLogPath = string.IsNullOrEmpty(p) ? DefaultDebugLogPath() : p;
                    continue;
                }

                kept.Add(arg);
            }

            return kept.ToArray();
        }

        internal static void PrintUsage()
        {
            Console.WriteLine("Usage: AsusFanControl <args>");
            Console.WriteLine("\t--gui (launch GUI, can be combined with --debug-log)");
            Console.WriteLine("\t--debug-log[=path] (optional path; default: timestamped file in current directory)");
            Console.WriteLine("\t--get-fan-speeds");
            Console.WriteLine("\t--set-fan-speeds=0-100 (percent value, 0 for turning off test mode)");
            Console.WriteLine("\t--get-fan-count");
            Console.WriteLine("\t--get-fan-speed=fanId (comma separated)");
            Console.WriteLine("\t--set-fan-speed=fanId:0-100 (comma separated, percent value, 0 for turning off test mode)");
            Console.WriteLine("\t--get-cpu-temp");
        }

        internal static int Run(string[] args)
        {
            bool skipResetOnExit = false;
            bool isDisposed = false;
            bool hasError = false;

            IFanController asusControl = new AsusControl();

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                if (!isDisposed && !skipResetOnExit)
                {
                    try { asusControl.ResetToDefaultAsync().GetAwaiter().GetResult(); }
                    catch (Exception ex) { Console.Error.WriteLine("[ProcessExit] Reset error: " + ex.Message); }
                }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (!isDisposed && !skipResetOnExit)
                {
                    try { asusControl.ResetToDefaultAsync().GetAwaiter().GetResult(); }
                    catch (Exception ex) { Console.Error.WriteLine("[UnhandledException] Reset error: " + ex.Message); }
                }
                if (!isDisposed)
                {
                    try { asusControl.Dispose(); }
                    catch (Exception ex) { Console.Error.WriteLine("[UnhandledException] Dispose error: " + ex.Message); }
                }
            };

            try
            {
                foreach (var arg in args)
                {
                    if (arg == "--get-fan-speeds")
                    {
                        var fanSpeeds = asusControl.GetFanSpeeds();
                        Console.WriteLine("Current fan speeds: " + string.Join(" ", fanSpeeds) + " RPM");
                    }
                    else if (arg.StartsWith("--set-fan-speeds="))
                    {
                        var parts = arg.Split('=');
                        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
                        {
                            Console.WriteLine("Error: Invalid format for --set-fan-speeds. Usage: --set-fan-speeds=50");
                            hasError = true;
                            continue;
                        }

                        if (int.TryParse(parts[1], out int newSpeed))
                        {
                            if (newSpeed < 0 || newSpeed > 100)
                            {
                                Console.WriteLine("Error: Speed must be between 0 and 100");
                                hasError = true;
                                continue;
                            }

                            asusControl.SetFanSpeeds(newSpeed);
                            skipResetOnExit = true;

                            if (newSpeed == 0)
                                Console.WriteLine("Test mode turned off");
                            else
                                Console.WriteLine("New fan speeds: " + newSpeed + "%");
                        }
                        else
                        {
                            Console.WriteLine("Error: Invalid number format for speed: " + parts[1]);
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
                                    Console.WriteLine("Current fan speed for fan " + fanId + ": " + fanSpeed + " RPM");
                                }
                                else
                                {
                                    Console.WriteLine("Error: fan id must be between 0 and 255: " + fanId);
                                }
                            }
                            else
                            {
                                Console.WriteLine("Error: Invalid fan ID: " + fanIdStr);
                            }
                        }
                    }
                    else if (arg == "--get-fan-count")
                    {
                        var fanCount = asusControl.HealthyTable_FanCounts();
                        Console.WriteLine("Fan count: " + fanCount);
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
                                    if (fanSpeed < 0 || fanSpeed > 100)
                                    {
                                        Console.WriteLine("Error: Speed must be between 0 and 100");
                                        continue;
                                    }

                                    asusControl.SetFanSpeed(fanSpeed, (byte)fanId);
                                    anyValid = true;

                                    if (fanSpeed == 0)
                                        Console.WriteLine("Test mode turned off for fan " + fanId);
                                    else
                                        Console.WriteLine("New fan speed for fan " + fanId + ": " + fanSpeed + "%");
                                }
                                else
                                {
                                    Console.WriteLine("Error: fan id must be between 0 and 255: " + fanId);
                                }
                            }
                            else
                            {
                                Console.WriteLine("Error: Invalid fan setting format: " + fanSetting);
                            }
                        }
                        if (anyValid)
                            skipResetOnExit = true;
                    }
                    else if (arg == "--get-cpu-temp")
                    {
                        var cpuTemp = asusControl.Thermal_Read_Cpu_Temperature();
                        Console.WriteLine("Current CPU temp: " + cpuTemp);
                    }
                    else
                    {
                        Console.WriteLine("Error: Unknown argument: " + arg);
                        hasError = true;
                    }
                }
            }
            finally
            {
                if (!skipResetOnExit)
                {
                    try { asusControl.ResetToDefaultAsync().GetAwaiter().GetResult(); }
                    catch (Exception ex) { Console.Error.WriteLine("[Finally] Reset error: " + ex.Message); }
                }

                isDisposed = true;
                asusControl.Dispose();
            }

            return hasError ? 1 : 0;
        }
    }
}
