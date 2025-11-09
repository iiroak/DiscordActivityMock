using Microsoft.Win32;

namespace DiscordActivityMock
{
    public partial class DiscordActivityMock : Form
    {
        private string? _WordPadFolderPath;
        private string? _WordPadExePath;

        public DiscordActivityMock()
        {
            InitializeComponent();
            WordPad_ActivityList.Items.Add("Custom Activity");
            WordPad_ActivityList.SelectedIndex = 0;
            WordPad_Activity.Enabled = false;
            WordPad_ActivityList.Enabled = false;
            WordPad_ActivityFetch.Enabled  = false;
            WordPad_FolderName.Enabled = false;
            WordPad_FileExe.Enabled = false;
            WordPad_FileClear.Enabled = false;
            CleanTemporaryFiles();
        }

        private void ToggleStep2()
        {
            WordPad_ActivityList.Enabled = !WordPad_ActivityList.Enabled;
            WordPad_ActivityFetch.Enabled = !WordPad_ActivityFetch.Enabled;
        }

        private void ToggleStep21()
        {
            WordPad_FolderName.Enabled = !WordPad_FolderName.Enabled;
            WordPad_FileExe.Enabled = !WordPad_FileExe.Enabled;
            WordPad_FileClear.Enabled = !WordPad_FileClear.Enabled;
        }

        private string CopyWordPadFolder()
        {
            string pathWordPad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows NT", "Accessories");
            string tempPath = Path.Combine(Path.GetTempPath(), "WordPadBackup");
            try
            {
                CleanTemporaryFiles();
                Directory.CreateDirectory(tempPath);
                foreach (var file in Directory.GetFiles(pathWordPad))
                {
                    string destFile = Path.Combine(tempPath, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error at copying WordPad folder: {ex.Message}");
                return "";
            }
            return tempPath;
        }

        private void WordPadSystem_Click(object sender, EventArgs e)
        {
            string tempPath = CopyWordPadFolder();
            if (tempPath != "")
            {
                WordPadStatus.Text = "Status: Ready";
                WordPadStatus.ForeColor = System.Drawing.Color.Green;
                _WordPadFolderPath = tempPath;
                _WordPadExePath = Path.Combine(tempPath, "wordpad.exe");
                this.ToggleStep2();
            }
        }

        private void WordPad_Activity_Click(object sender, EventArgs e)
        {
            if (File.Exists(_WordPadExePath))
            {
                if (Directory.Exists(_WordPadFolderPath))
                {
                    try
                    {
                        // Check if the new names are the same as the current ones
                        string newExePath = Path.Combine(Path.GetDirectoryName(_WordPadExePath)!, WordPad_FileExe.Text + ".exe");
                        string newFolderPath = Path.Combine(Path.GetDirectoryName(_WordPadFolderPath)!, WordPad_FolderName.Text);

                        // Update the executable path to reflect the new folder name
                        string updatedExePath = Path.Combine(Path.GetDirectoryName(_WordPadExePath)!, Path.GetFileName(newExePath));

                        // Rename the executable file
                        if (!_WordPadExePath.Equals(updatedExePath, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Move(_WordPadExePath, updatedExePath);
                            _WordPadExePath = updatedExePath;
                        }

                        // Rename the folder first
                        if (!_WordPadFolderPath.Equals(newFolderPath, StringComparison.OrdinalIgnoreCase))
                        {
                            Directory.Move(_WordPadFolderPath, newFolderPath);
                            _WordPadFolderPath = newFolderPath;
                        }

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error during renaming: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Folder not found. Please copy WordPad again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show("Executable not found. Please copy WordPad again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CleanTemporaryFiles()
        {
            try
            {
                // Clean up WordPadBackup folder
                string tempPath = Path.Combine(Path.GetTempPath(), "WordPadBackup");
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }

                // Clean up any previous WordPad folder if it exists
                if (!string.IsNullOrEmpty(_WordPadFolderPath) && Directory.Exists(_WordPadFolderPath))
                {
                    Directory.Delete(_WordPadFolderPath, true);
                    _WordPadFolderPath = null;
                    _WordPadExePath = null;
                }
            }
            catch
            {
                // Silently ignore cleanup errors to avoid unnecessary user notifications
                // Files might be in use or already deleted
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CleanTemporaryFiles();
            base.OnFormClosing(e);
        }

    }
}
