/*
 * bootloader.c
 *
 *  Created on: Jul 3, 2026
 *      Author: Eylül Öztek
 */

#include "bootloader.h"
#include "stdio.h"

/*
 * Response format:
 * Byte 0: ACK or NACK
 * Byte 1: protection mode
 * Byte 2: active protection mask
 */
#define WRP_OPERATION_GET_STATUS             0x00U
#define WRP_OPERATION_SET_MASK               0x01U
#define FLASH_PROTECTION_MODE_WRP             0x00U
#define FLASH_PROTECTION_MODE_PCROP           0x01U
#define WRP_RESPONSE_SIZE                     3U
#define WRP_VALID_SECTOR_MASK                 0xFFU
#define WRP_BOOTLOADER_SECTOR_MASK            0x03U
#define WRP_APPLICATION_SECTOR_MASK           0xFCU

#define BOOTLOADER_BUILD_TAG                   "WRP_RESET_FIX_01"

/*
 * SPRMOD is bit 31 of OPTCR on STM32F446. Keep a target-specific fallback
 * for older CMSIS device headers that do not expose the named bit mask.
 */
#ifndef FLASH_OPTCR_SPRMOD
#define FLASH_OPTCR_SPRMOD                     (1UL << 31U)
#endif

extern uint8_t messageBuffer[BOOTLOADER_RX_BUFFER_SIZE];
extern uint8_t counterTest;

static uint8_t validateBootloaderPacket(uint16_t packetLength);
static uint8_t validateCommandLength(uint8_t command, uint8_t commandLength);

static uint32_t readUint32BigEndian(const uint8_t *data);
static void writeUint32BigEndian(uint8_t *destination, uint32_t value);
static uint32_t crc32Update(uint32_t crc, const uint8_t *data, uint32_t length);
static uint32_t calculateBlockCrc32(uint8_t n, const uint8_t *data,
		uint16_t dataLength);
static uint8_t getFlashProtectionMode(void);
static uint8_t getRawProtectionSectorBits(void);
static uint8_t getActiveProtectionMask(uint8_t protectionMode);
static HAL_StatusTypeDef stageWriteProtectionChange(uint8_t sectorMask,
		uint32_t wrpState);
static HAL_StatusTypeDef sendWriteProtectionResponse(uint8_t status,
		uint8_t protectionMode, uint8_t activeProtectionMask);
static void waitForUartTransmissionComplete(void);
static void sendWriteProtectionResponseAndReset(uint8_t status,
		uint8_t protectionMode, uint8_t activeProtectionMask);

static uint32_t readUint32BigEndian(const uint8_t *data) {
	if (data == NULL) {
		return 0U;
	}

	return ((uint32_t) data[0] << 24U) | ((uint32_t) data[1] << 16U)
			| ((uint32_t) data[2] << 8U) | ((uint32_t) data[3]);
}

static void writeUint32BigEndian(uint8_t *destination, uint32_t value) {
	if (destination == NULL) {
		return;
	}

	destination[0] = (uint8_t) ((value >> 24U) & 0xFFU);
	destination[1] = (uint8_t) ((value >> 16U) & 0xFFU);
	destination[2] = (uint8_t) ((value >> 8U) & 0xFFU);
	destination[3] = (uint8_t) (value & 0xFFU);
}

static uint32_t crc32Update(uint32_t crc, const uint8_t *data, uint32_t length) {
	if ((data == NULL) && (length != 0U)) {
		return crc;
	}

	for (uint32_t byteIndex = 0U; byteIndex < length; byteIndex++) {
		crc ^= data[byteIndex];

		for (uint8_t bitIndex = 0U; bitIndex < 8U; bitIndex++) {
			uint32_t mask = 0U - (crc & 1U);

			crc = (crc >> 1U) ^ (CRC32_REVERSED_POLYNOMIAL & mask);
		}
	}

	return crc;
}

uint32_t calculateCrc32(const uint8_t *data, uint32_t length) {
	if ((data == NULL) && (length != 0U)) {
		return 0U;
	}

	uint32_t crc = CRC32_INITIAL_VALUE;

	crc = crc32Update(crc, data, length);

	return crc ^ CRC32_FINAL_XOR_VALUE;
}

static uint32_t calculateBlockCrc32(uint8_t n, const uint8_t *data,
		uint16_t dataLength) {
	uint32_t crc = CRC32_INITIAL_VALUE;

	/*
	 * Block CRC input:
	 *
	 * N byte + firmware data bytes
	 */
	crc = crc32Update(crc, &n, 1U);

	crc = crc32Update(crc, data, dataLength);

	return crc ^ CRC32_FINAL_XOR_VALUE;
}

static uint8_t validateCommandLength(uint8_t command, uint8_t commandLength) {
	switch (command) {
	case GET_HELP:
	case GET_VERSION:
	case GET_ID:
	case RESET_COMMAND: {
		return commandLength == 1U;
	}

	case READ_MEMORY: {
		return commandLength == 8U;
	}

	case GO_TO_ADDRESS: {
		return commandLength == 6U;
	}

	case WRITE_MEMORY: {
		return commandLength == 14U;
	}

	case GET_CHECKSUM: {
		/*
		 * Command byte : 1 byte
		 * Address      : 4 bytes
		 * Length       : 4 bytes
		 */
		return commandLength == 9U;
	}

	case ERASE: {
		/*
		 * Command + N + 1-6 sectors + inner checksum
		 */
		return (commandLength >= 4U) && (commandLength <= 9U);
	}

	case WRITE_PROTECT_UNPROTECT: {
		/*
		 * Command byte          : 1 byte
		 * Operation             : 1 byte
		 * Protected-sector mask : 1 byte
		 */
		return commandLength == 3U;
	}

	case READOUT_PROTECT_UNPROTECT: {
		return commandLength == 2U;
	}

	default: {
		/*
		 * Unknown commands are allowed through framing
		 * validation so that handleUnknownCommand() can
		 * return the protocol's normal unknown response.
		 */
		return 1U;
	}
	}
}

static uint8_t validateBootloaderPacket(uint16_t packetLength) {
	uint16_t minimumPacketLength = BOOTLOADER_MIN_COMMAND_LENGTH
			+ BOOTLOADER_FRAME_OVERHEAD;

	if ((packetLength < minimumPacketLength)
			|| (packetLength > BOOTLOADER_RX_BUFFER_SIZE)) {
		return 0U;
	}

	if (messageBuffer[0] != BOOTLOADER_HEADER) {
		return 0U;
	}

	uint8_t commandLength = messageBuffer[1];

	if ((commandLength < BOOTLOADER_MIN_COMMAND_LENGTH)
			|| (commandLength > BOOTLOADER_MAX_COMMAND_LENGTH)) {
		return 0U;
	}

	uint16_t expectedPacketLength = (uint16_t) commandLength
			+ BOOTLOADER_FRAME_OVERHEAD;

	if (packetLength != expectedPacketLength) {
		return 0U;
	}

	uint8_t command = messageBuffer[2];

	if (!validateCommandLength(command, commandLength)) {
		return 0U;
	}

	/*
	 * CRC starts immediately after the command body.
	 *
	 * Header is at index 0.
	 * Length is at index 1.
	 * Command body starts at index 2.
	 */
	uint16_t crcIndex = 2U + (uint16_t) commandLength;

	uint32_t receivedCrc = readUint32BigEndian(&messageBuffer[crcIndex]);

	/*
	 * CRC input:
	 *
	 * Length byte + command body
	 */
	uint32_t calculatedCrc = calculateCrc32(&messageBuffer[1],
			(uint32_t) commandLength + 1U);

	if (calculatedCrc != receivedCrc) {
#ifdef DEBUG_PRINT
		printf("Command CRC-32 error. "
				"Calculated: 0x%08lX, Received: 0x%08lX\r\n", calculatedCrc,
				receivedCrc);
#endif

		return 0U;
	}

	return 1U;
}

static uint8_t getFlashProtectionMode(void) {
	if (READ_BIT(FLASH->OPTCR, FLASH_OPTCR_SPRMOD) != 0U) {
		return FLASH_PROTECTION_MODE_PCROP;
	}

	return FLASH_PROTECTION_MODE_WRP;
}

static uint8_t getRawProtectionSectorBits(void) {
	/*
	 * On STM32F446, OPTCR bits 16-23 contain nWRP0-nWRP7.
	 */
	return (uint8_t) ((FLASH->OPTCR >> 16U) & WRP_VALID_SECTOR_MASK);
}

static uint8_t getActiveProtectionMask(uint8_t protectionMode) {
	uint8_t rawSectorBits = getRawProtectionSectorBits();

	if (protectionMode == FLASH_PROTECTION_MODE_PCROP) {
		/*
		 * PCROP mode:
		 * nWRPi = 1 means that PCROP protection is active.
		 */
		return (uint8_t) (rawSectorBits & WRP_VALID_SECTOR_MASK);
	}

	/*
	 * Normal write-protection mode:
	 * nWRPi = 0 means that write protection is active.
	 */
	return (uint8_t) ((~rawSectorBits) & WRP_VALID_SECTOR_MASK);
}

static HAL_StatusTypeDef stageWriteProtectionChange(uint8_t sectorMask,
		uint32_t wrpState) {
	if (sectorMask == 0U) {
		return HAL_OK;
	}

	FLASH_OBProgramInitTypeDef optionBytes = { 0 };

	optionBytes.OptionType = OPTIONBYTE_WRP;
	optionBytes.WRPState = wrpState;
	optionBytes.WRPSector = (uint32_t) sectorMask;
	optionBytes.Banks = FLASH_BANK_1;

	return HAL_FLASHEx_OBProgram(&optionBytes);
}

static HAL_StatusTypeDef sendWriteProtectionResponse(uint8_t status,
		uint8_t protectionMode, uint8_t activeProtectionMask) {
	uint8_t response[WRP_RESPONSE_SIZE] = { status, protectionMode,
			activeProtectionMask };

	return HAL_UART_Transmit(UART_PORT, response, sizeof(response), 1000U);
}

static void waitForUartTransmissionComplete(void) {
	while (__HAL_UART_GET_FLAG(UART_PORT, UART_FLAG_TC) == 0U) {
		/*
		 * Wait until the final response byte leaves the shift register.
		 */
	}
}

static void sendWriteProtectionResponseAndReset(uint8_t status,
		uint8_t protectionMode, uint8_t activeProtectionMask) {
	if (sendWriteProtectionResponse(status, protectionMode,
			activeProtectionMask) == HAL_OK) {
		waitForUartTransmissionComplete();
	}

	HAL_Delay(20U);
	HAL_NVIC_SystemReset();

	while (1) {
		/*
		 * System reset must not return.
		 */
	}
}

void processBootloaderCommand(uint16_t packetLength) {

	if (!validateBootloaderPacket(packetLength)) {
		uint8_t response = NACK;

#ifdef DEBUG_PRINT
		printf("Invalid bootloader command frame rejected. "
				"Packet length: %u\r\n", packetLength);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}
	uint8_t command = messageBuffer[2];

	switch (command) {
	case GET_VERSION:
		handleGetVersion();
		break;
	case GET_HELP:
		handleGetHelp();
		break;
	case GET_ID:
		handleGetID();
		break;
	case READ_MEMORY:
		handleReadMemory();
		break;
	case GO_TO_ADDRESS:
		handleGoToAddress();
		break;
	case WRITE_MEMORY:
		handleWriteMemory();
		break;
	case ERASE:
		handleErase();
		break;
	case WRITE_PROTECT_UNPROTECT:
		handleWriteProtectUnprotect();
		break;
	case READOUT_PROTECT_UNPROTECT:
		handleReadoutProtectUnprotect();
		break;
	case GET_CHECKSUM:
		handleGetChecksum();
		break;
	case RESET_COMMAND:
		handleResetOperation();
		break;
	default:
		handleUnknownCommand();
		break;
	}

}

void handleGetVersion(void) {
	uint8_t response[2] = { ACK, BOOTLOADER_VERSION };

#ifdef DEBUG_PRINT
	printf("Bootloader Version: 0x%02X\r\n"
			"Bootloader Build  : %s\r\n",
	BOOTLOADER_VERSION,
	BOOTLOADER_BUILD_TAG);
#endif

	HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);

}

void handleGetHelp(void) {
	const uint8_t commands[] = {
	GET_HELP,
	GET_VERSION,
	GET_ID,
	READ_MEMORY,
	GO_TO_ADDRESS,
	WRITE_MEMORY,
	ERASE,
	WRITE_PROTECT_UNPROTECT,
	READOUT_PROTECT_UNPROTECT,
	GET_CHECKSUM, RESET_COMMAND };

	const uint8_t totalCommands = (uint8_t) (sizeof(commands)
			/ sizeof(commands[0]));

	uint8_t response[3U + sizeof(commands)] = { 0 };

	response[0] = ACK;
	response[1] = totalCommands;
	response[2] = BOOTLOADER_VERSION;

	memcpy(&response[3], commands, sizeof(commands));

#ifdef DEBUG_PRINT
	printf("Supported bootloader commands:\r\n");

	for (uint32_t i = 0U; i < sizeof(commands); i++) {
		printf("0x%02X ", commands[i]);
	}

	printf("\r\n");
#endif

	HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);
}

void handleGetID(void) {
	uint8_t response[4] = { 0 };
	uint32_t IDCode = DBGMCU->IDCODE;
	uint8_t PIDLSB = IDCode & 0xFF;

	response[0] = ACK;
	response[1] = 0x01;
	response[2] = 0x04;
	response[3] = PIDLSB;

#ifdef DEBUG_PRINT
	printf("Chip ID Message:\r\n");
	for (int i = 0; i < sizeof(response); i++) {
		printf("%02X ", response[i]);
	}
	printf("\r\n");
#endif

	HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);

}

void handleReadMemory(void) {
	uint8_t response = NACK;
	const uint8_t offset = 3U;
	const uint8_t expectedCommandLength = 8U;

	/*
	 * The command length contains:
	 *
	 * Command byte       : 1 byte
	 * Address            : 4 bytes
	 * Address checksum   : 1 byte
	 * N                  : 1 byte
	 * N complement       : 1 byte
	 *
	 * Total              : 8 bytes
	 */
	if ((uint8_t) messageBuffer[1] != expectedCommandLength) {
#ifdef DEBUG_PRINT
		printf("Invalid Read Memory command length: %u\r\n",
				(uint8_t) messageBuffer[1]);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);

		return;
	}

	uint32_t address = ((uint32_t) (uint8_t) messageBuffer[offset] << 24U)
			| ((uint32_t) (uint8_t) messageBuffer[offset + 1U] << 16U)
			| ((uint32_t) (uint8_t) messageBuffer[offset + 2U] << 8U)
			| ((uint32_t) (uint8_t) messageBuffer[offset + 3U]);

	uint8_t receivedAddressChecksum = (uint8_t) messageBuffer[offset + 4U];

	uint8_t calculatedAddressChecksum = (uint8_t) messageBuffer[offset]
			^ (uint8_t) messageBuffer[offset + 1U]
			^ (uint8_t) messageBuffer[offset + 2U]
			^ (uint8_t) messageBuffer[offset + 3U];

	if (receivedAddressChecksum != calculatedAddressChecksum) {
#ifdef DEBUG_PRINT
		printf("Read Memory address checksum error. "
				"Calculated: 0x%02X, Received: 0x%02X\r\n",
				calculatedAddressChecksum, receivedAddressChecksum);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	uint8_t n = (uint8_t) messageBuffer[offset + 5U];

	uint8_t nComplement = (uint8_t) messageBuffer[offset + 6U];

	if ((uint8_t) (n ^ nComplement) != 0xFFU) {
#ifdef DEBUG_PRINT
		printf("Read Memory length complement error. "
				"N: 0x%02X, Complement: 0x%02X\r\n", n, nComplement);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * uint16_t is required because N = 255 represents
	 * a 256-byte read operation.
	 */
	uint16_t numberOfBytes = (uint16_t) n + 1U;

	if (!verifyReadRange(address, (uint32_t) numberOfBytes)) {
#ifdef DEBUG_PRINT
		printf("Read Memory range rejected. "
				"Address: 0x%08lX, Length: %u\r\n", address, numberOfBytes);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	response = ACK;

	HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);

	uint8_t readBuffer[256U];

	memcpy(readBuffer, (const void*) address, numberOfBytes);

	HAL_UART_Transmit(UART_PORT, readBuffer, numberOfBytes, HAL_MAX_DELAY);
}

void handleGoToAddress(void) {
	uint8_t response = NACK;
	const uint8_t offset = 3U;
	const uint8_t expectedCommandLength = 6U;

	/*
	 * The command length contains:
	 *
	 * Command byte     : 1 byte
	 * Address          : 4 bytes
	 * Address checksum : 1 byte
	 *
	 * Total            : 6 bytes
	 */
	if ((uint8_t) messageBuffer[1] != expectedCommandLength) {
#ifdef DEBUG_PRINT
		printf("Invalid Go To Address command length: %u\r\n",
				(uint8_t) messageBuffer[1]);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	uint32_t address = ((uint32_t) (uint8_t) messageBuffer[offset] << 24U)
			| ((uint32_t) (uint8_t) messageBuffer[offset + 1U] << 16U)
			| ((uint32_t) (uint8_t) messageBuffer[offset + 2U] << 8U)
			| ((uint32_t) (uint8_t) messageBuffer[offset + 3U]);

	uint8_t receivedAddressChecksum = (uint8_t) messageBuffer[offset + 4U];

	uint8_t calculatedAddressChecksum = (uint8_t) messageBuffer[offset]
			^ (uint8_t) messageBuffer[offset + 1U]
			^ (uint8_t) messageBuffer[offset + 2U]
			^ (uint8_t) messageBuffer[offset + 3U];

	if (receivedAddressChecksum != calculatedAddressChecksum) {
#ifdef DEBUG_PRINT
		printf("Go To Address address checksum error. "
				"Calculated: 0x%02X, Received: 0x%02X\r\n",
				calculatedAddressChecksum, receivedAddressChecksum);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	uint32_t initialStackPointer = 0U;
	uint32_t resetHandlerAddress = 0U;

	/*
	 * Validate the complete application vector table before
	 * acknowledging the command.
	 */
	if (!validateApplicationVectorTable(address, &initialStackPointer,
			&resetHandlerAddress)) {
#ifdef DEBUG_PRINT
		printf("Go To Address rejected: invalid application image "
				"at 0x%08lX.\r\n", address);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	response = ACK;

	if (HAL_UART_Transmit(UART_PORT, &response, 1U, 1000U) != HAL_OK) {
		return;
	}

	/*
	 * Ensure that ACK has completely left the UART peripheral
	 * before the UART is deinitialized.
	 */
	while (__HAL_UART_GET_FLAG(
			UART_PORT,
			UART_FLAG_TC) == 0U) {
		/* Wait for transmission completion. */
	}

	/*
	 * This call does not return when the application is valid.
	 */
	(void) JumpToApplication(address);
}

void handleWriteMemory(void) {
	uint8_t response = NACK;

	const uint8_t offset = 3U;
	const uint8_t expectedCommandLength = 14U;

	if (messageBuffer[1] != expectedCommandLength) {
#ifdef DEBUG_PRINT
		printf("Invalid Write Memory command length: %u\r\n", messageBuffer[1]);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);

		return;
	}

	uint32_t startAddress = readUint32BigEndian(&messageBuffer[offset]);

	uint8_t receivedAddressChecksum = messageBuffer[offset + 4U];

	uint8_t calculatedAddressChecksum = messageBuffer[offset]
			^ messageBuffer[offset + 1U] ^ messageBuffer[offset + 2U]
			^ messageBuffer[offset + 3U];

	if (receivedAddressChecksum != calculatedAddressChecksum) {
#ifdef DEBUG_PRINT
		printf("Write Memory address checksum error. "
				"Calculated: 0x%02X, Received: 0x%02X\r\n",
				calculatedAddressChecksum, receivedAddressChecksum);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);

		return;
	}

	uint32_t totalLength = readUint32BigEndian(&messageBuffer[offset + 5U]);

	uint32_t expectedImageCrc = readUint32BigEndian(
			&messageBuffer[offset + 9U]);

	if (totalLength == 0U) {
#ifdef DEBUG_PRINT
		printf("Write Memory rejected: image length is zero.\r\n");
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);

		return;
	}

	if (!verifyWriteRange(startAddress, totalLength)) {
#ifdef DEBUG_PRINT
		printf("Write Memory range rejected. "
				"Address: 0x%08lX, Length: %lu\r\n", startAddress, totalLength);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);

		return;
	}

#ifdef DEBUG_PRINT
	printf("Write Memory request accepted.\r\n"
			"Address      : 0x%08lX\r\n"
			"Length       : %lu\r\n"
			"Expected CRC : 0x%08lX\r\n", startAddress, totalLength,
			expectedImageCrc);
#endif

	/*
	 * Notify the host that the bootloader is ready for
	 * the first firmware block.
	 */
	response = ACK;

	if (HAL_UART_Transmit(UART_PORT, &response, 1U, WRITE_COMMAND_TIMEOUT_MS)
			!= HAL_OK) {
		return;
	}

	uint32_t currentAddress = startAddress;
	uint32_t bytesWritten = 0U;

	while (bytesWritten < totalLength) {
		uint8_t n = 0U;

		if (HAL_UART_Receive(UART_PORT, &n, 1U, WRITE_BLOCK_TIMEOUT_MS)
				!= HAL_OK) {
#ifdef DEBUG_PRINT
			printf("Timeout or UART error while waiting "
					"for block length.\r\n");
#endif

			response = NACK;

			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);

			return;
		}

		uint16_t blockLength = (uint16_t) n + 1U;

		uint32_t remainingImageLength = totalLength - bytesWritten;

		if ((uint32_t) blockLength > remainingImageLength) {
#ifdef DEBUG_PRINT
			printf("Invalid firmware block length. "
					"Block: %u, Remaining: %lu\r\n", blockLength,
					remainingImageLength);
#endif

			response = NACK;

			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);

			return;
		}

		/*
		 * Buffer layout:
		 *
		 * Data   : up to 256 bytes
		 * CRC-32 : 4 bytes
		 */
		uint8_t blockBuffer[256U + BOOTLOADER_CRC_SIZE] = { 0 };

		uint16_t receivedBlockLength = blockLength + BOOTLOADER_CRC_SIZE;

		if (HAL_UART_Receive(UART_PORT, blockBuffer, receivedBlockLength,
		WRITE_BLOCK_TIMEOUT_MS) != HAL_OK) {
#ifdef DEBUG_PRINT
			printf("Timeout or UART error while receiving "
					"firmware block.\r\n");
#endif

			response = NACK;

			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);

			return;
		}

		uint32_t receivedBlockCrc = readUint32BigEndian(
				&blockBuffer[blockLength]);

		uint32_t calculatedBlockCrc = calculateBlockCrc32(n, blockBuffer,
				blockLength);

		if (receivedBlockCrc != calculatedBlockCrc) {
#ifdef DEBUG_PRINT
			printf("Firmware block CRC-32 error. "
					"Calculated: 0x%08lX, Received: 0x%08lX\r\n",
					calculatedBlockCrc, receivedBlockCrc);
#endif

			response = NACK;

			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);

			return;
		}

		if (flashWrite(currentAddress, blockBuffer, blockLength) != HAL_OK) {
#ifdef DEBUG_PRINT
			printf("Flash programming failed at address "
					"0x%08lX.\r\n", currentAddress);
#endif

			response = NACK;

			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);

			return;
		}

		/*
		 * Verify the block directly from Flash before accepting it.
		 */
		if (memcmp((const void*) currentAddress, blockBuffer, blockLength)
				!= 0) {
#ifdef DEBUG_PRINT
			printf("Flash readback verification failed at "
					"address 0x%08lX.\r\n", currentAddress);
#endif

			response = NACK;

			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);

			return;
		}

		currentAddress += blockLength;
		bytesWritten += blockLength;

#ifdef DEBUG_PRINT
		printf("Firmware block verified. "
				"Progress: %lu / %lu bytes\r\n", bytesWritten, totalLength);
#endif

		if (bytesWritten < totalLength) {
			response = ACK;

			if (HAL_UART_Transmit(UART_PORT, &response, 1U,
			WRITE_COMMAND_TIMEOUT_MS) != HAL_OK) {
				return;
			}

			continue;
		}

		/*
		 * All blocks have been written. Recalculate CRC-32 from
		 * the actual Flash contents instead of the receive buffer.
		 */
		uint32_t actualImageCrc = calculateCrc32((const uint8_t*) startAddress,
				totalLength);

#ifdef DEBUG_PRINT
		printf("Firmware image verification completed.\r\n"
				"Expected CRC : 0x%08lX\r\n"
				"Actual CRC   : 0x%08lX\r\n", expectedImageCrc, actualImageCrc);
#endif

		if (actualImageCrc != expectedImageCrc) {
			response = NACK;

#ifdef DEBUG_PRINT
			printf("Firmware image CRC-32 mismatch.\r\n");
#endif
		} else {
			response = WRITE_COMPLETE;

#ifdef DEBUG_PRINT
			printf("Firmware image CRC-32 verified successfully.\r\n");
#endif
		}

		HAL_UART_Transmit(UART_PORT, &response, 1U, WRITE_COMMAND_TIMEOUT_MS);

		return;
	}
}

void handleErase(void) {
	uint8_t response = NACK;
	const uint8_t offset = 3U;

	uint8_t n = (uint8_t) messageBuffer[offset];

	/*
	 * 0xFF normally represents a Mass Erase command.
	 * Mass Erase is not supported because it would also erase
	 * the sectors containing the bootloader.
	 */
	if (n == 0xFFU) {
#ifdef DEBUG_PRINT
		printf("Mass Erase rejected: bootloader sectors are protected.\r\n");
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * According to the protocol:
	 *
	 * Sector count = N + 1
	 *
	 * Only sectors 2 through 7 belong to the application.
	 * Therefore, a maximum of six sectors may be requested.
	 */
	uint16_t sectorCount = (uint16_t) n + 1U;

	if ((sectorCount == 0U) || (sectorCount > APPLICATION_SECTOR_COUNT)) {
#ifdef DEBUG_PRINT
		printf("Invalid erase sector count: %u\r\n",
				(unsigned int) sectorCount);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * Checksum calculation:
	 *
	 * N XOR Sector1 XOR Sector2 XOR ...
	 */
	uint8_t calculatedChecksum = n;

	for (uint16_t i = 0U; i < sectorCount; i++) {
		calculatedChecksum ^= (uint8_t) messageBuffer[offset + 1U + i];
	}

	uint8_t receivedChecksum =
			(uint8_t) messageBuffer[offset + 1U + sectorCount];

	if (calculatedChecksum != receivedChecksum) {
#ifdef DEBUG_PRINT
		printf("Erase checksum error. Calculated: 0x%02X, Received: 0x%02X\r\n",
				calculatedChecksum, receivedChecksum);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * Validate all requested sector numbers before unlocking
	 * or modifying the flash memory.
	 *
	 * For example, if the packet contains:
	 *
	 * Sector 2, Sector 3, Sector 0
	 *
	 * the complete request is rejected before any sector is erased.
	 * This prevents partial erase operations caused by an invalid
	 * sector appearing later in the packet.
	 */
	uint32_t selectedSectorMask = 0U;

	for (uint16_t i = 0U; i < sectorCount; i++) {
		uint8_t sectorNumber = (uint8_t) messageBuffer[offset + 1U + i];

		/*
		 * Only application sectors are allowed.
		 *
		 * Sector 0 and Sector 1 contain the bootloader and
		 * must never be erased by this command.
		 */
		if ((sectorNumber < APPLICATION_FIRST_SECTOR)
				|| (sectorNumber > APPLICATION_LAST_SECTOR)) {
#ifdef DEBUG_PRINT
			printf("Erase rejected for protected or invalid sector: %u\r\n",
					sectorNumber);
#endif

			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
			return;
		}

		uint32_t sectorBit = (1UL << sectorNumber);

		/*
		 * Reject packets containing the same sector more than once.
		 */
		if ((selectedSectorMask & sectorBit) != 0U) {
#ifdef DEBUG_PRINT
			printf("Duplicate sector rejected: %u\r\n", sectorNumber);
#endif

			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
			return;
		}

		selectedSectorMask |= sectorBit;
	}

	if (HAL_FLASH_Unlock() != HAL_OK) {
#ifdef DEBUG_PRINT
		printf("Flash unlock failed.\r\n");
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	FLASH_EraseInitTypeDef eraseInit = { 0 };
	uint32_t sectorError = 0xFFFFFFFFUL;

	eraseInit.TypeErase = FLASH_TYPEERASE_SECTORS;
	eraseInit.VoltageRange = FLASH_VOLTAGE_RANGE_3;
	eraseInit.NbSectors = 1U;

	for (uint16_t i = 0U; i < sectorCount; i++) {
		uint8_t sectorNumber = (uint8_t) messageBuffer[offset + 1U + i];

		eraseInit.Sector = sectorNumber;

#ifdef DEBUG_PRINT
		printf("Erasing application sector: %u\r\n", sectorNumber);
#endif

		if (HAL_FLASHEx_Erase(&eraseInit, &sectorError) != HAL_OK) {
#ifdef DEBUG_PRINT
			printf("Sector erase failed. Requested: %u, Error sector: %lu\r\n",
					sectorNumber, sectorError);
#endif

			HAL_FLASH_Lock();

			response = NACK;
			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
			return;
		}
	}

	HAL_FLASH_Lock();

	response = ACK;
	HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
}

void handleWriteProtectUnprotect(void) {
	const uint8_t offset = 3U;
	const uint8_t expectedCommandLength = 3U;
	uint8_t protectionMode = getFlashProtectionMode();
	uint8_t activeProtectionMask = getActiveProtectionMask(protectionMode);
	uint8_t rawSectorBits = getRawProtectionSectorBits();
	uint32_t optcrSnapshot = FLASH->OPTCR;
	uint32_t sprmodMask = FLASH_OPTCR_SPRMOD;
	uint8_t sprmodBit = ((optcrSnapshot & sprmodMask) != 0U) ? 1U : 0U;

	if (messageBuffer[1] != expectedCommandLength) {
#ifdef DEBUG_PRINT
		printf("Invalid protection command length: %u\r\n", messageBuffer[1]);
#endif

		(void) sendWriteProtectionResponse(NACK, protectionMode,
				activeProtectionMask);
		return;
	}

	uint8_t operation = messageBuffer[offset];
	uint8_t requestedProtectedMask = messageBuffer[offset + 1U];

#ifdef DEBUG_PRINT
	printf("Flash protection request.\r\n"
			"Operation        : 0x%02X\r\n"
			"Protection mode  : 0x%02X\r\n"
			"SPRMOD           : %u\r\n"
			"SPRMOD mask      : 0x%08lX\r\n"
			"Raw nWRP bits    : 0x%02X\r\n"
			"Active mask      : 0x%02X\r\n"
			"Requested mask   : 0x%02X\r\n"
			"OPTCR            : 0x%08lX\r\n", operation, protectionMode,
			sprmodBit, sprmodMask, rawSectorBits, activeProtectionMask,
			requestedProtectedMask, optcrSnapshot);
#endif

	/*
	 * Status is always readable. In PCROP mode, the returned mask denotes
	 * PCROP-protected sectors instead of write-protected sectors.
	 */
	if (operation == WRP_OPERATION_GET_STATUS) {
		if (requestedProtectedMask != 0U) {
#ifdef DEBUG_PRINT
			printf("Protection status request rejected: "
					"mask field must be zero.\r\n");
#endif

			(void) sendWriteProtectionResponse(NACK, protectionMode,
					activeProtectionMask);
			return;
		}

#ifdef DEBUG_PRINT
		printf("Protection status returned. "
				"Mode: 0x%02X, Mask: 0x%02X\r\n", protectionMode,
				activeProtectionMask);
#endif

		(void) sendWriteProtectionResponse(ACK, protectionMode,
				activeProtectionMask);
		return;
	}

	if (operation != WRP_OPERATION_SET_MASK) {
#ifdef DEBUG_PRINT
		printf("Unsupported protection operation: 0x%02X\r\n", operation);
#endif

		(void) sendWriteProtectionResponse(NACK, protectionMode,
				activeProtectionMask);
		return;
	}

	/*
	 * HAL's WRP enable/disable interpretation is not valid in PCROP mode.
	 * Do not modify the option bytes while SPRMOD selects PCROP.
	 */
	if (protectionMode != FLASH_PROTECTION_MODE_WRP) {
#ifdef DEBUG_PRINT
		printf("Write-protection update rejected because "
				"the device is in PCROP mode.\r\n");
#endif

		(void) sendWriteProtectionResponse(NACK, protectionMode,
				activeProtectionMask);
		return;
	}

	uint8_t currentProtectedMask = activeProtectionMask;

	/*
	 * Sector 0 and Sector 1 contain the bootloader and must remain
	 * write-protected in every accepted target mask.
	 */
	if ((requestedProtectedMask & WRP_BOOTLOADER_SECTOR_MASK)
			!= WRP_BOOTLOADER_SECTOR_MASK) {
#ifdef DEBUG_PRINT
		printf("Write-protection request rejected because "
				"the bootloader sectors must remain protected.\r\n");
#endif

		(void) sendWriteProtectionResponse(NACK,
		FLASH_PROTECTION_MODE_WRP, currentProtectedMask);
		return;
	}

	if (requestedProtectedMask == currentProtectedMask) {
#ifdef DEBUG_PRINT
		printf("Requested write-protection mask is already active.\r\n");
#endif

		(void) sendWriteProtectionResponse(ACK,
		FLASH_PROTECTION_MODE_WRP, currentProtectedMask);
		return;
	}

	uint8_t sectorsToProtect = (uint8_t) (requestedProtectedMask
			& (uint8_t) (~currentProtectedMask));

	uint8_t sectorsToUnprotect = (uint8_t) (currentProtectedMask
			& (uint8_t) (~requestedProtectedMask));

	sectorsToProtect &= WRP_VALID_SECTOR_MASK;
	sectorsToUnprotect &= WRP_APPLICATION_SECTOR_MASK;

#ifdef DEBUG_PRINT
	printf("Sectors to protect   : 0x%02X\r\n"
			"Sectors to unprotect : 0x%02X\r\n", sectorsToProtect,
			sectorsToUnprotect);
#endif

	if (HAL_FLASH_Unlock() != HAL_OK) {
#ifdef DEBUG_PRINT
		printf("Flash unlock failed during write-protection update.\r\n");
#endif

		(void) sendWriteProtectionResponse(NACK,
		FLASH_PROTECTION_MODE_WRP, currentProtectedMask);
		return;
	}

	if ((READ_BIT(FLASH->OPTCR, FLASH_OPTCR_OPTLOCK) != 0U)
			&& (HAL_FLASH_OB_Unlock() != HAL_OK)) {
#ifdef DEBUG_PRINT
		printf("Option-byte unlock failed during "
				"write-protection update.\r\n");
#endif

		HAL_FLASH_Lock();
		(void) sendWriteProtectionResponse(NACK,
		FLASH_PROTECTION_MODE_WRP, currentProtectedMask);
		return;
	}

	__HAL_FLASH_CLEAR_FLAG(
			FLASH_FLAG_EOP | FLASH_FLAG_OPERR | FLASH_FLAG_WRPERR | FLASH_FLAG_PGAERR | FLASH_FLAG_PGPERR | FLASH_FLAG_PGSERR);

	if (stageWriteProtectionChange(sectorsToProtect,
	OB_WRPSTATE_ENABLE) != HAL_OK) {
#ifdef DEBUG_PRINT
		printf("Could not stage write protection. "
				"Flash error: 0x%08lX\r\n", HAL_FLASH_GetError());
#endif

		HAL_FLASH_OB_Lock();
		HAL_FLASH_Lock();
		sendWriteProtectionResponseAndReset(NACK,
		FLASH_PROTECTION_MODE_WRP, currentProtectedMask);
	}

	if (stageWriteProtectionChange(sectorsToUnprotect,
	OB_WRPSTATE_DISABLE) != HAL_OK) {
#ifdef DEBUG_PRINT
		printf("Could not stage write unprotection. "
				"Flash error: 0x%08lX\r\n", HAL_FLASH_GetError());
#endif

		HAL_FLASH_OB_Lock();
		HAL_FLASH_Lock();
		sendWriteProtectionResponseAndReset(NACK,
		FLASH_PROTECTION_MODE_WRP, currentProtectedMask);
	}

	uint8_t stagedProtectedMask = getActiveProtectionMask(
	FLASH_PROTECTION_MODE_WRP);

	if (stagedProtectedMask != requestedProtectedMask) {
#ifdef DEBUG_PRINT
		printf("Staged write-protection mask mismatch. "
				"Expected: 0x%02X, Staged: 0x%02X\r\n", requestedProtectedMask,
				stagedProtectedMask);
#endif

		HAL_FLASH_OB_Lock();
		HAL_FLASH_Lock();
		sendWriteProtectionResponseAndReset(NACK,
		FLASH_PROTECTION_MODE_WRP, currentProtectedMask);
	}

	if (HAL_FLASH_OB_Launch() != HAL_OK) {
#ifdef DEBUG_PRINT
		printf("Write-protection option-byte launch failed. "
				"Flash error: 0x%08lX\r\n", HAL_FLASH_GetError());
#endif

		uint8_t reportedMask = getActiveProtectionMask(
		FLASH_PROTECTION_MODE_WRP);

		HAL_FLASH_OB_Lock();
		HAL_FLASH_Lock();
		sendWriteProtectionResponseAndReset(NACK,
		FLASH_PROTECTION_MODE_WRP, reportedMask);
	}

	uint8_t programmedProtectedMask = getActiveProtectionMask(
	FLASH_PROTECTION_MODE_WRP);

	HAL_FLASH_OB_Lock();
	HAL_FLASH_Lock();

	if (programmedProtectedMask != requestedProtectedMask) {
#ifdef DEBUG_PRINT
		printf("Programmed write-protection mask mismatch. "
				"Expected: 0x%02X, Programmed: 0x%02X\r\n",
				requestedProtectedMask, programmedProtectedMask);
#endif

		sendWriteProtectionResponseAndReset(NACK,
		FLASH_PROTECTION_MODE_WRP, programmedProtectedMask);
	}

#ifdef DEBUG_PRINT
	printf("Write-protection mask programmed successfully: "
			"0x%02X\r\n", programmedProtectedMask);
#endif

	sendWriteProtectionResponseAndReset(ACK,
	FLASH_PROTECTION_MODE_WRP, programmedProtectedMask);
}

void handleReadoutProtectUnprotect(void) {
	uint8_t response = NACK;
	const uint8_t offset = 3U;
	const uint8_t expectedCommandLength = 2U;

	/*
	 * The length field contains:
	 *
	 * Command byte + one RDP request byte
	 */
	if ((uint8_t) messageBuffer[1] != expectedCommandLength) {
#ifdef DEBUG_PRINT
		printf("Invalid RDP command length.\r\n");
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	uint8_t requestedLevel = (uint8_t) messageBuffer[offset];

	/*
	 * Only RDP Level 0 and Level 1 requests are supported.
	 * RDP Level 2 is intentionally rejected because it is irreversible.
	 */
	if ((requestedLevel != RDP_REQUEST_LEVEL_0)
			&& (requestedLevel != RDP_REQUEST_LEVEL_1)) {
#ifdef DEBUG_PRINT
		printf("Unsupported RDP request: 0x%02X\r\n", requestedLevel);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	FLASH_OBProgramInitTypeDef optionBytes = { 0 };

	HAL_FLASHEx_OBGetConfig(&optionBytes);

	uint8_t currentLevel;

	/*
	 * Any value other than Level 0 and Level 2 represents
	 * RDP Level 1 on STM32F4 devices.
	 */
	if ((uint8_t) optionBytes.RDPLevel == OB_RDP_LEVEL_0) {
		currentLevel = RDP_REQUEST_LEVEL_0;
	} else if ((uint8_t) optionBytes.RDPLevel == OB_RDP_LEVEL_2) {
		currentLevel = 2U;
	} else {
		currentLevel = RDP_REQUEST_LEVEL_1;
	}

#ifdef DEBUG_PRINT
	printf("Current RDP level: %u, Requested RDP level: %u\r\n", currentLevel,
			requestedLevel);
#endif

	/*
	 * No option-byte changes are possible after RDP Level 2
	 * has been enabled.
	 */
	if (currentLevel == 2U) {
#ifdef DEBUG_PRINT
		printf("RDP request rejected: Level 2 is permanently active.\r\n");
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * A Level 1 to Level 0 regression causes a complete Flash
	 * mass erase, including the bootloader sectors.
	 */
	if ((currentLevel == RDP_REQUEST_LEVEL_1)
			&& (requestedLevel == RDP_REQUEST_LEVEL_0)) {
#ifdef DEBUG_PRINT
		printf("RDP Level 1 to Level 0 regression rejected because "
				"it would mass-erase the entire Flash.\r\n");
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * Acknowledge the request without modifying the option bytes
	 * when the requested level is already active.
	 */
	if (currentLevel == requestedLevel) {
#ifdef DEBUG_PRINT
		printf("Requested RDP level is already active.\r\n");
#endif

		response = ACK;
		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * The only permitted state transition at this point is:
	 *
	 * RDP Level 0 -> RDP Level 1
	 */
	optionBytes.OptionType = OPTIONBYTE_RDP;
	optionBytes.RDPLevel = OB_RDP_LEVEL_1;

	if (HAL_FLASH_Unlock() != HAL_OK) {
#ifdef DEBUG_PRINT
		printf("Flash unlock failed during RDP configuration.\r\n");
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	if (HAL_FLASH_OB_Unlock() != HAL_OK) {
#ifdef DEBUG_PRINT
		printf("Option-byte unlock failed.\r\n");
#endif

		HAL_FLASH_Lock();

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * Prepare the requested RDP value in the option control register.
	 * The actual option-byte programming starts when
	 * HAL_FLASH_OB_Launch() is called.
	 */
	if (HAL_FLASHEx_OBProgram(&optionBytes) != HAL_OK) {
#ifdef DEBUG_PRINT
		printf("RDP option-byte configuration failed. Flash error: 0x%08lX\r\n",
				HAL_FLASH_GetError());
#endif

		HAL_FLASH_OB_Lock();
		HAL_FLASH_Lock();

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * Send ACK before starting option-byte programming.
	 *
	 * Enabling RDP Level 1 can terminate an active debug session
	 * or require a reset before normal execution continues.
	 * Therefore, no UART response should be expected after
	 * HAL_FLASH_OB_Launch().
	 */
	response = ACK;

	if (HAL_UART_Transmit(
	UART_PORT, &response, 1U, 1000U) != HAL_OK) {
		HAL_FLASH_OB_Lock();
		HAL_FLASH_Lock();
		return;
	}

	/*
	 * Wait until the UART shift register has completely transmitted
	 * the ACK byte before starting the option-byte operation.
	 */
	while (__HAL_UART_GET_FLAG(UART_PORT, UART_FLAG_TC) == 0U) {
		/* Wait for transmission completion. */
	}

	HAL_Delay(20U);

	/*
	 * Start the actual option-byte programming operation.
	 *
	 * Code execution after this call must not be required for
	 * protocol completion because the target may reset or an
	 * active debug connection may be terminated.
	 */
	HAL_StatusTypeDef launchStatus = HAL_FLASH_OB_Launch();

	if (launchStatus != HAL_OK) {
#ifdef DEBUG_PRINT
		printf("Option-byte launch failed. Flash error: 0x%08lX\r\n",
				HAL_FLASH_GetError());
#endif

		HAL_FLASH_OB_Lock();
		HAL_FLASH_Lock();
		return;
	}

	HAL_FLASH_OB_Lock();
	HAL_FLASH_Lock();

	/*
	 * Reload the new option-byte configuration.
	 */
	HAL_Delay(20U);
	HAL_NVIC_SystemReset();
}

void handleGetChecksum(void) {
	uint8_t response[BOOTLOADER_CRC_RESPONSE_SIZE] = { 0 };

	const uint8_t offset = 3U;

	/*
	 * Payload:
	 *
	 * Address : 4 bytes
	 * Length  : 4 bytes
	 */
	uint32_t address = readUint32BigEndian(&messageBuffer[offset]);

	uint32_t length = readUint32BigEndian(&messageBuffer[offset + 4U]);

	/*
	 * This command is intended for application image
	 * verification, so access is restricted to the
	 * application Flash area.
	 */
	if (!verifyWriteRange(address, length)) {
#ifdef DEBUG_PRINT
		printf("Get Checksum range rejected. "
				"Address: 0x%08lX, Length: %lu\r\n", address, length);
#endif

		response[0] = NACK;

		HAL_UART_Transmit(UART_PORT, response, 1U, HAL_MAX_DELAY);

		return;
	}

	uint32_t crc = calculateCrc32((const uint8_t*) address, length);

	response[0] = ACK;

	writeUint32BigEndian(&response[1], crc);

#ifdef DEBUG_PRINT
	printf("Application CRC-32 calculated. "
			"Address: 0x%08lX, Length: %lu, CRC: 0x%08lX\r\n", address, length,
			crc);
#endif

	HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);
}

HAL_StatusTypeDef flashWrite(uint32_t address, const uint8_t *data,
		uint32_t dataLength) {
	if ((data == NULL) || (dataLength == 0U)) {
		return HAL_ERROR;
	}

	if (!verifyWriteRange(address, dataLength)) {
		return HAL_ERROR;
	}

	if (HAL_FLASH_Unlock() != HAL_OK) {
		return HAL_ERROR;
	}

	uint32_t currentAddress = address;
	uint32_t dataIndex = 0U;
	uint32_t remainingLength = dataLength;

	/*
	 * Program individual bytes until the destination address
	 * becomes 32-bit aligned.
	 */
	while (((currentAddress & 0x03U) != 0U) && (remainingLength > 0U)) {
		if (HAL_FLASH_Program(FLASH_TYPEPROGRAM_BYTE, currentAddress,
				data[dataIndex]) != HAL_OK) {
			HAL_FLASH_Lock();
			return HAL_ERROR;
		}

		currentAddress++;
		dataIndex++;
		remainingLength--;
	}

	/*
	 * Program complete 32-bit words whenever possible.
	 */
	while (remainingLength >= 4U) {
		uint32_t word = ((uint32_t) data[dataIndex])
				| ((uint32_t) data[dataIndex + 1U] << 8U)
				| ((uint32_t) data[dataIndex + 2U] << 16U)
				| ((uint32_t) data[dataIndex + 3U] << 24U);

		if (HAL_FLASH_Program(FLASH_TYPEPROGRAM_WORD, currentAddress, word)
				!= HAL_OK) {
			HAL_FLASH_Lock();
			return HAL_ERROR;
		}

		currentAddress += 4U;
		dataIndex += 4U;
		remainingLength -= 4U;
	}

	/*
	 * Program the final one to three bytes.
	 */
	while (remainingLength > 0U) {
		if (HAL_FLASH_Program(FLASH_TYPEPROGRAM_BYTE, currentAddress,
				data[dataIndex]) != HAL_OK) {
			HAL_FLASH_Lock();
			return HAL_ERROR;
		}

		currentAddress++;
		dataIndex++;
		remainingLength--;
	}

	HAL_FLASH_Lock();

	return HAL_OK;
}

uint8_t isRangeInsideMemoryRegion(uint32_t address, uint32_t length,
		uint32_t regionStart, uint32_t regionEnd) {
	/*
	 * Zero-length memory operations are not valid.
	 */
	if (length == 0U) {
		return 0U;
	}

	/*
	 * Validate the starting address before performing
	 * any arithmetic with the requested length.
	 */
	if ((address < regionStart) || (address > regionEnd)) {
		return 0U;
	}

	/*
	 * Avoid calculating:
	 *
	 * address + length - 1
	 *
	 * directly because that expression may overflow uint32_t.
	 *
	 * The range is valid when the requested length minus one
	 * fits inside the number of bytes remaining in the region.
	 */
	if ((length - 1U) > (regionEnd - address)) {
		return 0U;
	}

	return 1U;
}

uint8_t verifyReadRange(uint32_t address, uint32_t length) {
	/*
	 * Reading is permitted from internal Flash memory.
	 */
	if (isRangeInsideMemoryRegion(address, length, FLASH_MEMORY_START_ADDRESS,
	FLASH_MEMORY_END_ADDRESS)) {
		return 1U;
	}

	/*
	 * SRAM1 and SRAM2 form one contiguous address range
	 * on the STM32F446RE.
	 */
	if (isRangeInsideMemoryRegion(address, length, SRAM_MEMORY_START_ADDRESS,
	SRAM_MEMORY_END_ADDRESS)) {
		return 1U;
	}

	/*
	 * Backup SRAM is validated as a separate memory region.
	 */
	if (isRangeInsideMemoryRegion(address, length, BACKUP_SRAM_START_ADDRESS,
	BACKUP_SRAM_END_ADDRESS)) {
		return 1U;
	}

	return 0U;
}

uint8_t verifyWriteRange(uint32_t address, uint32_t length) {
	/*
	 * Firmware programming is restricted to the application area.
	 *
	 * Sector 0 and Sector 1 contain the bootloader and must never
	 * be modified through the Write Memory command.
	 */
	return isRangeInsideMemoryRegion(address, length, APPLICATION_START_ADDRESS,
	APPLICATION_END_ADDRESS);
}

uint8_t verifyGoAddress(uint32_t address) {
	/*
	 * The current project uses a single application image whose
	 * vector table starts at APPLICATION_START_ADDRESS.
	 *
	 * Arbitrary function addresses are intentionally rejected.
	 */
	if (address != APPLICATION_START_ADDRESS) {
		return 0U;
	}

	/*
	 * A vector table must be word-aligned and must contain at
	 * least the initial stack pointer and reset handler entries.
	 */
	if ((address & 0x03U) != 0U) {
		return 0U;
	}

	return isRangeInsideMemoryRegion(address, 8U, APPLICATION_START_ADDRESS,
	APPLICATION_END_ADDRESS);
}

uint8_t validateApplicationVectorTable(uint32_t applicationAddress,
		uint32_t *initialStackPointer, uint32_t *resetHandlerAddress) {
	if ((initialStackPointer == NULL) || (resetHandlerAddress == NULL)) {
		return 0U;
	}

	/*
	 * The current project supports a single application image
	 * located at APPLICATION_START_ADDRESS.
	 */
	if (!verifyGoAddress(applicationAddress)) {
		return 0U;
	}

	/*
	 * The Cortex-M4 vector table must satisfy the VTOR
	 * alignment requirement.
	 */
	if ((applicationAddress & (APPLICATION_VECTOR_ALIGNMENT - 1UL)) != 0U) {
		return 0U;
	}

	/*
	 * The vector table must contain at least:
	 *
	 * Entry 0: Initial stack pointer
	 * Entry 1: Reset handler address
	 */
	if (!isRangeInsideMemoryRegion(applicationAddress,
	APPLICATION_VECTOR_ENTRY_SIZE, APPLICATION_START_ADDRESS,
	APPLICATION_END_ADDRESS)) {
		return 0U;
	}

	uint32_t stackPointer = *((volatile uint32_t*) applicationAddress);

	uint32_t resetHandler = *((volatile uint32_t*) (applicationAddress + 4UL));

	/*
	 * Erased Flash normally contains 0xFFFFFFFF.
	 */
	if ((stackPointer == 0xFFFFFFFFUL) || (resetHandler == 0xFFFFFFFFUL)
			|| (stackPointer == 0UL) || (resetHandler == 0UL)) {
		return 0U;
	}

	/*
	 * The initial stack pointer must be located inside SRAM.
	 *
	 * SRAM_STACK_TOP_ADDRESS is accepted because the initial
	 * stack pointer normally starts at the first address above
	 * the allocated SRAM region.
	 */
	if ((stackPointer < SRAM_MEMORY_START_ADDRESS)
			|| (stackPointer > SRAM_STACK_TOP_ADDRESS)) {
		return 0U;
	}

	/*
	 * The ARM procedure call standard requires an 8-byte aligned
	 * stack at public interfaces.
	 */
	if ((stackPointer & 0x07UL) != 0UL) {
		return 0U;
	}

	/*
	 * Cortex-M function addresses must have the Thumb bit set.
	 */
	if ((resetHandler & 0x01UL) == 0UL) {
		return 0U;
	}

	/*
	 * Remove the Thumb bit before validating the physical
	 * Reset Handler address.
	 */
	uint32_t resetHandlerCodeAddress = resetHandler & ~0x01UL;

	if (!isRangeInsideMemoryRegion(resetHandlerCodeAddress, 2UL,
	APPLICATION_START_ADDRESS, APPLICATION_END_ADDRESS)) {
		return 0U;
	}

	*initialStackPointer = stackPointer;
	*resetHandlerAddress = resetHandler;

	return 1U;
}

void handleResetOperation(void) {
	counterTest++;
	HAL_NVIC_SystemReset();
}

uint8_t calculateCRC(const uint8_t *data, uint16_t startIndex, uint16_t length) {
	if ((data == NULL) || (length == 0U)) {
		return 0U;
	}

	uint8_t checksum = 0U;

	for (uint16_t i = 0U; i < length; i++) {
		checksum ^= data[startIndex + i];
	}

	return checksum;
}

void handleUnknownCommand(void) {
	uint8_t response[1] = { 0 };

	response[0] = UNKNOWN;
	HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);
}

typedef void (*ApplicationEntryPoint_t)(void);

HAL_StatusTypeDef JumpToApplication(uint32_t applicationAddress) {
	uint32_t initialStackPointer = 0U;
	uint32_t resetHandlerAddress = 0U;

	if (!validateApplicationVectorTable(applicationAddress,
			&initialStackPointer, &resetHandlerAddress)) {
#ifdef DEBUG_PRINT
		printf("Application validation failed at address 0x%08lX.\r\n",
				applicationAddress);
#endif

		return HAL_ERROR;
	}

	ApplicationEntryPoint_t applicationEntryPoint =
			(ApplicationEntryPoint_t) resetHandlerAddress;

#ifdef DEBUG_PRINT
	printf("Application validation successful.\r\n"
			"Initial MSP: 0x%08lX\r\n"
			"Reset Handler: 0x%08lX\r\n", initialStackPointer,
			resetHandlerAddress);
#endif

	/*
	 * Stop the interrupt-based UART reception before disabling
	 * interrupts globally.
	 *
	 * A synchronous abort is used intentionally. The asynchronous
	 * abort callback normally restarts bootloader reception, which
	 * is not desired during an application jump.
	 */
	(void) HAL_UART_AbortReceive(UART_PORT);

	/*
	 * Wait until any pending UART transmission has completely
	 * left the shift register.
	 */
	while (__HAL_UART_GET_FLAG(
			UART_PORT,
			UART_FLAG_TC) == 0U) {
		/* Wait for UART transmission completion. */
	}

	/*
	 * Prevent interrupts from running while the bootloader
	 * execution environment is being removed.
	 */
	__disable_irq();

	/*
	 * Stop the SysTick timer inherited from the bootloader.
	 */
	SysTick->CTRL = 0U;
	SysTick->LOAD = 0U;
	SysTick->VAL = 0U;

	/*
	 * Disable all external interrupts and clear all pending
	 * external interrupt requests.
	 */
	for (uint32_t index = 0U;
			index < (sizeof(NVIC->ICER) / sizeof(NVIC->ICER[0])); index++) {
		NVIC->ICER[index] = 0xFFFFFFFFUL;
		NVIC->ICPR[index] = 0xFFFFFFFFUL;
	}

	/*
	 * Deinitialize peripherals and restore the clock tree to
	 * its reset-compatible state.
	 */
	(void) HAL_UART_DeInit(UART_PORT);
	(void) HAL_RCC_DeInit();
	(void) HAL_DeInit();

	/*
	 * Select the application vector table before interrupts
	 * are enabled again.
	 */
	SCB->VTOR = applicationAddress;

	__DSB();
	__ISB();

	/*
	 * Ensure that the application starts in privileged Thread
	 * mode using the Main Stack Pointer.
	 */
	__set_CONTROL(0U);
	__set_PSP(0U);

	__DSB();
	__ISB();

	/*
	 * All NVIC interrupts are disabled and pending requests have
	 * been cleared, so interrupts can safely be enabled before
	 * loading the application MSP.
	 */
	__enable_irq();

	/*
	 * No normal C function should be called after changing MSP.
	 * Branch directly to the application Reset Handler.
	 */
	__set_MSP(initialStackPointer);

	applicationEntryPoint();

	/*
	 * A valid Reset Handler must never return.
	 */
	while (1) {
	}
}
