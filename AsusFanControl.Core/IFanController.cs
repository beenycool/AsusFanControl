using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsusFanControl.Core
{
    public interface IFanController : IDisposable
    {
        void SetFanSpeed(int percent, byte fanIndex = 0);
        void SetFanSpeeds(int percent);
        int GetFanSpeed(byte fanIndex = 0);
        List<int> GetFanSpeeds();
        int HealthyTable_FanCounts();
        ulong Thermal_Read_Cpu_Temperature();
        Task ResetToDefaultAsync();
    }
}
