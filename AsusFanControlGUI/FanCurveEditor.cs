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
    }
}
