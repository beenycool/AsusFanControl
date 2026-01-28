using AsusFanControl.Core;
using System;
using System.Windows.Forms;

namespace AsusFanControlGUI
{
    public partial class Form1 : Form
    {
        AsusControl asusControl = new AsusControl();
        int fanSpeed = 0;
        Timer timer;
        NotifyIcon trayIcon;
        FanCurve currentFanCurve;

        public Form1()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            // Watchdog for crash
            AppDomain.CurrentDomain.UnhandledException += (s, e) => { try { if (asusControl != null) asusControl.ResetToDefault(); } catch { } };
            Application.ThreadException += (s, e) => { try { if (asusControl != null) asusControl.ResetToDefault(); } catch { } };

            toolStripMenuItemTurnOffControlOnExit.Checked = Properties.Settings.Default.turnOffControlOnExit;
            toolStripMenuItemForbidUnsafeSettings.Checked = Properties.Settings.Default.forbidUnsafeSettings;
            toolStripMenuItemMinimizeToTrayOnClose.Checked = Properties.Settings.Default.minimizeToTrayOnClose;
            toolStripMenuItemAutoRefreshStats.Checked = Properties.Settings.Default.autoRefreshStats;
            trackBarFanSpeed.Value = Properties.Settings.Default.fanSpeed;

            checkBoxAuto.Checked = Properties.Settings.Default.autoMode;
            numericUpdateInterval.Value = Properties.Settings.Default.updateInterval;
            currentFanCurve = FanCurve.FromString(Properties.Settings.Default.fanCurve);

            if (currentFanCurve.Points.Count == 0)
            {
                 // Default curve
                 currentFanCurve.Points.Add(new FanCurvePoint(30, 0));
                 currentFanCurve.Points.Add(new FanCurvePoint(60, 50));
                 currentFanCurve.Points.Add(new FanCurvePoint(90, 100));
            }

            updateUIState();
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
                    // Ensure fans are reset if configured to do so
                    if (Properties.Settings.Default.turnOffControlOnExit)
                    {
                        try
                        {
                            asusControl.ResetToDefault();
                        }
                        catch
                        {
                            // Swallow exceptions during disposal to prevent crashing
                        }
                    }

                    try
                    {
                        asusControl.Dispose();
                    }
                    catch { }

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
                catch
                {
                    // Ignore exceptions during shutdown
                }
            }
            // Do not dispose here to avoid race with Form.Dispose(bool) or double-dispose.
            // asusControl.Dispose() will be called when the Form is disposed.
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

            if (checkBoxAuto.Checked && asusControl != null)
            {
                ulong tempU = asusControl.Thermal_Read_Cpu_Temperature();
                int temp = (int)tempU;
                int targetSpeed = currentFanCurve.GetTargetSpeed(temp);

                labelValue.Text = targetSpeed.ToString() + " (Auto)";

                // Hysteresis to prevent rapid oscillation
                if (targetSpeed > fanSpeed || Math.Abs(targetSpeed - fanSpeed) > 2)
                {
                    fanSpeed = targetSpeed;
                    asusControl.SetFanSpeeds(targetSpeed);
                }
            }
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
    }
}