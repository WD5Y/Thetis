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

#include "masking_estimator.h"
#include "../configurations.h"
#include "../utils/spectral_utils.h"
#include "absolute_hearing_thresholds.h"
#include "critical_bands.h"
#include <math.h>
#include <stdlib.h>
#include <string.h>

static void compute_spectral_spreading_function(MaskingEstimator *self);
static float compute_tonality_factor(MaskingEstimator *self,
                                     const float *spectrum, uint32_t band);

struct MaskingEstimator {

  uint32_t fft_size;
  uint32_t real_spectrum_size;
  uint32_t sample_rate;
  uint32_t number_critical_bands;

  AbsoluteHearingThresholds *reference_spectrum;
  CriticalBands *critical_bands;
  CriticalBandIndexes band_indexes;

  float *spectral_spreading_function;
  float *unity_gain_critical_bands_spectrum;
  float *spreaded_unity_gain_critical_bands_spectrum;
  float *threshold_j;
  float *masking_offset;
  float *spreaded_spectrum;
  float *critical_bands_reference_spectrum;
};

MaskingEstimator *masking_estimation_initialize(const uint32_t fft_size,
                                                const uint32_t sample_rate,
                                                SpectrumType spectrum_type) {

  MaskingEstimator *self =
      (MaskingEstimator *)calloc(1U, sizeof(MaskingEstimator));

  self->fft_size = fft_size;
  self->real_spectrum_size = self->fft_size / 2U + 1U;
  self->sample_rate = sample_rate;

  self->critical_bands = critical_bands_initialize(
      self->sample_rate, self->fft_size, CRITICAL_BANDS_TYPE);
  self->number_critical_bands =
      get_number_of_critical_bands(self->critical_bands);

  self->spectral_spreading_function =
      (float *)calloc(((size_t)self->number_critical_bands *
                       (size_t)self->number_critical_bands),
                      sizeof(float));
  self->unity_gain_critical_bands_spectrum =
      (float *)calloc(self->number_critical_bands, sizeof(float));
  self->spreaded_unity_gain_critical_bands_spectrum =
      (float *)calloc(self->number_critical_bands, sizeof(float));
  self->threshold_j =
      (float *)calloc(self->number_critical_bands, sizeof(float));
  self->masking_offset =
      (float *)calloc(self->number_critical_bands, sizeof(float));
  self->spreaded_spectrum =
      (float *)calloc(self->number_critical_bands, sizeof(float));
  self->critical_bands_reference_spectrum =
      (float *)calloc(self->number_critical_bands, sizeof(float));

  self->reference_spectrum = absolute_hearing_thresholds_initialize(
      self->sample_rate, self->fft_size, spectrum_type);

  compute_spectral_spreading_function(self);
  initialize_spectrum_with_value(self->unity_gain_critical_bands_spectrum,
                                 self->number_critical_bands, 1.F);
  direct_matrix_to_vector_spectral_convolution(
      self->spectral_spreading_function,
      self->unity_gain_critical_bands_spectrum,
      self->spreaded_unity_gain_critical_bands_spectrum,
      self->number_critical_bands);

  return self;
}

void masking_estimation_free(MaskingEstimator *self) {
  absolute_hearing_thresholds_free(self->reference_spectrum);
  critical_bands_free(self->critical_bands);

  free(self->spectral_spreading_function);
  free(self->unity_gain_critical_bands_spectrum);
  free(self->spreaded_unity_gain_critical_bands_spectrum);
  free(self->threshold_j);
  free(self->masking_offset);
  free(self->spreaded_spectrum);
  free(self->critical_bands_reference_spectrum);

  free(self);
}

bool compute_masking_thresholds(MaskingEstimator *self, const float *spectrum,
                                float *masking_thresholds) {
  if (!self || !spectrum || !masking_thresholds) {
    return false;
  }

  compute_critical_bands_spectrum(self->critical_bands, spectrum,
                                  self->critical_bands_reference_spectrum);

  direct_matrix_to_vector_spectral_convolution(
      self->spectral_spreading_function,
      self->critical_bands_reference_spectrum, self->spreaded_spectrum,
      self->number_critical_bands);

  for (uint32_t j = 0U; j < self->number_critical_bands; j++) {

    const float tonality_factor = compute_tonality_factor(self, spectrum, j);

    self->masking_offset[j] = (tonality_factor * (14.5F + (float)(j + 1)) +
                               5.5F * (1.F - tonality_factor));

#if BIAS
    masking_offset[j] = relative_thresholds[j];

    if (j > 15) {
      masking_offset[j] += HIGH_FREQ_BIAS;
    }
#endif

    self->threshold_j[j] =
        powf(10.F, log10f(self->spreaded_spectrum[j]) -
                       (self->masking_offset[j] / 10.F)) -
        (10.F * log10f(self->spreaded_unity_gain_critical_bands_spectrum[j]));

    self->band_indexes = get_band_indexes(self->critical_bands, j);

    for (uint32_t k = self->band_indexes.start_position;
         k < self->band_indexes.end_position; k++) {
      masking_thresholds[k] = self->threshold_j[j];
    }
  }

  apply_thresholds_as_floor(self->reference_spectrum, masking_thresholds);

  return true;
}

static void compute_spectral_spreading_function(MaskingEstimator *self) {
  for (uint32_t i = 0U; i < self->number_critical_bands; i++) {
    for (uint32_t j = 0U; j < self->number_critical_bands; j++) {
      const uint32_t y = (i + 1) - (j + 1);

      self->spectral_spreading_function[i * self->number_critical_bands + j] =
          15.81F + 7.5F * ((float)y + 0.474F) -
          17.5F * sqrtf(1.F + ((float)y + 0.474F) * ((float)y + 0.474F));

      self->spectral_spreading_function[i * self->number_critical_bands + j] =
          powf(10.F, self->spectral_spreading_function
                             [i * self->number_critical_bands + j] /
                         10.F);
    }
  }
}

static float compute_tonality_factor(MaskingEstimator *self,
                                     const float *spectrum, uint32_t band) {
  float sum_bins = 0.F;
  float sum_log_bins = 0.F;

  self->band_indexes = get_band_indexes(self->critical_bands, band);

  for (uint32_t k = self->band_indexes.start_position;
       k < self->band_indexes.end_position; k++) {
    sum_bins += spectrum[k];
    sum_log_bins += log10f(spectrum[k]);
  }

  float bins_in_band = (float)self->band_indexes.end_position -
                       (float)self->band_indexes.start_position;

  const float SFM =
      10.F * (sum_log_bins / bins_in_band) - log10f(sum_bins / bins_in_band);

  const float tonality_factor = fminf(SFM / -60.F, 1.F);

  return tonality_factor;
}
