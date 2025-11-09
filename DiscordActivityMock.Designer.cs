namespace DiscordActivityMock
{
    partial class DiscordActivityMock
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            WordPadSystem = new Button();
            WordPadGitHub = new Button();
            WordPadStatus = new Label();
            label3 = new Label();
            WordPad_ActivityList = new ComboBox();
            WordPad_ActivityFetch = new Button();
            label4 = new Label();
            WordPad_FolderName = new TextBox();
            label5 = new Label();
            label6 = new Label();
            WordPad_FileExe = new TextBox();
            WordPad_FileClear = new Button();
            label7 = new Label();
            label8 = new Label();
            WordPad_Activity = new Button();
            button6 = new Button();
            SuspendLayout();
            // 
            // label1
            // 
            label1.Location = new Point(16, 9);
            label1.Name = "label1";
            label1.Size = new Size(128, 23);
            label1.TabIndex = 0;
            label1.Text = "Step 1:  Get WordPad";
            label1.TextAlign = ContentAlignment.BottomCenter;
            // 
            // WordPadSystem
            // 
            WordPadSystem.Location = new Point(150, 9);
            WordPadSystem.Name = "WordPadSystem";
            WordPadSystem.Size = new Size(180, 44);
            WordPadSystem.TabIndex = 1;
            WordPadSystem.Text = "Copy WordPad System";
            WordPadSystem.UseVisualStyleBackColor = true;
            WordPadSystem.Click += WordPadSystem_Click;
            // 
            // WordPadGitHub
            // 
            WordPadGitHub.Location = new Point(336, 9);
            WordPadGitHub.Name = "WordPadGitHub";
            WordPadGitHub.Size = new Size(180, 44);
            WordPadGitHub.TabIndex = 2;
            WordPadGitHub.Text = "Download Github Backup";
            WordPadGitHub.UseVisualStyleBackColor = true;
            // 
            // WordPadStatus
            // 
            WordPadStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            WordPadStatus.ForeColor = Color.Red;
            WordPadStatus.Location = new Point(16, 32);
            WordPadStatus.Name = "WordPadStatus";
            WordPadStatus.Size = new Size(128, 20);
            WordPadStatus.TabIndex = 3;
            WordPadStatus.Text = "Status: Pending";
            WordPadStatus.TextAlign = ContentAlignment.TopCenter;
            // 
            // label3
            // 
            label3.Location = new Point(16, 61);
            label3.Name = "label3";
            label3.Size = new Size(128, 23);
            label3.TabIndex = 4;
            label3.Text = "Step 2: Select Activity";
            label3.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // WordPad_ActivityList
            // 
            WordPad_ActivityList.FormattingEnabled = true;
            WordPad_ActivityList.Location = new Point(150, 61);
            WordPad_ActivityList.Name = "WordPad_ActivityList";
            WordPad_ActivityList.Size = new Size(267, 23);
            WordPad_ActivityList.TabIndex = 5;
            // 
            // WordPad_ActivityFetch
            // 
            WordPad_ActivityFetch.Location = new Point(423, 61);
            WordPad_ActivityFetch.Name = "WordPad_ActivityFetch";
            WordPad_ActivityFetch.Size = new Size(93, 23);
            WordPad_ActivityFetch.TabIndex = 6;
            WordPad_ActivityFetch.Text = "Fetch List";
            WordPad_ActivityFetch.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            label4.Location = new Point(16, 94);
            label4.Name = "label4";
            label4.Size = new Size(128, 43);
            label4.TabIndex = 7;
            label4.Text = "Step 2.1: Custom Activity";
            label4.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // WordPad_FolderName
            // 
            WordPad_FolderName.Location = new Point(257, 94);
            WordPad_FolderName.Name = "WordPad_FolderName";
            WordPad_FolderName.Size = new Size(160, 23);
            WordPad_FolderName.TabIndex = 8;
            WordPad_FolderName.Text = "Accessories";
            // 
            // label5
            // 
            label5.Location = new Point(150, 94);
            label5.Name = "label5";
            label5.Size = new Size(101, 23);
            label5.TabIndex = 9;
            label5.Text = "Name Folder";
            label5.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label6
            // 
            label6.Location = new Point(150, 117);
            label6.Name = "label6";
            label6.Size = new Size(101, 23);
            label6.TabIndex = 10;
            label6.Text = "Name Executable";
            label6.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // WordPad_FileExe
            // 
            WordPad_FileExe.Location = new Point(257, 118);
            WordPad_FileExe.Name = "WordPad_FileExe";
            WordPad_FileExe.Size = new Size(160, 23);
            WordPad_FileExe.TabIndex = 11;
            WordPad_FileExe.Text = "M1-Win64-Shipping";
            // 
            // WordPad_FileClear
            // 
            WordPad_FileClear.Location = new Point(423, 94);
            WordPad_FileClear.Name = "WordPad_FileClear";
            WordPad_FileClear.Size = new Size(93, 23);
            WordPad_FileClear.TabIndex = 12;
            WordPad_FileClear.Text = "Clear";
            WordPad_FileClear.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            label7.Location = new Point(16, 146);
            label7.Name = "label7";
            label7.Size = new Size(128, 23);
            label7.TabIndex = 13;
            label7.Text = "Step 4: Run Activity";
            label7.TextAlign = ContentAlignment.BottomCenter;
            // 
            // label8
            // 
            label8.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label8.ForeColor = Color.Red;
            label8.Location = new Point(16, 169);
            label8.Name = "label8";
            label8.Size = new Size(128, 20);
            label8.TabIndex = 14;
            label8.Text = "Status: Inactive";
            label8.TextAlign = ContentAlignment.TopCenter;
            // 
            // WordPad_Activity
            // 
            WordPad_Activity.Location = new Point(150, 147);
            WordPad_Activity.Name = "WordPad_Activity";
            WordPad_Activity.Size = new Size(366, 44);
            WordPad_Activity.TabIndex = 15;
            WordPad_Activity.Text = "Start Activity";
            WordPad_Activity.UseVisualStyleBackColor = true;
            WordPad_Activity.Click += WordPad_Activity_Click;
            // 
            // button6
            // 
            button6.Location = new Point(423, 118);
            button6.Name = "button6";
            button6.Size = new Size(93, 23);
            button6.TabIndex = 16;
            button6.Text = "GitHub";
            button6.UseVisualStyleBackColor = true;
            // 
            // DiscordActivityMock
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            ClientSize = new Size(538, 201);
            Controls.Add(button6);
            Controls.Add(WordPad_Activity);
            Controls.Add(label8);
            Controls.Add(label7);
            Controls.Add(WordPad_FileClear);
            Controls.Add(WordPad_FileExe);
            Controls.Add(label6);
            Controls.Add(label5);
            Controls.Add(WordPad_FolderName);
            Controls.Add(label4);
            Controls.Add(WordPad_ActivityFetch);
            Controls.Add(WordPad_ActivityList);
            Controls.Add(label3);
            Controls.Add(WordPadStatus);
            Controls.Add(WordPadGitHub);
            Controls.Add(WordPadSystem);
            Controls.Add(label1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "DiscordActivityMock";
            Text = "Discord Activity Mock";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private Button WordPadSystem;
        private Button WordPadGitHub;
        private Label WordPadStatus;
        private Label label3;
        private ComboBox WordPad_ActivityList;
        private Button WordPad_ActivityFetch;
        private Label label4;
        private TextBox WordPad_FolderName;
        private Label label5;
        private Label label6;
        private TextBox WordPad_FileExe;
        private Button WordPad_FileClear;
        private Label label7;
        private Label label8;
        private Button WordPad_Activity;
        private Button button6;
    }
}
