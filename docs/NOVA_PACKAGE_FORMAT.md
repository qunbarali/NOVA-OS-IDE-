# NOVA application package format — Alpha 1

The IDE's first package library defines a transport/container format only. It does not make arbitrary firmware images safe or runnable as NOVA processes.

## Header
- 8-byte signature: NOVAAPP1
- Format version: uint16 little-endian (1)
- Target: uint8 (1 RP2040, 2 RP2350 Cortex-M33)
- Image kind: uint8 (1 raw BIN, 2 ARM ELF32)
- NOVA ABI version: uint32 little-endian
- Payload length: uint32
- Load address: uint32 (ELF entry point or explicit BIN load address)
- UTF-8 application name: 48 bytes, NUL padded
- UTF-8 application version: 16 bytes, NUL padded
- SHA-256 payload digest: 32 bytes
- Reserved zero bytes to 128-byte header
- Payload bytes

Maximum total package size is 2 MiB. The payload limit is therefore 2 MiB minus 128 bytes.

## Input policy
- ELF: only little-endian ARM ELF32 is accepted by the Alpha 1 packer.
- BIN: requires an explicit non-zero load address.
- UF2: rejected as an application payload. UF2 is reserved for firmware flashing/recovery.
- File extension is only an import hint; the actual ELF format header is checked.

## Runtime safety
The package hash detects corruption; it is not a digital signature and does not authenticate publishers. The current NOVA kernel does not yet implement the matching application loader, relocation policy, memory isolation, ABI, or lifecycle contract. Do not execute packages until the firmware runtime implements and validates those requirements.
