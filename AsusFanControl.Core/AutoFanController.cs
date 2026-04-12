using System;
using System.Threading;
using System.Threading.Tasks;

namespace AsusFanControl.Core
{
    public class AutoFanController : IDisposable
    {
        private readonly IFanController _fanController;
        private FanCurve _fanCurve;
        private CancellationTokenSource _cts;
        private Task _loopTask;
        private int _currentSpeed = -1;

        public event EventHandler<int> FanSpeedChanged;

        public AutoFanController(IFanController fanController, FanCurve fanCurve)
        {
            _fanController = fanController ?? throw new ArgumentNullException(nameof(fanController));
            _fanCurve = fanCurve ?? throw new ArgumentNullException(nameof(fanCurve));
        }

        public void UpdateFanCurve(FanCurve fanCurve)
        {
            _fanCurve = fanCurve ?? throw new ArgumentNullException(nameof(fanCurve));
        }

        public void Start(int updateIntervalMs)
        {
            if (_loopTask != null) return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _loopTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(updateIntervalMs, token);
                        
                        var temp = (int)_fanController.Thermal_Read_Cpu_Temperature();
                        int targetSpeed = _fanCurve.GetTargetSpeed(temp);

                        // Hysteresis: only change if target is higher or significantly lower (more than 2%)
                        if (_currentSpeed == -1 || targetSpeed > _currentSpeed || Math.Abs(targetSpeed - _currentSpeed) > 2)
                        {
                            _currentSpeed = targetSpeed;
                            try
                            {
                                _fanController.SetFanSpeeds(targetSpeed);
                                FanSpeedChanged?.Invoke(this, targetSpeed);
                            }
                            catch { }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch { }
                }
            }, token);
        }

        public void Stop()
        {
            if (_loopTask == null) return;

            try
            {
                _cts?.Cancel();
                _loopTask?.Wait(500);
            }
            catch { }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _loopTask = null;
                _currentSpeed = -1;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
