// Copyright 2012 Google Inc. All Rights Reserved.
//
// Use of this source code is governed by a BSD-style license
// that can be found in the COPYING file in the root of the source
// tree. An additional intellectual property rights grant can be found
// in the file PATENTS. All contributing project authors may
// be found in the AUTHORS file in the root of the source tree.
// -----------------------------------------------------------------------------
//
// Author: Jyrki Alakuijala (jyrki@google.com)
//
// Models the histograms of literal and distance codes.

#ifndef WEBP_ENC_HISTOGRAM_H_
#define WEBP_ENC_HISTOGRAM_H_

#include <assert.h>
#include <stddef.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>

#include "./backward_references.h"
#include "../webp/format_constants.h"
#include "../webp/types.h"

#ifdef __cplusplus
extern "C" {
#endif

// A simple container for histograms of data.
typedef struct {
  // literal_ contains green literal, palette-code and
  // copy-length-prefix histogram
  uint32_t* literal_;         // Pointer to the allocated buffer for literal.
  uint32_t red_[NUM_LITERAL_CODES];
  uint32_t blue_[NUM_LITERAL_CODES];
  uint32_t alpha_[NUM_LITERAL_CODES];
  // Backward reference prefix-code histogram.
  uint32_t distance_[NUM_DISTANCE_CODES];
  int palette_code_bits_;
  uint32_t trivial_symbol_;  // True, if histograms for Red, Blue & Alpha
                             // literal symbols are single valued.
  double bit_cost_;          // cached value of bit cost.
  double literal_cost_;      // Cached values of dominant entropy costs:
  double red_cost_;          // literal, red & blue.
  double blue_cost_;
} VP8LHistogram;

// Collection of histograms with fixed capacity, allocated as one
// big memory chunk. Can be destroyed by calling WebPSafeFree().
typedef struct {
  int size;         // number of slots currently in use
  int max_size;     // maximum capacity
  VP8LHistogram** histograms;
} VP8LHistogramSet;

// Create the histogram.
//
// The input data is the PixOrCopy data, which models the literals, stop
// codes and backward references (both distances and lengths).  Also: if
// palette_code_bits is >= 0, initialize the histogram with this value.
void VP8LHistogramCreate(VP8LHistogram* const p,
                         const VP8LBackwardRefs* const refs,
                         int palette_code_bits);

// Return the size of the histogram for a given palette_code_bits.
int VP8LGetHistogramSize(int palette_code_bits);

// Set the palette_code_bits and reset the stats.
void VP8LHistogramInit(VP8LHistogram* const p, int palette_code_bits);

// Collect all the references into a histogram (without reset)
void VP8LHistogramStoreRefs(const VP8LBackwardRefs* const refs,
                            VP8LHistogram* const histo);

// Free the memory allocated for the histogram.
void VP8LFreeHistogram(VP8LHistogram* const histo);

// Free the memory allocated for the histogram set.
void VP8LFreeHistogramSet(VP8LHistogramSet* const histo);

// Allocate an array of pointer to histograms, allocated and initialized
// using 'cache_bits'. Return NULL in case of memory error.
VP8LHistogramSet* VP8LAllocateHistogramSet(int size, int cache_bits);

// Allocate and initialize histogram object with specified 'cache_bits'.
// Returns NULL in case of memory error.
// Special case of VP8LAllocateHistogramSet, with size equals 1.
VP8LHistogram* VP8LAllocateHistogram(int cache_bits);

// Accumulate a token 'v' into a histogram.
void VP8LHistogramAddSinglePixOrCopy(VP8LHistogram* const histo,
                                     const PixOrCopy* const v);

// Estimate how many bits the combined entropy of literals and distance
// approximately maps to.
double VP8LHistogramEstimateBits(const VP8LHistogram* const p);

// This function estimates the cost in bits excluding the bits needed to
// represent the entropy code itself.
double VP8LHistogramEstimateBitsBulk(const VP8LHistogram* const p);

static WEBP_INLINE int VP8LHistogramNumCodes(int palette_code_bits) {
  return NUM_LITERAL_CODES + NUM_LENGTH_CODES +
      ((palette_code_bits > 0) ? (1 << palette_code_bits) : 0);
}

// Builds the histogram image.
int VP8LGetHistoImageSymbols(int xsize, int ysize,
                             const VP8LBackwardRefs* const refs,
                             int quality, int histogram_bits, int cache_bits,
                             VP8LHistogramSet* const image_in,
                             uint16_t* const histogram_symbols);

#ifdef __cplusplus
}
#endif

#endif  // WEBP_ENC_HISTOGRAM_H_
