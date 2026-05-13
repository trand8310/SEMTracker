namespace MainClient
{
    partial class MainForm
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
            comboBox_QTPName = new ComboBox();
            label6 = new Label();
            label8 = new Label();
            label7 = new Label();
            label9 = new Label();
            label5 = new Label();
            linkLabel1 = new LinkLabel();
            checkBox_IsDetailLog = new CheckBox();
            textBox_DevApiUrl = new TextBox();
            label14 = new Label();
            label18 = new Label();
            numericUpDown_MainResetTimeout = new NumericUpDown();
            label26 = new Label();
            checkBox_IsProxyMode = new CheckBox();
            checkBox_IsHiddenMode = new CheckBox();
            label110 = new Label();
            numericUpDown_Multiple = new NumericUpDown();
            buttonClear = new Button();
            textBox_TaskApiUrl = new TextBox();
            label100 = new Label();
            numericUpDown_FetchTaskInterval = new NumericUpDown();
            label27 = new Label();
            numericUpDown_MaximumConcurrency = new NumericUpDown();
            label4 = new Label();
            textBox_TaskName = new TextBox();
            label3 = new Label();
            label2 = new Label();
            textBox_ProxyIpUrl = new TextBox();
            label1 = new Label();
            groupBox6 = new GroupBox();
            radioButton_UseLocalDev = new RadioButton();
            radioButton_UseSystemDev = new RadioButton();
            groupBox33 = new GroupBox();
            statusStrip1 = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            toolStripStatusLabel1 = new ToolStripStatusLabel();
            toolStripStatusLabel4 = new ToolStripStatusLabel();
            toolStripStatusLabel5 = new ToolStripStatusLabel();
            toolStripStatusLabel6 = new ToolStripStatusLabel();
            toolStripProgressBarDownload = new ToolStripProgressBar();
            label25 = new Label();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            checkBox_BlockMedia = new CheckBox();
            checkBox_BlockImage = new CheckBox();
            checkBox_AutoUpdate = new CheckBox();
            button3 = new Button();
            button2 = new Button();
            button6 = new Button();
            checkBox_IsTest = new CheckBox();
            btnStartStop = new Button();
            label43 = new Label();
            comboBox_VersionList = new ComboBox();
            btnUpdate = new Button();
            checkBox_PVsTriggerOne = new CheckBox();
            label30 = new Label();
            comboBox_KernelVersion = new ComboBox();
            checkBox_UVsTriggerOne = new CheckBox();
            label38 = new Label();
            label39 = new Label();
            numericUpDown_IpTtl = new NumericUpDown();
            textBox_PVOverride = new TextBox();
            textBox_UVOverride = new TextBox();
            label33 = new Label();
            label31 = new Label();
            checkBox_GetIpInfo = new CheckBox();
            linkLabel2 = new LinkLabel();
            checkBox_IsRealIp = new CheckBox();
            label36 = new Label();
            textBox_PageloadedDelay = new TextBox();
            label35 = new Label();
            label28 = new Label();
            label29 = new Label();
            numericUpDown_PageLoadingTimeout = new NumericUpDown();
            groupBox9 = new GroupBox();
            label12 = new Label();
            label11 = new Label();
            label10 = new Label();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_MainResetTimeout).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_Multiple).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_FetchTaskInterval).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_MaximumConcurrency).BeginInit();
            groupBox6.SuspendLayout();
            statusStrip1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_IpTtl).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_PageLoadingTimeout).BeginInit();
            groupBox9.SuspendLayout();
            SuspendLayout();
            // 
            // comboBox_QTPName
            // 
            comboBox_QTPName.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox_QTPName.FormattingEnabled = true;
            comboBox_QTPName.Location = new Point(371, 105);
            comboBox_QTPName.Margin = new Padding(3, 2, 3, 2);
            comboBox_QTPName.Name = "comboBox_QTPName";
            comboBox_QTPName.Size = new Size(145, 28);
            comboBox_QTPName.TabIndex = 95;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(6, 52);
            label6.Margin = new Padding(5, 0, 5, 0);
            label6.Name = "label6";
            label6.Size = new Size(82, 20);
            label6.TabIndex = 85;
            label6.Text = "执行数量:0";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(6, 117);
            label8.Margin = new Padding(5, 0, 5, 0);
            label8.Name = "label8";
            label8.Size = new Size(82, 20);
            label8.TabIndex = 84;
            label8.Text = "点击数量:0";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(6, 85);
            label7.Margin = new Padding(5, 0, 5, 0);
            label7.Name = "label7";
            label7.Size = new Size(82, 20);
            label7.TabIndex = 83;
            label7.Text = "曝光数量:0";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(6, 150);
            label9.Margin = new Padding(5, 0, 5, 0);
            label9.Name = "label9";
            label9.Size = new Size(82, 20);
            label9.TabIndex = 82;
            label9.Text = "成功数量:0";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(6, 20);
            label5.Margin = new Padding(5, 0, 5, 0);
            label5.Name = "label5";
            label5.Size = new Size(82, 20);
            label5.TabIndex = 81;
            label5.Text = "提交数量:0";
            // 
            // linkLabel1
            // 
            linkLabel1.AutoSize = true;
            linkLabel1.Location = new Point(555, 328);
            linkLabel1.Margin = new Padding(4, 0, 4, 0);
            linkLabel1.Name = "linkLabel1";
            linkLabel1.Size = new Size(69, 20);
            linkLabel1.TabIndex = 80;
            linkLabel1.TabStop = true;
            linkLabel1.Text = "开机启动";
            linkLabel1.LinkClicked += linkLabel1_LinkClicked;
            // 
            // checkBox_IsDetailLog
            // 
            checkBox_IsDetailLog.AutoSize = true;
            checkBox_IsDetailLog.Location = new Point(659, 250);
            checkBox_IsDetailLog.Margin = new Padding(5);
            checkBox_IsDetailLog.Name = "checkBox_IsDetailLog";
            checkBox_IsDetailLog.Size = new Size(91, 24);
            checkBox_IsDetailLog.TabIndex = 75;
            checkBox_IsDetailLog.Text = "详细日志";
            checkBox_IsDetailLog.UseVisualStyleBackColor = true;
            // 
            // textBox_DevApiUrl
            // 
            textBox_DevApiUrl.Location = new Point(117, 73);
            textBox_DevApiUrl.Margin = new Padding(5, 4, 5, 4);
            textBox_DevApiUrl.Name = "textBox_DevApiUrl";
            textBox_DevApiUrl.Size = new Size(545, 27);
            textBox_DevApiUrl.TabIndex = 74;
            textBox_DevApiUrl.Text = "http://117.21.200.18:9000/api/fingerprint.php";
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.Location = new Point(39, 76);
            label14.Margin = new Padding(5, 0, 5, 0);
            label14.Name = "label14";
            label14.Size = new Size(73, 20);
            label14.TabIndex = 73;
            label14.Text = "设备接口:";
            // 
            // label18
            // 
            label18.AutoSize = true;
            label18.Location = new Point(197, 206);
            label18.Margin = new Padding(5, 0, 5, 0);
            label18.Name = "label18";
            label18.Size = new Size(83, 20);
            label18.TabIndex = 64;
            label18.Text = "分钟±30秒";
            // 
            // numericUpDown_MainResetTimeout
            // 
            numericUpDown_MainResetTimeout.Location = new Point(117, 201);
            numericUpDown_MainResetTimeout.Margin = new Padding(5, 4, 5, 4);
            numericUpDown_MainResetTimeout.Maximum = new decimal(new int[] { 1440, 0, 0, 0 });
            numericUpDown_MainResetTimeout.Name = "numericUpDown_MainResetTimeout";
            numericUpDown_MainResetTimeout.Size = new Size(75, 27);
            numericUpDown_MainResetTimeout.TabIndex = 61;
            // 
            // label26
            // 
            label26.AutoSize = true;
            label26.Location = new Point(39, 206);
            label26.Margin = new Padding(5, 0, 5, 0);
            label26.Name = "label26";
            label26.Size = new Size(73, 20);
            label26.TabIndex = 60;
            label26.Text = "进程重置:";
            // 
            // checkBox_IsProxyMode
            // 
            checkBox_IsProxyMode.AutoSize = true;
            checkBox_IsProxyMode.Location = new Point(555, 221);
            checkBox_IsProxyMode.Margin = new Padding(5);
            checkBox_IsProxyMode.Name = "checkBox_IsProxyMode";
            checkBox_IsProxyMode.Size = new Size(91, 24);
            checkBox_IsProxyMode.TabIndex = 59;
            checkBox_IsProxyMode.Text = "代理模式";
            checkBox_IsProxyMode.UseVisualStyleBackColor = true;
            // 
            // checkBox_IsHiddenMode
            // 
            checkBox_IsHiddenMode.AutoSize = true;
            checkBox_IsHiddenMode.Location = new Point(555, 192);
            checkBox_IsHiddenMode.Margin = new Padding(5);
            checkBox_IsHiddenMode.Name = "checkBox_IsHiddenMode";
            checkBox_IsHiddenMode.Size = new Size(91, 24);
            checkBox_IsHiddenMode.TabIndex = 58;
            checkBox_IsHiddenMode.Text = "隐藏模式";
            checkBox_IsHiddenMode.UseVisualStyleBackColor = true;
            // 
            // label110
            // 
            label110.AutoSize = true;
            label110.Location = new Point(348, 141);
            label110.Margin = new Padding(5, 0, 5, 0);
            label110.Name = "label110";
            label110.Size = new Size(73, 20);
            label110.TabIndex = 31;
            label110.Text = "任务倍速:";
            // 
            // numericUpDown_Multiple
            // 
            numericUpDown_Multiple.Location = new Point(429, 140);
            numericUpDown_Multiple.Margin = new Padding(5, 4, 5, 4);
            numericUpDown_Multiple.Name = "numericUpDown_Multiple";
            numericUpDown_Multiple.Size = new Size(87, 27);
            numericUpDown_Multiple.TabIndex = 32;
            numericUpDown_Multiple.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // buttonClear
            // 
            buttonClear.Font = new Font("宋体", 9F);
            buttonClear.ForeColor = Color.Red;
            buttonClear.Location = new Point(672, 101);
            buttonClear.Margin = new Padding(5);
            buttonClear.Name = "buttonClear";
            buttonClear.Size = new Size(66, 31);
            buttonClear.TabIndex = 22;
            buttonClear.Text = "清理";
            buttonClear.UseVisualStyleBackColor = true;
            buttonClear.Click += buttonClear_Click;
            // 
            // textBox_TaskApiUrl
            // 
            textBox_TaskApiUrl.Location = new Point(117, 9);
            textBox_TaskApiUrl.Margin = new Padding(5, 4, 5, 4);
            textBox_TaskApiUrl.Name = "textBox_TaskApiUrl";
            textBox_TaskApiUrl.Size = new Size(545, 27);
            textBox_TaskApiUrl.TabIndex = 21;
            // 
            // label100
            // 
            label100.AutoSize = true;
            label100.Location = new Point(39, 13);
            label100.Margin = new Padding(5, 0, 5, 0);
            label100.Name = "label100";
            label100.Size = new Size(73, 20);
            label100.TabIndex = 20;
            label100.Text = "任务接口:";
            // 
            // numericUpDown_FetchTaskInterval
            // 
            numericUpDown_FetchTaskInterval.Location = new Point(117, 137);
            numericUpDown_FetchTaskInterval.Margin = new Padding(5, 4, 5, 4);
            numericUpDown_FetchTaskInterval.Maximum = new decimal(new int[] { 60000, 0, 0, 0 });
            numericUpDown_FetchTaskInterval.Name = "numericUpDown_FetchTaskInterval";
            numericUpDown_FetchTaskInterval.Size = new Size(75, 27);
            numericUpDown_FetchTaskInterval.TabIndex = 14;
            numericUpDown_FetchTaskInterval.Value = new decimal(new int[] { 1000, 0, 0, 0 });
            // 
            // label27
            // 
            label27.AutoSize = true;
            label27.Location = new Point(39, 174);
            label27.Margin = new Padding(5, 0, 5, 0);
            label27.Name = "label27";
            label27.Size = new Size(73, 20);
            label27.TabIndex = 9;
            label27.Text = "并发数量:";
            // 
            // numericUpDown_MaximumConcurrency
            // 
            numericUpDown_MaximumConcurrency.Location = new Point(117, 169);
            numericUpDown_MaximumConcurrency.Margin = new Padding(5, 4, 5, 4);
            numericUpDown_MaximumConcurrency.Maximum = new decimal(new int[] { 999, 0, 0, 0 });
            numericUpDown_MaximumConcurrency.Name = "numericUpDown_MaximumConcurrency";
            numericUpDown_MaximumConcurrency.Size = new Size(75, 27);
            numericUpDown_MaximumConcurrency.TabIndex = 10;
            numericUpDown_MaximumConcurrency.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(39, 109);
            label4.Margin = new Padding(5, 0, 5, 0);
            label4.Name = "label4";
            label4.Size = new Size(73, 20);
            label4.TabIndex = 0;
            label4.Text = "任务名称:";
            // 
            // textBox_TaskName
            // 
            textBox_TaskName.Location = new Point(117, 105);
            textBox_TaskName.Margin = new Padding(5, 4, 5, 4);
            textBox_TaskName.Name = "textBox_TaskName";
            textBox_TaskName.Size = new Size(152, 27);
            textBox_TaskName.TabIndex = 2;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(197, 141);
            label3.Margin = new Padding(5, 0, 5, 0);
            label3.Name = "label3";
            label3.Size = new Size(39, 20);
            label3.TabIndex = 0;
            label3.Text = "毫秒";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(39, 141);
            label2.Margin = new Padding(5, 0, 5, 0);
            label2.Name = "label2";
            label2.Size = new Size(73, 20);
            label2.TabIndex = 0;
            label2.Text = "获取间隔:";
            // 
            // textBox_ProxyIpUrl
            // 
            textBox_ProxyIpUrl.Location = new Point(117, 41);
            textBox_ProxyIpUrl.Margin = new Padding(5, 4, 5, 4);
            textBox_ProxyIpUrl.Name = "textBox_ProxyIpUrl";
            textBox_ProxyIpUrl.Size = new Size(545, 27);
            textBox_ProxyIpUrl.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(26, 45);
            label1.Margin = new Padding(5, 0, 5, 0);
            label1.Name = "label1";
            label1.Size = new Size(86, 20);
            label1.TabIndex = 0;
            label1.Text = "代理IP接口:";
            // 
            // groupBox6
            // 
            groupBox6.Controls.Add(radioButton_UseLocalDev);
            groupBox6.Controls.Add(radioButton_UseSystemDev);
            groupBox6.Location = new Point(832, 299);
            groupBox6.Margin = new Padding(5);
            groupBox6.Name = "groupBox6";
            groupBox6.Padding = new Padding(5);
            groupBox6.Size = new Size(185, 56);
            groupBox6.TabIndex = 52;
            groupBox6.TabStop = false;
            groupBox6.Text = "设备库";
            // 
            // radioButton_UseLocalDev
            // 
            radioButton_UseLocalDev.AutoSize = true;
            radioButton_UseLocalDev.Location = new Point(104, 25);
            radioButton_UseLocalDev.Margin = new Padding(5);
            radioButton_UseLocalDev.Name = "radioButton_UseLocalDev";
            radioButton_UseLocalDev.Size = new Size(75, 24);
            radioButton_UseLocalDev.TabIndex = 56;
            radioButton_UseLocalDev.TabStop = true;
            radioButton_UseLocalDev.Text = "本地库";
            radioButton_UseLocalDev.UseVisualStyleBackColor = true;
            // 
            // radioButton_UseSystemDev
            // 
            radioButton_UseSystemDev.AutoSize = true;
            radioButton_UseSystemDev.Checked = true;
            radioButton_UseSystemDev.Location = new Point(10, 25);
            radioButton_UseSystemDev.Margin = new Padding(5);
            radioButton_UseSystemDev.Name = "radioButton_UseSystemDev";
            radioButton_UseSystemDev.Size = new Size(75, 24);
            radioButton_UseSystemDev.TabIndex = 54;
            radioButton_UseSystemDev.TabStop = true;
            radioButton_UseSystemDev.Text = "网络库";
            radioButton_UseSystemDev.UseVisualStyleBackColor = true;
            // 
            // groupBox33
            // 
            groupBox33.Dock = DockStyle.Fill;
            groupBox33.Location = new Point(0, 440);
            groupBox33.Margin = new Padding(5, 4, 5, 4);
            groupBox33.Name = "groupBox33";
            groupBox33.Padding = new Padding(5, 4, 5, 4);
            groupBox33.Size = new Size(1091, 321);
            groupBox33.TabIndex = 4;
            groupBox33.TabStop = false;
            groupBox33.Text = "日志";
            // 
            // statusStrip1
            // 
            statusStrip1.ImageScalingSize = new Size(20, 20);
            statusStrip1.Items.AddRange(new ToolStripItem[] { lblStatus, toolStripStatusLabel1, toolStripStatusLabel4, toolStripStatusLabel5, toolStripStatusLabel6, toolStripProgressBarDownload });
            statusStrip1.Location = new Point(0, 761);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Padding = new Padding(3, 0, 18, 0);
            statusStrip1.Size = new Size(1091, 26);
            statusStrip1.TabIndex = 7;
            statusStrip1.Text = "statusStrip1";
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(167, 20);
            lblStatus.Text = "toolStripStatusLabel2";
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new Size(52, 20);
            toolStripStatusLabel1.Text = "CPU:0";
            // 
            // toolStripStatusLabel4
            // 
            toolStripStatusLabel4.Name = "toolStripStatusLabel4";
            toolStripStatusLabel4.Size = new Size(82, 20);
            toolStripStatusLabel4.Text = "执行总量:0";
            // 
            // toolStripStatusLabel5
            // 
            toolStripStatusLabel5.Name = "toolStripStatusLabel5";
            toolStripStatusLabel5.Size = new Size(82, 20);
            toolStripStatusLabel5.Text = "曝光总量:0";
            // 
            // toolStripStatusLabel6
            // 
            toolStripStatusLabel6.Name = "toolStripStatusLabel6";
            toolStripStatusLabel6.Size = new Size(82, 20);
            toolStripStatusLabel6.Text = "点击总量:0";
            // 
            // toolStripProgressBarDownload
            // 
            toolStripProgressBarDownload.Name = "toolStripProgressBarDownload";
            toolStripProgressBarDownload.Size = new Size(100, 19);
            toolStripProgressBarDownload.Visible = false;
            // 
            // label25
            // 
            label25.AutoSize = true;
            label25.Location = new Point(286, 109);
            label25.Margin = new Padding(5, 0, 5, 0);
            label25.Name = "label25";
            label25.Size = new Size(77, 20);
            label25.TabIndex = 96;
            label25.Text = "任务DLLs:";
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Dock = DockStyle.Top;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Margin = new Padding(3, 2, 3, 2);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1091, 440);
            tabControl1.TabIndex = 8;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(checkBox_BlockMedia);
            tabPage1.Controls.Add(checkBox_BlockImage);
            tabPage1.Controls.Add(checkBox_AutoUpdate);
            tabPage1.Controls.Add(button3);
            tabPage1.Controls.Add(button2);
            tabPage1.Controls.Add(button6);
            tabPage1.Controls.Add(checkBox_IsTest);
            tabPage1.Controls.Add(btnStartStop);
            tabPage1.Controls.Add(label43);
            tabPage1.Controls.Add(comboBox_VersionList);
            tabPage1.Controls.Add(btnUpdate);
            tabPage1.Controls.Add(groupBox6);
            tabPage1.Controls.Add(checkBox_PVsTriggerOne);
            tabPage1.Controls.Add(textBox_DevApiUrl);
            tabPage1.Controls.Add(label30);
            tabPage1.Controls.Add(label14);
            tabPage1.Controls.Add(comboBox_KernelVersion);
            tabPage1.Controls.Add(checkBox_UVsTriggerOne);
            tabPage1.Controls.Add(label38);
            tabPage1.Controls.Add(label39);
            tabPage1.Controls.Add(numericUpDown_IpTtl);
            tabPage1.Controls.Add(textBox_PVOverride);
            tabPage1.Controls.Add(textBox_UVOverride);
            tabPage1.Controls.Add(label33);
            tabPage1.Controls.Add(label31);
            tabPage1.Controls.Add(checkBox_GetIpInfo);
            tabPage1.Controls.Add(linkLabel2);
            tabPage1.Controls.Add(checkBox_IsRealIp);
            tabPage1.Controls.Add(numericUpDown_Multiple);
            tabPage1.Controls.Add(label110);
            tabPage1.Controls.Add(textBox_TaskName);
            tabPage1.Controls.Add(label4);
            tabPage1.Controls.Add(label36);
            tabPage1.Controls.Add(textBox_PageloadedDelay);
            tabPage1.Controls.Add(label35);
            tabPage1.Controls.Add(label28);
            tabPage1.Controls.Add(label29);
            tabPage1.Controls.Add(numericUpDown_PageLoadingTimeout);
            tabPage1.Controls.Add(checkBox_IsDetailLog);
            tabPage1.Controls.Add(checkBox_IsHiddenMode);
            tabPage1.Controls.Add(checkBox_IsProxyMode);
            tabPage1.Controls.Add(textBox_ProxyIpUrl);
            tabPage1.Controls.Add(linkLabel1);
            tabPage1.Controls.Add(label1);
            tabPage1.Controls.Add(groupBox9);
            tabPage1.Controls.Add(label100);
            tabPage1.Controls.Add(textBox_TaskApiUrl);
            tabPage1.Controls.Add(label25);
            tabPage1.Controls.Add(numericUpDown_MainResetTimeout);
            tabPage1.Controls.Add(label26);
            tabPage1.Controls.Add(buttonClear);
            tabPage1.Controls.Add(label18);
            tabPage1.Controls.Add(comboBox_QTPName);
            tabPage1.Controls.Add(numericUpDown_FetchTaskInterval);
            tabPage1.Controls.Add(label2);
            tabPage1.Controls.Add(label3);
            tabPage1.Controls.Add(numericUpDown_MaximumConcurrency);
            tabPage1.Controls.Add(label27);
            tabPage1.Location = new Point(4, 29);
            tabPage1.Margin = new Padding(3, 2, 3, 2);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3, 2, 3, 2);
            tabPage1.Size = new Size(1083, 407);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "信息";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // checkBox_BlockMedia
            // 
            checkBox_BlockMedia.AutoSize = true;
            checkBox_BlockMedia.Location = new Point(659, 279);
            checkBox_BlockMedia.Margin = new Padding(5);
            checkBox_BlockMedia.Name = "checkBox_BlockMedia";
            checkBox_BlockMedia.Size = new Size(91, 24);
            checkBox_BlockMedia.TabIndex = 199;
            checkBox_BlockMedia.Text = "禁止视频";
            checkBox_BlockMedia.UseVisualStyleBackColor = true;
            // 
            // checkBox_BlockImage
            // 
            checkBox_BlockImage.AutoSize = true;
            checkBox_BlockImage.Location = new Point(555, 279);
            checkBox_BlockImage.Margin = new Padding(5);
            checkBox_BlockImage.Name = "checkBox_BlockImage";
            checkBox_BlockImage.Size = new Size(91, 24);
            checkBox_BlockImage.TabIndex = 198;
            checkBox_BlockImage.Text = "禁止图片";
            checkBox_BlockImage.UseVisualStyleBackColor = true;
            // 
            // checkBox_AutoUpdate
            // 
            checkBox_AutoUpdate.AutoSize = true;
            checkBox_AutoUpdate.Location = new Point(352, 335);
            checkBox_AutoUpdate.Name = "checkBox_AutoUpdate";
            checkBox_AutoUpdate.Size = new Size(91, 24);
            checkBox_AutoUpdate.TabIndex = 197;
            checkBox_AutoUpdate.Text = "自动更新";
            checkBox_AutoUpdate.UseVisualStyleBackColor = true;
            // 
            // button3
            // 
            button3.Font = new Font("宋体", 9F);
            button3.ForeColor = Color.Red;
            button3.Location = new Point(672, 139);
            button3.Margin = new Padding(5);
            button3.Name = "button3";
            button3.Size = new Size(142, 31);
            button3.TabIndex = 196;
            button3.Text = "重启系统";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click_1;
            // 
            // button2
            // 
            button2.Font = new Font("宋体", 9F);
            button2.ForeColor = Color.Red;
            button2.Location = new Point(748, 101);
            button2.Margin = new Padding(5);
            button2.Name = "button2";
            button2.Size = new Size(66, 31);
            button2.TabIndex = 195;
            button2.Text = "注销";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // button6
            // 
            button6.Location = new Point(295, 298);
            button6.Name = "button6";
            button6.Size = new Size(54, 30);
            button6.TabIndex = 193;
            button6.Text = "下载";
            button6.UseVisualStyleBackColor = true;
            button6.Click += button6_Click;
            // 
            // checkBox_IsTest
            // 
            checkBox_IsTest.AutoSize = true;
            checkBox_IsTest.Location = new Point(659, 192);
            checkBox_IsTest.Margin = new Padding(5);
            checkBox_IsTest.Name = "checkBox_IsTest";
            checkBox_IsTest.Size = new Size(91, 24);
            checkBox_IsTest.TabIndex = 186;
            checkBox_IsTest.Text = "测试模式";
            checkBox_IsTest.UseVisualStyleBackColor = true;
            // 
            // btnStartStop
            // 
            btnStartStop.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnStartStop.Location = new Point(672, 8);
            btnStartStop.Margin = new Padding(5);
            btnStartStop.Name = "btnStartStop";
            btnStartStop.Size = new Size(142, 73);
            btnStartStop.TabIndex = 185;
            btnStartStop.Text = "开始";
            btnStartStop.UseVisualStyleBackColor = true;
            btnStartStop.Click += btnStartStop_Click;
            // 
            // label43
            // 
            label43.AutoSize = true;
            label43.Location = new Point(39, 337);
            label43.Margin = new Padding(5, 0, 5, 0);
            label43.Name = "label43";
            label43.Size = new Size(73, 20);
            label43.TabIndex = 181;
            label43.Text = "更新列表:";
            // 
            // comboBox_VersionList
            // 
            comboBox_VersionList.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox_VersionList.FormattingEnabled = true;
            comboBox_VersionList.Location = new Point(117, 332);
            comboBox_VersionList.Margin = new Padding(3, 2, 3, 2);
            comboBox_VersionList.Name = "comboBox_VersionList";
            comboBox_VersionList.Size = new Size(174, 28);
            comboBox_VersionList.TabIndex = 180;
            // 
            // btnUpdate
            // 
            btnUpdate.Location = new Point(295, 331);
            btnUpdate.Name = "btnUpdate";
            btnUpdate.Size = new Size(54, 30);
            btnUpdate.TabIndex = 179;
            btnUpdate.Text = "更新";
            btnUpdate.UseVisualStyleBackColor = true;
            btnUpdate.Click += btnUpdate_Click;
            // 
            // checkBox_PVsTriggerOne
            // 
            checkBox_PVsTriggerOne.AutoSize = true;
            checkBox_PVsTriggerOne.Location = new Point(253, 368);
            checkBox_PVsTriggerOne.Margin = new Padding(5);
            checkBox_PVsTriggerOne.Name = "checkBox_PVsTriggerOne";
            checkBox_PVsTriggerOne.Size = new Size(202, 24);
            checkBox_PVsTriggerOne.TabIndex = 154;
            checkBox_PVsTriggerOne.Text = "多PV时,仅触发1个广告位.";
            checkBox_PVsTriggerOne.UseVisualStyleBackColor = true;
            // 
            // label30
            // 
            label30.AutoSize = true;
            label30.Location = new Point(24, 303);
            label30.Margin = new Padding(5, 0, 5, 0);
            label30.Name = "label30";
            label30.Size = new Size(88, 20);
            label30.TabIndex = 152;
            label30.Text = "浏览器版本:";
            // 
            // comboBox_KernelVersion
            // 
            comboBox_KernelVersion.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox_KernelVersion.FormattingEnabled = true;
            comboBox_KernelVersion.Location = new Point(117, 299);
            comboBox_KernelVersion.Margin = new Padding(3, 2, 3, 2);
            comboBox_KernelVersion.Name = "comboBox_KernelVersion";
            comboBox_KernelVersion.Size = new Size(174, 28);
            comboBox_KernelVersion.TabIndex = 151;
            // 
            // checkBox_UVsTriggerOne
            // 
            checkBox_UVsTriggerOne.AutoSize = true;
            checkBox_UVsTriggerOne.Location = new Point(39, 368);
            checkBox_UVsTriggerOne.Margin = new Padding(5);
            checkBox_UVsTriggerOne.Name = "checkBox_UVsTriggerOne";
            checkBox_UVsTriggerOne.Size = new Size(204, 24);
            checkBox_UVsTriggerOne.TabIndex = 149;
            checkBox_UVsTriggerOne.Text = "多UV时,仅触发1个广告位.";
            checkBox_UVsTriggerOne.UseVisualStyleBackColor = true;
            // 
            // label38
            // 
            label38.AutoSize = true;
            label38.Location = new Point(521, 239);
            label38.Margin = new Padding(5, 0, 5, 0);
            label38.Name = "label38";
            label38.Size = new Size(24, 20);
            label38.TabIndex = 141;
            label38.Text = "秒";
            // 
            // label39
            // 
            label39.AutoSize = true;
            label39.Location = new Point(335, 237);
            label39.Margin = new Padding(5, 0, 5, 0);
            label39.Name = "label39";
            label39.Size = new Size(86, 20);
            label39.TabIndex = 139;
            label39.Text = "IP有效时长:";
            // 
            // numericUpDown_IpTtl
            // 
            numericUpDown_IpTtl.Location = new Point(429, 233);
            numericUpDown_IpTtl.Margin = new Padding(5, 4, 5, 4);
            numericUpDown_IpTtl.Maximum = new decimal(new int[] { 1800, 0, 0, 0 });
            numericUpDown_IpTtl.Name = "numericUpDown_IpTtl";
            numericUpDown_IpTtl.Size = new Size(87, 27);
            numericUpDown_IpTtl.TabIndex = 140;
            // 
            // textBox_PVOverride
            // 
            textBox_PVOverride.Location = new Point(429, 201);
            textBox_PVOverride.Margin = new Padding(5, 4, 5, 4);
            textBox_PVOverride.Name = "textBox_PVOverride";
            textBox_PVOverride.Size = new Size(86, 27);
            textBox_PVOverride.TabIndex = 136;
            // 
            // textBox_UVOverride
            // 
            textBox_UVOverride.Location = new Point(429, 169);
            textBox_UVOverride.Margin = new Padding(5, 4, 5, 4);
            textBox_UVOverride.Name = "textBox_UVOverride";
            textBox_UVOverride.Size = new Size(86, 27);
            textBox_UVOverride.TabIndex = 135;
            // 
            // label33
            // 
            label33.AutoSize = true;
            label33.Location = new Point(389, 206);
            label33.Margin = new Padding(5, 0, 5, 0);
            label33.Name = "label33";
            label33.Size = new Size(32, 20);
            label33.TabIndex = 133;
            label33.Text = "PV:";
            // 
            // label31
            // 
            label31.AutoSize = true;
            label31.Location = new Point(387, 174);
            label31.Margin = new Padding(5, 0, 5, 0);
            label31.Name = "label31";
            label31.Size = new Size(34, 20);
            label31.TabIndex = 131;
            label31.Text = "UV:";
            // 
            // checkBox_GetIpInfo
            // 
            checkBox_GetIpInfo.AutoSize = true;
            checkBox_GetIpInfo.Location = new Point(555, 250);
            checkBox_GetIpInfo.Margin = new Padding(5);
            checkBox_GetIpInfo.Name = "checkBox_GetIpInfo";
            checkBox_GetIpInfo.Size = new Size(104, 24);
            checkBox_GetIpInfo.TabIndex = 130;
            checkBox_GetIpInfo.Text = "获取IP详情";
            checkBox_GetIpInfo.UseVisualStyleBackColor = true;
            // 
            // linkLabel2
            // 
            linkLabel2.AutoSize = true;
            linkLabel2.Location = new Point(659, 328);
            linkLabel2.Margin = new Padding(4, 0, 4, 0);
            linkLabel2.Name = "linkLabel2";
            linkLabel2.Size = new Size(69, 20);
            linkLabel2.TabIndex = 127;
            linkLabel2.TabStop = true;
            linkLabel2.Text = "应用目录";
            linkLabel2.LinkClicked += linkLabel2_LinkClicked;
            // 
            // checkBox_IsRealIp
            // 
            checkBox_IsRealIp.AutoSize = true;
            checkBox_IsRealIp.Location = new Point(659, 221);
            checkBox_IsRealIp.Margin = new Padding(5);
            checkBox_IsRealIp.Name = "checkBox_IsRealIp";
            checkBox_IsRealIp.Size = new Size(74, 24);
            checkBox_IsRealIp.TabIndex = 35;
            checkBox_IsRealIp.Text = "真实IP";
            checkBox_IsRealIp.UseVisualStyleBackColor = true;
            // 
            // label36
            // 
            label36.AutoSize = true;
            label36.Location = new Point(197, 237);
            label36.Margin = new Padding(5, 0, 5, 0);
            label36.Name = "label36";
            label36.Size = new Size(24, 20);
            label36.TabIndex = 113;
            label36.Text = "秒";
            // 
            // textBox_PageloadedDelay
            // 
            textBox_PageloadedDelay.Location = new Point(117, 233);
            textBox_PageloadedDelay.Margin = new Padding(5, 4, 5, 4);
            textBox_PageloadedDelay.Name = "textBox_PageloadedDelay";
            textBox_PageloadedDelay.Size = new Size(75, 27);
            textBox_PageloadedDelay.TabIndex = 112;
            // 
            // label35
            // 
            label35.AutoSize = true;
            label35.Location = new Point(9, 237);
            label35.Margin = new Padding(5, 0, 5, 0);
            label35.Name = "label35";
            label35.Size = new Size(103, 20);
            label35.TabIndex = 111;
            label35.Text = "页面加载延时:";
            // 
            // label28
            // 
            label28.AutoSize = true;
            label28.Location = new Point(197, 269);
            label28.Margin = new Padding(5, 0, 5, 0);
            label28.Name = "label28";
            label28.Size = new Size(24, 20);
            label28.TabIndex = 101;
            label28.Text = "秒";
            // 
            // label29
            // 
            label29.AutoSize = true;
            label29.Location = new Point(9, 269);
            label29.Margin = new Padding(5, 0, 5, 0);
            label29.Name = "label29";
            label29.Size = new Size(103, 20);
            label29.TabIndex = 99;
            label29.Text = "页面加载超时:";
            // 
            // numericUpDown_PageLoadingTimeout
            // 
            numericUpDown_PageLoadingTimeout.Location = new Point(117, 265);
            numericUpDown_PageLoadingTimeout.Margin = new Padding(5, 4, 5, 4);
            numericUpDown_PageLoadingTimeout.Name = "numericUpDown_PageLoadingTimeout";
            numericUpDown_PageLoadingTimeout.Size = new Size(75, 27);
            numericUpDown_PageLoadingTimeout.TabIndex = 100;
            numericUpDown_PageLoadingTimeout.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // groupBox9
            // 
            groupBox9.Controls.Add(label12);
            groupBox9.Controls.Add(label11);
            groupBox9.Controls.Add(label10);
            groupBox9.Controls.Add(label5);
            groupBox9.Controls.Add(label9);
            groupBox9.Controls.Add(label6);
            groupBox9.Controls.Add(label7);
            groupBox9.Controls.Add(label8);
            groupBox9.Location = new Point(832, 14);
            groupBox9.Margin = new Padding(3, 2, 3, 2);
            groupBox9.Name = "groupBox9";
            groupBox9.Padding = new Padding(3, 2, 3, 2);
            groupBox9.Size = new Size(236, 278);
            groupBox9.TabIndex = 98;
            groupBox9.TabStop = false;
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Location = new Point(6, 248);
            label12.Margin = new Padding(5, 0, 5, 0);
            label12.Name = "label12";
            label12.Size = new Size(113, 20);
            label12.TabIndex = 88;
            label12.Text = "运行时间:00:00";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new Point(6, 215);
            label11.Margin = new Padding(5, 0, 5, 0);
            label11.Name = "label11";
            label11.Size = new Size(82, 20);
            label11.TabIndex = 87;
            label11.Text = "完成数量:0";
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(6, 182);
            label10.Margin = new Padding(5, 0, 5, 0);
            label10.Name = "label10";
            label10.Size = new Size(82, 20);
            label10.TabIndex = 86;
            label10.Text = "失败数量:0";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(120F, 120F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1091, 787);
            Controls.Add(groupBox33);
            Controls.Add(statusStrip1);
            Controls.Add(tabControl1);
            Margin = new Padding(5, 4, 5, 4);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "广告测试(百度)-";
            Load += MainForm_Load;
            ((System.ComponentModel.ISupportInitialize)numericUpDown_MainResetTimeout).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_Multiple).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_FetchTaskInterval).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_MaximumConcurrency).EndInit();
            groupBox6.ResumeLayout(false);
            groupBox6.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_IpTtl).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_PageLoadingTimeout).EndInit();
            groupBox9.ResumeLayout(false);
            groupBox9.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.TextBox textBox_ProxyIpUrl;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox33;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBox_TaskName;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.NumericUpDown numericUpDown_MaximumConcurrency;
        private System.Windows.Forms.NumericUpDown numericUpDown_FetchTaskInterval;
        private System.Windows.Forms.TextBox textBox_TaskApiUrl;
        private System.Windows.Forms.Label label100;
        private System.Windows.Forms.Button buttonClear;
        private System.Windows.Forms.Label label110;
        private System.Windows.Forms.NumericUpDown numericUpDown_Multiple;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.RadioButton radioButton_UseSystemDev;
        private System.Windows.Forms.CheckBox checkBox_IsProxyMode;
        private System.Windows.Forms.CheckBox checkBox_IsHiddenMode;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.NumericUpDown numericUpDown_MainResetTimeout;
        private System.Windows.Forms.Label label26;
        private RadioButton radioButton_UseLocalDev;
        private TextBox textBox_DevApiUrl;
        private Label label14;
        private CheckBox checkBox_IsDetailLog;
        private LinkLabel linkLabel1;
        private Label label6;
        private Label label8;
        private Label label7;
        private Label label9;
        private Label label5;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel toolStripStatusLabel1;
        private ToolStripStatusLabel toolStripStatusLabel4;
        private ToolStripStatusLabel toolStripStatusLabel5;
        private ToolStripStatusLabel toolStripStatusLabel6;
        private NumericUpDown numericUpDown_PageLoadingTimeout;
        private ComboBox comboBox_QTPName;
        private Label label25;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private GroupBox groupBox9;
        private Label label11;
        private Label label10;
        private Label label12;
        private Label label28;
        private Label label29;
        private TextBox textBox_PageloadedDelay;
        private Label label35;
        private Label label36;
        private LinkLabel linkLabel2;
        private CheckBox checkBox_IsRealIp;
        private CheckBox checkBox_GetIpInfo;
        private Label label31;
        private Label label33;
        private TextBox textBox_PVOverride;
        private TextBox textBox_UVOverride;
        private Label label38;
        private Label label39;
        private NumericUpDown numericUpDown_IpTtl;
        private CheckBox checkBox_UVsTriggerOne;
        private Label label30;
        private ComboBox comboBox_KernelVersion;
        private CheckBox checkBox_PVsTriggerOne;
        private Label label43;
        private ComboBox comboBox_VersionList;
        private Button btnUpdate;
        private ToolStripProgressBar toolStripProgressBarDownload;
        private Button btnStartStop;
        private ToolStripStatusLabel lblStatus;
        private CheckBox checkBox_IsTest;
        private Button button6;
        private Button button2;
        private Button button3;
        private CheckBox checkBox_AutoUpdate;
        private CheckBox checkBox_BlockImage;
        private CheckBox checkBox_BlockMedia;
    }
}

