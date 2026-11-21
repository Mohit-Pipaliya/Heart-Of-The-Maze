# OptiWater

A lightweight, high-performance stylized water system for the Universal Render Pipeline (URP), optimized for mobile (iOS / iPad). It is implemented as a single-pass, per-fragment solution and requires no mesh tessellation.

## Directory Structure

```
OptiWater/
├── Runtime/
│   ├── OptiWaterController.cs               # Master controller: material parameter sync, global variables, per-frame PropertyBlock sync
│   ├── OptiWaterPlanarReflectionRenderer.cs # Planar reflection: dedicated reflection camera + oblique clip projection, outputs _ReflectionTex
│   ├── OptiWaterUnderwaterFeature.cs        # Underwater post-processing RenderFeature (submersion tint / depth-reconstructed test)
│   ├── OptiWaterUnderwaterBootstrap.cs      # Underwater post-processing bootstrap registration
│   └── Shaders/
│       ├── OptiWater.shader                 # Main water surface Shader (single pass)
│       └── OptiWaterUnderwater.shader       # Underwater full-screen post-processing Shader
├── Editor/
│   └── OptiWaterShaderGUI.cs                # Material inspector (grouped collapsibles + tooltips)
└── Demo/                                    # Demo scene and assets
```

## Feature List

| Feature | Description |
| --- | --- |
| Water Color & Specular | Dual-tone shallow/deep water color interpolated by depth, real-time depth-reconstructed soft-edge Alpha (shoreline / cliffs), and sun glitter specular (width / intensity / breakup adjustable) |
| Gerstner Wave | Analytic per-fragment Gerstner waves with no mesh tessellation; four independent wave layers (A–D) with per-layer direction / amplitude / frequency; direction rotates over time and frequency "breathes" so the surface never repeats |
| Normal Map | Detail normals blended over the macro wave, with optional micro-normal (screen-space derivative perturbation) and a master toggle |
| Shoreline Depth | Real-time depth-reconstructed shoreline anchoring that feeds the foam / shore-wave gradient |
| Caustics | Underwater caustic light network that fades with water-path length and is only visible in shallow water |
| Foam | Shoreline foam + crest foam with breathing scale |
| Shore Wave | Advancing shore wave: procedural sine wave band + two-layer foam mask breakup, with wave-line normal ridges and terrain-slope offset |
| Deep Water Foam | Random surface scum in deep water, reusing the shore-wave mask + foam texture noise |
| Shallow Bottom Distortion | Shallow bottom distortion (shoreline refraction): perturbs the bottom seen through thin water (requires URP Opaque Texture) |
| Crest Glow | Crest glow masked by Gerstner wave peaks / high-frequency normal slope |
| Planar Reflection | Planar reflection: `OptiWaterPlanarReflectionRenderer` uses a dedicated camera + oblique clip projection to render into `_ReflectionTex`; the Shader reprojects and samples it via `_OptiWaterMirrorVP`, with Fresnel + normal perturbation |
| Underwater Effect | Underwater post-processing: depth-reconstructed world position + water-surface mask test for submersion tint (does not rely on camera-height tests) |

## Key Performance Design: Terrain Elevation Early Return

Before **any** water computation, the water fragment shader reconstructs the terrain world height `hWorld` at the covered pixel from the scene depth, and compares it against the clip line:

```
clipLine = _WaterSurfaceHeight + _WaterClipThreshold
if (hWorld > clipLine)  →  directly return, skipping all water computation
```

In other words: **any pixel whose terrain is higher than the water surface (plus threshold) early-returns**, so all expensive work — waves, normals, foam, caustics, reflection, shore wave — is skipped entirely. For scenes where the water mesh covers a large area but the actually visible water is small (e.g. a basin lake, a river valley), this culls a large number of redundant pixels.

- `_WaterClipThreshold`: clipping elevation bias (meters), 0 = exactly equal to the water height, positive clips higher (adjustable in the material inspector under **Performance & Culling**).
- `_WaterSurfaceHeight`: the real water elevation, globally synced by `OptiWaterController`; do not edit manually.

**Measured result: stable 50+ FPS on iPad Air 2 / iPhone.**

## URP Configuration Requirements

On the URP Asset (Universal Render Pipeline Asset), the following features must be / may be enabled:

| URP Feature | Required? | Purpose |
| --- | --- | --- |
| **Depth Texture** | **Required** | The foundation of the entire system: `SampleSceneDepth` reconstructs terrain world position, driving shoreline soft-edge Alpha, shoreline foam / shore wave, caustics falloff, shallow/deep color blend, **early-return elevation clipping**, and the underwater test. Without it, the water degrades and the performance cull becomes ineffective |
| **Opaque Texture** | Optional | Only the shallow bottom distortion (Shallow Bottom Distortion) uses `SampleSceneColor` to sample the bottom image. If you do not use that feature, you can disable it to save bandwidth |

Additional notes:

- **The underwater post-processing RenderFeature is added automatically**: `OptiWaterUnderwaterBootstrap` registers `OptiWaterUnderwaterFeature` to every Renderer of the URP Asset at runtime (AfterSceneLoad); if no URP Asset is found it safely skips with a Warning.
- **Planar reflection does not depend on extra URP settings**: it is rendered by a dedicated reflection camera into an RT; post-processing is auto-disabled and shadows are auto-enabled on that camera.
- The water material uses the Transparent queue and therefore does not write into the depth texture; the Depth Texture always contains terrain / object depth, so the depth-reconstruction logic is never polluted by the water surface itself.

## Usage Notes

1. Attach `OptiWaterController` to the water object (**required** — it syncs the reflection RT and other global resources into the MaterialPropertyBlock every frame; without it, reflection and related features will not work).
2. When you need planar reflection, place `OptiWaterPlanarReflectionRenderer` in the scene and point its `optiWaterSurface` to the water Transform.
3. Enable URP Depth Texture (required) and Opaque Texture (only when using bottom distortion) as described in the section above.
4. Use the `OptiWater/OptiWater` Shader for the material; the inspector is provided by `OptiWaterShaderGUI` with grouped explanations.
