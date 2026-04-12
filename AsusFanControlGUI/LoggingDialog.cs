using System;
using System.IO;
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

        static string GetInitialLogDirectory()
        {
            foreach (var folder in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            })
            {
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    return folder;
            }

            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "AsusFanControl",
                "Logs");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                saveFileDialog.Title = "Save Log File";
                saveFileDialog.FileName = "log.csv";
                saveFileDialog.InitialDirectory = GetInitialLogDirectory();
                saveFileDialog.RestoreDirectory = true;

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

            try
            {
                var fullPath = Path.GetFullPath(textBoxFilePath.Text.Trim());
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                textBoxFilePath.Text = fullPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Invalid file path: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
