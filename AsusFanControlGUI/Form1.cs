using AsusFanControl.Core;
using System;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AsusFanControlGUI
{
    public partial class Form1 : Form
    {
        AsusControl asusControl;
        int fanSpeed = 0;
        Timer timer;
        NotifyIcon trayIcon;
        FanCurve currentFanCurve;
        PerformanceCounter cpuCounter;
        Timer loggingTimer;
        StreamWriter loggingWriter;
        bool isLoggingWriting = false;
        System.Threading.CancellationTokenSource _backgroundCts;
        ProfileManager _profileManager;
        string _activeProfileName;
        System.Threading.Tasks.Task _profileMonitorTask;
        System.Threading.CancellationTokenSource _autoCts;
        System.Threading.Tasks.Task _autoControlTask;

        public Form1()
        {
            // constructor entered

            Console.Error.WriteLine("[startup] Before AsusControl construction");
            Debug.WriteLine("[startup] Before AsusControl construction");

            try
            {
                asusControl = new AsusControl();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[startup] AsusControl construction failed: " + ex);
                Debug.WriteLine("[startup] AsusControl construction failed: " + ex);

                throw;
            }

            Console.Error.WriteLine("[startup] AsusControl constructed");
            Debug.WriteLine("[startup] AsusControl constructed");

            InitializeComponent();
            // Apply dark menu renderer
            try { menuStrip1.Renderer = new DarkMenuRenderer(); } catch { }

            // Delay creating PerformanceCounter to avoid blocking UI
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var pc = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    pc.NextValue(); // prime
                    cpuCounter = pc;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Form1] Failed to create PerformanceCounter async: {ex.Message}");
                }
            });
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            // Watchdog for crash
            AppDomain.CurrentDomain.UnhandledException += (s, e) => { try { if (asusControl != null) asusControl.ResetToDefault(); } catch (Exception ex) { Debug.WriteLine($"[UnhandledException] Reset error: {ex.Message}"); } };
            Application.ThreadException += (s, e) => { try { if (asusControl != null) asusControl.ResetToDefault(); } catch (Exception ex) { Debug.WriteLine($"[ThreadException] Reset error: {ex.Message}"); } };

            toolStripMenuItemTurnOffControlOnExit.Checked = Properties.Settings.Default.turnOffControlOnExit;
            toolStripMenuItemForbidUnsafeSettings.Checked = Properties.Settings.Default.forbidUnsafeSettings;
            toolStripMenuItemMinimizeToTrayOnClose.Checked = Properties.Settings.Default.minimizeToTrayOnClose;
            toolStripMenuItemAutoRefreshStats.Checked = Properties.Settings.Default.autoRefreshStats;
            trackBarFanSpeed.Value = Properties.Settings.Default.fanSpeed;

            checkBoxAuto.Checked = Properties.Settings.Default.autoMode;
            numericUpdateInterval.Value = Properties.Settings.Default.updateInterval;
            currentFanCurve = FanCurve.FromString(Properties.Settings.Default.fanCurve);

            if (currentFanCurve.PointCount == 0)
            {
                 currentFanCurve.SetPoints(new[]
                 {
                     new FanCurvePoint(30, 0),
                     new FanCurvePoint(60, 50),
                     new FanCurvePoint(90, 100)
                 });
            }

            updateUIState();

            Console.Error.WriteLine("[startup] Form1 constructor completed");
            Debug.WriteLine("[startup] Form1 constructor completed");

            // Initialize profile manager and background tasks
            _profileManager = new ProfileManager();
            try { _profileManager.LoadProfiles(Properties.Settings.Default.profiles); } catch { }
            _backgroundCts = new System.Threading.CancellationTokenSource();

            // Start profile monitor
            _profileMonitorTask = System.Threading.Tasks.Task.Run(async () =>
            {
                var token = _backgroundCts.Token;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(2000, token);
                        var profile = _profileManager.CheckActiveProfile(currentFanCurve);
                        if (profile != null && profile.Name != _activeProfileName)
                        {
                            _activeProfileName = profile.Name;
                            currentFanCurve = profile.Curve ?? currentFanCurve;
                            Properties.Settings.Default.fanCurve = currentFanCurve.ToString();
                            Properties.Settings.Default.Save();
                            try { if (asusControl != null) asusControl.SetFanSpeeds(fanSpeed); } catch { }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch { }
                }
            }, _backgroundCts.Token);

            // Check task scheduler registration for menu state
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var registered = TaskSchedulerHelper.IsTaskRegistered();
                    if (toolStripMenuItemStartWithWindows != null && !toolStripMenuItemStartWithWindows.IsDisposed)
                    {
                        try { toolStripMenuItemStartWithWindows.Checked = registered; } catch { }
                    }
                }
                catch { }
            });
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }

                // Custom cleanup
                if (asusControl != null)
                {
                    if (Properties.Settings.Default.turnOffControlOnExit)
                    {
                        try
                        {
                            asusControl.ResetToDefault();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Dispose] Reset error: {ex.Message}");
                        }
                    }

                    try
                    {
                        asusControl.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Dispose] Dispose error: {ex.Message}");
                    }

                    asusControl = null; // Prevent use-after-dispose
                }
            }
            base.Dispose(disposing);
        }

        private void updateUIState()
        {
            bool auto = checkBoxAuto.Checked;

            if (auto)
            {
                checkBoxTurnOn.Checked = false;
                checkBoxTurnOn.Enabled = false;
                trackBarFanSpeed.Enabled = false;
            }
            else
            {
                checkBoxTurnOn.Enabled = true;
                trackBarFanSpeed.Enabled = true;
            }

            timerRefreshStats();
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.turnOffControlOnExit && asusControl != null)
            {
                try
                {
                    asusControl.ResetToDefault();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OnProcessExit] Reset error: {ex.Message}");
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Timer already started in updateUIState called from ctor, but calling it here is safe too
            timerRefreshStats();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Properties.Settings.Default.minimizeToTrayOnClose && Visible)
            {
                if(trayIcon == null)
                {
                    trayIcon = new NotifyIcon()
                    {
                        Icon = Icon,
                        ContextMenu = new ContextMenu(new MenuItem[] {
                            new MenuItem("Show", (s1, e1) =>
                            {
                                trayIcon.Visible = false;
                                Show();
                            }),
                            new MenuItem("Exit", (s1, e1) =>
                            {
                                Close();
                                trayIcon.Visible = false;
                                Application.Exit();
                            }),
                        }),
                    };

                    trayIcon.MouseClick += (s1, e1) =>
                    {
                        if (e1.Button != MouseButtons.Left)
                            return;

                        trayIcon.Visible = false;
                        Show();
                    };
                }

                trayIcon.Visible = true;
                e.Cancel = true;
                Hide();
            }
        }

        private void timerRefreshStats()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
            }

            bool auto = checkBoxAuto.Checked;
            bool refresh = Properties.Settings.Default.autoRefreshStats;

            if (!auto && !refresh)
                return;

            timer = new Timer();
            timer.Interval = (int)numericUpdateInterval.Value;
            timer.Tick += new EventHandler(TimerEventProcessor);
            timer.Start();
        }

        private void TimerEventProcessor(object sender, EventArgs e)
        {
            buttonRefreshRPM_Click(sender, e);
            buttonRefreshCPUTemp_Click(sender, e);
            // Auto-control runs in background task; UI timer only updates stats when configured
        }

        private void toolStripMenuItemTurnOffControlOnExit_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.turnOffControlOnExit = toolStripMenuItemTurnOffControlOnExit.Checked;
            Properties.Settings.Default.Save();
        }

        private void toolStripMenuItemForbidUnsafeSettings_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.forbidUnsafeSettings = toolStripMenuItemForbidUnsafeSettings.Checked;
            Properties.Settings.Default.Save();
        }

        private void toolStripMenuItemMinimizeToTrayOnClose_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.minimizeToTrayOnClose = toolStripMenuItemMinimizeToTrayOnClose.Checked;
            Properties.Settings.Default.Save();
        }

        private void toolStripMenuItemAutoRefreshStats_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.autoRefreshStats = toolStripMenuItemAutoRefreshStats.Checked;
            Properties.Settings.Default.Save();

            timerRefreshStats();
        }

        private async void toolStripMenuItemStartWithWindows_CheckedChanged(object sender, EventArgs e)
        {
            var want = toolStripMenuItemStartWithWindows.Checked;
            try
            {
                var ok = false;
                await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        if (want)
                            ok = TaskSchedulerHelper.RegisterTask(Application.ExecutablePath);
                        else
                            ok = TaskSchedulerHelper.UnregisterTask();
                    }
                    catch { ok = false; }
                });

                if (!ok)
                {
                    // revert
                    toolStripMenuItemStartWithWindows.Checked = !want;
                    MessageBox.Show("Failed to update Startup task. You may need elevated privileges.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                toolStripMenuItemStartWithWindows.Checked = !want;
                MessageBox.Show("Failed to update Startup task: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void toolStripMenuItemProfileManager_Click(object sender, EventArgs e)
        {
            using (var dlg = new ProfileEditorDialog(_profileManager, currentFanCurve))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        Properties.Settings.Default.profiles = _profileManager.SaveProfiles();
                        Properties.Settings.Default.Save();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[ProfileManager] Failed to save profiles: " + ex);
                    }
                }
            }
        }

        private void toolStripMenuItemCheckForUpdates_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/Karmel0x/AsusFanControl/releases");
        }

        private void setFanSpeed()
        {
            if (checkBoxAuto.Checked || asusControl == null)
                return;

            var value = trackBarFanSpeed.Value;
            Properties.Settings.Default.fanSpeed = value;
            Properties.Settings.Default.Save();

            if (!checkBoxTurnOn.Checked)
                value = 0;

            if (value == 0)
                labelValue.Text = "turned off";
            else
                labelValue.Text = value.ToString();

            if (fanSpeed == value)
                return;

            fanSpeed = value;

            asusControl.SetFanSpeeds(value);
        }

        private void checkBoxTurnOn_CheckedChanged(object sender, EventArgs e)
        {
            setFanSpeed();
        }

        private void trackBarFanSpeed_MouseCaptureChanged(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.forbidUnsafeSettings)
            {
                if (trackBarFanSpeed.Value < 40)
                    trackBarFanSpeed.Value = 40;
                else if (trackBarFanSpeed.Value > 99)
                    trackBarFanSpeed.Value = 99;
            }

            setFanSpeed();
        }

        private void trackBarFanSpeed_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Left && e.KeyCode != Keys.Right)
                return;

            trackBarFanSpeed_MouseCaptureChanged(sender, e);
        }

        private void buttonRefreshRPM_Click(object sender, EventArgs e)
        {
            if (asusControl != null)
                labelRPM.Text = string.Join(" ", asusControl.GetFanSpeeds());
        }

        private void buttonRefreshCPUTemp_Click(object sender, EventArgs e)
        {
            if (asusControl != null)
                labelCPUTemp.Text = $"{asusControl.Thermal_Read_Cpu_Temperature()}";
        }

        private void checkBoxAuto_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.autoMode = checkBoxAuto.Checked;
            Properties.Settings.Default.Save();
            updateUIState();

            // Start or stop background auto-control
            try
            {
                if (checkBoxAuto.Checked)
                {
                    // start
                    _autoCts = new System.Threading.CancellationTokenSource();
                    var token = _autoCts.Token;
                    _autoControlTask = System.Threading.Tasks.Task.Run(async () =>
                    {
                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                await System.Threading.Tasks.Task.Delay(Properties.Settings.Default.updateInterval, token);
                                if (asusControl == null) continue;
                                var tempU = asusControl.Thermal_Read_Cpu_Temperature();
                                int temp = (int)tempU;
                                int targetSpeed = currentFanCurve.GetTargetSpeed(temp);

                                // Hysteresis
                                if (targetSpeed > fanSpeed || Math.Abs(targetSpeed - fanSpeed) > 2)
                                {
                                    fanSpeed = targetSpeed;
                                    try { asusControl.SetFanSpeeds(targetSpeed); } catch { }
                                }

                                // update UI
                                try { this.BeginInvoke(new Action(() => labelValue.Text = targetSpeed.ToString() + " (Auto)")); } catch { }
                            }
                            catch (OperationCanceledException) { break; }
                            catch { }
                        }
                    }, token);
                }
                else
                {
                    // stop
                    try { _autoCts?.Cancel(); } catch { }
                    try { _autoControlTask?.Wait(500); } catch { }
                    try { _autoCts?.Dispose(); } catch { }
                    _autoCts = null;
                    _autoControlTask = null;
                }
            }
            catch { }
        }

        private void buttonEditCurve_Click(object sender, EventArgs e)
        {
            var editor = new FanCurveEditor(currentFanCurve);
            if (editor.ShowDialog() == DialogResult.OK)
            {
                currentFanCurve = editor.ResultCurve;
                Properties.Settings.Default.fanCurve = currentFanCurve.ToString();
                Properties.Settings.Default.Save();
            }
        }

        private void numericUpdateInterval_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.updateInterval = (int)numericUpdateInterval.Value;
            Properties.Settings.Default.Save();
            timerRefreshStats();
        }

        private void toolStripMenuItemStartLogging_Click(object sender, EventArgs e)
        {
            if (loggingTimer != null && loggingTimer.Enabled)
            {
                stopLogging();
            }
            else
            {
                startLogging();
            }
        }

        private void startLogging()
        {
            using (var dlg = new LoggingDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        loggingWriter = new StreamWriter(new FileStream(dlg.FilePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true));
                        if (loggingWriter.BaseStream.Length == 0)
                        {
                            loggingWriter.WriteLine("Timestamp,CPU Temp (C),Fan Speed (RPM),CPU Load (%)");
                        }

                        loggingTimer = new Timer();
                        loggingTimer.Interval = dlg.Interval;
                        loggingTimer.Tick += LoggingTimer_Tick;
                        loggingTimer.Start();

                        toolStripMenuItemStartLogging.Text = "Stop Logging";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error starting logging: " + ex.Message);
                        stopLogging();
                    }
                }
            }
        }

        private void stopLogging()
        {
            if (loggingTimer != null)
            {
                loggingTimer.Stop();
                loggingTimer.Dispose();
                loggingTimer = null;
            }

            if (loggingWriter != null)
            {
                try
                {
                    loggingWriter.Close();
                    loggingWriter.Dispose();
                }
                catch { }
                loggingWriter = null;
            }

            if (toolStripMenuItemStartLogging != null && !toolStripMenuItemStartLogging.IsDisposed)
                toolStripMenuItemStartLogging.Text = "Start Logging";
        }

        private async void LoggingTimer_Tick(object sender, EventArgs e)
        {
            if (isLoggingWriting) return;
            isLoggingWriting = true;
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var cpuTemp = asusControl.Thermal_Read_Cpu_Temperature();
                var fanSpeeds = string.Join("|", asusControl.GetFanSpeeds());

                float cpuLoad = 0;
                if (cpuCounter != null)
                {
                    cpuLoad = await Task.Run(() => cpuCounter.NextValue());
                }

                if (loggingWriter != null)
                {
                     await loggingWriter.WriteLineAsync($"{timestamp},{cpuTemp},{fanSpeeds},{cpuLoad:F2}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoggingTimer] Error: {ex.Message}");
            }
            finally
            {
                isLoggingWriting = false;
            }
        }
    }
}
