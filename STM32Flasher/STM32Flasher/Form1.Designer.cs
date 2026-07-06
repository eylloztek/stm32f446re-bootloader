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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnGetHelp = new System.Windows.Forms.Button();
            this.btnGetVer = new System.Windows.Forms.Button();
            this.txtReceiveMessage = new System.Windows.Forms.TextBox();
            this.btnClear = new System.Windows.Forms.Button();
            this.grpBoxConnection.SuspendLayout();
            this.groupBox1.SuspendLayout();
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
            "Even",
            "Odd",
            "None"});
            this.cBoxParity.Location = new System.Drawing.Point(103, 152);
            this.cBoxParity.Name = "cBoxParity";
            this.cBoxParity.Size = new System.Drawing.Size(142, 28);
            this.cBoxParity.TabIndex = 7;
            this.cBoxParity.Text = "Even";
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
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.btnClear);
            this.groupBox1.Controls.Add(this.txtReceiveMessage);
            this.groupBox1.Controls.Add(this.btnGetVer);
            this.groupBox1.Controls.Add(this.btnGetHelp);
            this.groupBox1.Location = new System.Drawing.Point(402, 13);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(959, 799);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Commands";
            // 
            // btnGetHelp
            // 
            this.btnGetHelp.Location = new System.Drawing.Point(6, 29);
            this.btnGetHelp.Name = "btnGetHelp";
            this.btnGetHelp.Size = new System.Drawing.Size(109, 37);
            this.btnGetHelp.TabIndex = 9;
            this.btnGetHelp.Text = "Get Help";
            this.btnGetHelp.UseVisualStyleBackColor = true;
            // 
            // btnGetVer
            // 
            this.btnGetVer.Location = new System.Drawing.Point(6, 72);
            this.btnGetVer.Name = "btnGetVer";
            this.btnGetVer.Size = new System.Drawing.Size(109, 37);
            this.btnGetVer.TabIndex = 10;
            this.btnGetVer.Text = "Get Version";
            this.btnGetVer.UseVisualStyleBackColor = true;
            this.btnGetVer.Click += new System.EventHandler(this.btnGetVer_Click);
            // 
            // txtReceiveMessage
            // 
            this.txtReceiveMessage.Location = new System.Drawing.Point(211, 29);
            this.txtReceiveMessage.Multiline = true;
            this.txtReceiveMessage.Name = "txtReceiveMessage";
            this.txtReceiveMessage.Size = new System.Drawing.Size(425, 167);
            this.txtReceiveMessage.TabIndex = 11;
            // 
            // btnClear
            // 
            this.btnClear.Location = new System.Drawing.Point(375, 207);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(109, 37);
            this.btnClear.TabIndex = 12;
            this.btnClear.Text = "Clear";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // STM32Flasher
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1373, 824);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.grpBoxConnection);
            this.Name = "STM32Flasher";
            this.Text = "STM32Flasher";
            this.Load += new System.EventHandler(this.STM32Flasher_Load);
            this.grpBoxConnection.ResumeLayout(false);
            this.grpBoxConnection.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
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
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.TextBox txtReceiveMessage;
        private System.Windows.Forms.Button btnGetVer;
        private System.Windows.Forms.Button btnGetHelp;
    }
}

