using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AsusFanControl.Core
{
    public class CsvLogger : IDisposable
    {
        private readonly Func<float> _getCpuTemp;
        private readonly Func<string> _getFanSpeeds;
        private readonly Func<Task<float>> _getCpuLoadAsync;
        private CancellationTokenSource _cts;
        private Task _loopTask;
        private StreamWriter _writer;

        public CsvLogger(Func<float> getCpuTemp, Func<string> getFanSpeeds, Func<Task<float>> getCpuLoadAsync)
        {
            _getCpuTemp = getCpuTemp ?? throw new ArgumentNullException(nameof(getCpuTemp));
            _getFanSpeeds = getFanSpeeds ?? throw new ArgumentNullException(nameof(getFanSpeeds));
            _getCpuLoadAsync = getCpuLoadAsync ?? throw new ArgumentNullException(nameof(getCpuLoadAsync));
        }

        public void Start(string filePath, int intervalMs)
        {
            if (_loopTask != null) return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _loopTask = Task.Run(async () =>
            {
                try
                {
                    _writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true));
                    if (_writer.BaseStream.Length == 0)
                    {
                        await _writer.WriteLineAsync("Timestamp,CPU Temp (C),Fan Speed (RPM),CPU Load (%)");
                        await _writer.FlushAsync();
                    }

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(intervalMs, token);

                            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            var cpuTemp = _getCpuTemp();
                            var fanSpeeds = _getFanSpeeds();
                            var cpuLoad = await _getCpuLoadAsync();

                            if (_writer != null)
                            {
                                await _writer.WriteLineAsync($"{timestamp},{cpuTemp},{fanSpeeds},{cpuLoad:F2}");
                                await _writer.FlushAsync();
                            }
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CsvLogger] Loop error: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CsvLogger] Start error: {ex.Message}");
                }
                finally
                {
                    StopInternal();
                }
            }, token);
        }

        public void Stop()
        {
            if (_loopTask == null) return;
            _cts?.Cancel();
            try { _loopTask?.Wait(500); } catch { }
        }

        private void StopInternal()
        {
            try
            {
                if (_writer != null)
                {
                    _writer.Close();
                    _writer.Dispose();
                    _writer = null;
                }
            }
            catch { }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _loopTask = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
