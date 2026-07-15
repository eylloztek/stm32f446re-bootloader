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
        private const byte BootloaderAck = 0x79;
        private const byte BootloaderNack = 0x1F;

        private readonly object statusResponseLock = new object();
        private TaskCompletionSource<byte> pendingStatusResponse;

        private TaskCompletionSource<byte> CreateStatusResponseWaiter()
        {
            lock (statusResponseLock)
            {
                if (pendingStatusResponse != null)
                {
                    throw new InvalidOperationException(
                        "Another command is already waiting for a status response."
                    );
                }

                pendingStatusResponse = new TaskCompletionSource<byte>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );

                return pendingStatusResponse;
            }
        }

        private void CompleteStatusResponse(byte[] receivedBytes)
        {
            byte statusByte = 0x00;
            bool statusFound = false;

            foreach (byte value in receivedBytes)
            {
                if ((value == BootloaderAck) ||
                    (value == BootloaderNack))
                {
                    statusByte = value;
                    statusFound = true;
                    break;
                }
            }

            if (!statusFound)
            {
                return;
            }

            TaskCompletionSource<byte> waiter = null;

            lock (statusResponseLock)
            {
                if (pendingStatusResponse != null)
                {
                    waiter = pendingStatusResponse;
                    pendingStatusResponse = null;
                }
            }

            waiter?.TrySetResult(statusByte);
        }

        private void CancelStatusResponseWaiter(
            TaskCompletionSource<byte> waiter)
        {
            lock (statusResponseLock)
            {
                if (ReferenceEquals(pendingStatusResponse, waiter))
                {
                    pendingStatusResponse = null;
                }
            }
        }

        private async Task<byte?> SendCommandAndWaitForStatusAsync(
            byte command,
            byte[] payload,
            int timeoutMilliseconds = 3000)
        {
            if (!serialPort1.IsOpen)
            {
                throw new InvalidOperationException("Serial port is not open.");
            }

            /*
             * Remove stale response bytes before starting a new
             * request-response transaction.
             */
            serialPort1.DiscardInBuffer();

            TaskCompletionSource<byte> waiter =
                CreateStatusResponseWaiter();

            try
            {
                SendBootLoaderCommand(command, payload);

                Task timeoutTask = Task.Delay(timeoutMilliseconds);

                Task completedTask = await Task.WhenAny(
                    waiter.Task,
                    timeoutTask
                );

                if (completedTask != waiter.Task)
                {
                    return null;
                }

                return await waiter.Task;
            }
            finally
            {
                CancelStatusResponseWaiter(waiter);
            }
        }
        public STM32Flasher()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            grpBoxCommands.Enabled = false;
            grpBoxMode.Enabled = false;
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

            /*
            * Sector 0 and Sector 1 contain the bootloader.
            * They must never be erased from this application.
            */
            chBoxSelect0.Checked = false;
            chBoxSelect0.Enabled = false;
            chBoxSelect0.Text = "Sector 0 (Bootloader)";

            chBoxSelect1.Checked = false;
            chBoxSelect1.Enabled = false;
            chBoxSelect1.Text = "Sector 1 (Bootloader)";

            /*
             * Real Mass Erase would erase the bootloader as well.
             * Application erase must be performed by selecting Sector 2-7.
             */
            chBoxMassErase.Checked = false;
            chBoxMassErase.Enabled = false;
            chBoxMassErase.Text = "Mass Erase (Disabled)";

            /*
            * RDP Level 2 is intentionally not exposed because it is irreversible.
            */
            cBoxReadoutPro.Items.Clear();
            cBoxReadoutPro.Items.Add("Level 0 (No Protection)");
            cBoxReadoutPro.Items.Add("Level 1 (Read Protection)");
            cBoxReadoutPro.SelectedIndex = 0;
            cBoxReadoutPro.DropDownStyle = ComboBoxStyle.DropDownList;
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
                grpBoxCommands.Enabled = true;
                grpBoxMode.Enabled = true;
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
                grpBoxCommands.Enabled = false;
                grpBoxMode.Enabled = false;
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

                if (bytesToRead <= 0)
                {
                    return;
                }

                byte[] buffer = new byte[bytesToRead];
                int bytesRead = serialPort1.Read(buffer, 0,buffer.Length);

                if (bytesRead <= 0)
                {
                    return;
                }

                if (bytesRead != buffer.Length)
                {
                    Array.Resize(ref buffer, bytesRead);
                }

                /*
                * Complete an active ACK/NACK request before updating the UI.
                */
                CompleteStatusResponse(buffer);

                receivedData.AddRange(buffer);

                string hexOutput = BitConverter.ToString(buffer).Replace("-", " ");

                BeginInvoke(new Action(() =>
                {
                    txtReceiveMessage.AppendText(
                        hexOutput + Environment.NewLine
                    );

                    txtReceiveMessage.SelectionStart =
                        txtReceiveMessage.TextLength;

                    txtReceiveMessage.ScrollToCaret();
                }));

            } catch (Exception ex)
            {
                BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(
                        ex.Message,
                        "Serial Port Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }));
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
            /*
             * Defense in depth:
             *
             * Bu kontroller UI üzerinde disabled olsa bile,
             * herhangi bir kod değişikliği veya yanlış durum nedeniyle
             * işaretlenmişlerse komut gönderilmez.
             */
            if (chBoxSelect0.Checked ||
                chBoxSelect1.Checked ||
                chBoxMassErase.Checked)
            {
                chBoxSelect0.Checked = false;
                chBoxSelect1.Checked = false;
                chBoxMassErase.Checked = false;

                MessageBox.Show(
                    "Sector 0, Sector 1 and Mass Erase are disabled because they can erase the bootloader.",
                    "Protected Bootloader Area",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return;
            }

            List<byte> selectedSectors = new List<byte>();

            /*
             * Only application sectors are allowed.
             */
            if (chBoxSelect2.Checked) selectedSectors.Add(0x02);
            if (chBoxSelect3.Checked) selectedSectors.Add(0x03);
            if (chBoxSelect4.Checked) selectedSectors.Add(0x04);
            if (chBoxSelect5.Checked) selectedSectors.Add(0x05);
            if (chBoxSelect6.Checked) selectedSectors.Add(0x06);
            if (chBoxSelect7.Checked) selectedSectors.Add(0x07);

            if (selectedSectors.Count == 0)
            {
                MessageBox.Show(
                    "Please select at least one application sector between Sector 2 and Sector 7.",
                    "No Application Sector Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
            }

            string sectorList = string.Join(
                ", ",
                selectedSectors.Select(sector => $"Sector {sector}")
            );

            DialogResult confirmation = MessageBox.Show(
                $"The following application sectors will be erased:\n\n{sectorList}\n\nContinue?",
                "Confirm Sector Erase",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2
            );

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            /*
             * Protocol:
             *
             * N = number of sectors - 1
             * Checksum = N XOR Sector1 XOR Sector2 XOR ...
             */
            byte n = (byte)(selectedSectors.Count - 1);
            byte checksum = n;

            List<byte> payload = new List<byte>
            {
                n
            };

            foreach (byte sector in selectedSectors)
            {
                payload.Add(sector);
                checksum ^= sector;
            }

            payload.Add(checksum);

            byte command = (byte)BootloaderCommand.Erase;
            SendBootLoaderCommand(command, payload.ToArray());
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

        private async Task SendReadoutProtectionDataAsync()
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(
                    "Serial port is not open.",
                    "Connection Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return;
            }

            byte requestedLevel;
            string selectedLevelText;

            switch (cBoxReadoutPro.SelectedIndex)
            {
                case 0:
                    requestedLevel = 0x00;
                    selectedLevelText = "RDP Level 0";
                    break;

                case 1:
                    requestedLevel = 0x01;
                    selectedLevelText = "RDP Level 1";
                    break;

                default:
                    MessageBox.Show(
                        "Please select a valid RDP level.",
                        "Invalid Selection",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
            }

            if (requestedLevel == 0x00)
            {
                DialogResult level0Confirmation = MessageBox.Show(
                    "This command only confirms Level 0 when the device is already unprotected.\n\n" +
                    "A Level 1 to Level 0 regression would mass-erase the entire Flash and is therefore blocked by the bootloader.\n\n" +
                    "Continue?",
                    "RDP Level 0",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button2
                );

                if (level0Confirmation != DialogResult.Yes)
                {
                    return;
                }
            }
            else
            {
                DialogResult level1Confirmation = MessageBox.Show(
                    "RDP Level 1 will prevent external read access through debugging and programming interfaces.\n\n" +
                    "Returning from Level 1 to Level 0 requires a complete Flash mass erase.\n\n" +
                    "Continue?",
                    "Enable RDP Level 1",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2
                );

                if (level1Confirmation != DialogResult.Yes)
                {
                    return;
                }
            }

            byte[] payload = {requestedLevel};

            byte command = (byte)BootloaderCommand.ReadoutProtect;

            btnReadoutPro.Enabled = false;
            cBoxReadoutPro.Enabled = false;

            try
            {
                byte? response = await SendCommandAndWaitForStatusAsync(
                    command,
                    payload,
                    3000
                );

                if (!response.HasValue)
                {
                    MessageBox.Show(
                        "No ACK or NACK response was received from the bootloader.",
                        "RDP Request Timeout",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );

                    return;
                }

                if (response.Value == BootloaderAck)
                {
                    if (requestedLevel == 0x00)
                    {
                        MessageBox.Show(
                            "The device is already operating at RDP Level 0.",
                            "RDP Level Confirmed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }
                    else
                    {
                        MessageBox.Show(
                            "The RDP Level 1 request was accepted.\n\n" +
                            "The microcontroller will program the option bytes and reset. " +
                            "If the target does not restart normally, disconnect the active debugger and perform a power cycle.",
                            "RDP Request Accepted",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }

                    return;
                }

                if (requestedLevel == 0x00)
                {
                    MessageBox.Show(
                        "The Level 0 request was rejected.\n\n" +
                        "If the device is currently at Level 1, returning to Level 0 is blocked because it would mass-erase both the bootloader and application.",
                        "RDP Request Rejected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
                else
                {
                    MessageBox.Show(
                        $"{selectedLevelText} could not be enabled. The bootloader returned NACK.",
                        "RDP Programming Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "RDP Command Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                btnReadoutPro.Enabled = true;
                cBoxReadoutPro.Enabled = true;
            }
        }

        private async void btnReadoutPro_Click(object sender, EventArgs e)
        {
            await SendReadoutProtectionDataAsync();
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            byte cmd = (byte)BootloaderCommand.Reset;
            SendBootLoaderCommand(cmd, new byte[0]);
        }

        private void btnExitBoot_Click(object sender, EventArgs e)
        {
            uint defaultAddress = 0x08008000;
            sendGoAddressData(defaultAddress);
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            cBoxComPort.Items.Clear();
            String[] ports = SerialPort.GetPortNames();
            cBoxComPort.Items.AddRange(ports);
        }
    }
}
