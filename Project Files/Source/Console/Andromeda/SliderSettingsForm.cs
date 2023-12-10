//=================================================================
// SliderSettingsForm.cs
//=================================================================
// Thetis is a C# implementation of a Software Defined Radio.
// Copyright (C) 2019  Laurence Barker, G8NJJ
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
//
// The author can be reached by email at  
//
// laurence@nicklebyhouse.co.uk
//=================================================================

namespace Thetis
{
    using System;
    using System.Windows.Forms;
    using System.Timers;

    /// <summary>
    /// Analogue sliders view for collapsed display.
    /// </summary>
    public class SliderSettingsForm : Form
    {
        #region Variable Declaration

        private Console console;
        private TrackBarTS tbRX1AF;
        private GroupBoxTS grpRX1;
        private LabelTS lblPan;
        private TrackBarTS tbRX1Pan;
        private TrackBarTS tbRX1Sql;
        private TrackBarTS tbRX1RF;
        private GroupBoxTS grpRX2;
        private TrackBarTS tbRX2Pan;
        private TrackBarTS tbRX2Sql;
        private TrackBarTS tbRX2RF;
        private TrackBarTS tbRX2AF;
        private GroupBoxTS grpSubRX;
        private TrackBarTS tbSubRXPan;
        private TrackBarTS tbDrive;
        private TrackBarTS tbMasterAF;
        private TrackBarTS tbSubRXAF;
        private LabelTS lblDrive;
        private LabelTS lblMasterAF;
        public  CheckBoxTS chkRX1Sql;
        public  CheckBoxTS chkRX2Sql;
        private ButtonTS btnClose;
        private LabelTS labelTS1;
        private LabelTS labelTS5;
        private LabelTS labelTS4;
        private LabelTS labelTS11;
        private TrackBarTS tbRX2Atten;
        private CheckBoxTS chkRX2Mute;
        private LabelTS labelTS3;
        private LabelTS labelTS2;
        private LabelTS labelTS10;
        private TrackBarTS tbRX1Atten;
        private LabelTS labelTS7;
        private CheckBoxTS chkRX1Mute;
        private LabelTS labelTS8;
        private LabelTS labelTS9;
        private LabelTS labelTS6;
        public  CheckBoxTS chkSubRX;
        private LabelTS labelTS15;
        private LabelTS labelTS14;
        private CheckBoxTS chkRX2VAC;
        private TrackBarTS tbRX2VACTX;
        private TrackBarTS tbRX2VACRX;
        private CheckBoxTS chkRX1VAC;
        private LabelTS labelTS13;
        private TrackBarTS tbRX1VACTX;
        private LabelTS labelTS12;
        private TrackBarTS tbRX1VACRX;
        private TrackBarTS tbMicGain;
        private LabelTS labelTS16;
        private System.ComponentModel.IContainer components = null;
        private bool FormAutoShown;
        public LabelTS lblAFrx1;
        public LabelTS lblAFrx2;
        public LabelTS lblRX1rf;
        public LabelTS lblRX2rf;
        public CheckBoxTS chkAGCAut;
        public CheckBoxTS chkRX2AGCAut;
        public LabelTS lblRX1sql;
        public LabelTS lblRX2sql;
        public LabelTS lblRX1at;
        public LabelTS lblRX2at;
        public CheckBoxTS chkATT1;
        public CheckBoxTS chkATT2;
        public LabelTS lblRX1VACRX;
        public LabelTS lblRX1VACTX;
        public LabelTS lblRX2VACRX;
        public LabelTS lblRX2VACTX;
        public LabelTS lblMainPan;
        public LabelTS lblRX2Pan;
        public LabelTS lblSubPan;
        public LabelTS lblSubRXAF;
        public LabelTS lblaf;
        public LabelTS lblmic;
        public LabelTS lblDRV;
        System.Timers.Timer AutoHideTimer;                         // times auto hiding of form 

        #endregion

        #region Constructor and Destructor

        public SliderSettingsForm(Console c)
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();
            console = c;
            // create timer for autohide and attach callback
            AutoHideTimer = new System.Timers.Timer(); 
            AutoHideTimer.Elapsed += new ElapsedEventHandler(Callback);
            AutoHideTimer.Enabled = false;

            //wd5y
            lblAFrx1.Text = console.ptbRX0Gain.Value.ToString();
            lblAFrx2.Text = console.ptbRX2Gain.Value.ToString();
            lblRX1rf.Text = console.ptbRF.Value.ToString();
            lblRX2rf.Text = console.ptbRX2RF.Value.ToString();
            chkAGCAut.Checked = console.AutoAGCRX1;
            chkRX2AGCAut.Checked = console.AutoAGCRX2;
            lblRX1sql.Text = console.chkSquelch.Text;
            lblRX2sql.Text = console.chkRX2Squelch.Text;
            lblRX1at.Text = console.SetupForm.HermesAttenuatorData.ToString();
            lblRX2at.Text = console.SetupForm.HermesAttenuatorDataRX2.ToString();
            chkATT1.Checked = console.SetupForm.chkHermesStepAttenuator.Checked;
            chkATT2.Checked = console.SetupForm.chkRX2StepAtt.Checked;
            lblRX1VACRX.Text = console.SetupForm.VACRXGain.ToString();
            lblRX1VACTX.Text = console.SetupForm.VACTXGain.ToString();
            lblRX2VACRX.Text = console.SetupForm.VAC2RXGain.ToString();
            lblRX2VACTX.Text = console.SetupForm.VAC2TXGain.ToString();
            lblMainPan.Text = console.PanMainRX.ToString();
            lblRX2Pan.Text = console.RX2Pan.ToString();
            lblSubPan.Text = console.PanSubRX.ToString();
            lblSubRXAF.Text = console.ptbRX1Gain.Value.ToString();
            lblaf.Text = console.ptbAF.Value.ToString();
            lblmic.Text = console.lblMicVal.Text;
            lblDRV.Text = console.lblPWR.Text;
            chkSubRX.Checked = console.chkEnableMultiRX.Checked;
            //wd5y
        }

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

        #endregion

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SliderSettingsForm));
            this.labelTS16 = new System.Windows.Forms.LabelTS();
            this.tbMicGain = new System.Windows.Forms.TrackBarTS();
            this.btnClose = new System.Windows.Forms.ButtonTS();
            this.lblMasterAF = new System.Windows.Forms.LabelTS();
            this.lblDrive = new System.Windows.Forms.LabelTS();
            this.grpSubRX = new System.Windows.Forms.GroupBoxTS();
            this.lblSubRXAF = new System.Windows.Forms.LabelTS();
            this.lblSubPan = new System.Windows.Forms.LabelTS();
            this.chkSubRX = new System.Windows.Forms.CheckBoxTS();
            this.labelTS5 = new System.Windows.Forms.LabelTS();
            this.labelTS4 = new System.Windows.Forms.LabelTS();
            this.tbSubRXPan = new System.Windows.Forms.TrackBarTS();
            this.tbSubRXAF = new System.Windows.Forms.TrackBarTS();
            this.tbDrive = new System.Windows.Forms.TrackBarTS();
            this.grpRX2 = new System.Windows.Forms.GroupBoxTS();
            this.lblRX2Pan = new System.Windows.Forms.LabelTS();
            this.lblRX2VACTX = new System.Windows.Forms.LabelTS();
            this.lblRX2VACRX = new System.Windows.Forms.LabelTS();
            this.chkATT2 = new System.Windows.Forms.CheckBoxTS();
            this.lblRX2at = new System.Windows.Forms.LabelTS();
            this.lblRX2sql = new System.Windows.Forms.LabelTS();
            this.chkRX2AGCAut = new System.Windows.Forms.CheckBoxTS();
            this.lblRX2rf = new System.Windows.Forms.LabelTS();
            this.lblAFrx2 = new System.Windows.Forms.LabelTS();
            this.labelTS15 = new System.Windows.Forms.LabelTS();
            this.labelTS14 = new System.Windows.Forms.LabelTS();
            this.chkRX2VAC = new System.Windows.Forms.CheckBoxTS();
            this.tbRX2VACTX = new System.Windows.Forms.TrackBarTS();
            this.tbRX2VACRX = new System.Windows.Forms.TrackBarTS();
            this.lblPan = new System.Windows.Forms.LabelTS();
            this.labelTS11 = new System.Windows.Forms.LabelTS();
            this.tbRX2Atten = new System.Windows.Forms.TrackBarTS();
            this.chkRX2Mute = new System.Windows.Forms.CheckBoxTS();
            this.labelTS3 = new System.Windows.Forms.LabelTS();
            this.labelTS2 = new System.Windows.Forms.LabelTS();
            this.labelTS1 = new System.Windows.Forms.LabelTS();
            this.chkRX2Sql = new System.Windows.Forms.CheckBoxTS();
            this.tbRX2Pan = new System.Windows.Forms.TrackBarTS();
            this.tbRX2Sql = new System.Windows.Forms.TrackBarTS();
            this.tbRX2RF = new System.Windows.Forms.TrackBarTS();
            this.tbRX2AF = new System.Windows.Forms.TrackBarTS();
            this.tbMasterAF = new System.Windows.Forms.TrackBarTS();
            this.grpRX1 = new System.Windows.Forms.GroupBoxTS();
            this.lblMainPan = new System.Windows.Forms.LabelTS();
            this.lblRX1VACTX = new System.Windows.Forms.LabelTS();
            this.lblRX1VACRX = new System.Windows.Forms.LabelTS();
            this.chkATT1 = new System.Windows.Forms.CheckBoxTS();
            this.lblRX1at = new System.Windows.Forms.LabelTS();
            this.lblRX1sql = new System.Windows.Forms.LabelTS();
            this.chkAGCAut = new System.Windows.Forms.CheckBoxTS();
            this.lblRX1rf = new System.Windows.Forms.LabelTS();
            this.lblAFrx1 = new System.Windows.Forms.LabelTS();
            this.chkRX1VAC = new System.Windows.Forms.CheckBoxTS();
            this.labelTS13 = new System.Windows.Forms.LabelTS();
            this.tbRX1VACTX = new System.Windows.Forms.TrackBarTS();
            this.labelTS12 = new System.Windows.Forms.LabelTS();
            this.tbRX1VACRX = new System.Windows.Forms.TrackBarTS();
            this.labelTS10 = new System.Windows.Forms.LabelTS();
            this.tbRX1Atten = new System.Windows.Forms.TrackBarTS();
            this.labelTS7 = new System.Windows.Forms.LabelTS();
            this.chkRX1Mute = new System.Windows.Forms.CheckBoxTS();
            this.labelTS8 = new System.Windows.Forms.LabelTS();
            this.labelTS9 = new System.Windows.Forms.LabelTS();
            this.labelTS6 = new System.Windows.Forms.LabelTS();
            this.chkRX1Sql = new System.Windows.Forms.CheckBoxTS();
            this.tbRX1Pan = new System.Windows.Forms.TrackBarTS();
            this.tbRX1Sql = new System.Windows.Forms.TrackBarTS();
            this.tbRX1RF = new System.Windows.Forms.TrackBarTS();
            this.tbRX1AF = new System.Windows.Forms.TrackBarTS();
            this.lblaf = new System.Windows.Forms.LabelTS();
            this.lblmic = new System.Windows.Forms.LabelTS();
            this.lblDRV = new System.Windows.Forms.LabelTS();
            ((System.ComponentModel.ISupportInitialize)(this.tbMicGain)).BeginInit();
            this.grpSubRX.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbSubRXPan)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbSubRXAF)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbDrive)).BeginInit();
            this.grpRX2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2VACTX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2VACRX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2Atten)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2Pan)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2Sql)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2RF)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2AF)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbMasterAF)).BeginInit();
            this.grpRX1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1VACTX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1VACRX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1Atten)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1Pan)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1Sql)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1RF)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1AF)).BeginInit();
            this.SuspendLayout();
            // 
            // labelTS16
            // 
            this.labelTS16.AutoSize = true;
            this.labelTS16.ForeColor = System.Drawing.SystemColors.Control;
            this.labelTS16.Image = null;
            this.labelTS16.Location = new System.Drawing.Point(750, 109);
            this.labelTS16.Name = "labelTS16";
            this.labelTS16.Size = new System.Drawing.Size(49, 13);
            this.labelTS16.TabIndex = 13;
            this.labelTS16.Text = "Mic Gain";
            // 
            // tbMicGain
            // 
            this.tbMicGain.Location = new System.Drawing.Point(740, 77);
            this.tbMicGain.Maximum = 70;
            this.tbMicGain.Minimum = -96;
            this.tbMicGain.Name = "tbMicGain";
            this.tbMicGain.Size = new System.Drawing.Size(131, 45);
            this.tbMicGain.TabIndex = 12;
            this.tbMicGain.Scroll += new System.EventHandler(this.TbMicGain_Scroll);
            // 
            // btnClose
            // 
            this.btnClose.Image = null;
            this.btnClose.Location = new System.Drawing.Point(759, 211);
            this.btnClose.Name = "btnClose";
            this.btnClose.Selectable = true;
            this.btnClose.Size = new System.Drawing.Size(96, 40);
            this.btnClose.TabIndex = 11;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // lblMasterAF
            // 
            this.lblMasterAF.AutoSize = true;
            this.lblMasterAF.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.lblMasterAF.Image = null;
            this.lblMasterAF.Location = new System.Drawing.Point(750, 44);
            this.lblMasterAF.Name = "lblMasterAF";
            this.lblMasterAF.Size = new System.Drawing.Size(80, 13);
            this.lblMasterAF.TabIndex = 10;
            this.lblMasterAF.Text = "Master AF Gain";
            // 
            // lblDrive
            // 
            this.lblDrive.AutoSize = true;
            this.lblDrive.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.lblDrive.Image = null;
            this.lblDrive.Location = new System.Drawing.Point(750, 176);
            this.lblDrive.Name = "lblDrive";
            this.lblDrive.Size = new System.Drawing.Size(24, 13);
            this.lblDrive.TabIndex = 9;
            this.lblDrive.Text = "TX:";
            // 
            // grpSubRX
            // 
            this.grpSubRX.Controls.Add(this.lblSubRXAF);
            this.grpSubRX.Controls.Add(this.lblSubPan);
            this.grpSubRX.Controls.Add(this.chkSubRX);
            this.grpSubRX.Controls.Add(this.labelTS5);
            this.grpSubRX.Controls.Add(this.labelTS4);
            this.grpSubRX.Controls.Add(this.tbSubRXPan);
            this.grpSubRX.Controls.Add(this.tbSubRXAF);
            this.grpSubRX.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.grpSubRX.Location = new System.Drawing.Point(579, 9);
            this.grpSubRX.Name = "grpSubRX";
            this.grpSubRX.Size = new System.Drawing.Size(155, 303);
            this.grpSubRX.TabIndex = 8;
            this.grpSubRX.TabStop = false;
            this.grpSubRX.Text = "Sub RX";
            // 
            // lblSubRXAF
            // 
            this.lblSubRXAF.AutoSize = true;
            this.lblSubRXAF.Image = null;
            this.lblSubRXAF.Location = new System.Drawing.Point(61, 156);
            this.lblSubRXAF.Name = "lblSubRXAF";
            this.lblSubRXAF.Size = new System.Drawing.Size(20, 13);
            this.lblSubRXAF.TabIndex = 22;
            this.lblSubRXAF.Text = "AF";
            // 
            // lblSubPan
            // 
            this.lblSubPan.AutoSize = true;
            this.lblSubPan.Image = null;
            this.lblSubPan.Location = new System.Drawing.Point(94, 254);
            this.lblSubPan.Name = "lblSubPan";
            this.lblSubPan.Size = new System.Drawing.Size(29, 13);
            this.lblSubPan.TabIndex = 28;
            this.lblSubPan.Text = "PAN";
            // 
            // chkSubRX
            // 
            this.chkSubRX.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkSubRX.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.chkSubRX.Image = null;
            this.chkSubRX.Location = new System.Drawing.Point(23, 179);
            this.chkSubRX.Name = "chkSubRX";
            this.chkSubRX.Size = new System.Drawing.Size(110, 28);
            this.chkSubRX.TabIndex = 11;
            this.chkSubRX.Text = "Enable Sub RX";
            this.chkSubRX.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkSubRX.UseVisualStyleBackColor = true;
            this.chkSubRX.CheckedChanged += new System.EventHandler(this.chkSubRX_CheckedChanged);
            // 
            // labelTS5
            // 
            this.labelTS5.AutoSize = true;
            this.labelTS5.Image = null;
            this.labelTS5.Location = new System.Drawing.Point(40, 254);
            this.labelTS5.Name = "labelTS5";
            this.labelTS5.Size = new System.Drawing.Size(48, 13);
            this.labelTS5.TabIndex = 10;
            this.labelTS5.Text = "L/R Pan";
            // 
            // labelTS4
            // 
            this.labelTS4.AutoSize = true;
            this.labelTS4.Image = null;
            this.labelTS4.Location = new System.Drawing.Point(61, 16);
            this.labelTS4.Name = "labelTS4";
            this.labelTS4.Size = new System.Drawing.Size(20, 13);
            this.labelTS4.TabIndex = 10;
            this.labelTS4.Text = "AF";
            // 
            // tbSubRXPan
            // 
            this.tbSubRXPan.Location = new System.Drawing.Point(5, 213);
            this.tbSubRXPan.Maximum = 100;
            this.tbSubRXPan.Name = "tbSubRXPan";
            this.tbSubRXPan.Size = new System.Drawing.Size(144, 45);
            this.tbSubRXPan.TabIndex = 4;
            this.tbSubRXPan.Scroll += new System.EventHandler(this.tbSubRXPan_Scroll);
            // 
            // tbSubRXAF
            // 
            this.tbSubRXAF.Location = new System.Drawing.Point(55, 27);
            this.tbSubRXAF.Maximum = 100;
            this.tbSubRXAF.Name = "tbSubRXAF";
            this.tbSubRXAF.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbSubRXAF.Size = new System.Drawing.Size(45, 131);
            this.tbSubRXAF.TabIndex = 1;
            this.tbSubRXAF.Scroll += new System.EventHandler(this.tbSubRXAF_Scroll);
            // 
            // tbDrive
            // 
            this.tbDrive.Location = new System.Drawing.Point(740, 144);
            this.tbDrive.Maximum = 100;
            this.tbDrive.Name = "tbDrive";
            this.tbDrive.Size = new System.Drawing.Size(131, 45);
            this.tbDrive.TabIndex = 3;
            this.tbDrive.Scroll += new System.EventHandler(this.tbDrive_Scroll);
            // 
            // grpRX2
            // 
            this.grpRX2.Controls.Add(this.lblRX2Pan);
            this.grpRX2.Controls.Add(this.lblRX2VACTX);
            this.grpRX2.Controls.Add(this.lblRX2VACRX);
            this.grpRX2.Controls.Add(this.chkATT2);
            this.grpRX2.Controls.Add(this.lblRX2at);
            this.grpRX2.Controls.Add(this.lblRX2sql);
            this.grpRX2.Controls.Add(this.chkRX2AGCAut);
            this.grpRX2.Controls.Add(this.lblRX2rf);
            this.grpRX2.Controls.Add(this.lblAFrx2);
            this.grpRX2.Controls.Add(this.labelTS15);
            this.grpRX2.Controls.Add(this.labelTS14);
            this.grpRX2.Controls.Add(this.chkRX2VAC);
            this.grpRX2.Controls.Add(this.tbRX2VACTX);
            this.grpRX2.Controls.Add(this.tbRX2VACRX);
            this.grpRX2.Controls.Add(this.lblPan);
            this.grpRX2.Controls.Add(this.labelTS11);
            this.grpRX2.Controls.Add(this.tbRX2Atten);
            this.grpRX2.Controls.Add(this.chkRX2Mute);
            this.grpRX2.Controls.Add(this.labelTS3);
            this.grpRX2.Controls.Add(this.labelTS2);
            this.grpRX2.Controls.Add(this.labelTS1);
            this.grpRX2.Controls.Add(this.chkRX2Sql);
            this.grpRX2.Controls.Add(this.tbRX2Pan);
            this.grpRX2.Controls.Add(this.tbRX2Sql);
            this.grpRX2.Controls.Add(this.tbRX2RF);
            this.grpRX2.Controls.Add(this.tbRX2AF);
            this.grpRX2.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.grpRX2.Location = new System.Drawing.Point(295, 9);
            this.grpRX2.Name = "grpRX2";
            this.grpRX2.Size = new System.Drawing.Size(268, 303);
            this.grpRX2.TabIndex = 7;
            this.grpRX2.TabStop = false;
            this.grpRX2.Text = "RX2";
            // 
            // lblRX2Pan
            // 
            this.lblRX2Pan.AutoSize = true;
            this.lblRX2Pan.Image = null;
            this.lblRX2Pan.Location = new System.Drawing.Point(154, 280);
            this.lblRX2Pan.Name = "lblRX2Pan";
            this.lblRX2Pan.Size = new System.Drawing.Size(29, 13);
            this.lblRX2Pan.TabIndex = 26;
            this.lblRX2Pan.Text = "PAN";
            // 
            // lblRX2VACTX
            // 
            this.lblRX2VACTX.AutoSize = true;
            this.lblRX2VACTX.Image = null;
            this.lblRX2VACTX.Location = new System.Drawing.Point(239, 156);
            this.lblRX2VACTX.Name = "lblRX2VACTX";
            this.lblRX2VACTX.Size = new System.Drawing.Size(21, 13);
            this.lblRX2VACTX.TabIndex = 25;
            this.lblRX2VACTX.Text = "TX";
            // 
            // lblRX2VACRX
            // 
            this.lblRX2VACRX.AutoSize = true;
            this.lblRX2VACRX.Image = null;
            this.lblRX2VACRX.Location = new System.Drawing.Point(193, 156);
            this.lblRX2VACRX.Name = "lblRX2VACRX";
            this.lblRX2VACRX.Size = new System.Drawing.Size(22, 13);
            this.lblRX2VACRX.TabIndex = 24;
            this.lblRX2VACRX.Text = "RX";
            // 
            // chkATT2
            // 
            this.chkATT2.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkATT2.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.chkATT2.Image = null;
            this.chkATT2.Location = new System.Drawing.Point(140, 179);
            this.chkATT2.Name = "chkATT2";
            this.chkATT2.Size = new System.Drawing.Size(42, 28);
            this.chkATT2.TabIndex = 28;
            this.chkATT2.Text = "ATT2";
            this.chkATT2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkATT2.UseVisualStyleBackColor = true;
            this.chkATT2.CheckedChanged += new System.EventHandler(this.chkATT2_CheckedChanged);
            // 
            // lblRX2at
            // 
            this.lblRX2at.AutoSize = true;
            this.lblRX2at.Image = null;
            this.lblRX2at.Location = new System.Drawing.Point(148, 156);
            this.lblRX2at.Name = "lblRX2at";
            this.lblRX2at.Size = new System.Drawing.Size(28, 13);
            this.lblRX2at.TabIndex = 23;
            this.lblRX2at.Text = "ATT";
            // 
            // lblRX2sql
            // 
            this.lblRX2sql.AutoSize = true;
            this.lblRX2sql.Image = null;
            this.lblRX2sql.Location = new System.Drawing.Point(89, 156);
            this.lblRX2sql.Name = "lblRX2sql";
            this.lblRX2sql.Size = new System.Drawing.Size(52, 13);
            this.lblRX2sql.TabIndex = 22;
            this.lblRX2sql.Text = "SQL:-100";
            // 
            // chkRX2AGCAut
            // 
            this.chkRX2AGCAut.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkRX2AGCAut.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.chkRX2AGCAut.Image = null;
            this.chkRX2AGCAut.Location = new System.Drawing.Point(48, 179);
            this.chkRX2AGCAut.Name = "chkRX2AGCAut";
            this.chkRX2AGCAut.Size = new System.Drawing.Size(40, 37);
            this.chkRX2AGCAut.TabIndex = 27;
            this.chkRX2AGCAut.Text = "AGC\r\nAUT\r\n";
            this.chkRX2AGCAut.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkRX2AGCAut.UseVisualStyleBackColor = true;
            this.chkRX2AGCAut.CheckedChanged += new System.EventHandler(this.chkRX2AGCAut_CheckedChanged);
            // 
            // lblRX2rf
            // 
            this.lblRX2rf.AutoSize = true;
            this.lblRX2rf.Image = null;
            this.lblRX2rf.Location = new System.Drawing.Point(58, 156);
            this.lblRX2rf.Name = "lblRX2rf";
            this.lblRX2rf.Size = new System.Drawing.Size(21, 13);
            this.lblRX2rf.TabIndex = 21;
            this.lblRX2rf.Text = "RF";
            // 
            // lblAFrx2
            // 
            this.lblAFrx2.AutoSize = true;
            this.lblAFrx2.Image = null;
            this.lblAFrx2.Location = new System.Drawing.Point(11, 156);
            this.lblAFrx2.Name = "lblAFrx2";
            this.lblAFrx2.Size = new System.Drawing.Size(20, 13);
            this.lblAFrx2.TabIndex = 20;
            this.lblAFrx2.Text = "AF";
            // 
            // labelTS15
            // 
            this.labelTS15.AutoSize = true;
            this.labelTS15.Image = null;
            this.labelTS15.Location = new System.Drawing.Point(238, 16);
            this.labelTS15.Name = "labelTS15";
            this.labelTS15.Size = new System.Drawing.Size(21, 13);
            this.labelTS15.TabIndex = 20;
            this.labelTS15.Text = "TX";
            // 
            // labelTS14
            // 
            this.labelTS14.AutoSize = true;
            this.labelTS14.Image = null;
            this.labelTS14.Location = new System.Drawing.Point(193, 16);
            this.labelTS14.Name = "labelTS14";
            this.labelTS14.Size = new System.Drawing.Size(22, 13);
            this.labelTS14.TabIndex = 19;
            this.labelTS14.Text = "RX";
            // 
            // chkRX2VAC
            // 
            this.chkRX2VAC.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkRX2VAC.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.chkRX2VAC.Image = null;
            this.chkRX2VAC.Location = new System.Drawing.Point(197, 179);
            this.chkRX2VAC.Name = "chkRX2VAC";
            this.chkRX2VAC.Size = new System.Drawing.Size(55, 28);
            this.chkRX2VAC.TabIndex = 19;
            this.chkRX2VAC.Text = "VAC2";
            this.chkRX2VAC.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkRX2VAC.UseVisualStyleBackColor = true;
            this.chkRX2VAC.CheckedChanged += new System.EventHandler(this.ChkRX2VAC_CheckedChanged);
            // 
            // tbRX2VACTX
            // 
            this.tbRX2VACTX.AutoSize = false;
            this.tbRX2VACTX.Location = new System.Drawing.Point(231, 27);
            this.tbRX2VACTX.Maximum = 40;
            this.tbRX2VACTX.Minimum = -40;
            this.tbRX2VACTX.Name = "tbRX2VACTX";
            this.tbRX2VACTX.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbRX2VACTX.Size = new System.Drawing.Size(30, 131);
            this.tbRX2VACTX.TabIndex = 17;
            this.tbRX2VACTX.Scroll += new System.EventHandler(this.TbRX2VACTX_Scroll);
            // 
            // tbRX2VACRX
            // 
            this.tbRX2VACRX.AutoSize = false;
            this.tbRX2VACRX.Location = new System.Drawing.Point(186, 27);
            this.tbRX2VACRX.Maximum = 40;
            this.tbRX2VACRX.Minimum = -40;
            this.tbRX2VACRX.Name = "tbRX2VACRX";
            this.tbRX2VACRX.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbRX2VACRX.Size = new System.Drawing.Size(30, 131);
            this.tbRX2VACRX.TabIndex = 16;
            this.tbRX2VACRX.Scroll += new System.EventHandler(this.TbRX2VACRX_Scroll);
            // 
            // lblPan
            // 
            this.lblPan.AutoSize = true;
            this.lblPan.Image = null;
            this.lblPan.Location = new System.Drawing.Point(100, 280);
            this.lblPan.Name = "lblPan";
            this.lblPan.Size = new System.Drawing.Size(48, 13);
            this.lblPan.TabIndex = 6;
            this.lblPan.Text = "L/R Pan";
            // 
            // labelTS11
            // 
            this.labelTS11.AutoSize = true;
            this.labelTS11.Image = null;
            this.labelTS11.Location = new System.Drawing.Point(144, 16);
            this.labelTS11.Name = "labelTS11";
            this.labelTS11.Size = new System.Drawing.Size(32, 13);
            this.labelTS11.TabIndex = 15;
            this.labelTS11.Text = "Atten";
            // 
            // tbRX2Atten
            // 
            this.tbRX2Atten.AutoSize = false;
            this.tbRX2Atten.Location = new System.Drawing.Point(141, 27);
            this.tbRX2Atten.Maximum = 31;
            this.tbRX2Atten.Name = "tbRX2Atten";
            this.tbRX2Atten.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbRX2Atten.Size = new System.Drawing.Size(30, 131);
            this.tbRX2Atten.TabIndex = 15;
            this.tbRX2Atten.Scroll += new System.EventHandler(this.tbRX2Atten_Scroll);
            // 
            // chkRX2Mute
            // 
            this.chkRX2Mute.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkRX2Mute.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.chkRX2Mute.Image = null;
            this.chkRX2Mute.Location = new System.Drawing.Point(2, 179);
            this.chkRX2Mute.Name = "chkRX2Mute";
            this.chkRX2Mute.Size = new System.Drawing.Size(40, 28);
            this.chkRX2Mute.TabIndex = 10;
            this.chkRX2Mute.Text = "MUT";
            this.chkRX2Mute.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkRX2Mute.UseVisualStyleBackColor = true;
            this.chkRX2Mute.CheckedChanged += new System.EventHandler(this.chkRX2Mute_CheckedChanged);
            // 
            // labelTS3
            // 
            this.labelTS3.AutoSize = true;
            this.labelTS3.Image = null;
            this.labelTS3.Location = new System.Drawing.Point(100, 16);
            this.labelTS3.Name = "labelTS3";
            this.labelTS3.Size = new System.Drawing.Size(28, 13);
            this.labelTS3.TabIndex = 8;
            this.labelTS3.Text = "SQL";
            // 
            // labelTS2
            // 
            this.labelTS2.AutoSize = true;
            this.labelTS2.Image = null;
            this.labelTS2.Location = new System.Drawing.Point(54, 16);
            this.labelTS2.Name = "labelTS2";
            this.labelTS2.Size = new System.Drawing.Size(29, 13);
            this.labelTS2.TabIndex = 7;
            this.labelTS2.Text = "AGC";
            // 
            // labelTS1
            // 
            this.labelTS1.AutoSize = true;
            this.labelTS1.Image = null;
            this.labelTS1.Location = new System.Drawing.Point(14, 16);
            this.labelTS1.Name = "labelTS1";
            this.labelTS1.Size = new System.Drawing.Size(20, 13);
            this.labelTS1.TabIndex = 5;
            this.labelTS1.Text = "AF";
            // 
            // chkRX2Sql
            // 
            this.chkRX2Sql.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkRX2Sql.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.chkRX2Sql.Image = null;
            this.chkRX2Sql.Location = new System.Drawing.Point(93, 179);
            this.chkRX2Sql.Name = "chkRX2Sql";
            this.chkRX2Sql.Size = new System.Drawing.Size(42, 37);
            this.chkRX2Sql.TabIndex = 5;
            this.chkRX2Sql.Text = "SQL";
            this.chkRX2Sql.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkRX2Sql.ThreeState = true;
            this.chkRX2Sql.UseVisualStyleBackColor = true;
            this.chkRX2Sql.CheckStateChanged += new System.EventHandler(this.chkRX2Sql_CheckStateChanged);
            // 
            // tbRX2Pan
            // 
            this.tbRX2Pan.Location = new System.Drawing.Point(57, 240);
            this.tbRX2Pan.Maximum = 100;
            this.tbRX2Pan.Name = "tbRX2Pan";
            this.tbRX2Pan.Size = new System.Drawing.Size(150, 45);
            this.tbRX2Pan.TabIndex = 4;
            this.tbRX2Pan.Scroll += new System.EventHandler(this.tbRX2Pan_Scroll);
            // 
            // tbRX2Sql
            // 
            this.tbRX2Sql.AutoSize = false;
            this.tbRX2Sql.Location = new System.Drawing.Point(96, 27);
            this.tbRX2Sql.Maximum = 0;
            this.tbRX2Sql.Minimum = -160;
            this.tbRX2Sql.Name = "tbRX2Sql";
            this.tbRX2Sql.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbRX2Sql.Size = new System.Drawing.Size(30, 131);
            this.tbRX2Sql.TabIndex = 3;
            this.tbRX2Sql.Scroll += new System.EventHandler(this.tbRX2Sql_Scroll);
            // 
            // tbRX2RF
            // 
            this.tbRX2RF.AutoSize = false;
            this.tbRX2RF.Location = new System.Drawing.Point(51, 27);
            this.tbRX2RF.Maximum = 120;
            this.tbRX2RF.Minimum = -20;
            this.tbRX2RF.Name = "tbRX2RF";
            this.tbRX2RF.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbRX2RF.Size = new System.Drawing.Size(30, 131);
            this.tbRX2RF.TabIndex = 2;
            this.tbRX2RF.Scroll += new System.EventHandler(this.tbRX2RF_Scroll);
            // 
            // tbRX2AF
            // 
            this.tbRX2AF.AutoSize = false;
            this.tbRX2AF.Location = new System.Drawing.Point(6, 27);
            this.tbRX2AF.Maximum = 100;
            this.tbRX2AF.Name = "tbRX2AF";
            this.tbRX2AF.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbRX2AF.Size = new System.Drawing.Size(30, 131);
            this.tbRX2AF.TabIndex = 1;
            this.tbRX2AF.Scroll += new System.EventHandler(this.tbRX2AF_Scroll);
            // 
            // tbMasterAF
            // 
            this.tbMasterAF.Location = new System.Drawing.Point(740, 12);
            this.tbMasterAF.Maximum = 100;
            this.tbMasterAF.Name = "tbMasterAF";
            this.tbMasterAF.Size = new System.Drawing.Size(131, 45);
            this.tbMasterAF.TabIndex = 2;
            this.tbMasterAF.Scroll += new System.EventHandler(this.tbMasterAF_Scroll);
            // 
            // grpRX1
            // 
            this.grpRX1.Controls.Add(this.lblMainPan);
            this.grpRX1.Controls.Add(this.lblRX1VACTX);
            this.grpRX1.Controls.Add(this.lblRX1VACRX);
            this.grpRX1.Controls.Add(this.chkATT1);
            this.grpRX1.Controls.Add(this.lblRX1at);
            this.grpRX1.Controls.Add(this.lblRX1sql);
            this.grpRX1.Controls.Add(this.chkAGCAut);
            this.grpRX1.Controls.Add(this.lblRX1rf);
            this.grpRX1.Controls.Add(this.lblAFrx1);
            this.grpRX1.Controls.Add(this.chkRX1VAC);
            this.grpRX1.Controls.Add(this.labelTS13);
            this.grpRX1.Controls.Add(this.tbRX1VACTX);
            this.grpRX1.Controls.Add(this.labelTS12);
            this.grpRX1.Controls.Add(this.tbRX1VACRX);
            this.grpRX1.Controls.Add(this.labelTS10);
            this.grpRX1.Controls.Add(this.tbRX1Atten);
            this.grpRX1.Controls.Add(this.labelTS7);
            this.grpRX1.Controls.Add(this.chkRX1Mute);
            this.grpRX1.Controls.Add(this.labelTS8);
            this.grpRX1.Controls.Add(this.labelTS9);
            this.grpRX1.Controls.Add(this.labelTS6);
            this.grpRX1.Controls.Add(this.chkRX1Sql);
            this.grpRX1.Controls.Add(this.tbRX1Pan);
            this.grpRX1.Controls.Add(this.tbRX1Sql);
            this.grpRX1.Controls.Add(this.tbRX1RF);
            this.grpRX1.Controls.Add(this.tbRX1AF);
            this.grpRX1.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.grpRX1.Location = new System.Drawing.Point(9, 9);
            this.grpRX1.Name = "grpRX1";
            this.grpRX1.Size = new System.Drawing.Size(268, 303);
            this.grpRX1.TabIndex = 3;
            this.grpRX1.TabStop = false;
            this.grpRX1.Text = "RX1";
            // 
            // lblMainPan
            // 
            this.lblMainPan.AutoSize = true;
            this.lblMainPan.Image = null;
            this.lblMainPan.Location = new System.Drawing.Point(154, 280);
            this.lblMainPan.Name = "lblMainPan";
            this.lblMainPan.Size = new System.Drawing.Size(29, 13);
            this.lblMainPan.TabIndex = 26;
            this.lblMainPan.Text = "PAN";
            // 
            // lblRX1VACTX
            // 
            this.lblRX1VACTX.AutoSize = true;
            this.lblRX1VACTX.Image = null;
            this.lblRX1VACTX.Location = new System.Drawing.Point(239, 156);
            this.lblRX1VACTX.Name = "lblRX1VACTX";
            this.lblRX1VACTX.Size = new System.Drawing.Size(21, 13);
            this.lblRX1VACTX.TabIndex = 25;
            this.lblRX1VACTX.Text = "TX";
            // 
            // lblRX1VACRX
            // 
            this.lblRX1VACRX.AutoSize = true;
            this.lblRX1VACRX.Image = null;
            this.lblRX1VACRX.Location = new System.Drawing.Point(193, 156);
            this.lblRX1VACRX.Name = "lblRX1VACRX";
            this.lblRX1VACRX.Size = new System.Drawing.Size(22, 13);
            this.lblRX1VACRX.TabIndex = 24;
            this.lblRX1VACRX.Text = "RX";
            // 
            // chkATT1
            // 
            this.chkATT1.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkATT1.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.chkATT1.Image = null;
            this.chkATT1.Location = new System.Drawing.Point(140, 179);
            this.chkATT1.Name = "chkATT1";
            this.chkATT1.Size = new System.Drawing.Size(42, 28);
            this.chkATT1.TabIndex = 28;
            this.chkATT1.Text = "ATT1";
            this.chkATT1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkATT1.UseVisualStyleBackColor = true;
            this.chkATT1.CheckedChanged += new System.EventHandler(this.chkATT1_CheckedChanged);
            // 
            // lblRX1at
            // 
            this.lblRX1at.AutoSize = true;
            this.lblRX1at.Image = null;
            this.lblRX1at.Location = new System.Drawing.Point(148, 156);
            this.lblRX1at.Name = "lblRX1at";
            this.lblRX1at.Size = new System.Drawing.Size(28, 13);
            this.lblRX1at.TabIndex = 23;
            this.lblRX1at.Text = "ATT";
            // 
            // lblRX1sql
            // 
            this.lblRX1sql.AutoSize = true;
            this.lblRX1sql.Image = null;
            this.lblRX1sql.Location = new System.Drawing.Point(89, 156);
            this.lblRX1sql.Name = "lblRX1sql";
            this.lblRX1sql.Size = new System.Drawing.Size(52, 13);
            this.lblRX1sql.TabIndex = 22;
            this.lblRX1sql.Text = "SQL:-100";
            // 
            // chkAGCAut
            // 
            this.chkAGCAut.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkAGCAut.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.chkAGCAut.Image = null;
            this.chkAGCAut.Location = new System.Drawing.Point(48, 179);
            this.chkAGCAut.Name = "chkAGCAut";
            this.chkAGCAut.Size = new System.Drawing.Size(40, 37);
            this.chkAGCAut.TabIndex = 27;
            this.chkAGCAut.Text = "AGC\r\nAUT";
            this.chkAGCAut.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkAGCAut.UseVisualStyleBackColor = true;
            this.chkAGCAut.CheckedChanged += new System.EventHandler(this.chkAGCAut_CheckedChanged);
            // 
            // lblRX1rf
            // 
            this.lblRX1rf.AutoSize = true;
            this.lblRX1rf.Image = null;
            this.lblRX1rf.Location = new System.Drawing.Point(58, 156);
            this.lblRX1rf.Name = "lblRX1rf";
            this.lblRX1rf.Size = new System.Drawing.Size(21, 13);
            this.lblRX1rf.TabIndex = 21;
            this.lblRX1rf.Text = "RF";
            // 
            // lblAFrx1
            // 
            this.lblAFrx1.AutoSize = true;
            this.lblAFrx1.Image = null;
            this.lblAFrx1.Location = new System.Drawing.Point(12, 156);
            this.lblAFrx1.Name = "lblAFrx1";
            this.lblAFrx1.Size = new System.Drawing.Size(20, 13);
            this.lblAFrx1.TabIndex = 20;
            this.lblAFrx1.Text = "AF";
            // 
            // chkRX1VAC
            // 
            this.chkRX1VAC.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkRX1VAC.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.chkRX1VAC.Image = null;
            this.chkRX1VAC.Location = new System.Drawing.Point(197, 179);
            this.chkRX1VAC.Name = "chkRX1VAC";
            this.chkRX1VAC.Size = new System.Drawing.Size(55, 28);
            this.chkRX1VAC.TabIndex = 19;
            this.chkRX1VAC.Text = "VAC1";
            this.chkRX1VAC.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkRX1VAC.UseVisualStyleBackColor = true;
            this.chkRX1VAC.CheckedChanged += new System.EventHandler(this.ChkRX1VAC_CheckedChanged);
            // 
            // labelTS13
            // 
            this.labelTS13.AutoSize = true;
            this.labelTS13.Image = null;
            this.labelTS13.Location = new System.Drawing.Point(236, 16);
            this.labelTS13.Name = "labelTS13";
            this.labelTS13.Size = new System.Drawing.Size(21, 13);
            this.labelTS13.TabIndex = 18;
            this.labelTS13.Text = "TX";
            // 
            // tbRX1VACTX
            // 
            this.tbRX1VACTX.AutoSize = false;
            this.tbRX1VACTX.Location = new System.Drawing.Point(231, 27);
            this.tbRX1VACTX.Maximum = 40;
            this.tbRX1VACTX.Minimum = -40;
            this.tbRX1VACTX.Name = "tbRX1VACTX";
            this.tbRX1VACTX.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbRX1VACTX.Size = new System.Drawing.Size(30, 131);
            this.tbRX1VACTX.TabIndex = 17;
            this.tbRX1VACTX.Scroll += new System.EventHandler(this.TbRX1VACTX_Scroll);
            // 
            // labelTS12
            // 
            this.labelTS12.AutoSize = true;
            this.labelTS12.Image = null;
            this.labelTS12.Location = new System.Drawing.Point(191, 16);
            this.labelTS12.Name = "labelTS12";
            this.labelTS12.Size = new System.Drawing.Size(22, 13);
            this.labelTS12.TabIndex = 16;
            this.labelTS12.Text = "RX";
            // 
            // tbRX1VACRX
            // 
            this.tbRX1VACRX.AutoSize = false;
            this.tbRX1VACRX.Location = new System.Drawing.Point(185, 27);
            this.tbRX1VACRX.Maximum = 40;
            this.tbRX1VACRX.Minimum = -40;
            this.tbRX1VACRX.Name = "tbRX1VACRX";
            this.tbRX1VACRX.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbRX1VACRX.Size = new System.Drawing.Size(30, 131);
            this.tbRX1VACRX.TabIndex = 15;
            this.tbRX1VACRX.Scroll += new System.EventHandler(this.TbRX1VACRX_Scroll);
            // 
            // labelTS10
            // 
            this.labelTS10.Image = null;
            this.labelTS10.Location = new System.Drawing.Point(142, 16);
            this.labelTS10.Name = "labelTS10";
            this.labelTS10.Size = new System.Drawing.Size(32, 13);
            this.labelTS10.TabIndex = 14;
            this.labelTS10.Text = "Atten";
            // 
            // tbRX1Atten
            // 
            this.tbRX1Atten.AutoSize = false;
            this.tbRX1Atten.Location = new System.Drawing.Point(141, 27);
            this.tbRX1Atten.Maximum = 31;
            this.tbRX1Atten.Name = "tbRX1Atten";
            this.tbRX1Atten.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbRX1Atten.Size = new System.Drawing.Size(30, 131);
            this.tbRX1Atten.TabIndex = 13;
            this.tbRX1Atten.Scroll += new System.EventHandler(this.tbRX1Atten_Scroll);
            // 
            // labelTS7
            // 
            this.labelTS7.AutoSize = true;
            this.labelTS7.Image = null;
            this.labelTS7.Location = new System.Drawing.Point(100, 16);
            this.labelTS7.Name = "labelTS7";
            this.labelTS7.Size = new System.Drawing.Size(28, 13);
            this.labelTS7.TabIndex = 12;
            this.labelTS7.Text = "SQL";
            // 
            // chkRX1Mute
            // 
            this.chkRX1Mute.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkRX1Mute.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.chkRX1Mute.Image = null;
            this.chkRX1Mute.Location = new System.Drawing.Point(2, 179);
            this.chkRX1Mute.Name = "chkRX1Mute";
            this.chkRX1Mute.Size = new System.Drawing.Size(40, 28);
            this.chkRX1Mute.TabIndex = 10;
            this.chkRX1Mute.Text = "MUT";
            this.chkRX1Mute.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkRX1Mute.UseVisualStyleBackColor = true;
            this.chkRX1Mute.CheckedChanged += new System.EventHandler(this.chkRX1Mute_CheckedChanged);
            // 
            // labelTS8
            // 
            this.labelTS8.AutoSize = true;
            this.labelTS8.Image = null;
            this.labelTS8.Location = new System.Drawing.Point(55, 16);
            this.labelTS8.Name = "labelTS8";
            this.labelTS8.Size = new System.Drawing.Size(29, 13);
            this.labelTS8.TabIndex = 11;
            this.labelTS8.Text = "AGC";
            // 
            // labelTS9
            // 
            this.labelTS9.AutoSize = true;
            this.labelTS9.Image = null;
            this.labelTS9.Location = new System.Drawing.Point(12, 16);
            this.labelTS9.Name = "labelTS9";
            this.labelTS9.Size = new System.Drawing.Size(20, 13);
            this.labelTS9.TabIndex = 10;
            this.labelTS9.Text = "AF";
            // 
            // labelTS6
            // 
            this.labelTS6.AutoSize = true;
            this.labelTS6.Image = null;
            this.labelTS6.Location = new System.Drawing.Point(100, 280);
            this.labelTS6.Name = "labelTS6";
            this.labelTS6.Size = new System.Drawing.Size(48, 13);
            this.labelTS6.TabIndex = 10;
            this.labelTS6.Text = "L/R Pan";
            // 
            // chkRX1Sql
            // 
            this.chkRX1Sql.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkRX1Sql.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.chkRX1Sql.Image = null;
            this.chkRX1Sql.Location = new System.Drawing.Point(93, 179);
            this.chkRX1Sql.Name = "chkRX1Sql";
            this.chkRX1Sql.Size = new System.Drawing.Size(42, 37);
            this.chkRX1Sql.TabIndex = 5;
            this.chkRX1Sql.Text = "SQL";
            this.chkRX1Sql.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkRX1Sql.ThreeState = true;
            this.chkRX1Sql.UseVisualStyleBackColor = true;
            this.chkRX1Sql.CheckStateChanged += new System.EventHandler(this.chkRX1Sql_CheckStateChanged);
            // 
            // tbRX1Pan
            // 
            this.tbRX1Pan.Location = new System.Drawing.Point(57, 240);
            this.tbRX1Pan.Maximum = 100;
            this.tbRX1Pan.Name = "tbRX1Pan";
            this.tbRX1Pan.Size = new System.Drawing.Size(150, 45);
            this.tbRX1Pan.TabIndex = 4;
            this.tbRX1Pan.Scroll += new System.EventHandler(this.tbRX1Pan_Scroll);
            // 
            // tbRX1Sql
            // 
            this.tbRX1Sql.AutoSize = false;
            this.tbRX1Sql.Location = new System.Drawing.Point(96, 27);
            this.tbRX1Sql.Maximum = 0;
            this.tbRX1Sql.Minimum = -160;
            this.tbRX1Sql.Name = "tbRX1Sql";
            this.tbRX1Sql.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbRX1Sql.Size = new System.Drawing.Size(30, 131);
            this.tbRX1Sql.TabIndex = 3;
            this.tbRX1Sql.Scroll += new System.EventHandler(this.tbRX1Sql_Scroll);
            // 
            // tbRX1RF
            // 
            this.tbRX1RF.AutoSize = false;
            this.tbRX1RF.Location = new System.Drawing.Point(51, 27);
            this.tbRX1RF.Maximum = 120;
            this.tbRX1RF.Minimum = -20;
            this.tbRX1RF.Name = "tbRX1RF";
            this.tbRX1RF.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbRX1RF.Size = new System.Drawing.Size(30, 131);
            this.tbRX1RF.TabIndex = 2;
            this.tbRX1RF.Scroll += new System.EventHandler(this.tbRX1RF_Scroll);
            // 
            // tbRX1AF
            // 
            this.tbRX1AF.AutoSize = false;
            this.tbRX1AF.Location = new System.Drawing.Point(6, 27);
            this.tbRX1AF.Maximum = 100;
            this.tbRX1AF.Name = "tbRX1AF";
            this.tbRX1AF.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.tbRX1AF.Size = new System.Drawing.Size(30, 131);
            this.tbRX1AF.TabIndex = 1;
            this.tbRX1AF.Scroll += new System.EventHandler(this.tbRX1AF_Scroll);
            // 
            // lblaf
            // 
            this.lblaf.AutoSize = true;
            this.lblaf.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.lblaf.Image = null;
            this.lblaf.Location = new System.Drawing.Point(839, 44);
            this.lblaf.Name = "lblaf";
            this.lblaf.Size = new System.Drawing.Size(20, 13);
            this.lblaf.TabIndex = 22;
            this.lblaf.Text = "AF";
            // 
            // lblmic
            // 
            this.lblmic.AutoSize = true;
            this.lblmic.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.lblmic.Image = null;
            this.lblmic.Location = new System.Drawing.Point(808, 109);
            this.lblmic.Name = "lblmic";
            this.lblmic.Size = new System.Drawing.Size(20, 13);
            this.lblmic.TabIndex = 23;
            this.lblmic.Text = "AF";
            // 
            // lblDRV
            // 
            this.lblDRV.AutoSize = true;
            this.lblDRV.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.lblDRV.Image = null;
            this.lblDRV.Location = new System.Drawing.Point(781, 176);
            this.lblDRV.Name = "lblDRV";
            this.lblDRV.Size = new System.Drawing.Size(21, 13);
            this.lblDRV.TabIndex = 24;
            this.lblDRV.Text = "TX";
            // 
            // SliderSettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
            this.ClientSize = new System.Drawing.Size(883, 323);
            this.Controls.Add(this.lblDRV);
            this.Controls.Add(this.lblmic);
            this.Controls.Add(this.lblaf);
            this.Controls.Add(this.labelTS16);
            this.Controls.Add(this.tbMicGain);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.lblMasterAF);
            this.Controls.Add(this.lblDrive);
            this.Controls.Add(this.grpSubRX);
            this.Controls.Add(this.tbDrive);
            this.Controls.Add(this.grpRX2);
            this.Controls.Add(this.tbMasterAF);
            this.Controls.Add(this.grpRX1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SliderSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Analogue Gain Control Settings";
            this.TopMost = true;
            this.Activated += new System.EventHandler(this.SliderSettingsForm_Activated);
            this.Closing += new System.ComponentModel.CancelEventHandler(this.SliderSettingsForm_Closing);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.SliderSettingsForm_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.tbMicGain)).EndInit();
            this.grpSubRX.ResumeLayout(false);
            this.grpSubRX.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbSubRXPan)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbSubRXAF)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbDrive)).EndInit();
            this.grpRX2.ResumeLayout(false);
            this.grpRX2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2VACTX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2VACRX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2Atten)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2Pan)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2Sql)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2RF)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX2AF)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbMasterAF)).EndInit();
            this.grpRX1.ResumeLayout(false);
            this.grpRX1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1VACTX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1VACRX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1Atten)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1Pan)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1Sql)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1RF)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbRX1AF)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        #region Properties
        public int RX1Gain
        {
            get
            {
                if (tbRX1AF != null) return tbRX1AF.Value;
                else return -1;
            }
            set
            {
                if (tbRX1AF != null) tbRX1AF.Value = value;

            }
        }

        public int RX2Gain
        {
            get
            {
                if (tbRX2AF != null) return tbRX2AF.Value;
                else return -1;
            }
            set
            {
                if (tbRX2AF != null) tbRX2AF.Value = value;

            }
        }

        public int SubRXGain
        {
            get
            {
                if (tbSubRXAF != null) return tbSubRXAF.Value;
                else return -1;
            }
            set
            {
                if (tbSubRXAF != null) tbSubRXAF.Value = value;
                //                ptbRX0Gain_Scroll(this, EventArgs.Empty);

            }
        }

        public int RX1RFGainAGC
        {
            get
            {
                if (tbRX1RF != null) return tbRX1RF.Value;
                else return -1;
            }
            set
            {
                if (tbRX1RF != null) tbRX1RF.Value = value;

            }
        }

        public int RX2RFGainAGC
        {
            get
            {
                if (tbRX2RF != null) return tbRX2RF.Value;
                else return -1;
            }
            set
            {
                if (tbRX2RF != null) tbRX2RF.Value = value;

            }
        }

        public int RX1LRPan
        {
            get
            {
                if (tbRX1Pan != null) return tbRX1Pan.Value;
                else return -1;
            }
            set
            {
                if (tbRX1Pan != null) tbRX1Pan.Value = value;

            }
        }

        public int RX2LRPan
        {
            get
            {
                if (tbRX2Pan != null) return tbRX2Pan.Value;
                else return -1;
            }
            set
            {
                if (tbRX2Pan != null) tbRX2Pan.Value = value;

            }
        }

        public int SubRXLRPan
        {
            get
            {
                if (tbSubRXPan != null) return tbSubRXPan.Value;
                else return -1;
            }
            set
            {
                if (tbSubRXPan != null) tbSubRXPan.Value = value;
                //                ptbRX0Gain_Scroll(this, EventArgs.Empty);

            }
        }

        public int RX1Squelch
        {
            get
            {
                if (tbRX1Sql != null) return tbRX1Sql.Value;
                else return -1;
            }
            set
            {
                if (tbRX1Sql != null) tbRX1Sql.Value = value;

            }
        }

        public int RX2Squelch
        {
            get
            {
                if (tbRX2Sql != null) return tbRX2Sql.Value;
                else return -1;
            }
            set
            {
                if (tbRX2Sql != null) tbRX2Sql.Value = value;

            }
        }

        public int RX1Atten
        {
            get
            {
                if (tbRX1Atten != null) return tbRX1Atten.Value;
                else return -1;
            }
            set
            {
                if (value <= tbRX1Atten.Maximum)
                    tbRX1Atten.Value = value;
                else
                    tbRX1Atten.Value = tbRX2Atten.Maximum;
                }
        }

        public int RX2Atten
        {
            get
            {
                if (tbRX2Atten != null) return tbRX2Atten.Value;
                else return -1;
            }
            set
            {
                if (tbRX2Atten != null)
                {
                    if (value <= tbRX2Atten.Maximum)
                        tbRX2Atten.Value = value;
                    else
                        tbRX2Atten.Value = tbRX2Atten.Maximum;
                }

            }
        }
        public CheckState RX1SquelchState
        {
            get
            {
                if (chkRX1Sql != null) return chkRX1Sql.CheckState;
                else return CheckState.Unchecked;
            }
            set
            {
                if (chkRX1Sql != null)
                    chkRX1Sql.CheckState = value;
            }
        }
        public CheckState RX2SquelchState
        {
            get
            {
                if (chkRX2Sql != null) return chkRX2Sql.CheckState;
                else return CheckState.Unchecked;
            }
            set
            {
                if (chkRX2Sql != null)
                    chkRX2Sql.CheckState = value;
            }
        }
        //public bool RX1SquelchOnOff
        //{
        //    get
        //    {
        //        if (chkRX1Sql != null) return chkRX1Sql.Checked;
        //        else return false;
        //    }
        //    set
        //    {
        //        if (chkRX1Sql != null) chkRX1Sql.Checked = value;

        //    }
        //}

        //public bool RX2SquelchOnOff
        //{
        //    get
        //    {
        //        if (chkRX2Sql != null) return chkRX2Sql.Checked;
        //        else return false;
        //    }
        //    set
        //    {
        //        if (chkRX2Sql != null) chkRX2Sql.Checked = value;

        //    }
        //}

        public bool RX1MuteOnOff
        {
            get
            {
                if (chkRX1Mute != null) return chkRX1Mute.Checked;
                else return false;
            }
            set
            {
                if (chkRX1Mute != null) chkRX1Mute.Checked = value;

            }
        }

        public bool RX2MuteOnOff
        {
            get
            {
                if (chkRX2Mute != null) return chkRX2Mute.Checked;
                else return false;
            }
            set
            {
                if (chkRX2Mute != null) chkRX2Mute.Checked = value;

            }
        }

        public bool SubRXOnOff
        {
            get
            {
                if (chkSubRX != null) return chkSubRX.Checked;
                else return false;
            }
            set
            {
                if (chkSubRX != null) chkSubRX.Checked = value;

            }
        }

        public bool RX1VACOnOff
        {
            get
            {
                if (chkRX1VAC != null) return chkRX1VAC.Checked;
                else return false;
            }
            set
            {
                if (chkRX1VAC != null) chkRX1VAC.Checked = value;

            }
        }
        public bool RX2VACOnOff
        {
            get
            {
                if (chkRX2VAC != null) return chkRX2VAC.Checked;
                else return false;
            }
            set
            {
                if (chkRX2VAC != null) chkRX2VAC.Checked = value;

            }
        }

        public int MasterAFGain
        {
            get
            {
                if (tbMasterAF != null) return tbMasterAF.Value;
                else return -1;
            }
            set
            {
                if (tbMasterAF != null) tbMasterAF.Value = value;

            }
        }
        public int MicGain
        {
            get
            {
                if (tbMicGain != null) return tbMicGain.Value;
                else return -1;
            }
            set
            {
                if (tbMicGain != null) tbMicGain.Value = value;
            }
        }

        public int RX1VACRX
        {
            get
            {
                if (tbRX1VACRX != null) return tbRX1VACRX.Value;
                else return -1;
            }
            set
            {
                if (tbRX1VACRX != null) tbRX1VACRX.Value = value;
            }
        }

        public int RX2VACRX
        {
            get
            {
                if (tbRX2VACRX != null) return tbRX2VACRX.Value;
                else return -1;
            }
            set
            {
                if (tbRX2VACRX != null) tbRX2VACRX.Value = value;
            }
        }

        public int RX1VACTX
        {
            get
            {
                if (tbRX1VACTX != null) return tbRX1VACTX.Value;
                else return -1;
            }
            set
            {
                if (tbRX1VACTX != null) tbRX1VACTX.Value = value;
            }
        }

        public int RX2VACTX
        {
            get
            {
                if (tbRX2VACTX != null) return tbRX2VACTX.Value;
                else return -1;
            }
            set
            {
                if (tbRX2VACTX != null) tbRX2VACTX.Value = value;
            }
        }

        public int TXDrive
        {
            get
            {
                if (tbDrive != null) return tbDrive.Value;
                else return -1;
            }
            set
            {
                if (tbDrive != null) tbDrive.Value = value;

            }
        }



        #endregion
        #region Event Handlers

        private void SliderSettingsForm_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
            Common.SaveForm(this, "SliderSettingsForm");
        }
        #endregion

        //
        // copy initial settings from console controls when form shown
        //
        private void SliderSettingsForm_Activated(object sender, EventArgs e)
        {
            tbRX1AF.Value = console.RX0Gain;
            tbRX2AF.Value = console.RX2Gain;
            tbSubRXAF.Value = console.RX1Gain;
            tbRX1RF.Value = console.RF;
            tbRX2RF.Value = console.RX2RF;
            tbRX1Sql.Value = console.Squelch;
            tbRX2Sql.Value = console.Squelch2;
            tbRX1Pan.Value = console.PanMainRX;
            tbRX2Pan.Value = console.RX2Pan;
            // clip atten at 31dB (Andromeda is designed for 7000 series RF)
            if (console.SetupForm.HermesAttenuatorData <= tbRX1Atten.Maximum)
                tbRX1Atten.Value = console.SetupForm.HermesAttenuatorData;
            else
                tbRX1Atten.Value = tbRX1Atten.Maximum;
            if (/*console.RX2ATT*/console.SetupForm.HermesAttenuatorDataRX2 <= tbRX2Atten.Maximum)
                tbRX2Atten.Value = console.SetupForm.HermesAttenuatorDataRX2/*RX2ATT*/;
            else
                tbRX2Atten.Value = tbRX2Atten.Maximum;
            tbRX2Atten.Value = console.SetupForm.HermesAttenuatorDataRX2/*RX2ATT*/; //MW0LGE_21d step atten changes
            tbSubRXPan.Value = console.PanSubRX;
            tbMasterAF.Value = console.AF;
            tbDrive.Value = console.PWR;

            //[2.10.3.5]MW0LGE
            //if (console.CATSquelch == 1)
            //    chkRX1Sql.Checked = true;
            //else
            //    chkRX1Sql.Checked = false;
            //if (console.CATSquelch2 == 1)
            //    chkRX2Sql.Checked = true;
            //else
            //    chkRX2Sql.Checked = false;
            getSQLinfoOnFormActivate();

            chkRX1Mute.Checked = console.MUT;
            chkRX2Mute.Checked = console.MUT2;
            if (console.CATMultRX == "1")
                chkSubRX.Checked = true;
            else
                chkSubRX.Checked = false;

            tbMicGain.Value = console.CATMIC;
            tbMicGain.Minimum = console.MicGainMin;
            tbMicGain.Maximum = console.MicGainMax;
            tbMicGain.Value = console.CATMIC;
            if (console.SetupForm != null)
            {
                chkRX1VAC.Checked = console.SetupForm.VACEnable;
                chkRX2VAC.Checked = console.SetupForm.VAC2Enable;
                tbRX1VACRX.Value = console.SetupForm.VACRXGain;
                tbRX1VACTX.Value = console.SetupForm.VACTXGain;
                tbRX2VACRX.Value = console.SetupForm.VAC2RXGain;
                tbRX2VACTX.Value = console.SetupForm.VAC2TXGain;
            }
        }

        private void tbRX1AF_Scroll(object sender, EventArgs e)
        {
            console.RX0Gain = tbRX1AF.Value;

            //wd5y
            lblAFrx1.Text = console.ptbRX0Gain.Value.ToString();
            //wd5y
        }

        private void tbRX2AF_Scroll(object sender, EventArgs e)
        {
            console.RX2Gain = tbRX2AF.Value;

            //wd5y
            lblAFrx2.Text = console.ptbRX2Gain.Value.ToString();
            //wd5y
        }

        private void tbSubRXAF_Scroll(object sender, EventArgs e)
        {
            console.RX1Gain = tbSubRXAF.Value;

            //wd5y
            lblSubRXAF.Text = console.ptbRX1Gain.Value.ToString();
            //wd5y
        }

        private void tbRX1RF_Scroll(object sender, EventArgs e)
        {
            if (console.RF != tbRX1RF.Value && console.AutoAGCRX1) console.AutoAGCRX1 = false; // turn off 'auto agc' only if different MW0LGE_21k8

            //wd5y
            if (console.AutoAGCRX1)
            {
                chkAGCAut.Checked = true;
            }
            else
            {
                chkAGCAut.Checked = false;
            }
            //wd5y

            console.RF = tbRX1RF.Value;

            //wd5y
            lblRX1rf.Text = console.ptbRF.Value.ToString();
            //wd5y
        }

        private void tbRX2RF_Scroll(object sender, EventArgs e)
        {
            if (console.RX2RF != tbRX2RF.Value && console.AutoAGCRX2) console.AutoAGCRX2 = false; // turn off 'auto agc' only if different MW0LGE_21k8

            //wd5y
            if (console.AutoAGCRX2)
            {
                chkRX2AGCAut.Checked = true;
            }
            else
            {
                chkRX2AGCAut.Checked = false;
            }
            //wd5y

            console.RX2RF = tbRX2RF.Value;

            //wd5y
            lblRX2rf.Text = console.ptbRX2RF.Value.ToString();
            //wd5y
        }

        private void tbRX1Sql_Scroll(object sender, EventArgs e)
        {
            console.Squelch = tbRX1Sql.Value;

            //wd5y
            lblRX1sql.Text = console.chkSquelch.Text;
            //wd5y
        }

        private void tbRX2Sql_Scroll(object sender, EventArgs e)
        {
            console.Squelch2 = tbRX2Sql.Value;

            //wd5y
            lblRX2sql.Text = console.chkRX2Squelch.Text;
            //wd5y
        }

        private void tbRX1Pan_Scroll(object sender, EventArgs e)
        {
            console.PanMainRX = tbRX1Pan.Value;

            //wd5y
            lblMainPan.Text = console.PanMainRX.ToString();
            //wd5y
        }

        private void tbRX2Pan_Scroll(object sender, EventArgs e)
        {
            console.RX2Pan = tbRX2Pan.Value;

            //wd5y
            lblRX2Pan.Text = console.RX2Pan.ToString();
            //wd5y
        }

        private void tbSubRXPan_Scroll(object sender, EventArgs e)
        {
            console.PanSubRX = tbSubRXPan.Value;

            //wd5y
            lblSubPan.Text = console.PanSubRX.ToString();
            //wd5y
        }

        private void tbMasterAF_Scroll(object sender, EventArgs e)
        {
            console.AF = tbMasterAF.Value;

            //wd5y
            lblaf.Text = console.ptbAF.Value.ToString();
            //wd5y
        }

        private void tbDrive_Scroll(object sender, EventArgs e)
        {
            console.PWR = tbDrive.Value;

            //wd5y
            lblDRV.Text = console.lblPWR.Text;
            //wd5y
        }

        private void chkSubRX_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSubRX.Checked == true)
                console.CATMultRX = "1";
            else
                console.CATMultRX = "0";
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void SliderSettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            FormAutoShown = false;
        }

        private void tbRX1Atten_Scroll(object sender, EventArgs e)
        {
            console.SetupForm.HermesAttenuatorData = tbRX1Atten.Value;

            //wd5y            
            lblRX1at.Text = console.SetupForm.HermesAttenuatorData.ToString();
            //wd5y
        }

        private void tbRX2Atten_Scroll(object sender, EventArgs e)
        {            
            console.SetupForm.HermesAttenuatorDataRX2 = tbRX2Atten.Value; //MW0LGE_21d step atten

            //wd5y
            lblRX2at.Text = console.SetupForm.HermesAttenuatorDataRX2.ToString();
            //wd5y
        }

        private void chkRX1Mute_CheckedChanged(object sender, EventArgs e)
        {
            console.MUT = chkRX1Mute.Checked;
        }

        private void chkRX2Mute_CheckedChanged(object sender, EventArgs e)
        {
            console.MUT2 = chkRX2Mute.Checked;
        }

        private void ChkRX1VAC_CheckedChanged(object sender, EventArgs e)
        {
            if (console.SetupForm != null)
                console.SetupForm.VACEnable = chkRX1VAC.Checked;
        }

        private void ChkRX2VAC_CheckedChanged(object sender, EventArgs e)
        {
            if(console.SetupForm != null)
                console.SetupForm.VAC2Enable = chkRX2VAC.Checked;
        }

        private void TbMicGain_Scroll(object sender, EventArgs e)
        {
            console.CATMIC = tbMicGain.Value;

            //wd5y
            lblmic.Text = console.lblMicVal.Text;
            //wd5y
        }

        private void TbRX1VACRX_Scroll(object sender, EventArgs e)
        {
            if (console.SetupForm != null)
            {
                console.SetupForm.VACRXGain = tbRX1VACRX.Value;

                //wd5y
                lblRX1VACRX.Text = console.SetupForm.VACRXGain.ToString();
                //wd5y
            }
        }

        private void TbRX1VACTX_Scroll(object sender, EventArgs e)
        {
            if (console.SetupForm != null)
            {
                console.SetupForm.VACTXGain = tbRX1VACTX.Value;

                //wd5y
                lblRX1VACTX.Text = console.SetupForm.VACTXGain.ToString();
                //wd5y
            }
        }

        private void TbRX2VACRX_Scroll(object sender, EventArgs e)
        {
            if (console.SetupForm != null)
            {
                console.SetupForm.VAC2RXGain = tbRX2VACRX.Value;

                //wd5y
                lblRX2VACRX.Text = console.SetupForm.VAC2RXGain.ToString();
                //wd5y
            }
        }

        private void TbRX2VACTX_Scroll(object sender, EventArgs e)
        {
            if (console.SetupForm != null)
            {
                console.SetupForm.VAC2TXGain = tbRX2VACTX.Value;

                //wd5y
                lblRX2VACTX.Text = console.SetupForm.VAC2TXGain.ToString();
                //wd5y
            }
        }

        // method called by console encoder event. Provides option of auto-show and auto-hide
        // if form was not shown, mark it as opened by an encoder event

        public void FormEncoderEvent()
        {
            if (!this.Visible)
            {
                FormAutoShown = true;
                this.Show();
            }
            // set timer if form is auto shown
            if(FormAutoShown)
            {
                AutoHideTimer.Enabled = false;
                AutoHideTimer.AutoReset = false;                    // just one callback
                AutoHideTimer.Interval = 10000;                     // 10 seconds
                AutoHideTimer.Enabled = true;

            }
        }

        // callback function when 10 second timer expires; hide the form
        private void Callback(object source, ElapsedEventArgs e)
        {
            FormAutoShown = false;
            AutoHideTimer.Enabled = false;
            this.Hide();
        }
        //private void chkRX1Sql_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (chkRX1Sql.Checked == true)
        //        console.CATSquelch = 1;
        //    else
        //        console.CATSquelch = 0;
        //}

        //private void chkRX2Sql_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (chkRX2Sql.Checked == true)
        //        console.CATSquelch2 = 1;
        //    else
        //        console.CATSquelch2 = 0;
        //}
        private void chkRX1Sql_CheckStateChanged(object sender, EventArgs e)
        {
            switch (chkRX1Sql.CheckState)
            {
                case CheckState.Unchecked:
                    console.CATSquelch = 0;
                    break;
                case CheckState.Checked: //sql
                    console.CATSquelch = 1;
                    break;
                case CheckState.Indeterminate: //vsql
                    console.CATSquelch = 2;
                    break;
            }
            updateSQLButtons(1);
        }
        private void chkRX2Sql_CheckStateChanged(object sender, EventArgs e)
        {
            switch (chkRX2Sql.CheckState)
            {
                case CheckState.Unchecked:
                    console.CATSquelch2 = 0;
                    break;
                case CheckState.Checked: //sql
                    console.CATSquelch2 = 1;
                    break;
                case CheckState.Indeterminate: //vsql
                    console.CATSquelch2 = 2;
                    break;
            }
            updateSQLButtons(2);
        }
        private void updateSQLButtons(int rx)
        {
            if (rx == 1)
            {
                if (chkRX1Sql.CheckState == CheckState.Indeterminate)
                    chkRX1Sql.Text = "VSQL";

                //wd5y
                if (chkRX1Sql.CheckState == CheckState.Checked)
                    chkRX1Sql.Text = "SQL";
                if (chkRX1Sql.CheckState == CheckState.Unchecked)
                    chkRX1Sql.Text = "SQL OFF";
                //wd5y
            }
            else if (rx == 2)
            {
                if (chkRX2Sql.CheckState == CheckState.Indeterminate)
                    chkRX2Sql.Text = "VSQL";

                //wd5y
                if (chkRX2Sql.CheckState == CheckState.Checked)
                    chkRX2Sql.Text = "SQL";
                if (chkRX2Sql.CheckState == CheckState.Unchecked)
                    chkRX2Sql.Text = "SQL OFF";
                //wd5y
            }
        }
        private void getSQLinfoOnFormActivate()
        {
            if (console == null) return;

            switch (console.CATSquelch)
            {
                case 0:
                    chkRX1Sql.CheckState = CheckState.Unchecked;
                    break;
                case 1: //sql
                    chkRX1Sql.CheckState = CheckState.Checked;
                    break;
                case 2: //vsql
                    chkRX1Sql.CheckState = CheckState.Indeterminate;
                    break;
            }

            switch (console.CATSquelch2)
            {
                case 0:
                    chkRX2Sql.CheckState = CheckState.Unchecked;
                    break;
                case 1: //sql
                    chkRX2Sql.CheckState = CheckState.Checked;
                    break;
                case 2: //vsql
                    chkRX2Sql.CheckState = CheckState.Indeterminate;
                    break;
            }
        }

        //wd5y
        private void chkAGCAut_CheckedChanged(object sender, EventArgs e)
        {
            console.AutoAGCRX1 = chkAGCAut.Checked;
        }

        private void chkRX2AGCAut_CheckedChanged(object sender, EventArgs e)
        {
            console.AutoAGCRX2 = chkRX2AGCAut.Checked;
        }

        private void chkATT1_CheckedChanged(object sender, EventArgs e)
        {
            if (console.SetupForm != null)
                console.SetupForm.chkHermesStepAttenuator.Checked = chkATT1.Checked;
        }

        private void chkATT2_CheckedChanged(object sender, EventArgs e)
        {
            if (console.SetupForm != null)
                console.SetupForm.chkRX2StepAtt.Checked = chkATT2.Checked;
        }
        //wd5y
    }
}
