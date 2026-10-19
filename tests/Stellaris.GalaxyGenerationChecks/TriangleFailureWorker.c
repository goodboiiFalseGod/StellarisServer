/* Собственный fixture: не содержит Triangle/game code. Только failure boundary. */
#define _CRT_SECURE_NO_WARNINGS
#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <windows.h>

int main(int argc, char **argv)
{
    FILE *output;
    uint32_t header[8] = {0x31525453, 1, 3, 0x7FFFFFFF, 0, 0, 0, 0xA8};
    int i;
    if (argc != 3) return 40;
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
    if (strstr(argv[0], "crash") != NULL) {
        RaiseException(EXCEPTION_ACCESS_VIOLATION, EXCEPTION_NONCONTINUABLE, 0, NULL);
        return 44;
    }
    if (strstr(argv[0], "timeout") != NULL) { Sleep(5000); return 41; }
    if (strstr(argv[0], "corrupt") != NULL) {
        output = fopen(argv[2], "wb");
        if (output == NULL) return 43;
        fwrite(header, sizeof(uint32_t), 8, output);
        fclose(output);
        return 0;
    }
    /* Больше pipe buffer в оба потока: caller должен drain параллельно. */
    for (i = 0; i < 10000; ++i) { fputs("fixture stdout\n", stdout); fputs("fixture stderr\n", stderr); }
    return 42;
}
