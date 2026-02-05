using System;
using System.Collections.Generic;
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

        // Static lock to synchronize access to the shared hardware resource (driver/DLL state)
        private static readonly object _hardwareLock = new object();
        private readonly int _fanCount;
        private bool _disposed = false;

        // Cache for fan speeds to enable non-blocking reads
        private volatile int[] _cachedFanSpeeds;
        private CancellationTokenSource _cts;
        private Task _monitorTask;

        public AsusControl()
        {
            lock (_hardwareLock)
            {
                AsusWinIO64.InitializeWinIo();
                _fanCount = AsusWinIO64.HealthyTable_FanCounts();
            }

            _cachedFanSpeeds = new int[_fanCount];
            _cts = new CancellationTokenSource();

            // Perform initial read synchronously to populate cache immediately
            UpdateFanSpeeds();

            // Start background monitoring task
            _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
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
            if (!_disposed)
            {
                // Always stop the background task to prevent leak on non-disposing cleanup
                _cts?.Cancel();
                if (disposing)
                {
                    try
                    {
                        _monitorTask?.Wait(2000); // Wait up to 2 seconds for clean exit
                    }
                    catch (AggregateException ae)
                    {
                        ae.Handle(e => e is TaskCanceledException);
                    }
                    _cts?.Dispose();
                }

                // Unmanaged resources
                lock (_hardwareLock)
                {
                    AsusWinIO64.ShutdownWinIo();
                }
                _disposed = true;
            }
        }

        private void UpdateFanSpeeds()
        {
            if (_disposed) return;

            // Create a local array to store new values
            var newSpeeds = new int[_fanCount];

            // Read each fan speed individually
            // Locking per fan allows other operations (like SetFanSpeed) to interleave
            for (byte i = 0; i < _fanCount; i++)
            {
                lock (_hardwareLock)
                {
                    AsusWinIO64.HealthyTable_SetFanIndex(i);
                    newSpeeds[i] = AsusWinIO64.HealthyTable_FanRPM();
                }
            }

            // Atomically replace the reference to the cached array
            _cachedFanSpeeds = newSpeeds;
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(MonitorIntervalMs, token);

                    if (_disposed) break;

                    UpdateFanSpeeds();
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AsusControl] Monitor loop error: {ex.Message}");
                }
            }
        }

        private void SetFanSpeed(byte value, byte fanIndex = 0)
        {
            if (_disposed) return;
            lock (_hardwareLock)
            {
                AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
                AsusWinIO64.HealthyTable_SetFanTestMode(value > 0 ? FanModeManual : FanModeDefault);
                AsusWinIO64.HealthyTable_SetFanPwmDuty(value);
            }
        }

        private int ClampPercentage(int percent)
        {
            if (percent < MinFanSpeed) return MinFanSpeed;
            if (percent > MaxFanSpeed) return MaxFanSpeed;
            return percent;
        }

        public void SetFanSpeed(int percent, byte fanIndex = 0)
        {
            if (_disposed) return;
            percent = ClampPercentage(percent);
            var value = (byte)(percent / 100.0f * 255);
            SetFanSpeed(value, fanIndex);
        }

        private async Task SetFanSpeeds(byte value)
        {
            if (_disposed) return;
            var fanCount = _fanCount;
            for(byte fanIndex = 0; fanIndex < fanCount; fanIndex++)
            {
                // SetFanSpeed acquires the lock internally
                SetFanSpeed(value, fanIndex);

                // Keep delay to space out hardware commands if necessary
                await Task.Delay(20);
            }
        }

        public void SetFanSpeeds(int percent)
        {
            if (_disposed) return;
            percent = ClampPercentage(percent);
            var value = (byte)(percent / 100.0f * 255);
            _ = SetFanSpeeds(value);
        }

        public int GetFanSpeed(byte fanIndex = 0)
        {
            if (_disposed) return 0;

            // Live read with lock
            lock (_hardwareLock)
            {
                AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
                var fanSpeed = AsusWinIO64.HealthyTable_FanRPM();
                return fanSpeed;
            }
        }

        public List<int> GetFanSpeeds()
        {
            if (_disposed) return new List<int>();

            // Return a copy of the cached fan speeds
            // This is non-blocking and instant (O(N) memory copy)
            return new List<int>(_cachedFanSpeeds);
        }

        public int HealthyTable_FanCounts()
        {
            if (_disposed) return 0;
            return _fanCount;
        }

        public ulong Thermal_Read_Cpu_Temperature()
        {
            if (_disposed) return 0;
            lock (_hardwareLock)
            {
                return AsusWinIO64.Thermal_Read_Cpu_Temperature();
            }
        }

        public Task ResetToDefaultAsync()
        {
            if (_disposed) return Task.CompletedTask;
            // Synchronous reset for safety (e.g. ProcessExit)
            var fanCount = _fanCount;
            for(byte fanIndex = 0; fanIndex < fanCount; fanIndex++)
            {
                SetFanSpeed(0, fanIndex);
                // Minimal blocking delay to ensure hardware processes the command if needed,
                // but keep it fast for shutdown.
                System.Threading.Thread.Sleep(ResetCommandDelayMs);
            }
            return Task.CompletedTask;
        }
    }
}
