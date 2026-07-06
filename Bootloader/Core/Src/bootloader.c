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

void handleGetHelp(void){
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

	uint8_t response [1 + 1 + 1 + sizeof(commands)] = {0};

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
