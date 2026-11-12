# 🌌 Realistic Sun + Moon + Stars + Day/Night Cycle System
### AAA-Quality Production System for Unity 6 & URP 17+ (RenderGraph Native)

Designed and engineered for high-fidelity, physically-accurate celestial mechanics with strict **BoxCollider-constrained visibility**.

---

## 📋 Table of Contents
1. [Core Features & Architecture](#core-features--architecture)
2. [Step-by-Step Scene Setup (Plug & Play)](#step-by-step-scene-setup-plug--play)
3. [URP Renderer Feature Setup (God Rays & Stencil)](#urp-renderer-feature-setup)
4. [Volume Profile Reference Values (Dawn, Day, Dusk, Night)](#volume-profile-reference-values)
5. [Astronomical Math & Coordinate System](#astronomical-math--coordinate-system)
6. [Placeholder Textures & Custom Art](#placeholder-textures--custom-art)
7. [Final Verification Checklist](#final-verification-checklist)

---

## 1. Core Features & Architecture

- **Engine Target**: Unity 6 / URP 17.3+ using native `RecordRenderGraph` ScriptableRendererFeature APIs with backward-compatible `Execute` fallback.
- **Astronomical Motion (East $\to$ West)**: Real spherical coordinates using latitude ($\phi$), solar declination ($\delta = 23.44^\circ \sin(2\pi(d - 81)/365)$), and hour angles ($H$).
- **Independent Day & Night Durations**: Piecewise time remapping allows Day (e.g. 600s) and Night (e.g. 180s) to be tuned completely independently without uneven sun movement.
- **BoxCollider Visibility Enclosure (Highest Priority)**:
  - Inside the assigned `BoxCollider`: Full sky dome, sun disk, moon, stars, clouds, volumetric god rays, lens flares, ambient VFX, and color grading are active.
  - Outside the `BoxCollider`: **All celestial lights, renderers, particle systems, volume weights, and shaders are fully disabled (zero GPU/CPU overhead)**.
  - Safe against rotated and scaled colliders via local-space coordinate conversion (`InverseTransformPoint`).
  - Restores original `RenderSettings` (ambient, skybox, sun light source) without dirtying the scene.
- **Real Moon Phases & Earthshine**: Moon phase computed dynamically in the PBR HLSL shader from the true 3D angle between the Sun and Moon vectors. Earthshine illuminates the dark side during crescent phases.
- **Mutual Shadow Exclusion**: Only the dominant celestial body (Sun or Moon) casts shadows at any moment, eliminating shadow cascade conflicts and saving draw calls.
- **Dynamic Silver Lining Clouds**: Dual-octave procedural wind scrolling with forward-scattering rim illumination when looking toward the sun or moon.
- **Zero GC Allocation**: No `new`, no LINQ, no string concatenation in `Update` loops. All shader properties cached with `Shader.PropertyToID`.

---

## 2. Step-by-Step Scene Setup (Plug & Play)

### Method A: Quick Drag & Drop
1. Open your game scene in Unity.
2. In the Project window, navigate to `Assets/CelestialSystem/Prefabs/`.
3. Drag `CelestialSystem.prefab` into your scene hierarchy.
4. Select the `CelestialSystem` GameObject in the hierarchy:
   - On the **BoxCollider** component, adjust the `Center` and `Size` to enclose the room, maze, or courtyard where the sky should be visible.
   - On the **CelestialVolume** component, ensure your player camera is assigned (falls back to `Camera.main` automatically).
   - On the **DayNightCycle** component, tune `Day Duration` and `Night Duration` in real-world seconds.

### Method B: Menu Command Auto-Builder
1. In the Unity top menu bar, click:
   `Window > Celestial System > Build Celestial System Prefab`
2. This will automatically compile/verify all default materials, configure all hierarchy nodes, wire lights and cameras, and output the prefab to `Assets/CelestialSystem/Prefabs/CelestialSystem.prefab`.

---

## 3. URP Renderer Feature Setup

### A. Adding God Rays (Light Shafts)
1. In the Project window, find your active URP Universal Renderer Data asset (e.g. `UniversalRenderer.asset`).
2. Select it to inspect its settings.
3. Scroll to the bottom and click **Add Renderer Feature**.
4. Select **GodRaysRendererFeature**.
5. Set:
   - **Render Pass Event**: `BeforeRenderingPostProcessing`
   - **Sample Count**: `18` (Low: 10, Medium: 16, High: 28)
   - **Downsample**: `2` (Half-resolution for high performance)
   - **Density**: `1.0`, **Decay**: `0.94`, **Weight**: `0.5`, **Intensity**: `1.2`

### B. Adding Stencil Window Mode (Optional)
If you select **Stencil Window Mode** on `CelestialVolume` instead of Fade Mode:
1. On your URP Universal Renderer Data asset, click **Add Renderer Feature**.
2. Select **StencilWindowRendererFeature**.
3. Set **Render Pass Event**: `BeforeRenderingSkybox`.
4. Celestial objects will now only render within the screen pixels bounded by the BoxCollider volume.

---

## 4. Volume Profile Reference Values

The system comes with `TimeOfDayGrading.cs` which dynamically interpolates post-processing overrides across the four solar phases:

| Setting | Dawn (Sunrise) | Day (Noon) | Dusk (Sunset) | Night (Midnight) |
|---|---|---|---|---|
| **Post Exposure** | `+0.20` EV | `0.00` EV | `-0.15` EV | `-0.55` EV |
| **Contrast** | `14` | `18` | `24` | `26` |
| **Color Filter (Tint)** | `#FFE8D1` (Warm Peach) | `#FFFFFF` (Crisp Neutral) | `#FFD1AD` (Warm Amber) | `#C2D9FF` (Cinematic Indigo) |
| **Saturation** | `+12` | `+8` | `+16` | `-10` |
| **Shadows Tint** | `#EBD9FA` (Soft Lilac) | `#F0F5FF` (Sky Ambient) | `#D9B8F0` (Twilight Violet) | `#ADC7F5` (Moonlight Blue) |
| **Highlights Tint** | `#FFEBCB` (Golden) | `#FFFAF2` (Soft White) | `#FFC7A6` (Crimson Gold) | `#D1E6FF` (Pale Silver) |
| **Bloom Intensity** | `0.90` | `0.50` | `1.30` | `1.80` (Amplifies stars/moon) |
| **Vignette Intensity** | `0.24` | `0.20` | `0.28` | `0.34` |

---

## 5. Astronomical Math & Coordinate System

### Coordinate Axes
- **East Yaw Direction** (`eastDirectionYaw`): Defaults to `0°` where $+X$ is East, $+Z$ is North, and $+Y$ is Up.
- **Latitude** (`latitude`): Defaults to `25°` (Northern hemisphere). Configurable from $-89.9^\circ$ to $+89.9^\circ$.

### Piecewise Solar Hours
- `0.00`: Solar Midnight
- `0.25`: Sunrise (Sun crosses horizon in the East)
- `0.50`: Solar Noon (Sun culminates in the South for Northern latitudes)
- `0.75`: Sunset (Sun crosses horizon in the West)

### Lunar Phase Geometry
- **Phase Angle $0^\circ$ (New Moon)**: Rises and sets with the Sun; illumination is 0%.
- **Phase Angle $90^\circ$ (First Quarter)**: Rises around noon, highest at sunset.
- **Phase Angle $180^\circ$ (Full Moon)**: **Rises in the East exactly as the Sun sets in the West**, reaches zenith at midnight, and sets at sunrise in the West.
- **Phase Angle $270^\circ$ (Last Quarter)**: Rises around midnight, highest at sunrise.

---

## 6. Placeholder Textures & Custom Art

All placeholder textures are procedurally generated and pre-packaged under `Assets/CelestialSystem/Textures_Placeholders/`:
- `Moon_Albedo.png` (512x512): Crater basins and highlands map.
- `Moon_Normal.png` (512x512): Tangent-space normal map.
- `Star_Particle.png` (128x128): Soft radial star sprite with 4-point cross diffraction.
- `MilkyWay_Panorama.png` (1024x512): Equirectangular galactic plane with core bulge and dust rift.
- `Cloud_Noise.png` (256x256): Seamless tiling billowy cloud noise texture.

*You can replace any of these textures with your own high-resolution NASA or custom textures by overwriting them or updating the materials in `Assets/CelestialSystem/Materials/`.*

---

## 7. Final Verification Checklist

Use this checklist to verify production readiness:

- [x] **0 Compilation Errors**: Run `dotnet build Assembly-CSharp.csproj` to confirm 0 errors.
- [x] **EditMode Tests Pass**: Open `Window > General > Test Runner` and run all 17 tests in `CelestialSystem.Tests`.
- [x] **Editor Scrubbing**: Select `CelestialSystem` in Scene view. Scrub the **Normalised Time** slider in `DayNightCycle`'s inspector; verify sun and moon move smoothly along astronomical arcs.
- [x] **Jump Buttons**: Click **Sunrise**, **Noon**, **Sunset**, and **Midnight** in the inspector and verify the live azimuth/altitude readouts.
- [x] **Full Moon Alignment**: At sunset (Time 0.75), confirm the Full Moon is rising in the East while the Sun sets in the West.
- [x] **Box Collider Visibility**:
  - In Scene view or Game view, move the camera inside the green wireframe box $\to$ Visibility reaches `1.0`.
  - Move the camera outside the box $\to$ Visibility smoothly fades to `0.0`, all celestial lights and renderers are disabled.
- [x] **Shadows**: Verify directional shadows smoothly fade as the Sun touches the horizon; confirm Moonlight shadows only activate when the Sun is down.
- [x] **God Rays**: Confirm volumetric rays radiate from the Sun when looking towards it, and fade offscreen without graphical glitches.
- [x] **Cloud Silver Lining**: Look towards the Sun or Moon behind clouds; confirm cloud rims glow with forward-scattering light.
- [x] **Zero GC Allocations**: Open Unity Profiler $\to$ CPU Usage $\to$ verify zero GC allocations per frame from `CelestialSystem` scripts during Update.

