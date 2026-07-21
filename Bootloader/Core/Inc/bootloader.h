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

/*
 * Maximum number of bytes in the command body.
 *
 * The command body contains:
 * Command byte + payload bytes
 *
 * Firmware data blocks are transferred separately and are not
 * stored inside this command buffer.
 */
#define BOOTLOADER_MAX_COMMAND_LENGTH       32U

/*
 * Complete command frame:
 *
 * Header + Length + Command body + Checksum
 */
#define BOOTLOADER_RX_BUFFER_SIZE           \
    (BOOTLOADER_MAX_COMMAND_LENGTH + 3U)

#define BOOTLOADER_MIN_COMMAND_LENGTH       1U
#define BOOTLOADER_FRAME_TIMEOUT_MS         250U

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

/*
 * Bootloader:
 *   Sector 0 and Sector 1
 *   0x08000000 - 0x08007FFF
 *
 * Application:
 *   Sector 2 to Sector 7
 *   Starts at 0x08008000
 */

#define FLASH_MEMORY_START_ADDRESS      0x08000000UL
#define FLASH_MEMORY_END_ADDRESS        0x0807FFFFUL

#define APPLICATION_START_ADDRESS       0x08008000UL
#define APPLICATION_END_ADDRESS         0x0807FFFFUL
#define APPLICATION_MAX_SIZE            \
    ((APPLICATION_END_ADDRESS - APPLICATION_START_ADDRESS) + 1UL)

#define APPLICATION_FIRST_SECTOR        FLASH_SECTOR_2
#define APPLICATION_LAST_SECTOR         FLASH_SECTOR_7

#define APPLICATION_SECTOR_COUNT        \
    ((APPLICATION_LAST_SECTOR - APPLICATION_FIRST_SECTOR) + 1U)

#define SRAM_MEMORY_START_ADDRESS       0x20000000UL
#define SRAM_MEMORY_END_ADDRESS         0x2001FFFFUL

#define BACKUP_SRAM_START_ADDRESS       0x40024000UL
#define BACKUP_SRAM_END_ADDRESS         0x40024FFFUL

/*
 * The initial stack pointer may point one byte beyond the last
 * SRAM address because stacks grow downward.
 */
#define SRAM_STACK_TOP_ADDRESS          \
    (SRAM_MEMORY_END_ADDRESS + 1UL)

#define APPLICATION_VECTOR_ALIGNMENT    0x80UL
#define APPLICATION_VECTOR_ENTRY_SIZE   8UL

void processBootloaderCommand(uint16_t packetLength);
void handleGetVersion(void);
void handleGetHelp(void);
void handleGetID(void);
void handleReadMemory(void);
HAL_StatusTypeDef flashWrite(uint32_t address, const uint8_t *data,
		uint32_t dataLength);
void handleGoToAddress(void);
void handleWriteMemory(void);
void handleErase(void);
void handleWriteProtectUnprotect(void);
void handleReadoutProtectUnprotect(void);
void handleResetOperation(void);
void handleUnknownCommand(void);
uint8_t calculateCRC(const uint8_t *data, uint16_t startIndex, uint16_t length);
uint8_t isRangeInsideMemoryRegion(uint32_t address, uint32_t length,
		uint32_t regionStart, uint32_t regionEnd);
uint8_t verifyReadRange(uint32_t address, uint32_t length);
uint8_t verifyWriteRange(uint32_t address, uint32_t length);
uint8_t verifyGoAddress(uint32_t address);
uint8_t validateApplicationVectorTable(uint32_t applicationAddress,
		uint32_t *initialStackPointer, uint32_t *resetHandlerAddress);
HAL_StatusTypeDef JumpToApplication(uint32_t applicationAddress);

#endif /* INC_BOOTLOADER_H_ */
