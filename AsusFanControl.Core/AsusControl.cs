using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsusFanControl.Core
{
    public class AsusControl : IFanController
    {
        private const char FanModeManual = (char)0x01;
        private const char FanModeDefault = (char)0x00;
        private const int MinFanSpeed = 0;
        private const int MaxFanSpeed = 100;

        public AsusControl()
        {
            AsusWinIO64.InitializeWinIo();
        }

        ~AsusControl()
        {
            AsusWinIO64.ShutdownWinIo();
        }

        private void SetFanSpeed(byte value, byte fanIndex = 0)
        {
            AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
            AsusWinIO64.HealthyTable_SetFanTestMode(value > 0 ? FanModeManual : FanModeDefault);
            AsusWinIO64.HealthyTable_SetFanPwmDuty(value);
        }

        public void SetFanSpeed(int percent, byte fanIndex = 0)
        {
            if (percent < MinFanSpeed) percent = MinFanSpeed;
            if (percent > MaxFanSpeed) percent = MaxFanSpeed;

            var value = (byte)(percent / 100.0f * 255);
            SetFanSpeed(value, fanIndex);
        }

        private async Task SetFanSpeeds(byte value)
        {
            var fanCount = AsusWinIO64.HealthyTable_FanCounts();
            for(byte fanIndex = 0; fanIndex < fanCount; fanIndex++)
            {
                SetFanSpeed(value, fanIndex);
                await Task.Delay(20);
            }
        }

        public void SetFanSpeeds(int percent)
        {
            if (percent < MinFanSpeed) percent = MinFanSpeed;
            if (percent > MaxFanSpeed) percent = MaxFanSpeed;

            var value = (byte)(percent / 100.0f * 255);
            _ = SetFanSpeeds(value);
        }

        public int GetFanSpeed(byte fanIndex = 0)
        {
            AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
            var fanSpeed = AsusWinIO64.HealthyTable_FanRPM();
            return fanSpeed;
        }

        public List<int> GetFanSpeeds()
        {
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
            return AsusWinIO64.HealthyTable_FanCounts();
        }

        public ulong Thermal_Read_Cpu_Temperature()
        {
            return AsusWinIO64.Thermal_Read_Cpu_Temperature();
        }

        public void ResetToDefault()
        {
            // Synchronous reset for safety (e.g. ProcessExit)
            var fanCount = AsusWinIO64.HealthyTable_FanCounts();
            for(byte fanIndex = 0; fanIndex < fanCount; fanIndex++)
            {
                SetFanSpeed(0, fanIndex);
                // Minimal blocking delay to ensure hardware processes the command if needed,
                // but keep it fast for shutdown.
                System.Threading.Thread.Sleep(10);
            }
        }
    }
}
