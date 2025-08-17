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

#include "spectral_whitening.h"
#include "../configurations.h"
#include <float.h>
#include <math.h>
#include <stdlib.h>
#include <string.h>

struct SpectralWhitening {
  float *residual_max_spectrum;
  float *whitened_residual_spectrum;

  float max_decay_rate;
  uint32_t whitening_window_count;
  uint32_t fft_size;
  uint32_t sample_rate;
  uint32_t hop;
};

SpectralWhitening *spectral_whitening_initialize(const uint32_t fft_size,
                                                 const uint32_t sample_rate,
                                                 const uint32_t hop) {
  SpectralWhitening *self =
      (SpectralWhitening *)calloc(1U, sizeof(SpectralWhitening));

  self->fft_size = fft_size;
  self->sample_rate = sample_rate;
  self->hop = hop;

  self->whitened_residual_spectrum =
      (float *)calloc(self->fft_size, sizeof(float));
  self->residual_max_spectrum = (float *)calloc(self->fft_size, sizeof(float));
  self->max_decay_rate =
      expf(-1000.F / (((WHITENING_DECAY_RATE) * (float)self->sample_rate) /
                      (float)self->hop));
  self->whitening_window_count = 0U;

  return self;
}

void spectral_whitening_free(SpectralWhitening *self) {
  free(self->whitened_residual_spectrum);
  free(self->residual_max_spectrum);

  free(self);
}

bool spectral_whitening_run(SpectralWhitening *self,
                            const float whitening_factor, float *fft_spectrum) {
  if (!self || !fft_spectrum || whitening_factor < 0.F) {
    return false;
  }

  self->whitening_window_count++;

  for (uint32_t k = 1U; k < self->fft_size; k++) {
    if (self->whitening_window_count > 1U) {
      self->residual_max_spectrum[k] =
          fmaxf(fmaxf(fft_spectrum[k], WHITENING_FLOOR),
                self->residual_max_spectrum[k] * self->max_decay_rate);
    } else {
      self->residual_max_spectrum[k] = fmaxf(fft_spectrum[k], WHITENING_FLOOR);
    }
  }

  for (uint32_t k = 1U; k < self->fft_size; k++) {
    if (fft_spectrum[k] > FLT_MIN) {
      self->whitened_residual_spectrum[k] =
          fft_spectrum[k] / self->residual_max_spectrum[k];

      fft_spectrum[k] = (1.F - whitening_factor) * fft_spectrum[k] +
                        whitening_factor * self->whitened_residual_spectrum[k];
    }
  }

  return true;
}