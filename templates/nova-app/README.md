# NOVA ABI v1 application template

This is a freestanding ARM ELF32 application starter. It exports nova_app_main, uses only the NOVA ABI service table, and links with ENTRY(nova_app_main) so the IDE package writer can verify the entry symbol.

## Build prerequisites
- arm-none-eabi-gcc
- CMake and Ninja
- The final NOVA firmware linker map for the selected board

## Critical memory-map requirement
Do not guess the application RAM window. The CMake project intentionally fails unless NOVA_APP_RAM_ORIGIN and NOVA_APP_RAM_LENGTH are supplied. These must refer to a dedicated region reserved by the matching NOVA firmware linker layout. The firmware currently does not load .nova packages, and the final reserved region is not yet established. Do not deploy this ELF for execution until the loader and memory map are complete.

## Build
The IDE recognizes nova-app.toolchain.cmake and configures the selected RP2040 or RP2350 target. Set the application RAM origin and length in the IDE fields after the firmware map is available. The output is nova_app.elf and nova_app.map.

This template is freestanding and has no C library, startup code or dynamic relocation support. Applications must use the supplied ABI table for display, buttons, storage, yield and cooperative stop requests.
