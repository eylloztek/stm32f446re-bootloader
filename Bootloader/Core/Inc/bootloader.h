/*
 * bootloader.h
 *
 *  Created on: Jul 3, 2026
 *      Author: Eylül Öztek
 */

#ifndef INC_BOOTLOADER_H_
#define INC_BOOTLOADER_H_

#include "main.h"
#include "string.h"

#define BOOTLOADER_HEADER 				0x7F
#define APPLICATION_HEADER				0x7E
#define BOOTLOADER_VERSION				0x10

#define GET_HELP							0x00
#define GET_VERSION						0x01
#define GET_ID							0x02
#define READ_MEMORY						0x11
#define GO_TO_ADDRESS					0x21
#define WRITE_MEMORY						0x31
#define ERASE							0x43
#define WRITE_PROTECT_UNPROTECT			0x63
#define READOUT_PROTECT_UNPROTECT		0x82
#define GET_CHECKSUM						0xA1
#define RESET							0x89

#define ACK								0x79
#define WRITE_COMPLETE                  	0x7AU
#define NACK								0x1F
#define UNKNOWN							0x99

#define WRITE_COMMAND_TIMEOUT_MS        	3000U
#define WRITE_BLOCK_TIMEOUT_MS          	5000U

/*
 * Readout protection request codes used by the custom protocol.
 *
 * These values are protocol-level identifiers and are intentionally
 * different from the hardware option-byte values.
 */
#define RDP_REQUEST_LEVEL_0             	0x00U
#define RDP_REQUEST_LEVEL_1             	0x01U

#define SRAM1_END						0x2001BFFFUL
#define SRAM2_END						0x2001FFFFUL
#define BKPSRAM_END						0x40024FFFUL

/*
 * Bootloader:
 *   Sector 0 and Sector 1
 *   0x08000000 - 0x08007FFF
 *
 * Application:
 *   Sector 2 to Sector 7
 *   Starts at 0x08008000
 */

#define APPLICATION_START_ADDRESS       0x08008000UL

#define APPLICATION_FIRST_SECTOR        FLASH_SECTOR_2
#define APPLICATION_LAST_SECTOR         FLASH_SECTOR_7

#define APPLICATION_SECTOR_COUNT        \
    ((APPLICATION_LAST_SECTOR - APPLICATION_FIRST_SECTOR) + 1U)

void processBootloaderCommand(void);
void handleGetVersion(void);
void handleGetHelp(void);
void handleGetID(void);
void handleReadMemory(void);
uint8_t verifyAddress(uint32_t address);
HAL_StatusTypeDef flashWrite(uint32_t address, uint8_t *data,
		uint32_t dataLength);
void handleGoToAddress(void);
void handleWriteMemory(void);
void handleErase(void);
void handleWriteProtectUnprotect(void);
void handleReadoutProtectUnprotect(void);
void handleResetOperation(void);
void handleUnknownCommand(void);
uint8_t calculateCRC(char *data, uint16_t startIndex, uint16_t length);

#endif /* INC_BOOTLOADER_H_ */
