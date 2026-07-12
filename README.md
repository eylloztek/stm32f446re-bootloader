# STM32F446RE UART Bootloader and Flasher

A custom UART bootloader and Windows-based flasher application developed for the STM32F446RE microcontroller.

The bootloader runs on the STM32 and processes commands received over UART. The flasher application is developed in C# using Windows Forms and provides a graphical interface for communicating with the bootloader, reading and writing memory, erasing Flash sectors, and managing Flash protection.

> This project is currently under development.

## Development Status

* ~~UART communication between the STM32 bootloader and the C# flasher~~
* ~~Serial port connection and configuration~~
* ~~Get Bootloader Version command~~
* ~~Get Help command~~
* ~~Get Chip ID command~~
* ~~Read Memory command~~
* ~~Save received memory data as a `.bin` file~~
* ~~Go to Address command~~
* ~~Jump from the bootloader to the user application~~
* ~~Select and load a `.bin` firmware file~~
* ~~Write Memory command~~
* ~~Firmware transfer in data blocks~~
* ~~Flash sector erase~~
* ~~Mass erase~~
* ~~Flash sector write protection and unprotection~~
* Readout protection and unprotection
* UI improvements
* Reset and Exit Boot buttons
* CRC and Unknown Command functions
* Windows `.exe` release
* Firmware and flasher optimization
