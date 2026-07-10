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
	default:
		break;
	}

	bufferIndex = 0;
	memset(messageBuffer, 0, BUFFER_SIZE);
}

void handleGetVersion(void) {
	uint8_t response[2] = { 0 };

	if (BOOTLOADER_VERSION > 0 && BOOTLOADER_VERSION <= 255) {
		response[0] = ACK;
		response[1] = BOOTLOADER_VERSION;
	} else {
		response[0] = NACK;
		response[1] = UNKNOWN;
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
	uint8_t response[1] = { 0 };
	uint8_t offset = 3;
	uint32_t address = (messageBuffer[offset] << 24)
			| (messageBuffer[offset + 1] << 16)
			| (messageBuffer[offset + 2] << 8) | (messageBuffer[offset + 3]);

	uint8_t addressCheckSum = messageBuffer[offset + 4];
	uint8_t calculatedCheckSum = (messageBuffer[offset])
			^ (messageBuffer[offset + 1]) ^ (messageBuffer[offset + 2])
			^ (messageBuffer[offset + 3]);

	if (addressCheckSum != calculatedCheckSum) {
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

	uint32_t totalLength = (messageBuffer[offset + 5] << 24)
			| (messageBuffer[offset + 6] << 16)
			| (messageBuffer[offset + 7] << 8) | (messageBuffer[offset + 8]);

	bufferIndex = 0;
	memset(messageBuffer, 0, BUFFER_SIZE);
	response[0] = ACK;
	HAL_UART_Transmit(UART_PORT, response, sizeof(response), HAL_MAX_DELAY);

	uint32_t offsetData = 0;

	while (offsetData < totalLength) {
		uint8_t N;

		if (HAL_UART_Receive(UART_PORT, &N, 1, HAL_MAX_DELAY) != HAL_OK) {
			response[0] = NACK;
			HAL_UART_Transmit(UART_PORT, response, 1, HAL_MAX_DELAY);
			return;
		}

		uint32_t dataLength = N + 1;

		uint8_t buffer[256 + 1]; // data + checksum
		if (HAL_UART_Receive(UART_PORT, buffer, dataLength + 1, HAL_MAX_DELAY)
				!= HAL_OK) {
			response[0] = NACK;
			HAL_UART_Transmit(UART_PORT, response, 1, HAL_MAX_DELAY);
			return;
		}

		uint8_t calculatedChecksum = N;
		for (uint32_t i = 0; i < dataLength; i++) {
			calculatedChecksum ^= buffer[i];
		}

		uint8_t receivedChecksum = buffer[dataLength];
		if (receivedChecksum != calculatedChecksum) {
			response[0] = NACK;
			HAL_UART_Transmit(UART_PORT, response, 1, HAL_MAX_DELAY);
			return;
		}

		if (flashWrite(address, buffer, dataLength) == HAL_OK) {
			address += dataLength;
			offsetData += dataLength;
			response[0] = ACK;
			HAL_UART_Transmit(UART_PORT, response, 1, HAL_MAX_DELAY);
		} else {
			response[0] = NACK;
			HAL_UART_Transmit(UART_PORT, response, 1, HAL_MAX_DELAY);
			return;
		}
	}
}

void handleErase(void){
	uint8_t response[1] = {0};
	uint8_t offset = 3;
	uint8_t N = messageBuffer[offset];
	uint8_t receivedChecksum = N;
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
