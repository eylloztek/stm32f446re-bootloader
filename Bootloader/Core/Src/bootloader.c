/*
 * bootloader.c
 *
 *  Created on: Jul 3, 2026
 *      Author: Eylül Öztek
 */

#include "bootloader.h"

extern char messageBuffer[BUFFER_SIZE];
extern uint8_t bufferIndex;

void processBootloaderCommand(void){
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

void handleGetVersion(void){

}
