#include "nova/app_abi.h"

int nova_app_main(const nova_app_api_v1_t *api)
{
    if (api == 0 || api->abi_version != NOVA_APP_ABI_VERSION ||
        api->struct_size < sizeof(nova_app_api_v1_t)) {
        return NOVA_APP_EXIT_ERROR;
    }

    api->display_text(0u, 12u, "NOVA APP READY", 0xFFFFu);
    while (!api->stop_requested()) {
        uint8_t events = api->buttons_take_events();
        if ((events & NOVA_APP_BUTTON_BACK) != 0u) {
            break;
        }
        if ((events & NOVA_APP_BUTTON_SELECT) != 0u) {
            api->display_text(0u, 28u, "SELECT PRESSED", 0x07E0u);
        }
        api->yield();
    }
    return NOVA_APP_EXIT_NORMAL;
}
