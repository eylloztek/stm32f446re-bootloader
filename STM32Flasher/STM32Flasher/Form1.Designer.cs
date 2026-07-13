namespace STM32Flasher
{
    partial class STM32Flasher
    {
        /// <summary>
        ///Gerekli tasarımcı değişkeni.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///Kullanılan tüm kaynakları temizleyin.
        /// </summary>
        ///<param name="disposing">yönetilen kaynaklar dispose edilmeliyse doğru; aksi halde yanlış.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer üretilen kod

        /// <summary>
        /// Tasarımcı desteği için gerekli metot - bu metodun 
        ///içeriğini kod düzenleyici ile değiştirmeyin.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.grpBoxConnection = new System.Windows.Forms.GroupBox();
            this.prgBarStatus = new System.Windows.Forms.ProgressBar();
            this.lblConnectionStatus = new System.Windows.Forms.Label();
            this.btnDisconnect = new System.Windows.Forms.Button();
            this.btnConnect = new System.Windows.Forms.Button();
            this.cBoxParity = new System.Windows.Forms.ComboBox();
            this.cBoxStopBits = new System.Windows.Forms.ComboBox();
            this.lblParity = new System.Windows.Forms.Label();
            this.lblStopBits = new System.Windows.Forms.Label();
            this.cBoxBaudrate = new System.Windows.Forms.ComboBox();
            this.lblBaudrate = new System.Windows.Forms.Label();
            this.cBoxComPort = new System.Windows.Forms.ComboBox();
            this.lblComPort = new System.Windows.Forms.Label();
            this.serialPort1 = new System.IO.Ports.SerialPort(this.components);
            this.grpBoxCommands = new System.Windows.Forms.GroupBox();
            this.cBoxReadoutPro = new System.Windows.Forms.ComboBox();
            this.btnReadoutPro = new System.Windows.Forms.Button();
            this.chBoxWRP7 = new System.Windows.Forms.CheckBox();
            this.chBoxWRP6 = new System.Windows.Forms.CheckBox();
            this.chBoxWRP5 = new System.Windows.Forms.CheckBox();
            this.chBoxWRP4 = new System.Windows.Forms.CheckBox();
            this.chBoxWRP3 = new System.Windows.Forms.CheckBox();
            this.chBoxWRP2 = new System.Windows.Forms.CheckBox();
            this.chBoxWRP1 = new System.Windows.Forms.CheckBox();
            this.chBoxWRP0 = new System.Windows.Forms.CheckBox();
            this.btnWriteProtect = new System.Windows.Forms.Button();
            this.chBoxMassErase = new System.Windows.Forms.CheckBox();
            this.chBoxSelect7 = new System.Windows.Forms.CheckBox();
            this.chBoxSelect6 = new System.Windows.Forms.CheckBox();
            this.chBoxSelect5 = new System.Windows.Forms.CheckBox();
            this.chBoxSelect4 = new System.Windows.Forms.CheckBox();
            this.chBoxSelect3 = new System.Windows.Forms.CheckBox();
            this.chBoxSelect2 = new System.Windows.Forms.CheckBox();
            this.chBoxSelect1 = new System.Windows.Forms.CheckBox();
            this.chBoxSelect0 = new System.Windows.Forms.CheckBox();
            this.btnErase = new System.Windows.Forms.Button();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.txtBrowseFile = new System.Windows.Forms.TextBox();
            this.txtWriteMem = new System.Windows.Forms.TextBox();
            this.btnWriteMem = new System.Windows.Forms.Button();
            this.txtGoToAddress = new System.Windows.Forms.TextBox();
            this.btnGoToAddress = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.txtLength = new System.Windows.Forms.TextBox();
            this.txtAddress = new System.Windows.Forms.TextBox();
            this.lblLength = new System.Windows.Forms.Label();
            this.lblAddress = new System.Windows.Forms.Label();
            this.btnReadMemory = new System.Windows.Forms.Button();
            this.btnGetID = new System.Windows.Forms.Button();
            this.btnClear = new System.Windows.Forms.Button();
            this.txtReceiveMessage = new System.Windows.Forms.TextBox();
            this.btnGetVer = new System.Windows.Forms.Button();
            this.btnGetHelp = new System.Windows.Forms.Button();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.grpBoxConnection.SuspendLayout();
            this.grpBoxCommands.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpBoxConnection
            // 
            this.grpBoxConnection.Controls.Add(this.prgBarStatus);
            this.grpBoxConnection.Controls.Add(this.lblConnectionStatus);
            this.grpBoxConnection.Controls.Add(this.btnDisconnect);
            this.grpBoxConnection.Controls.Add(this.btnConnect);
            this.grpBoxConnection.Controls.Add(this.cBoxParity);
            this.grpBoxConnection.Controls.Add(this.cBoxStopBits);
            this.grpBoxConnection.Controls.Add(this.lblParity);
            this.grpBoxConnection.Controls.Add(this.lblStopBits);
            this.grpBoxConnection.Controls.Add(this.cBoxBaudrate);
            this.grpBoxConnection.Controls.Add(this.lblBaudrate);
            this.grpBoxConnection.Controls.Add(this.cBoxComPort);
            this.grpBoxConnection.Controls.Add(this.lblComPort);
            this.grpBoxConnection.Location = new System.Drawing.Point(13, 13);
            this.grpBoxConnection.Name = "grpBoxConnection";
            this.grpBoxConnection.Size = new System.Drawing.Size(383, 328);
            this.grpBoxConnection.TabIndex = 0;
            this.grpBoxConnection.TabStop = false;
            this.grpBoxConnection.Text = "Connection";
            // 
            // prgBarStatus
            // 
            this.prgBarStatus.Location = new System.Drawing.Point(158, 239);
            this.prgBarStatus.Name = "prgBarStatus";
            this.prgBarStatus.Size = new System.Drawing.Size(160, 23);
            this.prgBarStatus.TabIndex = 11;
            // 
            // lblConnectionStatus
            // 
            this.lblConnectionStatus.AutoSize = true;
            this.lblConnectionStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.lblConnectionStatus.Location = new System.Drawing.Point(173, 211);
            this.lblConnectionStatus.Name = "lblConnectionStatus";
            this.lblConnectionStatus.Size = new System.Drawing.Size(129, 25);
            this.lblConnectionStatus.TabIndex = 10;
            this.lblConnectionStatus.Text = "Unsuccessful";
            // 
            // btnDisconnect
            // 
            this.btnDisconnect.Location = new System.Drawing.Point(12, 264);
            this.btnDisconnect.Name = "btnDisconnect";
            this.btnDisconnect.Size = new System.Drawing.Size(109, 37);
            this.btnDisconnect.TabIndex = 9;
            this.btnDisconnect.Text = "Disconnect";
            this.btnDisconnect.UseVisualStyleBackColor = true;
            this.btnDisconnect.Click += new System.EventHandler(this.btnDisconnect_Click);
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(12, 211);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(109, 37);
            this.btnConnect.TabIndex = 8;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // cBoxParity
            // 
            this.cBoxParity.FormattingEnabled = true;
            this.cBoxParity.Items.AddRange(new object[] {
            "None",
            "Even",
            "Odd"});
            this.cBoxParity.Location = new System.Drawing.Point(103, 152);
            this.cBoxParity.Name = "cBoxParity";
            this.cBoxParity.Size = new System.Drawing.Size(142, 28);
            this.cBoxParity.TabIndex = 7;
            this.cBoxParity.Text = "None";
            // 
            // cBoxStopBits
            // 
            this.cBoxStopBits.FormattingEnabled = true;
            this.cBoxStopBits.Items.AddRange(new object[] {
            "1",
            "1.5",
            "2"});
            this.cBoxStopBits.Location = new System.Drawing.Point(103, 110);
            this.cBoxStopBits.Name = "cBoxStopBits";
            this.cBoxStopBits.Size = new System.Drawing.Size(142, 28);
            this.cBoxStopBits.TabIndex = 6;
            this.cBoxStopBits.Text = "1";
            // 
            // lblParity
            // 
            this.lblParity.AutoSize = true;
            this.lblParity.Location = new System.Drawing.Point(8, 155);
            this.lblParity.Name = "lblParity";
            this.lblParity.Size = new System.Drawing.Size(48, 20);
            this.lblParity.TabIndex = 5;
            this.lblParity.Text = "Parity";
            // 
            // lblStopBits
            // 
            this.lblStopBits.AutoSize = true;
            this.lblStopBits.Location = new System.Drawing.Point(8, 113);
            this.lblStopBits.Name = "lblStopBits";
            this.lblStopBits.Size = new System.Drawing.Size(74, 20);
            this.lblStopBits.TabIndex = 4;
            this.lblStopBits.Text = "Stop Bits";
            // 
            // cBoxBaudrate
            // 
            this.cBoxBaudrate.FormattingEnabled = true;
            this.cBoxBaudrate.Items.AddRange(new object[] {
            "9600",
            "19200",
            "28800",
            "38400",
            "57600",
            "76800",
            "115200",
            "230400",
            "460800"});
            this.cBoxBaudrate.Location = new System.Drawing.Point(103, 68);
            this.cBoxBaudrate.Name = "cBoxBaudrate";
            this.cBoxBaudrate.Size = new System.Drawing.Size(142, 28);
            this.cBoxBaudrate.TabIndex = 3;
            this.cBoxBaudrate.Text = "115200";
            // 
            // lblBaudrate
            // 
            this.lblBaudrate.AutoSize = true;
            this.lblBaudrate.Location = new System.Drawing.Point(8, 71);
            this.lblBaudrate.Name = "lblBaudrate";
            this.lblBaudrate.Size = new System.Drawing.Size(75, 20);
            this.lblBaudrate.TabIndex = 2;
            this.lblBaudrate.Text = "Baudrate";
            // 
            // cBoxComPort
            // 
            this.cBoxComPort.FormattingEnabled = true;
            this.cBoxComPort.Location = new System.Drawing.Point(103, 26);
            this.cBoxComPort.Name = "cBoxComPort";
            this.cBoxComPort.Size = new System.Drawing.Size(142, 28);
            this.cBoxComPort.TabIndex = 1;
            this.cBoxComPort.Text = "COM5";
            // 
            // lblComPort
            // 
            this.lblComPort.AutoSize = true;
            this.lblComPort.Location = new System.Drawing.Point(8, 29);
            this.lblComPort.Name = "lblComPort";
            this.lblComPort.Size = new System.Drawing.Size(45, 20);
            this.lblComPort.TabIndex = 0;
            this.lblComPort.Text = "COM";
            // 
            // serialPort1
            // 
            this.serialPort1.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(this.serialPort1_DataReceived);
            // 
            // grpBoxCommands
            // 
            this.grpBoxCommands.Controls.Add(this.cBoxReadoutPro);
            this.grpBoxCommands.Controls.Add(this.btnReadoutPro);
            this.grpBoxCommands.Controls.Add(this.chBoxWRP7);
            this.grpBoxCommands.Controls.Add(this.chBoxWRP6);
            this.grpBoxCommands.Controls.Add(this.chBoxWRP5);
            this.grpBoxCommands.Controls.Add(this.chBoxWRP4);
            this.grpBoxCommands.Controls.Add(this.chBoxWRP3);
            this.grpBoxCommands.Controls.Add(this.chBoxWRP2);
            this.grpBoxCommands.Controls.Add(this.chBoxWRP1);
            this.grpBoxCommands.Controls.Add(this.chBoxWRP0);
            this.grpBoxCommands.Controls.Add(this.btnWriteProtect);
            this.grpBoxCommands.Controls.Add(this.chBoxMassErase);
            this.grpBoxCommands.Controls.Add(this.chBoxSelect7);
            this.grpBoxCommands.Controls.Add(this.chBoxSelect6);
            this.grpBoxCommands.Controls.Add(this.chBoxSelect5);
            this.grpBoxCommands.Controls.Add(this.chBoxSelect4);
            this.grpBoxCommands.Controls.Add(this.chBoxSelect3);
            this.grpBoxCommands.Controls.Add(this.chBoxSelect2);
            this.grpBoxCommands.Controls.Add(this.chBoxSelect1);
            this.grpBoxCommands.Controls.Add(this.chBoxSelect0);
            this.grpBoxCommands.Controls.Add(this.btnErase);
            this.grpBoxCommands.Controls.Add(this.btnBrowse);
            this.grpBoxCommands.Controls.Add(this.txtBrowseFile);
            this.grpBoxCommands.Controls.Add(this.txtWriteMem);
            this.grpBoxCommands.Controls.Add(this.btnWriteMem);
            this.grpBoxCommands.Controls.Add(this.txtGoToAddress);
            this.grpBoxCommands.Controls.Add(this.btnGoToAddress);
            this.grpBoxCommands.Controls.Add(this.btnSave);
            this.grpBoxCommands.Controls.Add(this.txtLength);
            this.grpBoxCommands.Controls.Add(this.txtAddress);
            this.grpBoxCommands.Controls.Add(this.lblLength);
            this.grpBoxCommands.Controls.Add(this.lblAddress);
            this.grpBoxCommands.Controls.Add(this.btnReadMemory);
            this.grpBoxCommands.Controls.Add(this.btnGetID);
            this.grpBoxCommands.Controls.Add(this.btnClear);
            this.grpBoxCommands.Controls.Add(this.txtReceiveMessage);
            this.grpBoxCommands.Controls.Add(this.btnGetVer);
            this.grpBoxCommands.Controls.Add(this.btnGetHelp);
            this.grpBoxCommands.Location = new System.Drawing.Point(402, 13);
            this.grpBoxCommands.Name = "grpBoxCommands";
            this.grpBoxCommands.Size = new System.Drawing.Size(1016, 799);
            this.grpBoxCommands.TabIndex = 1;
            this.grpBoxCommands.TabStop = false;
            this.grpBoxCommands.Text = "Commands";
            // 
            // cBoxReadoutPro
            // 
            this.cBoxReadoutPro.FormattingEnabled = true;
            this.cBoxReadoutPro.Items.AddRange(new object[] {
            "Level 0 (No Protection)",
            "Level 1 (Read Protection)",
            "Level 2 (Chip Lock - Irreversible)"});
            this.cBoxReadoutPro.Location = new System.Drawing.Point(169, 629);
            this.cBoxReadoutPro.Name = "cBoxReadoutPro";
            this.cBoxReadoutPro.Size = new System.Drawing.Size(273, 28);
            this.cBoxReadoutPro.TabIndex = 46;
            this.cBoxReadoutPro.Text = "Level 0 (No Protection)";
            // 
            // btnReadoutPro
            // 
            this.btnReadoutPro.Location = new System.Drawing.Point(6, 615);
            this.btnReadoutPro.Name = "btnReadoutPro";
            this.btnReadoutPro.Size = new System.Drawing.Size(143, 54);
            this.btnReadoutPro.TabIndex = 45;
            this.btnReadoutPro.Text = "Readout Protection";
            this.btnReadoutPro.UseVisualStyleBackColor = true;
            this.btnReadoutPro.Click += new System.EventHandler(this.btnReadoutPro_Click);
            // 
            // chBoxWRP7
            // 
            this.chBoxWRP7.AutoSize = true;
            this.chBoxWRP7.Location = new System.Drawing.Point(519, 570);
            this.chBoxWRP7.Name = "chBoxWRP7";
            this.chBoxWRP7.Size = new System.Drawing.Size(81, 24);
            this.chBoxWRP7.TabIndex = 44;
            this.chBoxWRP7.Text = "WRP7";
            this.chBoxWRP7.UseVisualStyleBackColor = true;
            // 
            // chBoxWRP6
            // 
            this.chBoxWRP6.AutoSize = true;
            this.chBoxWRP6.Location = new System.Drawing.Point(393, 570);
            this.chBoxWRP6.Name = "chBoxWRP6";
            this.chBoxWRP6.Size = new System.Drawing.Size(81, 24);
            this.chBoxWRP6.TabIndex = 43;
            this.chBoxWRP6.Text = "WRP6";
            this.chBoxWRP6.UseVisualStyleBackColor = true;
            // 
            // chBoxWRP5
            // 
            this.chBoxWRP5.AutoSize = true;
            this.chBoxWRP5.Location = new System.Drawing.Point(283, 570);
            this.chBoxWRP5.Name = "chBoxWRP5";
            this.chBoxWRP5.Size = new System.Drawing.Size(81, 24);
            this.chBoxWRP5.TabIndex = 42;
            this.chBoxWRP5.Text = "WRP5";
            this.chBoxWRP5.UseVisualStyleBackColor = true;
            // 
            // chBoxWRP4
            // 
            this.chBoxWRP4.AutoSize = true;
            this.chBoxWRP4.Location = new System.Drawing.Point(169, 570);
            this.chBoxWRP4.Name = "chBoxWRP4";
            this.chBoxWRP4.Size = new System.Drawing.Size(81, 24);
            this.chBoxWRP4.TabIndex = 41;
            this.chBoxWRP4.Text = "WRP4";
            this.chBoxWRP4.UseVisualStyleBackColor = true;
            // 
            // chBoxWRP3
            // 
            this.chBoxWRP3.AutoSize = true;
            this.chBoxWRP3.Location = new System.Drawing.Point(519, 525);
            this.chBoxWRP3.Name = "chBoxWRP3";
            this.chBoxWRP3.Size = new System.Drawing.Size(81, 24);
            this.chBoxWRP3.TabIndex = 40;
            this.chBoxWRP3.Text = "WRP3";
            this.chBoxWRP3.UseVisualStyleBackColor = true;
            // 
            // chBoxWRP2
            // 
            this.chBoxWRP2.AutoSize = true;
            this.chBoxWRP2.Location = new System.Drawing.Point(393, 525);
            this.chBoxWRP2.Name = "chBoxWRP2";
            this.chBoxWRP2.Size = new System.Drawing.Size(81, 24);
            this.chBoxWRP2.TabIndex = 39;
            this.chBoxWRP2.Text = "WRP2";
            this.chBoxWRP2.UseVisualStyleBackColor = true;
            // 
            // chBoxWRP1
            // 
            this.chBoxWRP1.AutoSize = true;
            this.chBoxWRP1.Location = new System.Drawing.Point(283, 525);
            this.chBoxWRP1.Name = "chBoxWRP1";
            this.chBoxWRP1.Size = new System.Drawing.Size(81, 24);
            this.chBoxWRP1.TabIndex = 38;
            this.chBoxWRP1.Tag = "";
            this.chBoxWRP1.Text = "WRP1";
            this.chBoxWRP1.UseVisualStyleBackColor = true;
            // 
            // chBoxWRP0
            // 
            this.chBoxWRP0.AutoSize = true;
            this.chBoxWRP0.Location = new System.Drawing.Point(169, 525);
            this.chBoxWRP0.Name = "chBoxWRP0";
            this.chBoxWRP0.Size = new System.Drawing.Size(81, 24);
            this.chBoxWRP0.TabIndex = 37;
            this.chBoxWRP0.Text = "WRP0";
            this.chBoxWRP0.UseVisualStyleBackColor = true;
            // 
            // btnWriteProtect
            // 
            this.btnWriteProtect.Location = new System.Drawing.Point(6, 525);
            this.btnWriteProtect.Name = "btnWriteProtect";
            this.btnWriteProtect.Size = new System.Drawing.Size(143, 37);
            this.btnWriteProtect.TabIndex = 36;
            this.btnWriteProtect.Text = "Write Protection";
            this.btnWriteProtect.UseVisualStyleBackColor = true;
            this.btnWriteProtect.Click += new System.EventHandler(this.btnWriteProtect_Click);
            // 
            // chBoxMassErase
            // 
            this.chBoxMassErase.AutoSize = true;
            this.chBoxMassErase.Location = new System.Drawing.Point(519, 442);
            this.chBoxMassErase.Name = "chBoxMassErase";
            this.chBoxMassErase.Size = new System.Drawing.Size(119, 24);
            this.chBoxMassErase.TabIndex = 35;
            this.chBoxMassErase.Text = "Mass Erase";
            this.chBoxMassErase.UseVisualStyleBackColor = true;
            // 
            // chBoxSelect7
            // 
            this.chBoxSelect7.AutoSize = true;
            this.chBoxSelect7.Location = new System.Drawing.Point(393, 442);
            this.chBoxSelect7.Name = "chBoxSelect7";
            this.chBoxSelect7.Size = new System.Drawing.Size(95, 24);
            this.chBoxSelect7.TabIndex = 34;
            this.chBoxSelect7.Text = "Sector 7";
            this.chBoxSelect7.UseVisualStyleBackColor = true;
            // 
            // chBoxSelect6
            // 
            this.chBoxSelect6.AutoSize = true;
            this.chBoxSelect6.Location = new System.Drawing.Point(283, 442);
            this.chBoxSelect6.Name = "chBoxSelect6";
            this.chBoxSelect6.Size = new System.Drawing.Size(95, 24);
            this.chBoxSelect6.TabIndex = 33;
            this.chBoxSelect6.Text = "Sector 6";
            this.chBoxSelect6.UseVisualStyleBackColor = true;
            // 
            // chBoxSelect5
            // 
            this.chBoxSelect5.AutoSize = true;
            this.chBoxSelect5.Location = new System.Drawing.Point(169, 442);
            this.chBoxSelect5.Name = "chBoxSelect5";
            this.chBoxSelect5.Size = new System.Drawing.Size(95, 24);
            this.chBoxSelect5.TabIndex = 32;
            this.chBoxSelect5.Text = "Sector 5";
            this.chBoxSelect5.UseVisualStyleBackColor = true;
            // 
            // chBoxSelect4
            // 
            this.chBoxSelect4.AutoSize = true;
            this.chBoxSelect4.Location = new System.Drawing.Point(638, 397);
            this.chBoxSelect4.Name = "chBoxSelect4";
            this.chBoxSelect4.Size = new System.Drawing.Size(95, 24);
            this.chBoxSelect4.TabIndex = 31;
            this.chBoxSelect4.Text = "Sector 4";
            this.chBoxSelect4.UseVisualStyleBackColor = true;
            // 
            // chBoxSelect3
            // 
            this.chBoxSelect3.AutoSize = true;
            this.chBoxSelect3.Location = new System.Drawing.Point(519, 397);
            this.chBoxSelect3.Name = "chBoxSelect3";
            this.chBoxSelect3.Size = new System.Drawing.Size(95, 24);
            this.chBoxSelect3.TabIndex = 30;
            this.chBoxSelect3.Text = "Sector 3";
            this.chBoxSelect3.UseVisualStyleBackColor = true;
            // 
            // chBoxSelect2
            // 
            this.chBoxSelect2.AutoSize = true;
            this.chBoxSelect2.Location = new System.Drawing.Point(393, 397);
            this.chBoxSelect2.Name = "chBoxSelect2";
            this.chBoxSelect2.Size = new System.Drawing.Size(95, 24);
            this.chBoxSelect2.TabIndex = 29;
            this.chBoxSelect2.Text = "Sector 2";
            this.chBoxSelect2.UseVisualStyleBackColor = true;
            // 
            // chBoxSelect1
            // 
            this.chBoxSelect1.AutoSize = true;
            this.chBoxSelect1.Location = new System.Drawing.Point(283, 397);
            this.chBoxSelect1.Name = "chBoxSelect1";
            this.chBoxSelect1.Size = new System.Drawing.Size(95, 24);
            this.chBoxSelect1.TabIndex = 28;
            this.chBoxSelect1.Tag = "";
            this.chBoxSelect1.Text = "Sector 1";
            this.chBoxSelect1.UseVisualStyleBackColor = true;
            // 
            // chBoxSelect0
            // 
            this.chBoxSelect0.AutoSize = true;
            this.chBoxSelect0.Location = new System.Drawing.Point(169, 397);
            this.chBoxSelect0.Name = "chBoxSelect0";
            this.chBoxSelect0.Size = new System.Drawing.Size(95, 24);
            this.chBoxSelect0.TabIndex = 27;
            this.chBoxSelect0.Text = "Sector 0";
            this.chBoxSelect0.UseVisualStyleBackColor = true;
            // 
            // btnErase
            // 
            this.btnErase.Location = new System.Drawing.Point(6, 385);
            this.btnErase.Name = "btnErase";
            this.btnErase.Size = new System.Drawing.Size(128, 37);
            this.btnErase.TabIndex = 26;
            this.btnErase.Text = "Erase";
            this.btnErase.UseVisualStyleBackColor = true;
            this.btnErase.Click += new System.EventHandler(this.btnErase_Click);
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(576, 325);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(128, 37);
            this.btnBrowse.TabIndex = 25;
            this.btnBrowse.Text = "Browse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // txtBrowseFile
            // 
            this.txtBrowseFile.Location = new System.Drawing.Point(340, 330);
            this.txtBrowseFile.Name = "txtBrowseFile";
            this.txtBrowseFile.Size = new System.Drawing.Size(210, 26);
            this.txtBrowseFile.TabIndex = 24;
            // 
            // txtWriteMem
            // 
            this.txtWriteMem.Location = new System.Drawing.Point(169, 330);
            this.txtWriteMem.Name = "txtWriteMem";
            this.txtWriteMem.Size = new System.Drawing.Size(120, 26);
            this.txtWriteMem.TabIndex = 23;
            // 
            // btnWriteMem
            // 
            this.btnWriteMem.Location = new System.Drawing.Point(6, 325);
            this.btnWriteMem.Name = "btnWriteMem";
            this.btnWriteMem.Size = new System.Drawing.Size(128, 37);
            this.btnWriteMem.TabIndex = 22;
            this.btnWriteMem.Text = "Write Memory";
            this.btnWriteMem.UseVisualStyleBackColor = true;
            this.btnWriteMem.Click += new System.EventHandler(this.btnWriteMem_Click);
            // 
            // txtGoToAddress
            // 
            this.txtGoToAddress.Location = new System.Drawing.Point(169, 269);
            this.txtGoToAddress.Name = "txtGoToAddress";
            this.txtGoToAddress.Size = new System.Drawing.Size(120, 26);
            this.txtGoToAddress.TabIndex = 21;
            // 
            // btnGoToAddress
            // 
            this.btnGoToAddress.Location = new System.Drawing.Point(6, 264);
            this.btnGoToAddress.Name = "btnGoToAddress";
            this.btnGoToAddress.Size = new System.Drawing.Size(128, 37);
            this.btnGoToAddress.TabIndex = 20;
            this.btnGoToAddress.Text = "Go To Address";
            this.btnGoToAddress.UseVisualStyleBackColor = true;
            this.btnGoToAddress.Click += new System.EventHandler(this.btnGoToAddress_Click);
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(470, 204);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(128, 37);
            this.btnSave.TabIndex = 19;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // txtLength
            // 
            this.txtLength.Location = new System.Drawing.Point(340, 213);
            this.txtLength.Name = "txtLength";
            this.txtLength.Size = new System.Drawing.Size(102, 26);
            this.txtLength.TabIndex = 18;
            // 
            // txtAddress
            // 
            this.txtAddress.Location = new System.Drawing.Point(169, 213);
            this.txtAddress.Name = "txtAddress";
            this.txtAddress.Size = new System.Drawing.Size(120, 26);
            this.txtAddress.TabIndex = 17;
            // 
            // lblLength
            // 
            this.lblLength.AutoSize = true;
            this.lblLength.Location = new System.Drawing.Point(356, 190);
            this.lblLength.Name = "lblLength";
            this.lblLength.Size = new System.Drawing.Size(59, 20);
            this.lblLength.TabIndex = 16;
            this.lblLength.Text = "Length";
            // 
            // lblAddress
            // 
            this.lblAddress.AutoSize = true;
            this.lblAddress.Location = new System.Drawing.Point(194, 190);
            this.lblAddress.Name = "lblAddress";
            this.lblAddress.Size = new System.Drawing.Size(68, 20);
            this.lblAddress.TabIndex = 15;
            this.lblAddress.Text = "Address";
            // 
            // btnReadMemory
            // 
            this.btnReadMemory.Location = new System.Drawing.Point(6, 204);
            this.btnReadMemory.Name = "btnReadMemory";
            this.btnReadMemory.Size = new System.Drawing.Size(128, 37);
            this.btnReadMemory.TabIndex = 14;
            this.btnReadMemory.Text = "Read Memory";
            this.btnReadMemory.UseVisualStyleBackColor = true;
            this.btnReadMemory.Click += new System.EventHandler(this.btnReadMemory_Click);
            // 
            // btnGetID
            // 
            this.btnGetID.Location = new System.Drawing.Point(6, 115);
            this.btnGetID.Name = "btnGetID";
            this.btnGetID.Size = new System.Drawing.Size(128, 37);
            this.btnGetID.TabIndex = 13;
            this.btnGetID.Text = "Get ID";
            this.btnGetID.UseVisualStyleBackColor = true;
            this.btnGetID.Click += new System.EventHandler(this.btnGetID_Click);
            // 
            // btnClear
            // 
            this.btnClear.Location = new System.Drawing.Point(741, 202);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(109, 37);
            this.btnClear.TabIndex = 12;
            this.btnClear.Text = "Clear";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // txtReceiveMessage
            // 
            this.txtReceiveMessage.Location = new System.Drawing.Point(576, 25);
            this.txtReceiveMessage.Multiline = true;
            this.txtReceiveMessage.Name = "txtReceiveMessage";
            this.txtReceiveMessage.Size = new System.Drawing.Size(425, 167);
            this.txtReceiveMessage.TabIndex = 11;
            // 
            // btnGetVer
            // 
            this.btnGetVer.Location = new System.Drawing.Point(6, 72);
            this.btnGetVer.Name = "btnGetVer";
            this.btnGetVer.Size = new System.Drawing.Size(128, 37);
            this.btnGetVer.TabIndex = 10;
            this.btnGetVer.Text = "Get Version";
            this.btnGetVer.UseVisualStyleBackColor = true;
            this.btnGetVer.Click += new System.EventHandler(this.btnGetVer_Click);
            // 
            // btnGetHelp
            // 
            this.btnGetHelp.Location = new System.Drawing.Point(6, 29);
            this.btnGetHelp.Name = "btnGetHelp";
            this.btnGetHelp.Size = new System.Drawing.Size(128, 37);
            this.btnGetHelp.TabIndex = 9;
            this.btnGetHelp.Text = "Get Help";
            this.btnGetHelp.UseVisualStyleBackColor = true;
            this.btnGetHelp.Click += new System.EventHandler(this.btnGetHelp_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // STM32Flasher
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1430, 824);
            this.Controls.Add(this.grpBoxCommands);
            this.Controls.Add(this.grpBoxConnection);
            this.Name = "STM32Flasher";
            this.Text = "STM32Flasher";
            this.Load += new System.EventHandler(this.STM32Flasher_Load);
            this.grpBoxConnection.ResumeLayout(false);
            this.grpBoxConnection.PerformLayout();
            this.grpBoxCommands.ResumeLayout(false);
            this.grpBoxCommands.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox grpBoxConnection;
        private System.Windows.Forms.Label lblComPort;
        private System.Windows.Forms.ComboBox cBoxComPort;
        private System.Windows.Forms.ComboBox cBoxBaudrate;
        private System.Windows.Forms.Label lblBaudrate;
        private System.Windows.Forms.Label lblStopBits;
        private System.Windows.Forms.ComboBox cBoxParity;
        private System.Windows.Forms.ComboBox cBoxStopBits;
        private System.Windows.Forms.Label lblParity;
        private System.Windows.Forms.Label lblConnectionStatus;
        private System.Windows.Forms.Button btnDisconnect;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.ProgressBar prgBarStatus;
        private System.IO.Ports.SerialPort serialPort1;
        private System.Windows.Forms.GroupBox grpBoxCommands;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.TextBox txtReceiveMessage;
        private System.Windows.Forms.Button btnGetVer;
        private System.Windows.Forms.Button btnGetHelp;
        private System.Windows.Forms.Button btnGetID;
        private System.Windows.Forms.Label lblLength;
        private System.Windows.Forms.Label lblAddress;
        private System.Windows.Forms.Button btnReadMemory;
        private System.Windows.Forms.TextBox txtLength;
        private System.Windows.Forms.TextBox txtAddress;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.TextBox txtGoToAddress;
        private System.Windows.Forms.Button btnGoToAddress;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.TextBox txtBrowseFile;
        private System.Windows.Forms.TextBox txtWriteMem;
        private System.Windows.Forms.Button btnWriteMem;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.Button btnErase;
        private System.Windows.Forms.CheckBox chBoxSelect0;
        private System.Windows.Forms.CheckBox chBoxSelect3;
        private System.Windows.Forms.CheckBox chBoxSelect2;
        private System.Windows.Forms.CheckBox chBoxSelect1;
        private System.Windows.Forms.CheckBox chBoxSelect4;
        private System.Windows.Forms.CheckBox chBoxMassErase;
        private System.Windows.Forms.CheckBox chBoxSelect7;
        private System.Windows.Forms.CheckBox chBoxSelect6;
        private System.Windows.Forms.CheckBox chBoxSelect5;
        private System.Windows.Forms.CheckBox chBoxWRP7;
        private System.Windows.Forms.CheckBox chBoxWRP6;
        private System.Windows.Forms.CheckBox chBoxWRP5;
        private System.Windows.Forms.CheckBox chBoxWRP4;
        private System.Windows.Forms.CheckBox chBoxWRP3;
        private System.Windows.Forms.CheckBox chBoxWRP2;
        private System.Windows.Forms.CheckBox chBoxWRP1;
        private System.Windows.Forms.CheckBox chBoxWRP0;
        private System.Windows.Forms.Button btnWriteProtect;
        private System.Windows.Forms.ComboBox cBoxReadoutPro;
        private System.Windows.Forms.Button btnReadoutPro;
    }
}

