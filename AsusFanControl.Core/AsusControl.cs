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
                catch
                {
                    if (initializedHere)
                    {
                        try
                        {
                            AsusWinIO64.ShutdownWinIo();
                        }
                        catch
                        {
                        }
                    }

                    throw;
                }
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

                shouldShutdown = _instanceCount == 0;
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
                    ae.Handle(e => e is TaskCanceledException);
                }

                _cts?.Dispose();
            }

            if (shouldShutdown)
            {
                try
                {
                    ResetToDefaultInternal();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AsusControl] Error resetting fans on dispose: {ex.Message}");
                }

                lock (_hardwareLock)
                {
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
            var newSpeeds = new int[_fanCount];

            for (byte i = 0; i < _fanCount; i++)
            {
                lock (_hardwareLock)
                {
                    if (_disposed || _disposing) return;

                    AsusWinIO64.HealthyTable_SetFanIndex(i);
                    newSpeeds[i] = AsusWinIO64.HealthyTable_FanRPM();
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
                if (_disposed || _disposing) return;
                if (!IsValidFanIndex(fanIndex)) return;

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

        private async Task SetFanSpeeds(byte value)
        {
            if (_disposed) return;

            for (byte fanIndex = 0; fanIndex < _fanCount; fanIndex++)
            {
                SetFanSpeed(value, fanIndex);
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
            lock (_hardwareLock)
            {
                if (_disposed || _disposing) return 0;
                if (!IsValidFanIndex(fanIndex)) return 0;

                AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
                return AsusWinIO64.HealthyTable_FanRPM();
            }
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
            lock (_hardwareLock)
            {
                if (_disposed || _disposing) return 0;
                return AsusWinIO64.Thermal_Read_Cpu_Temperature();
            }
        }

        public Task ResetToDefaultAsync()
        {
            ResetToDefault();
            return Task.CompletedTask;
        }

        public void ResetToDefault()
        {
            ResetToDefaultInternal();
        }

        private void ResetToDefaultInternal()
        {
            for (byte fanIndex = 0; fanIndex < _fanCount; fanIndex++)
            {
                try
                {
                    lock (_hardwareLock)
                    {
                        if (_disposed && !_disposing) return;

                        AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
                        AsusWinIO64.HealthyTable_SetFanTestMode(FanModeDefault);
                        AsusWinIO64.HealthyTable_SetFanPwmDuty(0);
                    }
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
