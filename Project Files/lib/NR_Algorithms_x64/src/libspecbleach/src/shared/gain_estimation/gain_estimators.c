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

#include "gain_estimators.h"
#include "../configurations.h"
#include "../utils/general_utils.h"
#include <float.h>
#include <math.h>

static void wiener_subtraction(const uint32_t real_spectrum_size,
                               const uint32_t fft_size, const float *spectrum,
                               const float *noise_spectrum,
                               float *gain_spectrum) {
  for (uint32_t k = 1U; k < real_spectrum_size; k++) {
    if (noise_spectrum[k] > FLT_MIN) {
      if (spectrum[k] > noise_spectrum[k]) {
        gain_spectrum[k] = (spectrum[k] - (noise_spectrum[k])) / spectrum[k];
      } else {
        gain_spectrum[k] = 0.F;
      }
      gain_spectrum[fft_size - k] = gain_spectrum[k];
    } else {
      gain_spectrum[k] = 1.F;
      gain_spectrum[fft_size - k] = gain_spectrum[k];
    }
  }
}

static void spectral_gating(const uint32_t real_spectrum_size,
                            const uint32_t fft_size, const float *spectrum,
                            const float *noise_spectrum, float *gain_spectrum) {
  for (uint32_t k = 1U; k < real_spectrum_size; k++) {
    if (noise_spectrum[k] > FLT_MIN) {
      if (spectrum[k] >= noise_spectrum[k]) {
        gain_spectrum[k] = 1.F;
      } else {
        gain_spectrum[k] = 0.F;
      }
      gain_spectrum[fft_size - k] = gain_spectrum[k];
    } else {
      gain_spectrum[k] = 1.F;
      gain_spectrum[fft_size - k] = gain_spectrum[k];
    }
  }
}

static void generalized_spectral_subtraction(
    const uint32_t real_spectrum_size, const uint32_t fft_size,
    const float *spectrum, const float *noise_spectrum, float *gain_spectrum,
    const float *alpha, const float *beta) {
  for (uint32_t k = 1U; k < real_spectrum_size; k++) {
    if (spectrum[k] > FLT_MIN) {
      if (powf((noise_spectrum[k] / spectrum[k]), GSS_EXPONENT) <
          (1.F / (alpha[k] + beta[k]))) {
        gain_spectrum[k] =
            fmaxf(powf(1.F - (alpha[k] * powf((noise_spectrum[k] / spectrum[k]),
                                              GSS_EXPONENT)),
                       1.F / GSS_EXPONENT),
                  0.F);
      } else {
        gain_spectrum[k] =
            fmaxf(powf(beta[k] * powf((noise_spectrum[k] / spectrum[k]),
                                      GSS_EXPONENT),
                       1.F / GSS_EXPONENT),
                  0.F);
      }
      gain_spectrum[fft_size - k] = gain_spectrum[k];
    } else {
      gain_spectrum[k] = 1.F;
      gain_spectrum[fft_size - k] = gain_spectrum[k];
    }
  }
}

static void scale_noise_profile(uint32_t real_spectrum_size,
                                float *noise_spectrum, const float *alpha) {
  for (uint32_t k = 1U; k < real_spectrum_size; k++) {
    noise_spectrum[k] *= alpha[k];
  }
}

void estimate_gains(uint32_t real_spectrum_size, uint32_t fft_size,
                    const float *spectrum, float *noise_spectrum,
                    float *gain_spectrum, const float *alpha, const float *beta,
                    GainEstimationType type) {
  switch (type) {
  case GATES:
    scale_noise_profile(real_spectrum_size, noise_spectrum, alpha);
    spectral_gating(real_spectrum_size, fft_size, spectrum, noise_spectrum,
                    gain_spectrum);
    break;
  case WIENER:
    scale_noise_profile(real_spectrum_size, noise_spectrum, alpha);
    wiener_subtraction(real_spectrum_size, fft_size, spectrum, noise_spectrum,
                       gain_spectrum);
    break;
  case GENERALIZED_SPECTRALSUBTRACION:
    generalized_spectral_subtraction(real_spectrum_size, fft_size, spectrum,
                                     noise_spectrum, gain_spectrum, alpha,
                                     beta);
    break;

  default:
    break;
  }
}