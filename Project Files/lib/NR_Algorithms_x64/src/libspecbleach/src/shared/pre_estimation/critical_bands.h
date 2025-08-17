/*
libspecbleach - A spectral processing library

Copyright 2022 Luciano Dato <lucianodato@gmail.com>

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 2.1 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with this library; if not, write to the Free Software
Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
*/

#ifndef CRITICAL_BANDS_H
#define CRITICAL_BANDS_H

#include <stdbool.h>
#include <stdint.h>

typedef struct CriticalBands CriticalBands;

typedef enum CriticalBandType {
  BARK_SCALE = 0,
  MEL_SCALE = 1,
  OPUS_SCALE = 2,
  OCTAVE_SCALE = 3,
} CriticalBandType;

typedef struct CriticalBandIndexes {
  uint32_t start_position;
  uint32_t end_position;
} CriticalBandIndexes;

CriticalBands *critical_bands_initialize(uint32_t sample_rate,
                                         uint32_t fft_size,
                                         CriticalBandType type);
void critical_bands_free(CriticalBands *self);
bool compute_critical_bands_spectrum(CriticalBands *self, const float *spectrum,
                                     float *critical_bands);
CriticalBandIndexes get_band_indexes(CriticalBands *self, uint32_t band_number);
uint32_t get_number_of_critical_bands(CriticalBands *self);

#endif