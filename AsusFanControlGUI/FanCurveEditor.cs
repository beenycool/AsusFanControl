using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AsusFanControl.Core;

namespace AsusFanControlGUI
{
    public partial class FanCurveEditor : Form
    {
        public FanCurve ResultCurve { get; private set; }

        public FanCurveEditor(FanCurve existingCurve)
        {
            InitializeComponent();
            // Clone the curve so we don't modify the original until OK is pressed
            ResultCurve = new FanCurve();
            if (existingCurve != null)
            {
                foreach(var p in existingCurve.Points)
                {
                    ResultCurve.Points.Add(new FanCurvePoint(p.Temperature, p.Speed));
                }
            }
        }

        private void FanCurveEditor_Load(object sender, EventArgs e)
        {
            if (ResultCurve != null)
            {
                fanCurveControl1.SetCurve(ResultCurve);
            }
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            ResultCurve = fanCurveControl1.GetCurve();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void buttonImport_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Fan Curve Config (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var content = System.IO.File.ReadAllText(openFileDialog.FileName);
                        var curve = FanCurve.FromString(content);
                        fanCurveControl1.SetCurve(curve);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error importing file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Fan Curve Config (*.txt)|*.txt|All files (*.*)|*.*";
                saveFileDialog.RestoreDirectory = true;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var curve = fanCurveControl1.GetCurve();
                        System.IO.File.WriteAllText(saveFileDialog.FileName, curve.ToString());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error exporting file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}
