# NOVA application starter (ABI v1 draft)

This folder contains the application entry contract used by the NOVA ABI design. It is not yet a runnable application project: the firmware-side ELF loader, linker memory map and lifecycle supervisor are still under development.

An application entry point must use:

```c
int nova_app_main(const nova_app_api_v1_t *api);
```

Check `api->abi_version` and `api->struct_size` before using the service table. Do not build this as a standalone UF2 firmware. See the NOVA-OS repository's APP_RUNTIME_DESIGN.md before targeting hardware.
