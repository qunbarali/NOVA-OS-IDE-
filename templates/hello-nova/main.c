#include <stdio.h>
#include "pico/stdlib.h"

int main(void) {
    stdio_init_all();
    while (true) {
        puts("Hello from a NOVA-compatible Pico application template.");
        sleep_ms(1000u);
    }
}
