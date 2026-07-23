/*
 * bootloader.h
 *
 *  Created on: Jul 3, 2026
 *      Author: Eylül Öztek
 */

#ifndef INC_BOOTLOADER_H_
#define INC_BOOTLOADER_H_

#include "main.h"
#include <string.h>

#ifdef __cplusplus
extern "C" {
#endif

#define BOOTLOADER_HEADER                   0x7FU
#define BOOTLOADER_VERSION                  0x10U

/*
 * Custom bootloader command codes.
 */
#define GET_HELP                            0x00U
#define GET_VERSION                         0x01U
#define GET_ID                              0x02U
#define READ_MEMORY                         0x11U
#define GO_TO_ADDRESS                       0x21U
#define WRITE_MEMORY                        0x31U
#define ERASE                               0x43U
#define WRITE_PROTECT_UNPROTECT             0x63U
#define READOUT_PROTECT_UNPROTECT           0x82U
#define GET_CHECKSUM                        0xA1U
#define RESET_COMMAND                       0x89U

/*
 * Custom bootloader response codes.
 */
#define ACK                                 0x79U
#define WRITE_COMPLETE                      0x7AU
#define NACK                                0x1FU
#define UNKNOWN                             0x99U

/*
 * CRC-32/ISO-HDLC parameters.
 *
 * Normal polynomial  : 0x04C11DB7
 * Reversed polynomial: 0xEDB88320
 * Initial value      : 0xFFFFFFFF
 * Final XOR value    : 0xFFFFFFFF
 */
#define CRC32_REVERSED_POLYNOMIAL            0xEDB88320UL
#define CRC32_INITIAL_VALUE                  0xFFFFFFFFUL
#define CRC32_FINAL_XOR_VALUE                0xFFFFFFFFUL

#define BOOTLOADER_CRC_SIZE                  4U
#define BOOTLOADER_STATUS_RESPONSE_SIZE      1U
#define BOOTLOADER_CRC_RESPONSE_SIZE         5U

/*
 * Maximum number of bytes in the command body.
 *
 * The command body contains:
 * Command byte + payload bytes
 *
 * Firmware data blocks are transferred separately and are not
 * stored inside this command buffer.
 */
#define BOOTLOADER_MAX_COMMAND_LENGTH        32U

/*
 * Complete command frame:
 *
 * Header + Length + Command body + CRC-32
 */
#define BOOTLOADER_FRAME_OVERHEAD            \
    (2U + BOOTLOADER_CRC_SIZE)

#define BOOTLOADER_RX_BUFFER_SIZE            \
    (BOOTLOADER_MAX_COMMAND_LENGTH + BOOTLOADER_FRAME_OVERHEAD)

#define BOOTLOADER_MIN_COMMAND_LENGTH        1U
#define BOOTLOADER_FRAME_TIMEOUT_MS          250U

#define WRITE_COMMAND_TIMEOUT_MS             3000U
#define WRITE_BLOCK_TIMEOUT_MS               5000U

/*
 * Readout protection request codes used by the custom protocol.
 *
 * These values are protocol-level identifiers and are intentionally
 * different from the hardware option-byte values.
 */
#define RDP_REQUEST_LEVEL_0                  0x00U
#define RDP_REQUEST_LEVEL_1                  0x01U

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

/*
 * Write-protection protocol operations.
 */
#define WRP_OPERATION_GET_STATUS            0x00U
#define WRP_OPERATION_SET_MASK              0x01U

/*
 * STM32F446RE contains Flash sectors 0 through 7.
 *
 * Protocol mask:
 * Bit 0 -> Sector 0
 * Bit 1 -> Sector 1
 * ...
 * Bit 7 -> Sector 7
 *
 * The meaning of a set bit depends on the reported protection mode:
 *
 * FLASH_PROTECTION_MODE_WRP:
 *   A set bit means that write protection is active.
 *
 * FLASH_PROTECTION_MODE_PCROP:
 *   A set bit means that PCROP protection is active.
 */
#define WRP_VALID_SECTOR_MASK               0xFFU

/*
 * Sector 0 and Sector 1 contain the bootloader.
 *
 * The custom bootloader protocol never permits these sectors
 * to become unprotected.
 */
#define WRP_BOOTLOADER_SECTOR_MASK          0x03U
#define WRP_APPLICATION_SECTOR_MASK         0xFCU

/*
 * Flash protection modes reported by the custom protocol.
 */
#define FLASH_PROTECTION_MODE_WRP           0x00U
#define FLASH_PROTECTION_MODE_PCROP         0x01U
#define FLASH_PROTECTION_MODE_UNKNOWN       0xFFU

/*
 * Write-protection command response:
 *
 * Byte 0: ACK or NACK
 * Byte 1: Protection mode
 * Byte 2: Active protection mask
 */
#define WRP_RESPONSE_SIZE                    3U

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
void handleGetChecksum(void);
uint32_t calculateCrc32(const uint8_t *data, uint32_t length);
uint8_t isRangeInsideMemoryRegion(uint32_t address, uint32_t length,
		uint32_t regionStart, uint32_t regionEnd);
uint8_t verifyReadRange(uint32_t address, uint32_t length);
uint8_t verifyWriteRange(uint32_t address, uint32_t length);
uint8_t verifyGoAddress(uint32_t address);
uint8_t validateApplicationVectorTable(uint32_t applicationAddress,
		uint32_t *initialStackPointer, uint32_t *resetHandlerAddress);
HAL_StatusTypeDef JumpToApplication(uint32_t applicationAddress);

#ifdef __cplusplus
}
#endif

#endif /* INC_BOOTLOADER_H_ */
