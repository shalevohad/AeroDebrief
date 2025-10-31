// GPU Compositor Shader for Layered Waveform Rendering
// Phase 3.1: Advanced Features Implementation
// 
// This shader composites multiple waveform frequency layers into a final output texture.
// Each frequency is stored as an independent Texture1D on the GPU, and this shader
// blends them together with alpha blending, effects, and zoom support.
//
// PERFORMANCE TARGET: < 30ms for 20 frequencies @ 2000x400 resolution

// Reduced MAX_LAYERS to avoid forced unroll failure
// Most recordings have < 16 frequencies anyway
#define MAX_LAYERS 16

// Input: Array of waveform layers (each is a Texture1D with amplitude data)
Texture1D<float> inputLayers[MAX_LAYERS] : register(t0);
SamplerState linearSampler : register(s0);

// Output: Final composited waveform image
RWTexture2D<float4> outputCanvas : register(u0);

// Constant buffer with composition parameters
cbuffer CompositorConstants : register(b0)
{
    int layerCount;           // Number of active layers
    int outputWidth;          // Output canvas width in pixels
    int outputHeight;         // Output canvas height in pixels
    float zoomStart;          // Zoom range start (0-1 normalized)
    float zoomEnd;            // Zoom range end (0-1 normalized)
    float globalAmplitude;    // Maximum amplitude across all layers for normalization
    uint visibilityMask;      // Bit mask for layer visibility (1 = visible, 0 = hidden)
    uint effectsMask;         // Bit mask for effects (1 = effect enabled, 0 = disabled)
};

// Per-layer metadata (colors, opacity, dimensions, effects)
struct LayerInfo
{
    float4 color;             // RGBA color (0-1 range)
    float opacity;            // Layer opacity (0-1)
    int dataLength;           // Number of samples in this layer's Texture1D
    int effectType;           // 0 = none, 1 = glow, 2 = highlight, 3 = pulse
    float effectIntensity;    // Effect strength (0-1)
    float3 padding;           // Padding to 16-byte boundary
};

StructuredBuffer<LayerInfo> layerInfoBuffer : register(t32);

// Helper function to sample a specific layer (avoids dynamic indexing warning)
// Using explicit switch for literal indices
float SampleLayer(int layerIndex, int sampleIndex)
{
    // Manual switch to avoid dynamic texture array indexing
    // HLSL requires literal indices for texture arrays in shader model 5.0
    switch (layerIndex)
    {
        case 0:  return inputLayers[0][sampleIndex];
        case 1:  return inputLayers[1][sampleIndex];
        case 2:  return inputLayers[2][sampleIndex];
        case 3:  return inputLayers[3][sampleIndex];
        case 4:  return inputLayers[4][sampleIndex];
        case 5:  return inputLayers[5][sampleIndex];
        case 6:  return inputLayers[6][sampleIndex];
        case 7:  return inputLayers[7][sampleIndex];
        case 8:  return inputLayers[8][sampleIndex];
        case 9:  return inputLayers[9][sampleIndex];
        case 10: return inputLayers[10][sampleIndex];
        case 11: return inputLayers[11][sampleIndex];
        case 12: return inputLayers[12][sampleIndex];
        case 13: return inputLayers[13][sampleIndex];
        case 14: return inputLayers[14][sampleIndex];
        case 15: return inputLayers[15][sampleIndex];
        default: return 0.0;
    }
}

// Compute shader entry point
// Dispatched with 16x16 thread groups for optimal GPU occupancy
[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint x = dispatchThreadID.x;
    uint y = dispatchThreadID.y;
    
    // Bounds check
    if (x >= (uint)outputWidth || y >= (uint)outputHeight)
        return;
    
    // Calculate normalized X position within zoom range
    float normalizedX = (float)x / (float)outputWidth;
    float timePosition = lerp(zoomStart, zoomEnd, normalizedX);
    
    // Initialize output color (transparent black)
    float4 finalColor = float4(0, 0, 0, 0);
    
    // Center Y coordinate for waveform (middle of canvas)
    float centerY = outputHeight / 2.0;
    float yOffset = abs((float)y - centerY);
    
    // Clamp layer count to MAX_LAYERS
    int actualLayerCount = min(layerCount, MAX_LAYERS);
    
    // Blend all visible layers using alpha compositing
    // NO FORCED UNROLL - let compiler decide based on hardware
    for (int i = 0; i < actualLayerCount; i++)
    {
        // Check visibility bit (skip hidden layers instantly!)
        if ((visibilityMask & (1u << i)) == 0)
            continue;
        
        LayerInfo layer = layerInfoBuffer[i];
        
        // Sample waveform amplitude at this time position using helper function
        int sampleIndex = (int)(timePosition * (float)layer.dataLength);
        sampleIndex = clamp(sampleIndex, 0, layer.dataLength - 1);
        float amplitude = SampleLayer(i, sampleIndex);
        
        // Normalize amplitude to 0-1 range
        float normalizedAmplitude = amplitude / globalAmplitude;
        
        // Calculate maximum Y offset for this amplitude
        float maxYOffset = normalizedAmplitude * (centerY * 0.8); // Use 80% of canvas height
        
        // Check if this pixel is inside the waveform shape
        if (yOffset <= maxYOffset)
        {
            // Base alpha from layer opacity
            float alpha = layer.opacity;
            
            // Apply visual effects based on effect mask
            if ((effectsMask & (1u << i)) != 0)
            {
                // Calculate distance from waveform edge (0 = edge, 1 = center)
                float edgeFactor = 1.0 - (yOffset / maxYOffset);
                
                if (layer.effectType == 1) // Glow effect
                {
                    // Soft glow: increase alpha near center, fade near edges
                    alpha *= lerp(0.3, 1.0, edgeFactor);
                }
                else if (layer.effectType == 2) // Highlight effect
                {
                    // Brighten color near center
                    float brightnessBoost = lerp(1.0, 1.5, edgeFactor * layer.effectIntensity);
                    layer.color.rgb *= brightnessBoost;
                }
                else if (layer.effectType == 3) // Pulse effect
                {
                    // Animate alpha based on time position (creates pulsing effect)
                    float pulsePhase = frac(timePosition * 10.0); // 10 pulses across view
                    alpha *= lerp(0.5, 1.0, sin(pulsePhase * 3.14159265));
                }
            }
            
            // Alpha blending: blend this layer over previous layers
            // Formula: finalColor = finalColor * (1 - alpha) + layerColor * alpha
            finalColor.rgb = lerp(finalColor.rgb, layer.color.rgb, alpha);
            finalColor.a = max(finalColor.a, alpha);
        }
    }
    
    // Write final composited pixel to output texture
    outputCanvas[uint2(x, y)] = finalColor;
}
