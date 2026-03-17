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

        private readonly int _fanCount;
        private bool _disposed = false;

        private volatile int[] _cachedFanSpeeds;
        private CancellationTokenSource _cts;
        private Task _monitorTask;

        public AsusControl()
        {
            lock (_hardwareLock)
            {
                if (_instanceCount == 0)
                {
                    AsusWinIO64.InitializeWinIo();
                }

                _fanCount = AsusWinIO64.HealthyTable_FanCounts();
                _instanceCount++;
            }

            _cachedFanSpeeds = new int[_fanCount];
            _cts = new CancellationTokenSource();

            UpdateFanSpeeds();
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
            if (_disposed) return;

            _cts?.Cancel();
            if (disposing)
            {
                try
                {
                    _monitorTask?.Wait(2000);
                }
                catch (AggregateException ae)
                {
                    ae.Handle(e => e is TaskCanceledException);
                }

                _cts?.Dispose();
            }

            lock (_hardwareLock)
            {
                if (_disposed) return;

                _instanceCount--;
                if (_instanceCount == 0)
                {
                    try
                    {
                        AsusWinIO64.ShutdownWinIo();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AsusControl] Error shutting down WinIo: {ex.Message}");
                    }
                }

                _disposed = true;
            }
        }

        private void UpdateFanSpeeds()
        {
            var newSpeeds = new int[_fanCount];

            for (byte fanIndex = 0; fanIndex < _fanCount; fanIndex++)
            {
                lock (_hardwareLock)
                {
                    if (_disposed) return;

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

                    if (_disposed) break;

                    UpdateFanSpeeds();
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AsusControl] Monitor loop error: {ex.Message}");
                }
            }
        }

        private void SetFanSpeed(byte value, byte fanIndex = 0)
        {
            lock (_hardwareLock)
            {
                if (_disposed) return;

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

        public void SetFanSpeeds(int percent)
        {
            if (_disposed) return;

            percent = ClampPercentage(percent);
            var value = (byte)(percent / 100.0f * 255);
            for (byte fanIndex = 0; fanIndex < _fanCount; fanIndex++)
            {
                SetFanSpeed(value, fanIndex);
            }
        }

        public int GetFanSpeed(byte fanIndex = 0)
        {
            lock (_hardwareLock)
            {
                if (_disposed) return 0;

                AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
                return AsusWinIO64.HealthyTable_FanRPM();
            }
        }

        public List<int> GetFanSpeeds()
        {
            if (_disposed) return new List<int>();
            return new List<int>(_cachedFanSpeeds);
        }

        public int HealthyTable_FanCounts()
        {
            if (_disposed) return 0;
            return _fanCount;
        }

        public ulong Thermal_Read_Cpu_Temperature()
        {
            lock (_hardwareLock)
            {
                if (_disposed) return 0;
                return AsusWinIO64.Thermal_Read_Cpu_Temperature();
            }
        }

        public Task ResetToDefaultAsync()
        {
            if (_disposed) return Task.CompletedTask;
            return Task.Run(() => ResetToDefault());
        }

        public void ResetToDefault()
        {
            lock (_hardwareLock)
            {
                if (_disposed) return;
                ResetToDefaultInternal();
            }
        }

        private void ResetToDefaultInternal()
        {
            for (byte fanIndex = 0; fanIndex < _fanCount; fanIndex++)
            {
                try
                {
                    AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
                    AsusWinIO64.HealthyTable_SetFanTestMode(FanModeDefault);
                    AsusWinIO64.HealthyTable_SetFanPwmDuty(0);
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
