using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace STM32Flasher
{
    public partial class STM32Flasher : Form
    {
        List<byte> receivedData = new List<byte>();
        public STM32Flasher()
        {
            InitializeComponent();
        }

        public enum BootloaderCommand : byte
        {
            GetHelp = 0x00,
            GetVersion = 0x01,
            GetID = 0x02,
            ReadMemory = 0x011,
            Go = 0x21,
            WriteMemory = 0x31,
            Erase = 0x43,
            WriteProtect = 0x63,
            ReadoutProtect = 0x82,
            GetChecksum = 0xA1,
            Reset = 0x89
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
                txtReceiveMessage.Text = string.Empty;
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
                txtReceiveMessage.Text = string.Empty;
            }
        }

        byte calculateCRC(byte[] data)
        {
            byte crc = 0x00;
            foreach (byte b in data)
            {
                crc ^= b;   
            }
            return crc;
        }

        private void SendBootLoaderCommand(byte cmd, byte[] data)
        {
            List<byte> packet = new List<byte>();
            packet.Add(0x7F); //bootloader header
            packet.Add((byte)(1 + data.Length)); //len = cmd + data
            packet.Add(cmd);
            packet.AddRange(data);

            byte[] crcInput = packet.Skip(1).ToArray();
            byte crc = calculateCRC(crcInput);
            packet.Add(crc); //CRC

            if (serialPort1.IsOpen)
            {
                serialPort1.Write(packet.ToArray(), 0, packet.Count);
                serialPort1.Write("\r");
                serialPort1.Write("\n");
            }

        
        }
        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int bytesToRead = serialPort1.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                serialPort1.Read(buffer, 0, bytesToRead);

                receivedData.AddRange(buffer);

                string hexOutput = BitConverter.ToString(buffer).Replace("-", " ");

                this.Invoke(new Action(() =>
                {
                    txtReceiveMessage.AppendText(hexOutput + Environment.NewLine);
                    txtReceiveMessage.SelectionStart = txtReceiveMessage.TextLength;
                    txtReceiveMessage.ScrollToCaret();
                }));

            } catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtReceiveMessage.Text = string.Empty;
        }
        private void btnGetVer_Click(object sender, EventArgs e)
        {
            byte cmd = (byte)BootloaderCommand.GetVersion;
            SendBootLoaderCommand(cmd, new byte[0]);
        }

        private void btnGetHelp_Click(object sender, EventArgs e)
        {
            byte cmd = (byte)BootloaderCommand.GetHelp;
            SendBootLoaderCommand(cmd, new byte[0]);
        }

        private void btnGetID_Click(object sender, EventArgs e)
        {
            byte cmd = (byte)BootloaderCommand.GetID;
            SendBootLoaderCommand(cmd, new byte[0]);
        }

        private void sendReadMemoryData(uint address, byte length)
        {
            byte[] data = new byte[7];
            //address -> msb to lsb
            data[0] = (byte)((address>>24) & 0xFF); //msb
            data[1] = (byte)((address >> 16) & 0xFF);
            data[2] = (byte)((address >> 8) & 0xFF);
            data[3] = (byte)(address & 0xFF);

            byte addressCheckSum = (byte)(data[0] ^ data[1] ^ data[2] ^ data[3]);
            data[4] = addressCheckSum;

            byte n = (byte)(length - 1);
            data[5] = n;
            data[6] = (byte)(n ^ 0xFF);   // or: (byte)~n

            byte cmd = (byte)BootloaderCommand.ReadMemory;
            SendBootLoaderCommand(cmd, data);
        }

        private void btnReadMemory_Click(object sender, EventArgs e)
        {

            if (txtAddress.Text == "" || txtLength.Text == "")
            {
                MessageBox.Show("Please enter the address and the length values.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string addressText = txtAddress.Text.Trim();

                if (addressText.StartsWith("0x"))
                {
                    addressText = addressText.Substring(2);
                }

                uint address = Convert.ToUInt32(addressText, 16);

                byte length = byte.Parse(txtLength.Text);

                if(length > 255)
                {
                    MessageBox.Show("256 bytes maximum", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                }
                sendReadMemoryData(address, length);

            } 
            catch (Exception ex) 
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); 
            }

        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (receivedData == null || receivedData.Count == 0)
            {
                MessageBox.Show("No data received to save.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Binary files (*.bin)|*.bin";
                saveFileDialog.Title = "Save Binary File";
                saveFileDialog.FileName = "output.bin";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        byte[] dataToSave = receivedData.Skip(1).ToArray();
                        File.WriteAllBytes(saveFileDialog.FileName, dataToSave);
                        MessageBox.Show("Data saved successfully.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        receivedData.Clear();
                        txtReceiveMessage.Text = "";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error while saving: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void sendGoAddressData(uint address)
        {
            byte[] data = new byte[5];
            // address-> msb to lsb
            data[0] = (byte)((address >> 24) & 0xFF);
            data[1] = (byte)((address >> 16) & 0xFF);
            data[2] = (byte)((address >> 8) & 0xFF);
            data[3] = (byte)(address & 0xFF);

            byte adddressCheckSum = (byte)(data[0] ^ data[1] ^ data[2] ^ data[3]);
            data[4] = adddressCheckSum; //address checksum

            byte cmd = (byte)BootloaderCommand.Go;
            SendBootLoaderCommand(cmd, data);
        }

        private void btnGoToAddress_Click(object sender, EventArgs e)
        {
            if (txtGoToAddress.Text == "")
            {
                MessageBox.Show("Please enter the address", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string addressText = txtGoToAddress.Text.Trim();

                if (addressText.StartsWith("0x"))
                {
                    addressText = addressText.Substring(2);
                }
                uint address = Convert.ToUInt32(addressText, 16);

                sendGoAddressData(address);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        string binFilePath;
        byte[] binData;
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Binary files (*.bin)|*.bin";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                txtBrowseFile.Text = openFileDialog.FileName;
                binFilePath = txtBrowseFile.Text;
                binData = File.ReadAllBytes(binFilePath);
            }
        }

        private void sendWriteMemoryData(uint address)
        {
            byte[] data = new byte[9];

            byte[] lengthBytes = new byte[4];
            int length = binData.Length;

            // length-> msb to lsb
            lengthBytes[0] = (byte)((length >> 24) & 0xFF);
            lengthBytes[1] = (byte)((length >> 16) & 0xFF);
            lengthBytes[2] = (byte)((length >> 8) & 0xFF);
            lengthBytes[3] = (byte)(length & 0xFF);


            // address-> msb to lsb
            data[0] = (byte)((address >> 24) & 0xFF);
            data[1] = (byte)((address >> 16) & 0xFF);
            data[2] = (byte)((address >> 8) & 0xFF);
            data[3] = (byte)(address & 0xFF);

            byte adddressCheckSum = (byte)(data[0] ^ data[1] ^ data[2] ^ data[3]);
            data[4] = adddressCheckSum; //address checksum

            data[5] = lengthBytes[0];
            data[6] = lengthBytes[1];
            data[7] = lengthBytes[2];
            data[8] = lengthBytes[3];

            byte cmd = (byte)BootloaderCommand.WriteMemory;
            SendBootLoaderCommand(cmd, data);

            sendDataBlocks(address);

        }

        private void sendDataBlocks(uint startAddress)
        {
            int offset = 0;
            //int blockSize = 256;

            while (offset < binData.Length)
            {

                int remaining = binData.Length - offset;
                int currentBlockSize = remaining > 256 ? 256 : remaining;

                byte N = (byte)(currentBlockSize - 1);
                byte[] packet = new byte[currentBlockSize + 2];
                packet[0] = N;

                Array.Copy(binData, offset, packet, 1, currentBlockSize);

                byte checkSum = N;
                for (int i = 1; i <= currentBlockSize; i++)
                {
                    checkSum ^= packet[i];
                }

                packet[packet.Length - 1] = checkSum;

                sendBytesToSTM32(packet);

                offset += currentBlockSize;
                startAddress += (uint)currentBlockSize;
                System.Threading.Thread.Sleep(10);
            }

        }

        private void sendBytesToSTM32(byte[] data)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Write(data, 0, data.Length);
            }
            else
            {
                MessageBox.Show("Serial Port is not Open!");
            }

        }

        private void btnWriteMem_Click(object sender, EventArgs e)
        {
            if (txtWriteMem.Text == "" || txtBrowseFile.Text == "")
            {
                MessageBox.Show("Please enter the address and select the bin file", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string addressText = txtWriteMem.Text.Trim();

                if (addressText.StartsWith("0x"))
                {
                    addressText = addressText.Substring(2);
                }
                uint address = Convert.ToUInt32(addressText, 16);

                sendWriteMemoryData(address);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void sendEraseData()
        {
            byte[] data = new byte[258];
            int index = 0;

            List<byte> selectedSectors = new List<byte>();
            if (chBoxSelect0.Checked) selectedSectors.Add(0x00);
            if (chBoxSelect1.Checked) selectedSectors.Add(0x01);
            if (chBoxSelect2.Checked) selectedSectors.Add(0x02);
            if (chBoxSelect3.Checked) selectedSectors.Add(0x03);
            if (chBoxSelect4.Checked) selectedSectors.Add(0x04);
            if (chBoxSelect5.Checked) selectedSectors.Add(0x05);
            if (chBoxSelect6.Checked) selectedSectors.Add(0x06);
            if (chBoxSelect7.Checked) selectedSectors.Add(0x07);

            if (chBoxMassErase.Checked)
            {
                data[0] = 0xFF;
                data[1] = (byte)(data[0] ^ 0x00);
                index = 2;
            }
            else
            {
                if (selectedSectors.Count == 0)
                {
                    MessageBox.Show("Please select sector or sectors.");
                }

                byte N = (byte)(selectedSectors.Count - 1);
                byte checkSum = N;
                data[0] = N;
                index = 1;
                for (int i = 0; i < selectedSectors.Count; i++)
                {
                    data[index] = selectedSectors[i];
                    checkSum ^= selectedSectors[i];
                    index++;
                }
                data[index] = checkSum;
                index++;
            }

            byte[] finalData = new byte[index];
            Array.Copy(data, finalData, index);
            byte cmd = (byte)BootloaderCommand.Erase;
            SendBootLoaderCommand(cmd, finalData);
        }

        private void btnErase_Click(object sender, EventArgs e)
        {
            sendEraseData();
        }

        private void sendWriteProtectUnprotect()
        {
            List<byte> sectorsToProtect = new List<byte>();

            if (chBoxWRP0.Checked) sectorsToProtect.Add(0x00);
            if (chBoxWRP1.Checked) sectorsToProtect.Add(0x01);
            if (chBoxWRP2.Checked) sectorsToProtect.Add(0x02);
            if (chBoxWRP3.Checked) sectorsToProtect.Add(0x03);
            if (chBoxWRP4.Checked) sectorsToProtect.Add(0x04);
            if (chBoxWRP5.Checked) sectorsToProtect.Add(0x05);
            if (chBoxWRP6.Checked) sectorsToProtect.Add(0x06);
            if (chBoxWRP7.Checked) sectorsToProtect.Add(0x07);

            if (sectorsToProtect.Count > 0)
            {
                byte N = (byte)(sectorsToProtect.Count - 1);
                List<byte> payload = new List<byte> { N };
                payload.AddRange(sectorsToProtect);

                byte cmd = (byte)BootloaderCommand.WriteProtect;
                SendBootLoaderCommand(cmd, payload.ToArray());

                MessageBox.Show("Checked sectors have been write-protected.");
            }
            else
            {
                byte N = (byte)(sectorsToProtect.Count - 1);
                List<byte> payload = new List<byte> { N };
                payload.AddRange(sectorsToProtect);

                byte cmd = (byte)BootloaderCommand.WriteProtect;
                SendBootLoaderCommand(cmd, payload.ToArray());
                MessageBox.Show("All sectors have been set to unprotected.");
            }
        }

        private void btnWriteProtect_Click(object sender, EventArgs e)
        {
            sendWriteProtectUnprotect();
        }
    }
}
