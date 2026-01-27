using System.Collections.Generic;

namespace AsusFanControl.Core
{
    public interface IFanController
    {
        void SetFanSpeed(int percent, byte fanIndex = 0);
        void SetFanSpeeds(int percent);
        int GetFanSpeed(byte fanIndex = 0);
        List<int> GetFanSpeeds();
        int HealthyTable_FanCounts();
        ulong Thermal_Read_Cpu_Temperature();
        void ResetToDefault();
    }
}
