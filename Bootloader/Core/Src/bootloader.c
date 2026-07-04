/*
 * bootloader.c
 *
 *  Created on: Jul 3, 2026
 *      Author: Eylül Öztek
 */

#include "bootloader.h"

extern char messageBuffer[BUFFER_SIZE];
extern uint8_t bufferIndex;

void processBootloaderCommand(void) {
	uint8_t command = messageBuffer[2];

	switch (command) {
	case GET_VERSION:
		handleGetVersion();
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
