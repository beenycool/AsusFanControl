using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private bool _disposed = false;

        public AsusControl()
        {
            AsusWinIO64.InitializeWinIo();
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
                // Unmanaged resources
                AsusWinIO64.ShutdownWinIo();
                _disposed = true;
            }
        }

        private void SetFanSpeed(byte value, byte fanIndex = 0)
        {
            if (_disposed) return;
            AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
            AsusWinIO64.HealthyTable_SetFanTestMode(value > 0 ? FanModeManual : FanModeDefault);
            AsusWinIO64.HealthyTable_SetFanPwmDuty(value);
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

        private async Task SetFanSpeedsAsync(byte value)
        {
            if (_disposed) return;
            var fanCount = AsusWinIO64.HealthyTable_FanCounts();
            for(byte fanIndex = 0; fanIndex < fanCount; fanIndex++)
            {
                SetFanSpeed(value, fanIndex);
                await Task.Delay(20).ConfigureAwait(false);
            }
        }

        public void SetFanSpeeds(int percent)
        {
            if (_disposed) return;
            percent = ClampPercentage(percent);
            var value = (byte)(percent / 100.0f * 255);
            Task.Run(async () =>
            {
                try
                {
                    await SetFanSpeedsAsync(value).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AsusFanControl failed to set fan speeds: {ex}");
                }
            });
        }

        public int GetFanSpeed(byte fanIndex = 0)
        {
            if (_disposed) return 0;
            AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
            var fanSpeed = AsusWinIO64.HealthyTable_FanRPM();
            return fanSpeed;
        }

        public List<int> GetFanSpeeds()
        {
            if (_disposed) return new List<int>();

            var fanSpeeds = new List<int>();

            var fanCount = AsusWinIO64.HealthyTable_FanCounts();
            for (byte fanIndex = 0; fanIndex < fanCount; fanIndex++)
            {
                var fanSpeed = GetFanSpeed(fanIndex);
                fanSpeeds.Add(fanSpeed);
            }

            return fanSpeeds;
        }

        public int HealthyTable_FanCounts()
        {
            if (_disposed) return 0;
            return AsusWinIO64.HealthyTable_FanCounts();
        }

        public ulong Thermal_Read_Cpu_Temperature()
        {
            if (_disposed) return 0;
            return AsusWinIO64.Thermal_Read_Cpu_Temperature();
        }

        public void ResetToDefault()
        {
            if (_disposed) return;
            // Synchronous reset for safety (e.g. ProcessExit)
            var fanCount = AsusWinIO64.HealthyTable_FanCounts();
            for(byte fanIndex = 0; fanIndex < fanCount; fanIndex++)
            {
                SetFanSpeed(0, fanIndex);
                // Minimal blocking delay to ensure hardware processes the command if needed,
                // but keep it fast for shutdown.
                System.Threading.Thread.Sleep(ResetCommandDelayMs);
            }
        }
    }
}
