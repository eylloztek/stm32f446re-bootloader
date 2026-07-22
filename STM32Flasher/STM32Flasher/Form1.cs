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
        private const byte BootloaderWriteComplete = 0x7A;
        private const byte BootloaderNack = 0x1F;

        private const uint FlashMemoryStartAddress = 0x08000000U;
        private const uint FlashMemoryEndAddress = 0x0807FFFFU;

        private const uint ApplicationStartAddress = 0x08008000U;
        private const uint ApplicationEndAddress = 0x0807FFFFU;

        private const uint SramStartAddress = 0x20000000U;
        private const uint SramEndAddress = 0x2001FFFFU;

        private const uint BackupSramStartAddress = 0x40024000U;
        private const uint BackupSramEndAddress = 0x40024FFFU;

        private const int MaximumReadLength = 256;

        private readonly object responseLock = new object();

        private TaskCompletionSource<byte[]> pendingResponseWaiter;

        private readonly List<byte> pendingResponseBuffer = new List<byte>();

        private int pendingResponseExpectedLength;

        private const byte BootloaderHeader = 0x7F;
        private const int MaximumCommandLength = 32;

        private readonly object serialWriteLock = new object();

        private const uint Crc32ReversedPolynomial = 0xEDB88320U;
        private const uint Crc32InitialValue = 0xFFFFFFFFU;
        private const uint Crc32FinalXorValue = 0xFFFFFFFFU;

        private const int BootloaderCrcSize = 4;
        private const int CrcResponseLength = 5;

        private byte[] CreateBootloaderCommandPacket(byte command, byte[] payload)
        {
            if (payload == null)
            {
                payload = Array.Empty<byte>();
            }

            int commandLength = 1 + payload.Length;

            if ((commandLength < 1) || (commandLength > MaximumCommandLength))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(payload),
                    $"The command body must contain between 1 and " +
                    $"{MaximumCommandLength} bytes."
                );
            }

            /*
            * Complete packet:
            *
            * Header + Length + Command + Payload + CRC-32
            */
            byte[] packet = new byte[commandLength + 2 + BootloaderCrcSize];

            packet[0] = BootloaderHeader;
            packet[1] = (byte)commandLength;
            packet[2] = command;

            if (payload.Length > 0)
            {
                Array.Copy(payload, 0, packet, 3, payload.Length);
            }

            /*
            * CRC input:
            *
            * Length + Command + Payload
            */
            uint crc = CalculateCrc32(packet, 1, commandLength + 1);

            WriteUInt32BigEndian(packet, packet.Length - BootloaderCrcSize, crc);

            return packet;
        }

        private TaskCompletionSource<byte[]> CreateResponseWaiter(int expectedLength)
        {
            if (expectedLength <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedLength)
                );
            }

            lock (responseLock)
            {
                if (pendingResponseWaiter != null)
                {
                    throw new InvalidOperationException(
                        "Another command is already waiting for a response."
                    );
                }

                pendingResponseExpectedLength = expectedLength;

                pendingResponseBuffer.Clear();

                pendingResponseWaiter =
                    new TaskCompletionSource<byte[]>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    );

                return pendingResponseWaiter;
            }
        }

        private void CompletePendingResponse(byte[] receivedBytes)
        {
            if ((receivedBytes == null) ||
                (receivedBytes.Length == 0))
            {
                return;
            }

            TaskCompletionSource<byte[]> waiterToComplete = null;

            byte[] completedResponse = null;

            lock (responseLock)
            {
                if (pendingResponseWaiter == null)
                {
                    return;
                }

                int missingByteCount = pendingResponseExpectedLength - pendingResponseBuffer.Count;

                int byteCountToCopy =
                    Math.Min(
                        missingByteCount,
                        receivedBytes.Length
                    );

                for (int i = 0; i < byteCountToCopy; i++)
                {
                    pendingResponseBuffer.Add(receivedBytes[i]);
                }

                if (pendingResponseBuffer.Count == pendingResponseExpectedLength)
                {
                    completedResponse = pendingResponseBuffer.ToArray();

                    waiterToComplete = pendingResponseWaiter;

                    pendingResponseWaiter = null;
                    pendingResponseExpectedLength = 0;
                    pendingResponseBuffer.Clear();
                }
            }

            waiterToComplete?.TrySetResult(completedResponse);
        }

        private void CancelResponseWaiter(
            TaskCompletionSource<byte[]> waiter)
        {
            lock (responseLock)
            {
                if (ReferenceEquals(
                        pendingResponseWaiter,
                        waiter))
                {
                    pendingResponseWaiter = null;
                    pendingResponseExpectedLength = 0;
                    pendingResponseBuffer.Clear();
                }
            }
        }

        private async Task<byte[]>
            SendCommandAndWaitForResponseAsync(
                byte command,
                byte[] payload,
                int expectedResponseLength,
                int timeoutMilliseconds)
        {
            if (!serialPort1.IsOpen)
            {
                throw new InvalidOperationException(
                    "Serial port is not open."
                );
            }

            serialPort1.DiscardInBuffer();

            TaskCompletionSource<byte[]> waiter = CreateResponseWaiter(expectedResponseLength);

            try
            {
                SendBootLoaderCommand(command, payload);

                Task timeoutTask = Task.Delay(timeoutMilliseconds);

                Task completedTask =
                    await Task.WhenAny(
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
                CancelResponseWaiter(waiter);
            }
        }

        private async Task<byte[]>
            SendRawDataAndWaitForResponseAsync(
                byte[] data,
                int expectedResponseLength,
                int timeoutMilliseconds)
        {
            if (!serialPort1.IsOpen)
            {
                throw new InvalidOperationException(
                    "Serial port is not open."
                );
            }

            if ((data == null) ||
                (data.Length == 0))
            {
                throw new ArgumentException(
                    "The data block cannot be null or empty.",
                    nameof(data)
                );
            }

            TaskCompletionSource<byte[]> waiter = CreateResponseWaiter(expectedResponseLength);

            try
            {
                lock (serialWriteLock)
                {
                    serialPort1.Write(data, 0, data.Length);
                }

                Task timeoutTask = Task.Delay(timeoutMilliseconds);

                Task completedTask = await Task.WhenAny(waiter.Task, timeoutTask);

                if (completedTask != waiter.Task)
                {
                    return null;
                }

                return await waiter.Task;
            }
            finally
            {
                CancelResponseWaiter(waiter);
            }
        }

        private async Task<byte?>
            SendCommandAndWaitForStatusAsync(
                byte command,
                byte[] payload,
                int timeoutMilliseconds = 3000)
        {
            byte[] response =
                await SendCommandAndWaitForResponseAsync(
                    command,
                    payload,
                    1,
                    timeoutMilliseconds
                );

            if ((response == null) ||
                (response.Length != 1))
            {
                return null;
            }

            return response[0];
        }

        private async Task<byte?>
            SendRawDataAndWaitForStatusAsync(byte[] data, int timeoutMilliseconds = 5000)
        {
            byte[] response = await SendRawDataAndWaitForResponseAsync(data, 1, timeoutMilliseconds);

            if ((response == null) ||
                (response.Length != 1))
            {
                return null;
            }

            return response[0];
        }

        private byte[] CreateWriteBlockPacket(byte[] sourceData, int offset, int blockLength)
        {
            if (sourceData == null)
            {
                throw new ArgumentNullException(
                    nameof(sourceData)
                );
            }

            if ((blockLength < 1) || (blockLength > 256))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(blockLength),
                    "The block length must be between 1 and 256 bytes."
                );
            }

            if ((offset < 0) ||
                (offset + blockLength > sourceData.Length))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset)
                );
            }

            /*
             * Packet format:
             *
             * N        : 1 byte
             * Data     : 1-256 bytes
             * CRC-32   : 4 bytes
             */
            byte[] packet = new byte[1 + blockLength + BootloaderCrcSize];

            byte n = (byte)(blockLength - 1);

            packet[0] = n;

            Array.Copy(sourceData, offset, packet, 1, blockLength);

            /*
            * Block CRC input:
            *
            * N + Data
            */
            uint blockCrc = CalculateCrc32(packet, 0, blockLength + 1);

            WriteUInt32BigEndian(packet, blockLength + 1, blockCrc);

            return packet;
        }

        private static bool IsRangeInsideMemoryRegion(uint address, uint length, uint regionStart, uint regionEnd)
        {
            if (length == 0U)
            {
                return false;
            }

            if ((address < regionStart) || (address > regionEnd))
            {
                return false;
            }

            return (length - 1U) <= (regionEnd - address);
        }

        private static bool IsReadableRange(uint address, uint length)
        {
            return
                IsRangeInsideMemoryRegion(
                    address,
                    length,
                    FlashMemoryStartAddress,
                    FlashMemoryEndAddress
                ) ||
                IsRangeInsideMemoryRegion(
                    address,
                    length,
                    SramStartAddress,
                    SramEndAddress
                ) ||
                IsRangeInsideMemoryRegion(
                    address,
                    length,
                    BackupSramStartAddress,
                    BackupSramEndAddress
                );
        }

        private static bool IsApplicationWriteRange(uint address, uint length)
        {
            return IsRangeInsideMemoryRegion(
                address,
                length,
                ApplicationStartAddress,
                ApplicationEndAddress
            );
        }

        private static byte[] CreateGoToAddressPayload(uint address)
        {
            byte[] payload = new byte[5];

            /*
             * Address in big-endian format.
             */
            payload[0] = (byte)((address >> 24) & 0xFFU);
            payload[1] = (byte)((address >> 16) & 0xFFU);
            payload[2] = (byte)((address >> 8) & 0xFFU);
            payload[3] = (byte)(address & 0xFFU);

            payload[4] = (byte)(payload[0] ^ payload[1] ^ payload[2] ^ payload[3]);

            return payload;
        }

        private static uint CalculateCrc32(byte[] data, int offset, int count)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if ((offset < 0) ||
                (count < 0) ||
                (offset + count > data.Length))
            {
                throw new ArgumentOutOfRangeException();
            }

            uint crc = Crc32InitialValue;

            for (int byteIndex = 0; byteIndex < count; byteIndex++)
            {
                crc ^= data[offset + byteIndex];

                for (int bitIndex = 0; bitIndex < 8; bitIndex++)
                {
                    uint mask = 0U - (crc & 1U);

                    crc = (crc >> 1) ^ (Crc32ReversedPolynomial & mask);
                }
            }

            return crc ^ Crc32FinalXorValue;
        }

        private static uint CalculateCrc32(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return CalculateCrc32(data, 0, data.Length);
        }

        private static void WriteUInt32BigEndian(byte[] destination, int offset, uint value)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(
                    nameof(destination)
                );
            }

            if ((offset < 0) ||
                (offset + 4 > destination.Length))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset)
                );
            }

            destination[offset] = (byte)((value >> 24) & 0xFFU);

            destination[offset + 1] = (byte)((value >> 16) & 0xFFU);

            destination[offset + 2] = (byte)((value >> 8) & 0xFFU);

            destination[offset + 3] = (byte)(value & 0xFFU);
        }

        private static uint ReadUInt32BigEndian(byte[] source, int offset)
        {
            if (source == null)
            {
                throw new ArgumentNullException(
                    nameof(source)
                );
            }

            if ((offset < 0) ||
                (offset + 4 > source.Length))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset)
                );
            }

            return
                ((uint)source[offset] << 24) |
                ((uint)source[offset + 1] << 16) |
                ((uint)source[offset + 2] << 8) |
                source[offset + 3];
        }

        private async Task<uint?> GetMemoryCrc32Async(uint address, uint length)
        {
            if (!IsApplicationWriteRange(address, length))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(address),
                    "The CRC range must remain inside the application area."
                );
            }

            byte[] payload = new byte[8];

            WriteUInt32BigEndian(payload, 0, address);

            WriteUInt32BigEndian(payload, 4, length);

            byte[] response =
                await SendCommandAndWaitForResponseAsync(
                    (byte)BootloaderCommand.GetChecksum,
                    payload,
                    CrcResponseLength,
                    5000
                );

            if (response == null)
            {
                return null;
            }

            if (response[0] != BootloaderAck)
            {
                throw new InvalidOperationException(
                    $"The bootloader rejected the checksum request. " +
                    $"Response: 0x{response[0]:X2}"
                );
            }

            return ReadUInt32BigEndian(response, 1);
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

        private void SendBootLoaderCommand(byte command, byte[] payload)
        {
            if (!serialPort1.IsOpen)
            {
                throw new InvalidOperationException(
                    "Serial port is not open."
                );
            }

            byte[] packet = CreateBootloaderCommandPacket(command, payload);

            /*
             * Prevent multiple UI or asynchronous operations from
             * interleaving bytes on the serial port.
             */
            lock (serialWriteLock)
            {
                serialPort1.Write(packet, 0, packet.Length);
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
                CompletePendingResponse(buffer);

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

        private void sendReadMemoryData(uint address, int length)
        {

            if ((length < 1) || (length > MaximumReadLength))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    $"Read length must be between 1 and {MaximumReadLength} bytes."
                );
            }

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

            if (string.IsNullOrWhiteSpace(txtAddress.Text) || string.IsNullOrWhiteSpace(txtLength.Text))
            {
                MessageBox.Show(
                    "Please enter the address and length values.",
                    "Missing Read Parameters",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
            }

            try
            {
                string addressText = txtAddress.Text.Trim();

                if (addressText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    addressText = addressText.Substring(2);
                }

                uint address = Convert.ToUInt32(addressText, 16);

                if (!int.TryParse(txtLength.Text.Trim(), out int length))
                {
                    MessageBox.Show(
                        "The read length must be a decimal number.",
                        "Invalid Read Length",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                }

                if ((length < 1) || (length > MaximumReadLength))
                {
                    MessageBox.Show(
                        $"The read length must be between 1 and " +
                        $"{MaximumReadLength} bytes.",
                        "Invalid Read Length",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                }

                if (!IsReadableRange(address, (uint)length))
                {
                    MessageBox.Show(
                        $"The requested memory range is invalid.\n\n" +
                        $"Start address: 0x{address:X8}\n" +
                        $"Length: {length} bytes",
                        "Invalid Memory Range",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );

                    return;
                }

                sendReadMemoryData(address, length);
            }
            catch (FormatException)
            {
                MessageBox.Show(
                    "The address is not a valid hexadecimal value.",
                    "Invalid Address",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            catch (OverflowException)
            {
                MessageBox.Show(
                    "The address is outside the UInt32 range.",
                    "Invalid Address",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Read Memory Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
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

                if (address != ApplicationStartAddress)
                {
                    MessageBox.Show(
                        $"The Go command only accepts the application vector table address.\n\n" +
                        $"Expected address: 0x{ApplicationStartAddress:X8}\n" +
                        $"Entered address: 0x{address:X8}",
                        "Invalid Application Address",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );

                    return;
                }

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

        private async Task WriteMemoryAsync(uint address)
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

            if ((binData == null) || (binData.Length == 0))
            {
                MessageBox.Show(
                    "The selected binary file is empty or could not be loaded.",
                    "Invalid Binary File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
            }

            uint imageLength = (uint)binData.Length;

            if (!IsApplicationWriteRange(address, imageLength))
            {
                MessageBox.Show(
                    $"The firmware image does not fit inside the application area.\n\n" +
                    $"Application start: 0x{ApplicationStartAddress:X8}\n" +
                    $"Application end: 0x{ApplicationEndAddress:X8}\n" +
                    $"Write address: 0x{address:X8}\n" +
                    $"Image length: {imageLength} bytes",
                    "Invalid Firmware Range",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return;
            }

            uint expectedImageCrc = CalculateCrc32(binData);

            /*
             * Payload:
             *
             * Address          : 4 bytes
             * Address checksum : 1 byte
             * Image length     : 4 bytes
             * Image CRC-32     : 4 bytes
             */
            byte[] payload = new byte[13];

            WriteUInt32BigEndian(payload, 0, address);

            payload[4] = (byte)(payload[0] ^ payload[1] ^ payload[2] ^ payload[3]);

            WriteUInt32BigEndian(payload, 5, imageLength);

            WriteUInt32BigEndian( payload, 9, expectedImageCrc);

            byte command = (byte)BootloaderCommand.WriteMemory;

            serialPort1.DiscardInBuffer();

            byte? readyResponse = await SendCommandAndWaitForStatusAsync(command, payload, 3000);

            if (!readyResponse.HasValue)
            {
                MessageBox.Show(
                    "The bootloader did not respond to the Write Memory command.",
                    "Write Command Timeout",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return;
            }

            if (readyResponse.Value != BootloaderAck)
            {
                MessageBox.Show(
                    $"The bootloader rejected the Write Memory command.\n\n" +
                    $"Response: 0x{readyResponse.Value:X2}",
                    "Write Command Rejected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return;
            }

            int offset = 0;
            int blockNumber = 0;

            int totalBlocks = (binData.Length + 255) / 256;

            while (offset < binData.Length)
            {
                int remaining = binData.Length - offset;

                int currentBlockLength = Math.Min(256, remaining);

                bool isFinalBlock = (offset + currentBlockLength) == binData.Length;

                byte[] blockPacket = CreateWriteBlockPacket(binData, offset, currentBlockLength);

                blockNumber++;

                byte? blockResponse =
                    await SendRawDataAndWaitForStatusAsync(
                        blockPacket,
                        isFinalBlock ? 10000 : 5000
                    );

                if (!blockResponse.HasValue)
                {
                    MessageBox.Show(
                        $"No response was received for block " +
                        $"{blockNumber} of {totalBlocks}.",
                        "Firmware Write Timeout",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );

                    return;
                }

                if (blockResponse.Value == BootloaderNack)
                {
                    string failureReason =
                        isFinalBlock
                            ? "The complete firmware CRC-32 verification failed."
                            : "The firmware block was rejected.";

                    MessageBox.Show(
                        $"{failureReason}\n\n" +
                        $"Block: {blockNumber} of {totalBlocks}\n" +
                        $"Expected image CRC-32: 0x{expectedImageCrc:X8}",
                        "Firmware Verification Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );

                    return;
                }

                if (isFinalBlock)
                {
                    if (blockResponse.Value != BootloaderWriteComplete)
                    {
                        MessageBox.Show(
                            $"The final block returned an unexpected response: " +
                            $"0x{blockResponse.Value:X2}",
                            "Firmware Completion Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );

                        return;
                    }
                }
                else if (blockResponse.Value != BootloaderAck)
                {
                    MessageBox.Show(
                        $"Block {blockNumber} returned an unexpected response: " +
                        $"0x{blockResponse.Value:X2}",
                        "Firmware Protocol Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );

                    return;
                }

                offset += currentBlockLength;

                int progress = (int)((offset * 100L) / binData.Length);

                prgBarStatus.Value =
                    Math.Max(
                        prgBarStatus.Minimum,
                        Math.Min(
                            prgBarStatus.Maximum,
                            progress
                        )
                    );
            }

            MessageBox.Show(
                $"Firmware was written and verified successfully.\n\n" +
                $"Bytes written: {binData.Length}\n" +
                $"Blocks written: {totalBlocks}\n" +
                $"CRC-32: 0x{expectedImageCrc:X8}",
                "Firmware Write Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private async void btnWriteMem_Click(object sender,EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtWriteMem.Text) ||
                string.IsNullOrWhiteSpace(txtBrowseFile.Text))
            {
                MessageBox.Show(
                    "Please enter the destination address and select a binary file.",
                    "Missing Write Parameters",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
            }

            if ((binData == null) || (binData.Length == 0))
            {
                MessageBox.Show(
                    "The selected binary file is empty or has not been loaded.",
                    "Invalid Binary File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
            }

            try
            {
                string addressText = txtWriteMem.Text.Trim();

                if (addressText.StartsWith("0x",StringComparison.OrdinalIgnoreCase))
                {
                    addressText = addressText.Substring(2);
                }

                uint address = Convert.ToUInt32(addressText, 16);

                btnWriteMem.Enabled = false;
                btnBrowse.Enabled = false;
                txtWriteMem.Enabled = false;

                prgBarStatus.Value = 0;

                await WriteMemoryAsync(address);
            }
            catch (FormatException)
            {
                MessageBox.Show(
                    "The destination address is not a valid hexadecimal value.",
                    "Invalid Address",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            catch (OverflowException)
            {
                MessageBox.Show(
                    "The destination address is outside the UInt32 range.",
                    "Invalid Address",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Firmware Write Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                btnWriteMem.Enabled = true;
                btnBrowse.Enabled = true;
                txtWriteMem.Enabled = true;
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

        private async void btnExitBoot_Click(object sender, EventArgs e)
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

            byte command = (byte)BootloaderCommand.Go;

            byte[] payload = CreateGoToAddressPayload(ApplicationStartAddress);

            btnExitBoot.Enabled = false;

            try
            {
                byte? response = await SendCommandAndWaitForStatusAsync(command, payload, 3000);

                if (!response.HasValue)
                {
                    MessageBox.Show(
                        "The bootloader did not respond to the application jump request.",
                        "Application Jump Timeout",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );

                    return;
                }

                if (response.Value == BootloaderNack)
                {
                    MessageBox.Show(
                        "The application jump was rejected.\n\n" +
                        "The application vector table, initial stack pointer, " +
                        "or Reset Handler is invalid.",
                        "Invalid Application Image",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                }

                if (response.Value != BootloaderAck)
                {
                    MessageBox.Show(
                        $"Unexpected bootloader response: 0x{response.Value:X2}",
                        "Protocol Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );

                    return;
                }

                /*
                 * The serial port remains open because the application
                 * also uses the same UART. Bootloader command controls
                 * are disabled after the successful jump.
                 */
                grpBoxCommands.Enabled = false;
                btnExitBoot.Enabled = false;
                btnReset.Enabled = false;

                lblConnectionStatus.Text = "Application Mode";

                MessageBox.Show(
                    "The application jump was accepted.",
                    "Application Started",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Application Jump Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                /*
                 * Re-enable the button only when the application jump
                 * was not accepted.
                 */
                if (lblConnectionStatus.Text != "Application Mode")
                {
                    btnExitBoot.Enabled = true;
                }
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            cBoxComPort.Items.Clear();
            String[] ports = SerialPort.GetPortNames();
            cBoxComPort.Items.AddRange(ports);
        }
    }
}
