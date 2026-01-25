using System;
using System.Collections.Generic;
using System.Windows.Forms;

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
            if (ResultCurve != null && ResultCurve.Points != null)
            {
                foreach (var p in ResultCurve.Points)
                {
                    dataGridView1.Rows.Add(p.Temperature, p.Speed);
                }
            }
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            var newCurve = new FanCurve();
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.IsNewRow) continue;

                if (row.Cells[0].Value != null && row.Cells[1].Value != null)
                {
                    if (int.TryParse(row.Cells[0].Value.ToString(), out int temp) &&
                        int.TryParse(row.Cells[1].Value.ToString(), out int speed))
                    {
                        // Clamp values
                        if (speed < 0) speed = 0;
                        if (speed > 100) speed = 100;
                        if (temp < 0) temp = 0; // unlikely but safe

                        newCurve.Points.Add(new FanCurvePoint(temp, speed));
                    }
                }
            }

            // Sort
            newCurve.Points.Sort((a, b) => a.Temperature.CompareTo(b.Temperature));

            ResultCurve = newCurve;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
