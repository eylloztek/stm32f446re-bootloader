/*
 * bootloader.c
 *
 *  Created on: Jul 3, 2026
 *      Author: Eylül Öztek
 */

#include "bootloader.h"
#include "stdio.h"

extern char messageBuffer[BUFFER_SIZE];
extern uint8_t bufferIndex;
extern uint8_t counterTest;

void processBootloaderCommand(void) {
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
	case RESET:
		handleResetOperation();
		break;
	default:
		handleUnknownCommand();
		break;
	}

	bufferIndex = 0;
	memset(messageBuffer, 0, BUFFER_SIZE);
}

void handleGetVersion(void) {
	uint8_t response[2] = { 0 };

	uint8_t crc = calculateCRC(messageBuffer, 1, messageBuffer[1] + 1);

	if (crc != messageBuffer[3]) {
		response[0] = NACK;
	} else {
		if (BOOTLOADER_VERSION > 0 && BOOTLOADER_VERSION <= 255) {
			response[0] = ACK;
			response[1] = BOOTLOADER_VERSION;
		} else {
			response[0] = NACK;
			response[1] = UNKNOWN;
		}

	}

#ifdef DEBUG_PRINT
	printf("Bootloader Version: 0x%x \r\n", BOOTLOADER_VERSION);
#endif
	HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);

}

void handleGetHelp(void) {
	uint8_t commands[] = {
	GET_HELP, 						//0x00
			GET_VERSION,					//0x01
			GET_ID,							//0x02
			READ_MEMORY,					//0x11
			GO_TO_ADDRESS,					//0x21
			WRITE_MEMORY,					//0x31
			ERASE,							//0x43
			WRITE_PROTECT_UNPROTECT,		//0x63
			READOUT_PROTECT_UNPROTECT,		//0x82
			GET_CHECKSUM,					//0xA1
			};

	uint8_t totalCommands = sizeof(commands) / sizeof(commands[0]);

	uint8_t response[1 + 1 + 1 + sizeof(commands)] = { 0 };

	response[0] = ACK;
	response[1] = totalCommands;
	response[2] = BOOTLOADER_VERSION;
	memcpy(&response[3], commands, totalCommands);

#ifdef DEBUG_PRINT
	printf("Help Messages:\r\n");
	for (int i = 0; i < sizeof(response); i++) {
		printf("%02X ", response[i]);
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
	uint8_t response[1] = { 0 };
	uint8_t offset = 3;

	uint32_t address = (messageBuffer[offset] << 24)
			| (messageBuffer[offset + 1] << 16)
			| (messageBuffer[offset + 2] << 8) | (messageBuffer[offset + 3]);

	uint8_t addressChecksum = messageBuffer[offset + 4];
	uint8_t calculatedChecksum = (messageBuffer[offset])
			^ (messageBuffer[offset + 1]) ^ (messageBuffer[offset + 2])
			^ (messageBuffer[offset + 3]);

	if (addressChecksum != calculatedChecksum) {
		response[0] = NACK;
		HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);
		return;
	}

	uint8_t N = messageBuffer[offset + 5];
	uint8_t NComplement = messageBuffer[offset + 6];

#ifdef DEBUG_PRINT
	printf("N: 0x%02X\r\n", N);
	printf("NComplement: 0x%02X\r\n", NComplement);
	printf("XOR: 0x%02X\r\n", (uint8_t) (N ^ NComplement));
#endif

	if ((uint8_t) (N ^ NComplement) != 0xFF) {
		response[0] = NACK;
		HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);
		return;
	}

	uint8_t addressIsValid = verifyAddress(address);

	if (!addressIsValid) {
		response[0] = NACK;
		HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);
		return;
	}

	response[0] = ACK;
	HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);

	uint8_t numberOfBytes = N + 1;
	uint8_t buffer[256];
	memcpy(buffer, (uint8_t*) address, numberOfBytes);
	HAL_UART_Transmit(UART_PORT, buffer, numberOfBytes, HAL_MAX_DELAY);

}

void handleGoToAddress(void) {
	uint8_t response[1] = { 0 };
	uint8_t offset = 3;

	uint32_t address = (messageBuffer[offset] << 24)
			| (messageBuffer[offset + 1] << 16)
			| (messageBuffer[offset + 2] << 8) | (messageBuffer[offset + 3]);

	uint8_t addressChecksum = messageBuffer[offset + 4];
	uint8_t calculatedChecksum = (messageBuffer[offset])
			^ (messageBuffer[offset + 1]) ^ (messageBuffer[offset + 2])
			^ (messageBuffer[offset + 3]);

	if (addressChecksum != calculatedChecksum) {
		response[0] = NACK;
		HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);
		return;
	}

	uint8_t addressIsValid = verifyAddress(address);

	if (!addressIsValid) {
		response[0] = NACK;
		HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);
		return;
	}

	response[0] = ACK;
	HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);

	typedef void (*Function_Pointer)(void);
	uint32_t sp = *((volatile uint32_t*) address);
	uint32_t pc = *((volatile uint32_t*) (address + 4));

	__set_MSP(sp);

	Function_Pointer app_start = (Function_Pointer) pc;
	app_start();

}

void handleWriteMemory(void) {
	uint8_t response = NACK;
	const uint8_t offset = 3U;
	const uint8_t expectedCommandLength = 10U;

	/*
	 * The command length contains:
	 *
	 * Command byte       : 1 byte
	 * Address            : 4 bytes
	 * Address checksum   : 1 byte
	 * Total image length : 4 bytes
	 *
	 * Total              : 10 bytes
	 */
	if ((uint8_t) messageBuffer[1] != expectedCommandLength) {
#ifdef DEBUG_PRINT
		printf("Invalid Write Memory command length: %u\r\n",
				(uint8_t) messageBuffer[1]);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * Validate the outer command packet checksum.
	 *
	 * Checksum input:
	 * Length XOR Command XOR Payload
	 */
	uint16_t checksumIndex = 2U + (uint16_t) (uint8_t) messageBuffer[1];

	uint8_t calculatedPacketChecksum = calculateCRC(messageBuffer, 1U,
			(uint16_t) (uint8_t) messageBuffer[1] + 1U);

	uint8_t receivedPacketChecksum = (uint8_t) messageBuffer[checksumIndex];

	if (calculatedPacketChecksum != receivedPacketChecksum) {
#ifdef DEBUG_PRINT
		printf("Write Memory packet checksum error. "
				"Calculated: 0x%02X, Received: 0x%02X\r\n",
				calculatedPacketChecksum, receivedPacketChecksum);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * Reconstruct the destination address from the packet.
	 */
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
		printf("Write Memory address checksum error. "
				"Calculated: 0x%02X, Received: 0x%02X\r\n",
				calculatedAddressChecksum, receivedAddressChecksum);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * Reconstruct the complete image length from the packet.
	 */
	uint32_t totalLength = ((uint32_t) (uint8_t) messageBuffer[offset + 5U]
			<< 24U) | ((uint32_t) (uint8_t) messageBuffer[offset + 6U] << 16U)
			| ((uint32_t) (uint8_t) messageBuffer[offset + 7U] << 8U)
			| ((uint32_t) (uint8_t) messageBuffer[offset + 8U]);

	if (totalLength == 0U) {
#ifdef DEBUG_PRINT
		printf("Write Memory rejected: image length is zero.\r\n");
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

	/*
	 * This is currently a basic address check.
	 *
	 * A complete address-and-length range validation will be added
	 * in the dedicated memory protection step.
	 */
	if (!verifyAddress(address)) {
#ifdef DEBUG_PRINT
		printf("Write Memory rejected: invalid start address 0x%08lX.\r\n",
				address);
#endif

		HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
		return;
	}

#ifdef DEBUG_PRINT
	printf("Write Memory request accepted. "
			"Address: 0x%08lX, Length: %lu bytes\r\n", address, totalLength);
#endif

	/*
	 * The interrupt-based command reception is no longer needed
	 * while firmware data blocks are received synchronously.
	 */
	bufferIndex = 0U;
	memset(messageBuffer, 0, BUFFER_SIZE);

	/*
	 * Inform the host that the bootloader is ready to receive
	 * the first firmware data block.
	 */
	response = ACK;

	if (HAL_UART_Transmit(UART_PORT, &response, 1U, WRITE_COMMAND_TIMEOUT_MS) != HAL_OK) {
		return;
	}

	uint32_t bytesWritten = 0U;

	while (bytesWritten < totalLength) {
		uint8_t n = 0U;

		/*
		 * N represents:
		 *
		 * Block data length - 1
		 *
		 * N = 0   -> 1 byte
		 * N = 255 -> 256 bytes
		 */
		if (HAL_UART_Receive(UART_PORT, &n, 1U, WRITE_BLOCK_TIMEOUT_MS) != HAL_OK) {
#ifdef DEBUG_PRINT
			printf("Timeout or UART error while waiting for block length.\r\n");
#endif

			response = NACK;

			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
			return;
		}

		uint16_t blockLength = (uint16_t) n + 1U;

		uint32_t remainingLength = totalLength - bytesWritten;

		/*
		 * A block must never contain more data than the number
		 * of bytes remaining in the image.
		 */
		if ((uint32_t) blockLength > remainingLength) {
#ifdef DEBUG_PRINT
			printf("Invalid block length. Block: %u, Remaining: %lu\r\n",
					blockLength, remainingLength);
#endif

			response = NACK;

			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
			return;
		}

		/*
		 * The buffer contains:
		 *
		 * Data     : up to 256 bytes
		 * Checksum : 1 byte
		 */
		uint8_t blockBuffer[257U] = { 0 };

		if (HAL_UART_Receive(UART_PORT, blockBuffer, blockLength + 1U, WRITE_BLOCK_TIMEOUT_MS) != HAL_OK) {
#ifdef DEBUG_PRINT
			printf("Timeout or UART error while receiving firmware block.\r\n");
#endif

			response = NACK;

			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
			return;
		}

		/*
		 * Block checksum:
		 *
		 * N XOR Data[0] XOR Data[1] XOR ...
		 */
		uint8_t calculatedBlockChecksum = n;

		for (uint16_t i = 0U; i < blockLength; i++) {
			calculatedBlockChecksum ^= blockBuffer[i];
		}

		uint8_t receivedBlockChecksum = blockBuffer[blockLength];

		if (calculatedBlockChecksum != receivedBlockChecksum) {
#ifdef DEBUG_PRINT
			printf("Firmware block checksum error. "
					"Calculated: 0x%02X, Received: 0x%02X\r\n",
					calculatedBlockChecksum, receivedBlockChecksum);
#endif

			response = NACK;

			HAL_UART_Transmit(UART_PORT, &response, 1U, HAL_MAX_DELAY);
			return;
		}

		/*
		 * Write the validated block to Flash memory.
		 */
		if (flashWrite(address, blockBuffer, blockLength) != HAL_OK) {
#ifdef DEBUG_PRINT
			printf("Flash programming failed at address 0x%08lX.\r\n", address);
#endif

			response = NACK;

			HAL_UART_Transmit(UART_PORT, &response, 1U,HAL_MAX_DELAY);
			return;
		}

		address += blockLength;
		bytesWritten += blockLength;

#ifdef DEBUG_PRINT
		printf("Firmware block written. "
				"Progress: %lu / %lu bytes\r\n", bytesWritten, totalLength);
#endif

		/*
		 * Send ACK after every successfully written intermediate block.
		 *
		 * Send WRITE_COMPLETE instead of ACK after the final block.
		 */
		if (bytesWritten == totalLength) {
			response = WRITE_COMPLETE;
		} else {
			response = ACK;
		}

		if (HAL_UART_Transmit(UART_PORT, &response, 1U, WRITE_COMMAND_TIMEOUT_MS) != HAL_OK) {
			return;
		}
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
	uint8_t response[1] = { 0 };
	uint8_t offset = 3;
	uint8_t numSectors = messageBuffer[offset] + 1;
	uint8_t *sectorCodes = (uint8_t*) &messageBuffer[offset + 1];

	HAL_FLASH_OB_Unlock();

	FLASH_OBProgramInitTypeDef obInit;
	HAL_FLASHEx_OBGetConfig(&obInit);

	obInit.OptionType = OPTIONBYTE_WRP;
	obInit.WRPSector = 0xFF; //all sector unprotect
	obInit.WRPState = OB_WRPSTATE_DISABLE;
	HAL_FLASHEx_OBProgram(&obInit);

	uint8_t wrpMask = 0;

	for (uint8_t i = 0; i < numSectors; i++) {
		uint8_t sector = sectorCodes[i];
		if (sector < 8) {
			wrpMask |= (1 << sector);
		}
	}

	if (wrpMask > 0) {
		obInit.OptionType = OPTIONBYTE_WRP;
		obInit.WRPSector = wrpMask;
		obInit.WRPState = OB_WRPSTATE_ENABLE;

		if (HAL_FLASHEx_OBProgram(&obInit) != HAL_OK) {
			response[0] = NACK;
			HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);
			HAL_FLASH_OB_Lock();
		}
	}

	response[0] = ACK;
	HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);
	HAL_FLASH_OB_Launch();
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

	/*
	 * Validate the complete command packet checksum.
	 */
	uint16_t checksumIndex = 2U + (uint16_t) (uint8_t) messageBuffer[1];

	uint8_t calculatedChecksum = calculateCRC(messageBuffer, 1U,
			(uint16_t) (uint8_t) messageBuffer[1] + 1U);

	uint8_t receivedChecksum = (uint8_t) messageBuffer[checksumIndex];

	if (calculatedChecksum != receivedChecksum) {
#ifdef DEBUG_PRINT
		printf(
				"RDP packet checksum error. Calculated: 0x%02X, Received: 0x%02X\r\n",
				calculatedChecksum, receivedChecksum);
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
	while (__HAL_UART_GET_FLAG(UART_PORT, UART_FLAG_TC) == RESET) {
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

HAL_StatusTypeDef flashWrite(uint32_t address, uint8_t *data,
		uint32_t dataLength) {

	HAL_FLASH_Unlock();
	for (uint32_t i = 0; i < dataLength; i++) {
		if (HAL_FLASH_Program(FLASH_TYPEPROGRAM_BYTE, address + i, data[i])
				!= HAL_OK) {
			HAL_FLASH_Lock();
			return HAL_ERROR;
		}
	}
	HAL_FLASH_Lock();

	return HAL_OK;

}

uint8_t verifyAddress(uint32_t address) {

	if ((address >= FLASH_BASE && address <= FLASH_END)
			|| (address >= SRAM1_BASE && address <= SRAM1_END)
			|| (address >= SRAM2_BASE && address <= SRAM2_END)
			|| (address >= BKPSRAM_BASE && address <= BKPSRAM_END)) {

		return 1;
	}
	return 0;

}

void handleResetOperation(void) {
	counterTest++;
	HAL_NVIC_SystemReset();
}

uint8_t calculateCRC(char *data, uint16_t startIndex, uint16_t length) {
	uint8_t crc = 0x00;

	for (uint16_t i = 0; i < length; i++) {
		crc ^= (uint8_t) data[startIndex + i];
	}
	return crc;
}

void handleUnknownCommand(void) {
	uint8_t response[1] = { 0 };

	response[0] = UNKNOWN;
	HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);
}
