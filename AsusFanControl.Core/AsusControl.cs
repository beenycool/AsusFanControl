using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AsusFanControl.Core
{
    public class AsusControl : IFanController, IDisposable
    {
        private const char FanModeManual = (char)0x01;
        private const char FanModeDefault = (char)0x00;
        private const int MinFanSpeed = 0;
        private const int MaxFanSpeed = 100;
        private const int ResetCommandDelayMs = 10;
        private const int MonitorIntervalMs = 1000;

        private static readonly object _hardwareLock = new object();
        private static int _instanceCount = 0;
        private static bool _shutdownInProgress = false;

        private readonly int _fanCount;
        private bool _disposed = false;
        private bool _disposing = false;

        private volatile int[] _cachedFanSpeeds;
        private CancellationTokenSource _cts;
        private Task _monitorTask;

        public AsusControl()
        {
            try
            {
                #region agent log
                System.IO.File.AppendAllText("/home/ubuntu/projects/AsusFanControl/.cursor/debug-4df631.log",
                    "{\"sessionId\":\"4df631\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H5\",\"location\":\"AsusControl.cs:33\",\"message\":\"AsusControl constructor entered\",\"data\":{},\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n");
                #endregion
            }
            catch
            {
            }

            lock (_hardwareLock)
            {
                while (_shutdownInProgress)
                {
                    Monitor.Wait(_hardwareLock);
                }

                var initializedHere = false;
                try
                {
                    if (_instanceCount == 0)
                    {
                        AsusWinIO64.InitializeWinIo();
                        initializedHere = true;
                    }

                    _fanCount = AsusWinIO64.HealthyTable_FanCounts();
                    _instanceCount++;
                }
                catch (Exception ex)
                {
                    try
                    {
                        #region agent log
                        Console.Error.WriteLine("[startup] AsusControl initialization failed: " + ex);
                        Debug.WriteLine("[startup] AsusControl initialization failed: " + ex);
                        System.IO.File.AppendAllText("/home/ubuntu/projects/AsusFanControl/.cursor/debug-4df631.log",
                            "{\"sessionId\":\"4df631\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H5\",\"location\":\"AsusControl.cs:63\",\"message\":\"AsusControl initialization failed\",\"data\":{},\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n");
                        #endregion
                    }
                    catch
                    {
                    }

                    if (initializedHere)
                    {
                        try
                        {
                            AsusWinIO64.ShutdownWinIo();
                        }
		catch (Exception rollbackEx)
		{
			Debug.WriteLine($"[AsusControl] Error rolling back WinIo initialization: {rollbackEx.Message}");
		}
                    }

                    throw;
                }
            }

            _cachedFanSpeeds = new int[_fanCount];
            _cts = new CancellationTokenSource();

            UpdateFanSpeeds();
            _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));

            try
            {
                #region agent log
                Console.Error.WriteLine("[startup] AsusControl constructor completed, fanCount=" + _fanCount);
                Debug.WriteLine("[startup] AsusControl constructor completed, fanCount=" + _fanCount);
                System.IO.File.AppendAllText("/home/ubuntu/projects/AsusFanControl/.cursor/debug-4df631.log",
                    "{\"sessionId\":\"4df631\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H5\",\"location\":\"AsusControl.cs:88\",\"message\":\"AsusControl constructor completed\",\"data\":{\"fanCount\":" + _fanCount + "},\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n");
                #endregion
            }
            catch
            {
            }
        }

        ~AsusControl()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            var shouldShutdown = false;

            if (_disposed) return;

            lock (_hardwareLock)
            {
                if (_disposed) return;

                _disposing = true;
                _cts?.Cancel();

                if (_instanceCount > 0)
                {
                    _instanceCount--;
                }

                shouldShutdown = disposing && _instanceCount == 0;
                if (shouldShutdown)
                {
                    _shutdownInProgress = true;
                }
            }

            if (disposing)
            {
                try
                {
                    _monitorTask?.Wait(2000);
                }
                catch (AggregateException ae)
                {
                    ae.Handle(e => e is TaskCanceledException || e is OperationCanceledException);
                }

                _cts?.Dispose();
                _cts = null;
            }

            if (shouldShutdown)
            {
                lock (_hardwareLock)
                {
                    try
                    {
                        ResetToDefaultInternal();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AsusControl] Error resetting fans on dispose: {ex.Message}");
                    }

                    try
                    {
                        AsusWinIO64.ShutdownWinIo();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AsusControl] Error shutting down WinIo: {ex.Message}");
                    }
                    finally
                    {
                        _shutdownInProgress = false;
                        Monitor.PulseAll(_hardwareLock);
                    }
                }
            }

            lock (_hardwareLock)
            {
                _disposed = true;
                _disposing = false;
            }
        }

        private void UpdateFanSpeeds()
        {
            if (_disposed || _disposing) return;

            var newSpeeds = new int[_fanCount];
            for (byte fanIndex = 0; fanIndex < _fanCount; fanIndex++)
            {
                lock (_hardwareLock)
                {
                    if (_disposed || _disposing) return;

                    AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
                    newSpeeds[fanIndex] = AsusWinIO64.HealthyTable_FanRPM();
                }
            }

            _cachedFanSpeeds = newSpeeds;
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(MonitorIntervalMs, token);

                    if (_disposed || _disposing) break;

                    UpdateFanSpeeds();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AsusControl] Monitor loop error: {ex.Message}");
                }
            }
        }

        private static void SetFanSpeedUnsafe(byte value, byte fanIndex)
        {
            AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
            AsusWinIO64.HealthyTable_SetFanTestMode(value > 0 ? FanModeManual : FanModeDefault);
            AsusWinIO64.HealthyTable_SetFanPwmDuty(value);
        }

        private void SetFanSpeed(byte value, byte fanIndex = 0)
        {
            if (_disposed) return;

            lock (_hardwareLock)
            {
                if (_disposed || _disposing) return;
                if (!IsValidFanIndex(fanIndex)) return;

                SetFanSpeedUnsafe(value, fanIndex);
            }
        }

        private int ClampPercentage(int percent)
        {
            if (percent < MinFanSpeed) return MinFanSpeed;
            if (percent > MaxFanSpeed) return MaxFanSpeed;
            return percent;
        }

        private bool IsValidFanIndex(byte fanIndex)
        {
            return fanIndex < _fanCount;
        }

        public void SetFanSpeed(int percent, byte fanIndex = 0)
        {
            if (_disposed) return;

            percent = ClampPercentage(percent);
            var value = (byte)(percent / 100.0f * 255);
            SetFanSpeed(value, fanIndex);
        }

        public void SetFanSpeeds(int percent)
        {
            if (_disposed) return;

            percent = ClampPercentage(percent);
            var value = (byte)(percent / 100.0f * 255);

            lock (_hardwareLock)
            {
                if (_disposed || _disposing) return;

                for (byte fanIndex = 0; fanIndex < _fanCount; fanIndex++)
                {
                    SetFanSpeedUnsafe(value, fanIndex);
                    Thread.Sleep(ResetCommandDelayMs);
                }
            }
        }

        public int GetFanSpeed(byte fanIndex = 0)
        {
            if (_disposed || _disposing) return 0;

            var cachedFanSpeeds = _cachedFanSpeeds;
            if (fanIndex >= cachedFanSpeeds.Length) return 0;

            return cachedFanSpeeds[fanIndex];
        }

        public List<int> GetFanSpeeds()
        {
            if (_disposed || _disposing) return new List<int>();
            return new List<int>(_cachedFanSpeeds);
        }

        public int HealthyTable_FanCounts()
        {
            if (_disposed || _disposing) return 0;
            return _fanCount;
        }

        public ulong Thermal_Read_Cpu_Temperature()
        {
            if (_disposed) return 0;

            lock (_hardwareLock)
            {
                if (_disposed || _disposing) return 0;
                return AsusWinIO64.Thermal_Read_Cpu_Temperature();
            }
        }

        public Task ResetToDefaultAsync()
        {
            return _disposed || _disposing ? Task.CompletedTask : Task.Run(() => ResetToDefault());
        }

        public void ResetToDefault()
        {
            if (_disposed) return;

            lock (_hardwareLock)
            {
                if (_disposed || _disposing) return;
                ResetToDefaultInternal();
            }
        }

        private void ResetToDefaultInternal()
        {
            for (byte fanIndex = 0; fanIndex < _fanCount; fanIndex++)
            {
                try
                {
                    if (_disposed && !_disposing) return;

                    SetFanSpeedUnsafe(0, fanIndex);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AsusControl] Failed to reset fan {fanIndex}: {ex.Message}");
                }

                Thread.Sleep(ResetCommandDelayMs);
            }
        }
    }
}
