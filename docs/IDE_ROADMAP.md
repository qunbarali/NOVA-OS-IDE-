# NOVA IDE roadmap

## Alpha 0 — Windows shell and build integration
- WPF .NET 8 desktop application
- RP2040 and RP2350 Cortex-M33 selection
- Local CMake/Ninja/ARM GCC/Pico SDK detection
- Open a NOVA project folder and run configure/build
- Compiler output and status display

## Alpha 1 — NOVA application SDK
- Versioned application manifest and ABI headers
- Project templates and API browser
- ELF inspection and memory map validation
- Deterministic .nova package builder and verifier
- WPF package creation and inspection UI
- ABI v1 draft header and freestanding ARM ELF app template with linker script
- IDE app build mode that requires explicit reserved RAM origin/length from firmware map
- Strict 2 MiB package-size limit

## Alpha 2 — deployment
- USB CDC management protocol
- SD card package copy and install
- Firmware UF2 update/recovery kept separate from .nova application deployment

## Alpha 3 — developer experience
- Source editor, diagnostics and symbol navigation
- Serial monitor and crash reports
- Resource/font/image conversion tools
- Build profiles and reproducible toolchain versions

The ABI v1 header and package format exist, but arbitrary ELF/BIN execution remains disabled until the firmware loader, relocation policy, target-specific linker memory map, lifecycle supervisor and memory-safety policy are implemented and validated. UF2 is firmware transport, not an application executable.
