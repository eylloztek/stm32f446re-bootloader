/* USER CODE BEGIN Header */
/**
 ******************************************************************************
 * @file           : main.c
 * @brief          : Main program body
 ******************************************************************************
 * @attention
 *
 * Copyright (c) 2026 STMicroelectronics.
 * All rights reserved.
 *
 * This software is licensed under terms that can be found in the LICENSE file
 * in the root directory of this software component.
 * If no LICENSE file comes with this software, it is provided AS-IS.
 *
 ******************************************************************************
 */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include "main.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */
#include "string.h"
#include "stdio.h"
#include "bootloader.h"
/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */
typedef enum {
	BOOTLOADER_PARSER_WAIT_HEADER = 0,
	BOOTLOADER_PARSER_WAIT_LENGTH,
	BOOTLOADER_PARSER_WAIT_FRAME
} BootloaderParserState_t;
/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */
#define APP_START_ADDRESS 				0x08008000UL
#define APP_STACK_POINTER				*(__IO uint32_t*) APP_START_ADDRESS
#define APP_RESET_HANDLER				*(__IO uint32_t*) (APP_START_ADDRESS+4)

/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/
UART_HandleTypeDef huart2;

/* USER CODE BEGIN PV */

char message[] = "Going to Application...\r\n";
uint8_t rxChar = 0U;
uint8_t messageBuffer[BOOTLOADER_RX_BUFFER_SIZE] = { 0 };

volatile uint16_t bufferIndex = 0U;
volatile uint16_t expectedPacketLength = 0U;

volatile uint8_t commandReady = 0U;
volatile uint8_t parserErrorReady = 0U;

volatile uint32_t lastReceivedByteTick = 0U;

volatile BootloaderParserState_t parserState = BOOTLOADER_PARSER_WAIT_HEADER;

uint8_t counterTest = 0U;

/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
static void MX_GPIO_Init(void);
static void MX_USART2_UART_Init(void);
/* USER CODE BEGIN PFP */
int _write(int file, char *ptr, int len);
static void BootloaderParser_Reset(void);
static HAL_StatusTypeDef BootloaderUart_StartReception(void);
static void BootloaderParser_CheckTimeout(void);
void uartSend(const char *message);
/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */

int _write(int file, char *ptr, int len) {
	int i = 0;
	for (i = 0; i < len; i++) {
		ITM_SendChar((*ptr++));
	}
	return len;
}

static void BootloaderParser_Reset(void) {
	bufferIndex = 0U;
	expectedPacketLength = 0U;

	parserState = BOOTLOADER_PARSER_WAIT_HEADER;
}

static HAL_StatusTypeDef BootloaderUart_StartReception(void) {
	return HAL_UART_Receive_IT(UART_PORT, &rxChar, 1U);
}

static void BootloaderParser_CheckTimeout(void) {
	/*
	 * No timeout check is required while waiting for a new header
	 * or while a complete command is waiting to be processed.
	 */
	if ((parserState == BOOTLOADER_PARSER_WAIT_HEADER)
			|| (commandReady != 0U)) {
		return;
	}

	uint32_t elapsedTime = HAL_GetTick() - lastReceivedByteTick;

	if (elapsedTime <= BOOTLOADER_FRAME_TIMEOUT_MS) {
		return;
	}

#ifdef DEBUG_PRINT
	printf("UART command frame timeout. "
			"Received bytes: %u, Expected bytes: %u\r\n",
			(unsigned int) bufferIndex, (unsigned int) expectedPacketLength);
#endif

	/*
	 * Abort the incomplete interrupt-based reception before
	 * starting a new command frame.
	 */
	(void) HAL_UART_AbortReceive(UART_PORT);

	BootloaderParser_Reset();

	parserErrorReady = 1U;

	(void) BootloaderUart_StartReception();
}

void uartSend(const char *message) {

	if (message == NULL) {
		return;
	}

	HAL_UART_Transmit(UART_PORT, (const uint8_t*) message, strlen(message),
	HAL_MAX_DELAY);
}

/* USER CODE END 0 */

/**
 * @brief  The application entry point.
 * @retval int
 */
int main(void) {

	/* USER CODE BEGIN 1 */

	/* USER CODE END 1 */

	/* MCU Configuration--------------------------------------------------------*/

	/* Reset of all peripherals, Initializes the Flash interface and the Systick. */
	HAL_Init();

	/* USER CODE BEGIN Init */

	/* USER CODE END Init */

	/* Configure the system clock */
	SystemClock_Config();

	/* USER CODE BEGIN SysInit */

	/* USER CODE END SysInit */

	/* Initialize all configured peripherals */
	MX_GPIO_Init();
	MX_USART2_UART_Init();
	/* USER CODE BEGIN 2 */
	BootloaderParser_Reset();

	if (BootloaderUart_StartReception() != HAL_OK) {
		Error_Handler();
	}
	/* USER CODE END 2 */

	/* Infinite loop */
	/* USER CODE BEGIN WHILE */
	while (1) {
		/* USER CODE END WHILE */

		/* USER CODE BEGIN 3 */
		if (HAL_GPIO_ReadPin(button_GPIO_Port, button_Pin) == GPIO_PIN_RESET) {

			HAL_Delay(20U);

			if (HAL_GPIO_ReadPin(button_GPIO_Port, button_Pin)
					== GPIO_PIN_RESET) {
				uint32_t initialStackPointer = 0U;
				uint32_t resetHandlerAddress = 0U;

				if (validateApplicationVectorTable(APPLICATION_START_ADDRESS,
						&initialStackPointer, &resetHandlerAddress)) {
					uartSend(message);

#ifdef DEBUG_PRINT
					printf("Going to Application...\r\n");
#endif

					(void) JumpToApplication(APPLICATION_START_ADDRESS);
				} else {
					uartSend("No valid application image was found.\r\n");

#ifdef DEBUG_PRINT
					printf("Application jump rejected: "
							"invalid vector table.\r\n");
#endif
				}

				/*
				 * Wait for the button to be released so that an invalid
				 * application does not cause repeated messages.
				 */
				while (HAL_GPIO_ReadPin(button_GPIO_Port, button_Pin)
						== GPIO_PIN_RESET) {
					HAL_Delay(10U);
				}
			}
		}
		BootloaderParser_CheckTimeout();

		if (parserErrorReady != 0U) {
			uint8_t response = NACK;

			parserErrorReady = 0U;

			HAL_UART_Transmit(UART_PORT, &response, 1U, 1000U);
		}

		if (commandReady != 0U) {
			/*
			 * Reception is not rearmed after a complete packet, so the
			 * packet length and buffer remain stable during processing.
			 */
			uint16_t packetLength = bufferIndex;

			commandReady = 0U;

			processBootloaderCommand(packetLength);

			/*
			 * A successful Go command or option-byte operation may not
			 * return from processBootloaderCommand().
			 */
			BootloaderParser_Reset();

			if (BootloaderUart_StartReception() != HAL_OK) {
#ifdef DEBUG_PRINT
				printf("UART command reception could not be restarted.\r\n");
#endif

				parserErrorReady = 1U;
			}
		}
	}
	/* USER CODE END 3 */
}

/**
 * @brief System Clock Configuration
 * @retval None
 */
void SystemClock_Config(void) {
	RCC_OscInitTypeDef RCC_OscInitStruct = { 0 };
	RCC_ClkInitTypeDef RCC_ClkInitStruct = { 0 };

	/** Configure the main internal regulator output voltage
	 */
	__HAL_RCC_PWR_CLK_ENABLE();
	__HAL_PWR_VOLTAGESCALING_CONFIG(PWR_REGULATOR_VOLTAGE_SCALE3);

	/** Initializes the RCC Oscillators according to the specified parameters
	 * in the RCC_OscInitTypeDef structure.
	 */
	RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_HSI;
	RCC_OscInitStruct.HSIState = RCC_HSI_ON;
	RCC_OscInitStruct.HSICalibrationValue = RCC_HSICALIBRATION_DEFAULT;
	RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;
	RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSI;
	RCC_OscInitStruct.PLL.PLLM = 16;
	RCC_OscInitStruct.PLL.PLLN = 336;
	RCC_OscInitStruct.PLL.PLLP = RCC_PLLP_DIV4;
	RCC_OscInitStruct.PLL.PLLQ = 2;
	RCC_OscInitStruct.PLL.PLLR = 2;
	if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK) {
		Error_Handler();
	}

	/** Initializes the CPU, AHB and APB buses clocks
	 */
	RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK | RCC_CLOCKTYPE_SYSCLK
			| RCC_CLOCKTYPE_PCLK1 | RCC_CLOCKTYPE_PCLK2;
	RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_PLLCLK;
	RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;
	RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV2;
	RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV1;

	if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_2) != HAL_OK) {
		Error_Handler();
	}
}

/**
 * @brief USART2 Initialization Function
 * @param None
 * @retval None
 */
static void MX_USART2_UART_Init(void) {

	/* USER CODE BEGIN USART2_Init 0 */

	/* USER CODE END USART2_Init 0 */

	/* USER CODE BEGIN USART2_Init 1 */

	/* USER CODE END USART2_Init 1 */
	huart2.Instance = USART2;
	huart2.Init.BaudRate = 115200;
	huart2.Init.WordLength = UART_WORDLENGTH_8B;
	huart2.Init.StopBits = UART_STOPBITS_1;
	huart2.Init.Parity = UART_PARITY_NONE;
	huart2.Init.Mode = UART_MODE_TX_RX;
	huart2.Init.HwFlowCtl = UART_HWCONTROL_NONE;
	huart2.Init.OverSampling = UART_OVERSAMPLING_16;
	if (HAL_UART_Init(&huart2) != HAL_OK) {
		Error_Handler();
	}
	/* USER CODE BEGIN USART2_Init 2 */

	/* USER CODE END USART2_Init 2 */

}

/**
 * @brief GPIO Initialization Function
 * @param None
 * @retval None
 */
static void MX_GPIO_Init(void) {
	GPIO_InitTypeDef GPIO_InitStruct = { 0 };
	/* USER CODE BEGIN MX_GPIO_Init_1 */

	/* USER CODE END MX_GPIO_Init_1 */

	/* GPIO Ports Clock Enable */
	__HAL_RCC_GPIOC_CLK_ENABLE();
	__HAL_RCC_GPIOA_CLK_ENABLE();

	/*Configure GPIO pin Output Level */
	HAL_GPIO_WritePin(led_GPIO_Port, led_Pin, GPIO_PIN_RESET);

	/*Configure GPIO pin : button_Pin */
	GPIO_InitStruct.Pin = button_Pin;
	GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
	GPIO_InitStruct.Pull = GPIO_PULLUP;
	HAL_GPIO_Init(button_GPIO_Port, &GPIO_InitStruct);

	/*Configure GPIO pin : led_Pin */
	GPIO_InitStruct.Pin = led_Pin;
	GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
	GPIO_InitStruct.Pull = GPIO_NOPULL;
	GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_HIGH;
	HAL_GPIO_Init(led_GPIO_Port, &GPIO_InitStruct);

	/* USER CODE BEGIN MX_GPIO_Init_2 */

	/* USER CODE END MX_GPIO_Init_2 */
}

/* USER CODE BEGIN 4 */
void HAL_UART_RxCpltCallback(UART_HandleTypeDef *huart) {
	if ((huart == NULL) || (huart->Instance != USART2)) {
		return;
	}

	lastReceivedByteTick = HAL_GetTick();

	switch (parserState) {
	case BOOTLOADER_PARSER_WAIT_HEADER: {
		/*
		 * Ignore all bytes until a valid bootloader header
		 * is received.
		 */
		if (rxChar == BOOTLOADER_HEADER) {
			messageBuffer[0] = rxChar;
			bufferIndex = 1U;

			parserState = BOOTLOADER_PARSER_WAIT_LENGTH;
		}

		break;
	}

	case BOOTLOADER_PARSER_WAIT_LENGTH: {
		/*
		 * The length field contains:
		 *
		 * Command byte + payload bytes
		 */
		if ((rxChar < BOOTLOADER_MIN_COMMAND_LENGTH)
				|| (rxChar > BOOTLOADER_MAX_COMMAND_LENGTH)) {
#ifdef DEBUG_PRINT
			printf("Invalid command length received: %u\r\n", rxChar);
#endif

			parserErrorReady = 1U;

			BootloaderParser_Reset();
			break;
		}

		messageBuffer[1] = rxChar;
		bufferIndex = 2U;

		/*
		 * Complete frame:
		 *
		 * Header + Length + Command body + CRC-32
		 */
		expectedPacketLength = (uint16_t) rxChar + BOOTLOADER_FRAME_OVERHEAD;

		parserState = BOOTLOADER_PARSER_WAIT_FRAME;

		break;
	}

	case BOOTLOADER_PARSER_WAIT_FRAME: {
		if (bufferIndex >= BOOTLOADER_RX_BUFFER_SIZE) {
#ifdef DEBUG_PRINT
			printf("UART command buffer overflow rejected.\r\n");
#endif

			parserErrorReady = 1U;

			BootloaderParser_Reset();
			break;
		}

		messageBuffer[bufferIndex] = rxChar;

		bufferIndex++;

		if (bufferIndex == expectedPacketLength) {
			/*
			 * Stop rearming UART reception until the main loop
			 * has processed the complete command frame.
			 */
			commandReady = 1U;
			return;
		}

		if (bufferIndex > expectedPacketLength) {
#ifdef DEBUG_PRINT
			printf("UART command frame exceeded expected length.\r\n");
#endif

			parserErrorReady = 1U;

			BootloaderParser_Reset();
		}

		break;
	}

	default: {
		parserErrorReady = 1U;

		BootloaderParser_Reset();
		break;
	}
	}

	if (commandReady == 0U) {
		if (BootloaderUart_StartReception() != HAL_OK) {
			parserErrorReady = 1U;
		}
	}
}

void HAL_UART_ErrorCallback(UART_HandleTypeDef *huart) {
	if ((huart == NULL) || (huart->Instance != USART2)) {
		return;
	}

	uint32_t errorCode = huart->ErrorCode;

	if (errorCode == HAL_UART_ERROR_NONE) {
		return;
	}

#ifdef DEBUG_PRINT
	printf("UART receive error: 0x%08lX\r\n", errorCode);
#endif

	parserErrorReady = 1U;

	/*
	 * Abort the failed interrupt-based reception.
	 * Reception is restarted from the abort completion callback.
	 */
	if (HAL_UART_AbortReceive_IT(huart) != HAL_OK) {
		BootloaderParser_Reset();

		if (BootloaderUart_StartReception() != HAL_OK) {
#ifdef DEBUG_PRINT
			printf("UART reception could not be restarted "
					"after an error.\r\n");
#endif
		}
	}
}

void HAL_UART_AbortReceiveCpltCallback(UART_HandleTypeDef *huart) {
	if ((huart == NULL) || (huart->Instance != USART2)) {
		return;
	}

	BootloaderParser_Reset();

	(void) BootloaderUart_StartReception();
}
/* USER CODE END 4 */

/**
 * @brief  This function is executed in case of error occurrence.
 * @retval None
 */
void Error_Handler(void) {
	/* USER CODE BEGIN Error_Handler_Debug */
	/* User can add his own implementation to report the HAL error return state */
	__disable_irq();
	while (1) {
	}
	/* USER CODE END Error_Handler_Debug */
}
#ifdef USE_FULL_ASSERT
/**
  * @brief  Reports the name of the source file and the source line number
  *         where the assert_param error has occurred.
  * @param  file: pointer to the source file name
  * @param  line: assert_param error line source number
  * @retval None
  */
void assert_failed(uint8_t *file, uint32_t line)
{
  /* USER CODE BEGIN 6 */
  /* User can add his own implementation to report the file name and line number,
     ex: printf("Wrong parameters value: file %s on line %d\r\n", file, line) */
  /* USER CODE END 6 */
}
#endif /* USE_FULL_ASSERT */
