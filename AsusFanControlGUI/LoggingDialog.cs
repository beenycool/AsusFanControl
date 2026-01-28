using System;
using System.Windows.Forms;

namespace AsusFanControlGUI
{
    public partial class LoggingDialog : Form
    {
        public LoggingDialog()
        {
            InitializeComponent();
        }

        public int Interval
        {
            get { return (int)numericInterval.Value; }
        }

        public string FilePath
        {
            get { return textBoxFilePath.Text; }
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                saveFileDialog.Title = "Save Log File";
                saveFileDialog.FileName = "log.csv";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    textBoxFilePath.Text = saveFileDialog.FileName;
                }
            }
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxFilePath.Text))
            {
                MessageBox.Show("Please select a file path to save the log.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
