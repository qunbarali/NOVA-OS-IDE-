# NOVA OS IDE

Windows desktop development environment for NOVA-OS applications targeting Raspberry Pi Pico (RP2040) and Pico 2 (RP2350 Cortex-M33).

## Status
IDE Alpha 0 scaffold. This repository starts the native Windows WPF application. The current build supports project-folder selection, toolchain discovery, CMake configure/build invocation and build output display. NOVA package creation, application ABI tooling, USB deployment and debugging are planned follow-on milestones.

## Requirements
- Windows 10/11 x64
- .NET 8 SDK
- CMake and Ninja
- ARM GNU Embedded Toolchain (arm-none-eabi-gcc)
- Raspberry Pi Pico SDK (set PICO_SDK_PATH)

## Build
```powershell
dotnet build .\src\NOVA.IDE\NOVA.IDE.csproj -c Release
dotnet run --project .\src\NOVA.IDE\NOVA.IDE.csproj
```

The IDE does not download or execute toolchains automatically. Select a local NOVA project and configure the toolchain paths explicitly.

## Safety
The IDE does not execute arbitrary ELF/BIN/UF2 files on the PC. Firmware flashing and NOVA application deployment will be separate workflows with target and image validation.
