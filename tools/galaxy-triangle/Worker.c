/* Изолированный Triangle1.6 worker. Upstream source не модифицируется. */
#define _CRT_SECURE_NO_WARNINGS
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include <math.h>
#include <float.h>
#include <xmmintrin.h>
#include <windows.h>
#define REAL float
#define VOID void
#define ANSI_DECLARATORS
#include "triangle.h"

_Static_assert(sizeof(struct triangulateio) == 0xA8, "Требуется Windows x64 ABI");
_Static_assert(sizeof(float) == 4 && sizeof(int) == 4, "Требуется binary32/int32");

static int write_array(FILE *stream, const void *data, size_t size, size_t count)
{
    return count == 0 || (data != NULL && fwrite(data, size, count, stream) == count);
}

int main(int argc, char **argv)
{
    FILE *input_file, *output_file;
    uint32_t header[4];
    int32_t result_header[8];
    unsigned int control;
    struct triangulateio input = {0}, output = {0}, voronoi = {0};
    float *points;
    size_t values, i;
    int ok;
    char initial[] = "pczAevnQ", rebuild[] = "pczAevn";
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
    if (argc != 3) return 10;
    input_file = fopen(argv[1], "rb");
    if (input_file == NULL) return 11;
    if (fread(header, sizeof(uint32_t), 4, input_file) != 4 ||
        header[0] != 0x31525453 || header[1] != 1 || header[2] < 3 ||
        header[2] > 2047 || header[3] > 1) return 12;
    values = (size_t)header[2] * 2;
    points = (float *)malloc(values * sizeof(float));
    if (points == NULL) return 13;
    if (fread(points, sizeof(float), values, input_file) != values || fgetc(input_file) != EOF) return 14;
    fclose(input_file);
    for (i = 0; i < values; ++i)
        if (!isfinite(points[i]) || points[i] < -50000.0f || points[i] > 50000.0f) return 15;
    if (_controlfp_s(&control, _RC_NEAR, _MCW_RC) != 0) return 16;
    /* MXCSR: nearest, no flush-to-zero/denormals-are-zero; exception masks сохраняются. */
    _mm_setcsr(_mm_getcsr() & ~0xE040u);
    input.pointlist = points;
    input.numberofpoints = (int)header[2];
    triangulate(header[3] == 0 ? initial : rebuild, &input, &output, &voronoi);
    if (output.numberofpoints != (int)header[2] || output.numberofcorners != 3 ||
        output.numberoftriangles < 0 || output.numberofedges < 0 ||
        voronoi.numberofpoints != output.numberoftriangles ||
        voronoi.numberofedges != output.numberofedges) return 17;
    output_file = fopen(argv[2], "wb");
    if (output_file == NULL) return 18;
    result_header[0] = 0x31525453;
    result_header[1] = 1;
    result_header[2] = output.numberofpoints;
    result_header[3] = output.numberoftriangles;
    result_header[4] = output.numberofedges;
    result_header[5] = voronoi.numberofpoints;
    result_header[6] = (int32_t)_mm_getcsr() & 0xE040;
    result_header[7] = (int32_t)sizeof(struct triangulateio);
    ok = write_array(output_file, result_header, sizeof(int32_t), 8) &&
         write_array(output_file, output.pointlist, sizeof(float), values) &&
         write_array(output_file, output.trianglelist, sizeof(int), (size_t)output.numberoftriangles * 3) &&
         write_array(output_file, output.neighborlist, sizeof(int), (size_t)output.numberoftriangles * 3) &&
         write_array(output_file, output.edgelist, sizeof(int), (size_t)output.numberofedges * 2) &&
         write_array(output_file, voronoi.pointlist, sizeof(float), (size_t)voronoi.numberofpoints * 2) &&
         write_array(output_file, voronoi.edgelist, sizeof(int), (size_t)voronoi.numberofedges * 2) &&
         write_array(output_file, voronoi.normlist, sizeof(float), (size_t)voronoi.numberofedges * 2);
    if (fclose(output_file) != 0) ok = 0;
    /* Один запрос на процесс: OS освобождает все Triangle pools/output allocations.
       Не вызываем free на alias holelist/regionlist и не делим native globals с host. */
    return ok ? 0 : 19;
}
