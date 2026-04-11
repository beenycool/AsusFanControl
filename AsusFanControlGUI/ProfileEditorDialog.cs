using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AsusFanControl.Core;

namespace AsusFanControlGUI
{
    public class ProfileEditorDialog : Form
    {
        private ListBox listProfiles;
        private Button buttonAdd;
        private Button buttonRemove;
        private Button buttonClose;
        private ProfileManager _profileManager;
        private FanCurve _defaultCurve;

        public ProfileEditorDialog(ProfileManager profileManager, FanCurve defaultCurve)
        {
            _profileManager = profileManager;
            _defaultCurve = defaultCurve;
            InitializeComponents();
            RefreshList();
        }

        private void InitializeComponents()
        {
            this.Text = "Profile Manager";
            this.Size = new Size(420, 350);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            listProfiles = new ListBox
            {
                Location = new Point(12, 12),
                Size = new Size(380, 200),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(listProfiles);

            buttonAdd = new Button
            {
                Text = "Add Profile",
                Location = new Point(12, 220),
                Size = new Size(120, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White
            };
            buttonAdd.Click += ButtonAdd_Click;
            this.Controls.Add(buttonAdd);

            buttonRemove = new Button
            {
                Text = "Remove",
                Location = new Point(140, 220),
                Size = new Size(120, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(200, 50, 50),
                ForeColor = Color.White
            };
            buttonRemove.Click += ButtonRemove_Click;
            this.Controls.Add(buttonRemove);

            buttonClose = new Button
            {
                Text = "Close",
                Location = new Point(312, 270),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                DialogResult = DialogResult.OK
            };
            this.Controls.Add(buttonClose);
            this.AcceptButton = buttonClose;
        }

        private void RefreshList()
        {
            listProfiles.Items.Clear();
            foreach (var profile in _profileManager.Profiles)
            {
                var procs = string.Join(", ", profile.TriggerProcesses);
                listProfiles.Items.Add($"{profile.Name}  →  [{procs}]");
            }
        }

        private void ButtonAdd_Click(object sender, EventArgs e)
        {
            using (var dlg = new AddProfileDialog(_defaultCurve))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _profileManager.AddProfile(dlg.ResultProfile);
                    RefreshList();
                }
            }
        }

        private void ButtonRemove_Click(object sender, EventArgs e)
        {
            if (listProfiles.SelectedIndex >= 0 && listProfiles.SelectedIndex < _profileManager.Profiles.Count)
            {
                var profile = _profileManager.Profiles[listProfiles.SelectedIndex];
                _profileManager.RemoveProfile(profile.Name);
                RefreshList();
            }
        }
    }

    public class AddProfileDialog : Form
    {
        private TextBox textName;
        private TextBox textProcesses;
        private Button buttonEditCurve;
        private Button buttonOk;
        private Button buttonCancel;
        private FanCurve _curve;

        public FanProfile ResultProfile { get; private set; }

        public AddProfileDialog(FanCurve defaultCurve)
        {
            _curve = new FanCurve();
            if (defaultCurve != null)
            {
                _curve.SetPoints(defaultCurve.Points);
            }
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "Add Profile";
            this.Size = new Size(380, 220);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            var labelName = new Label { Text = "Profile Name:", Location = new Point(12, 15), AutoSize = true };
            this.Controls.Add(labelName);

            textName = new TextBox
            {
                Location = new Point(120, 12),
                Size = new Size(230, 20),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            this.Controls.Add(textName);

            var labelProcs = new Label { Text = "Trigger Processes:", Location = new Point(12, 45), AutoSize = true };
            this.Controls.Add(labelProcs);

            textProcesses = new TextBox
            {
                Location = new Point(120, 42),
                Size = new Size(230, 20),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            this.Controls.Add(textProcesses);

            var labelHint = new Label
            {
                Text = "e.g. chrome,devenv,Cyberpunk2077",
                Location = new Point(120, 65),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font(Font.FontFamily, 7.5f)
            };
            this.Controls.Add(labelHint);

            buttonEditCurve = new Button
            {
                Text = "Edit Fan Curve...",
                Location = new Point(12, 95),
                Size = new Size(120, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White
            };
            buttonEditCurve.Click += (s, e) =>
            {
                var editor = new FanCurveEditor(_curve);
                if (editor.ShowDialog() == DialogResult.OK)
                    _curve = editor.ResultCurve;
            };
            this.Controls.Add(buttonEditCurve);

            buttonOk = new Button
            {
                Text = "OK",
                Location = new Point(190, 140),
                Size = new Size(75, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White
            };
            buttonOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(textName.Text))
                {
                    MessageBox.Show("Please enter a profile name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                var processes = textProcesses.Text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                ResultProfile = new FanProfile(textName.Text.Trim(), _curve, processes);
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(buttonOk);

            buttonCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(275, 140),
                Size = new Size(75, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(buttonCancel);
            this.CancelButton = buttonCancel;
        }
    }
}
