#ifndef NOVA_APP_ABI_H
#define NOVA_APP_ABI_H
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#define NOVA_APP_ABI_VERSION 1u
#define NOVA_APP_ENTRY_SYMBOL "nova_app_main"
#define NOVA_APP_PACKAGE_MAX_BYTES (2u * 1024u * 1024u)
typedef enum { NOVA_APP_STOPPED=0, NOVA_APP_VALIDATING, NOVA_APP_READY, NOVA_APP_RUNNING, NOVA_APP_STOPPING, NOVA_APP_FAILED } nova_app_state_t;
typedef struct {
    uint32_t abi_version;
    uint32_t struct_size;
    uint32_t (*uptime_ms)(void);
    int (*display_text)(uint16_t x,uint16_t y,const char *text,uint16_t rgb565);
    int (*storage_mount)(void);
    int (*storage_unmount)(void);
    int (*storage_read)(const char *path,void *buffer,size_t capacity,size_t *read_bytes);
    int (*storage_write_atomic)(const char *path,const void *data,size_t length);
    void (*yield)(void);
    bool (*stop_requested)(void);
} nova_app_api_v1_t;
typedef int (*nova_app_entry_v1_t)(const nova_app_api_v1_t *api);
const nova_app_api_v1_t *nova_app_api_v1(void);
void nova_app_request_stop(void);
void nova_app_reset_stop(void);
bool nova_app_stop_requested(void);
#endif
