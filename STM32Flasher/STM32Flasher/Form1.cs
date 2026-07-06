using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace STM32Flasher
{
    public partial class STM32Flasher : Form
    {
        public STM32Flasher()
        {
            InitializeComponent();
        }

        private void STM32Flasher_Load(object sender, EventArgs e)
        {
            String[] ports = SerialPort.GetPortNames();
            cBoxComPort.Items.AddRange(ports);

            btnConnect.Enabled = true;
            btnDisconnect.Enabled = false;
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            serialPort1.PortName = cBoxComPort.Text;
            serialPort1.BaudRate = Int32.Parse(cBoxBaudrate.Text);
            serialPort1.DataBits = 8;
            serialPort1.Parity = (Parity)Enum.Parse(typeof(Parity), cBoxParity.Text);
            serialPort1.StopBits = (StopBits)Enum.Parse(typeof(StopBits), cBoxStopBits.Text);

            try
            {
                serialPort1.Open();
                btnConnect.Enabled = false;
                btnDisconnect.Enabled = true;
                cBoxComPort.Enabled = false;
                lblConnectionStatus.Text = "Successful";
                prgBarStatus.Value = 100;
            }
            catch (Exception ex){
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnConnect.Enabled=true;
                btnDisconnect.Enabled=false;
                lblConnectionStatus.Text = "Unsuccessful";
            }

        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
                btnConnect.Enabled = true;
                btnDisconnect.Enabled = false;
                cBoxComPort.Enabled = true;
                lblConnectionStatus.Text = "Unsuccessful";
                prgBarStatus.Value = 0;
            }
        }
    }
}
