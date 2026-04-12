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
        private IFanController _fanController;
        int fanSpeed = 0;
        Timer timer;
        NotifyIcon trayIcon;
        FanCurve currentFanCurve;
        PerformanceCounter cpuCounter;
        CsvLogger _csvLogger;
        System.Threading.CancellationTokenSource _backgroundCts;
        ProfileManager _profileManager;
        string _activeProfileName;
        System.Threading.Tasks.Task _profileMonitorTask;
        AutoFanController _autoFanController;

        public Form1(IFanController fanController)
        {
            _fanController = fanController ?? throw new ArgumentNullException(nameof(fanController));

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
            AppDomain.CurrentDomain.UnhandledException += (s, e) => { try { if (_fanController != null) _fanController.ResetToDefault(); } catch (Exception ex) { Debug.WriteLine($"[UnhandledException] Reset error: {ex.Message}"); } };
            Application.ThreadException += (s, e) => { try { if (_fanController != null) _fanController.ResetToDefault(); } catch (Exception ex) { Debug.WriteLine($"[ThreadException] Reset error: {ex.Message}"); } };

            currentFanCurve = FanCurve.FromJson(Properties.Settings.Default.fanCurve);
            if (currentFanCurve.PointCount == 0)
            {
                 currentFanCurve.SetPoints(new[]
                 {
                     new FanCurvePoint(30, 0),
                     new FanCurvePoint(60, 50),
                     new FanCurvePoint(90, 100)
                 });
            }

            _autoFanController = new AutoFanController(_fanController, currentFanCurve);
            _autoFanController.FanSpeedChanged += (s, speed) =>
            {
                try { if (!IsDisposed) this.BeginInvoke(new Action(() => labelValue.Text = speed.ToString() + " (Auto)")); } catch { }
            };

            toolStripMenuItemTurnOffControlOnExit.Checked = Properties.Settings.Default.turnOffControlOnExit;
            toolStripMenuItemForbidUnsafeSettings.Checked = Properties.Settings.Default.forbidUnsafeSettings;
            toolStripMenuItemMinimizeToTrayOnClose.Checked = Properties.Settings.Default.minimizeToTrayOnClose;
            toolStripMenuItemAutoRefreshStats.Checked = Properties.Settings.Default.autoRefreshStats;
            trackBarFanSpeed.Value = Properties.Settings.Default.fanSpeed;

            checkBoxAuto.Checked = Properties.Settings.Default.autoMode;
            numericUpdateInterval.Value = Properties.Settings.Default.updateInterval;

            updateUIState();

            // Ensure auto-controller starts if already checked
            if (checkBoxAuto.Checked)
            {
                _autoFanController.Start(Properties.Settings.Default.updateInterval);
            }

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
                            Properties.Settings.Default.fanCurve = currentFanCurve.ToJson();
                            Properties.Settings.Default.Save();
                            _autoFanController?.UpdateFanCurve(currentFanCurve);
                            try { if (_fanController != null) _fanController.SetFanSpeeds(fanSpeed); } catch { }
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
                
                trayIcon?.Dispose();

                // Custom cleanup
                _autoFanController?.Dispose();
                _csvLogger?.Dispose();

                if (_fanController != null)
                {
                    if (Properties.Settings.Default.turnOffControlOnExit)
                    {
                        try
                        {
                            _fanController.ResetToDefault();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Dispose] Reset error: {ex.Message}");
                        }
                    }

                    try
                    {
                        _fanController.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Dispose] Dispose error: {ex.Message}");
                    }

                    _fanController = null; // Prevent use-after-dispose
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
            if (Properties.Settings.Default.turnOffControlOnExit && _fanController != null)
            {
                try
                {
                    _fanController.ResetToDefault();
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
            try
            {
                var want = toolStripMenuItemStartWithWindows.Checked;
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
                // revert if possible
                try { toolStripMenuItemStartWithWindows.Checked = !toolStripMenuItemStartWithWindows.Checked; } catch { }
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
            if (checkBoxAuto.Checked || _fanController == null)
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

            _fanController.SetFanSpeeds(value);
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
            if (_fanController != null)
                labelRPM.Text = string.Join(" ", _fanController.GetFanSpeeds());
        }

        private void buttonRefreshCPUTemp_Click(object sender, EventArgs e)
        {
            if (_fanController != null)
                labelCPUTemp.Text = $"{_fanController.Thermal_Read_Cpu_Temperature()}";
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
                    _autoFanController?.UpdateFanCurve(currentFanCurve);
                    _autoFanController?.Start(Properties.Settings.Default.updateInterval);
                }
                else
                {
                    _autoFanController?.Stop();
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
                Properties.Settings.Default.fanCurve = currentFanCurve.ToJson();
                Properties.Settings.Default.Save();
                _autoFanController?.UpdateFanCurve(currentFanCurve);
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
            if (_csvLogger != null)
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
                        if (_csvLogger == null)
                        {
                            _csvLogger = new CsvLogger(
                                () => _fanController != null ? _fanController.Thermal_Read_Cpu_Temperature() : 0,
                                () => _fanController != null ? string.Join("|", _fanController.GetFanSpeeds()) : "0",
                                async () => {
                                    if (cpuCounter == null) return 0;
                                    try { return await Task.Run(() => cpuCounter.NextValue()); } catch { return 0; }
                                }
                            );
                        }

                        _csvLogger.Start(dlg.FilePath, dlg.Interval);
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
            if (_csvLogger != null)
            {
                _csvLogger.Stop();
                _csvLogger.Dispose();
                _csvLogger = null;
            }

            if (toolStripMenuItemStartLogging != null && !toolStripMenuItemStartLogging.IsDisposed)
                toolStripMenuItemStartLogging.Text = "Start Logging";
        }
    }
}
