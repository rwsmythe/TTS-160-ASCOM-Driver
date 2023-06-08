namespace ASCOM.TTS160
{
    partial class SetupDialogForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.cmdOK = new System.Windows.Forms.Button();
            this.cmdCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.picASCOM = new System.Windows.Forms.PictureBox();
            this.label2 = new System.Windows.Forms.Label();
            this.chkTrace = new System.Windows.Forms.CheckBox();
            this.comboBoxComPort = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.SiteAltTxt = new System.Windows.Forms.TextBox();
            this.SlewSetTimeTxt = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.SiteLatlbl = new System.Windows.Forms.Label();
            this.SiteLonglbl = new System.Windows.Forms.Label();
            this.compatBox = new System.Windows.Forms.GroupBox();
            this.noneBtn = new System.Windows.Forms.RadioButton();
            this.mpmBtn = new System.Windows.Forms.RadioButton();
            ((System.ComponentModel.ISupportInitialize)(this.picASCOM)).BeginInit();
            this.compatBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // cmdOK
            // 
            this.cmdOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.cmdOK.Location = new System.Drawing.Point(1103, 715);
            this.cmdOK.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.cmdOK.Name = "cmdOK";
            this.cmdOK.Size = new System.Drawing.Size(157, 57);
            this.cmdOK.TabIndex = 0;
            this.cmdOK.Text = "OK";
            this.cmdOK.UseVisualStyleBackColor = true;
            this.cmdOK.Click += new System.EventHandler(this.cmdOK_Click);
            // 
            // cmdCancel
            // 
            this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdCancel.Location = new System.Drawing.Point(1103, 787);
            this.cmdCancel.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.cmdCancel.Name = "cmdCancel";
            this.cmdCancel.Size = new System.Drawing.Size(157, 60);
            this.cmdCancel.TabIndex = 1;
            this.cmdCancel.Text = "Cancel";
            this.cmdCancel.UseVisualStyleBackColor = true;
            this.cmdCancel.Click += new System.EventHandler(this.cmdCancel_Click);
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(32, 21);
            this.label1.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(328, 74);
            this.label1.TabIndex = 2;
            this.label1.Text = "Construct your driver\'s setup dialog here.";
            // 
            // picASCOM
            // 
            this.picASCOM.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.picASCOM.Cursor = System.Windows.Forms.Cursors.Hand;
            this.picASCOM.Image = global::ASCOM.TTS160.Properties.Resources.ASCOM;
            this.picASCOM.Location = new System.Drawing.Point(1133, 21);
            this.picASCOM.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.picASCOM.Name = "picASCOM";
            this.picASCOM.Size = new System.Drawing.Size(48, 56);
            this.picASCOM.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.picASCOM.TabIndex = 3;
            this.picASCOM.TabStop = false;
            this.picASCOM.Click += new System.EventHandler(this.BrowseToAscom);
            this.picASCOM.DoubleClick += new System.EventHandler(this.BrowseToAscom);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(35, 215);
            this.label2.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(155, 32);
            this.label2.TabIndex = 5;
            this.label2.Text = "Comm Port";
            // 
            // chkTrace
            // 
            this.chkTrace.AutoSize = true;
            this.chkTrace.Location = new System.Drawing.Point(205, 281);
            this.chkTrace.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.chkTrace.Name = "chkTrace";
            this.chkTrace.Size = new System.Drawing.Size(163, 36);
            this.chkTrace.TabIndex = 6;
            this.chkTrace.Text = "Trace on";
            this.chkTrace.UseVisualStyleBackColor = true;
            // 
            // comboBoxComPort
            // 
            this.comboBoxComPort.FormattingEnabled = true;
            this.comboBoxComPort.Location = new System.Drawing.Point(205, 207);
            this.comboBoxComPort.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.comboBoxComPort.Name = "comboBoxComPort";
            this.comboBoxComPort.Size = new System.Drawing.Size(233, 39);
            this.comboBoxComPort.TabIndex = 7;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(526, 207);
            this.label3.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(216, 32);
            this.label3.TabIndex = 8;
            this.label3.Text = "Site Altitude (m)";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(526, 283);
            this.label4.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(319, 32);
            this.label4.TabIndex = 9;
            this.label4.Text = "Slew Settling Time (sec)";
            this.label4.Click += new System.EventHandler(this.label4_Click);
            // 
            // SiteAltTxt
            // 
            this.SiteAltTxt.CharacterCasing = System.Windows.Forms.CharacterCasing.Lower;
            this.SiteAltTxt.Location = new System.Drawing.Point(873, 200);
            this.SiteAltTxt.Name = "SiteAltTxt";
            this.SiteAltTxt.Size = new System.Drawing.Size(268, 38);
            this.SiteAltTxt.TabIndex = 10;
            this.SiteAltTxt.Text = "0";
            this.SiteAltTxt.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.SiteAltTxt.TextChanged += new System.EventHandler(this.SiteAltTxt_TextChanged);
            // 
            // SlewSetTimeTxt
            // 
            this.SlewSetTimeTxt.CharacterCasing = System.Windows.Forms.CharacterCasing.Lower;
            this.SlewSetTimeTxt.Location = new System.Drawing.Point(873, 277);
            this.SlewSetTimeTxt.Name = "SlewSetTimeTxt";
            this.SlewSetTimeTxt.Size = new System.Drawing.Size(268, 38);
            this.SlewSetTimeTxt.TabIndex = 11;
            this.SlewSetTimeTxt.Text = "2";
            this.SlewSetTimeTxt.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.SlewSetTimeTxt.TextChanged += new System.EventHandler(this.SlewSetTimeTxt_TextChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(35, 416);
            this.label5.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(304, 32);
            this.label5.TabIndex = 12;
            this.label5.Text = "Recorded Site Latitude";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(35, 469);
            this.label6.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(328, 32);
            this.label6.TabIndex = 13;
            this.label6.Text = "Recorded Site Longitude";
            // 
            // SiteLatlbl
            // 
            this.SiteLatlbl.AutoSize = true;
            this.SiteLatlbl.BackColor = System.Drawing.Color.White;
            this.SiteLatlbl.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.SiteLatlbl.Location = new System.Drawing.Point(414, 416);
            this.SiteLatlbl.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.SiteLatlbl.MinimumSize = new System.Drawing.Size(304, 32);
            this.SiteLatlbl.Name = "SiteLatlbl";
            this.SiteLatlbl.Size = new System.Drawing.Size(304, 34);
            this.SiteLatlbl.TabIndex = 14;
            // 
            // SiteLonglbl
            // 
            this.SiteLonglbl.AutoSize = true;
            this.SiteLonglbl.BackColor = System.Drawing.Color.White;
            this.SiteLonglbl.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.SiteLonglbl.Location = new System.Drawing.Point(414, 469);
            this.SiteLonglbl.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.SiteLonglbl.MinimumSize = new System.Drawing.Size(304, 32);
            this.SiteLonglbl.Name = "SiteLonglbl";
            this.SiteLonglbl.Size = new System.Drawing.Size(304, 34);
            this.SiteLonglbl.TabIndex = 15;
            // 
            // compatBox
            // 
            this.compatBox.Controls.Add(this.mpmBtn);
            this.compatBox.Controls.Add(this.noneBtn);
            this.compatBox.Location = new System.Drawing.Point(41, 553);
            this.compatBox.Name = "compatBox";
            this.compatBox.Size = new System.Drawing.Size(419, 200);
            this.compatBox.TabIndex = 16;
            this.compatBox.TabStop = false;
            this.compatBox.Text = "App Compatibility Mode";
            // 
            // noneBtn
            // 
            this.noneBtn.AutoSize = true;
            this.noneBtn.Checked = true;
            this.noneBtn.Location = new System.Drawing.Point(37, 67);
            this.noneBtn.Name = "noneBtn";
            this.noneBtn.Size = new System.Drawing.Size(119, 36);
            this.noneBtn.TabIndex = 0;
            this.noneBtn.TabStop = true;
            this.noneBtn.Text = "None";
            this.noneBtn.UseVisualStyleBackColor = true;
            // 
            // mpmBtn
            // 
            this.mpmBtn.AutoSize = true;
            this.mpmBtn.Location = new System.Drawing.Point(37, 123);
            this.mpmBtn.Name = "mpmBtn";
            this.mpmBtn.Size = new System.Drawing.Size(345, 36);
            this.mpmBtn.TabIndex = 1;
            this.mpmBtn.Text = "Moon Panorama Maker";
            this.mpmBtn.UseVisualStyleBackColor = true;
            // 
            // SetupDialogForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(16F, 31F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1287, 865);
            this.Controls.Add(this.compatBox);
            this.Controls.Add(this.SiteLonglbl);
            this.Controls.Add(this.SiteLatlbl);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.SlewSetTimeTxt);
            this.Controls.Add(this.SiteAltTxt);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.comboBoxComPort);
            this.Controls.Add(this.chkTrace);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.picASCOM);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cmdCancel);
            this.Controls.Add(this.cmdOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SetupDialogForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "TTS160 Setup";
            this.Load += new System.EventHandler(this.SetupDialogForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.picASCOM)).EndInit();
            this.compatBox.ResumeLayout(false);
            this.compatBox.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cmdOK;
        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PictureBox picASCOM;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox chkTrace;
        private System.Windows.Forms.ComboBox comboBoxComPort;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox SiteAltTxt;
        private System.Windows.Forms.TextBox SlewSetTimeTxt;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label SiteLatlbl;
        private System.Windows.Forms.Label SiteLonglbl;
        private System.Windows.Forms.GroupBox compatBox;
        private System.Windows.Forms.RadioButton mpmBtn;
        private System.Windows.Forms.RadioButton noneBtn;
    }
}