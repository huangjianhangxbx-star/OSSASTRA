#ifndef OSSASTRA_LOOKDEV01_REFERENCE_MATH_INCLUDED
#define OSSASTRA_LOOKDEV01_REFERENCE_MATH_INCLUDED
// LOOKDEV-01 v1.1 shared math, integrated by StylizedLook/Bloom Shader passes.
// Formulae preserve the approved package reference; actual Unity/GPU verification
// is recorded separately from the reference CPU analog tests.
// RGB is nonnegative LINEAR HDR; author sRGB colours must be converted once.
// Visibility, PMA, current-frame depth, valid sampling and lifecycle are EXTERNAL.
// CPU numerical analog tests do not certify HLSL compilation or image quality.

float LD_Luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

float3 LD_Contrast(float3 c, float contrast, float pivot)
{
    c = max(c, 0.0);
    float y = LD_Luma(c);
    if (y <= 1e-7) return c;
    pivot = max(pivot, 1e-5);
    float yc = pivot * pow(y / pivot, clamp(contrast, 0.1, 3.0));
    return c * (yc / y);
}

float LD_WarmGuard(float3 c, float peakLow, float peakHigh)
{
    float peak = max(c.r, max(c.g, c.b));
    if (peak <= 1e-7) return 0.0;
    float warmR = smoothstep(0.10, 0.35, (c.r - c.b) / peak);
    float warmG = smoothstep(0.02, 0.15, (c.g - c.b) / peak);
    return warmR * warmG * smoothstep(peakLow, max(peakLow + 1e-4, peakHigh), peak);
}

float3 LD_Theme(float3 c, float3 shadowColor, float3 midColor,
                float3 lightColor, float strength, float pivot, float warmGuard)
{
    c = max(c, 0.0);
    float y = LD_Luma(c);
    if (y <= 1e-7) return c;
    float u = y / (y + max(pivot, 1e-5));
    float3 p = u < 0.5 ? lerp(shadowColor, midColor, u * 2.0)
                       : lerp(midColor, lightColor, (u - 0.5) * 2.0);
    p = max(p, 0.0);
    float py = LD_Luma(p);
    if (py <= 1e-7) return c; // Invalid black palette cannot divide by zero.
    float3 mapped = p * (y / py);
    return lerp(c, mapped, saturate(strength) * (1.0 - saturate(warmGuard)));
}

float3 LD_PosterizeLuma(float3 c, float levels, float strength, float referenceWhite)
{
    c = max(c, 0.0);
    float y = LD_Luma(c);
    if (y <= 1e-7 || strength <= 0.0) return c;
    referenceWhite = max(referenceWhite, 1e-4);
    levels = clamp(floor(levels + 0.5), 2.0, 16.0);
    float u = sqrt(saturate(y / referenceWhite));
    float q = floor(u * (levels - 1.0) + 0.5) / (levels - 1.0);
    float yq = q * q * referenceWhite;
    float w = saturate(strength) *
              (1.0 - smoothstep(0.85 * referenceWhite, 1.15 * referenceWhite, y));
    return c * (lerp(y, yq, w) / y);
}

float3 LD_BloomPrefilter(float3 c, float threshold, float kneeRatio, float maxPeak)
{
    c = max(c, 0.0);
    float peak = max(c.r, max(c.g, c.b));
    if (peak <= 1e-7) return 0.0;
    c *= min(1.0, max(maxPeak, 1e-4) / peak); // Proportional cap preserves hue.
    peak = max(c.r, max(c.g, c.b));
    threshold = max(threshold, 0.0);
    float k = max(1e-5, threshold * saturate(kneeRatio));
    float soft = clamp(peak - threshold + k, 0.0, 2.0 * k);
    soft = soft * soft / (4.0 * k + 1e-5);
    float contribution = max(soft, peak - threshold) / max(peak, 1e-5);
    return c * max(contribution, 0.0);
}

// Mean exp(-k*h) over a wholly nonnegative height interval.
float LD_PositiveHeightMean(float lo, float hi, float k)
{
    float x = max(0.0, k * (hi - lo));
    // Stable as the ray becomes horizontal, avoiding cancellation at x ~= 0.
    float q = x < 0.001 ? (1.0 - 0.5 * x + x * x / 6.0)
                       : ((1.0 - exp(-x)) / x);
    return exp(-k * max(lo, 0.0)) * q;
}

float LD_AverageHeightDensity(float h0, float h1, float baseHeight, float k)
{
    k = max(k, 0.0);
    if (k <= 1e-7) return 1.0;
    float lo = min(h0, h1) - baseHeight;
    float hi = max(h0, h1) - baseHeight;
    float span = hi - lo;
    if (span < 1e-4) return exp(-k * max((lo + hi) * 0.5, 0.0));
    if (hi <= 0.0) return 1.0;
    if (lo >= 0.0) return LD_PositiveHeightMean(lo, hi, k);
    // Split at the base plane: density is constant below it, exponential above.
    return ((-lo) + hi * LD_PositiveHeightMean(0.0, hi, k)) / span;
}

float LD_FogAmount(float segmentLength, float h0, float h1, float baseHeight,
                   float distanceDensity, float heightDensity, float heightFalloff,
                   float maxOpacity)
{
    float lengthValid = max(segmentLength, 0.0);
    float meanDensity = LD_AverageHeightDensity(h0, h1, baseHeight, heightFalloff);
    float tau = lengthValid * (max(distanceDensity, 0.0) +
                               max(heightDensity, 0.0) * meanDensity);
    return min(saturate(maxOpacity), 1.0 - exp(-max(tau, 0.0)));
}

// p is an INTEGER pixel index local to the active viewport, NOT clip position.
// Representative location for depth/edge alignment; default colour uses block AREA MEAN.
// First/last block use only pixels within the viewport. Coverage is checked outside.
int2 LD_PixelSource(int2 p, int2 viewportSize, int blockSize)
{
    viewportSize = max(viewportSize, int2(1, 1));
    p = clamp(p, int2(0, 0), viewportSize - 1);
    blockSize = max(blockSize, 1);
    int2 begin = (p / blockSize) * blockSize;
    int2 extent = min(int2(blockSize, blockSize), viewportSize - begin);
    return begin + (extent - 1) / 2;
}

// Call only when computing a real light contribution. This is not ambient emission.
float LD_ToonRamp(float nDotL, float transitionWidth)
{
    float x = saturate(nDotL);
    float w = max(transitionWidth, 0.001);
    return 0.20 + 0.40 * smoothstep(0.32 - w, 0.32 + w, x)
                + 0.40 * smoothstep(0.70 - w, 0.70 + w, x);
}
#endif
