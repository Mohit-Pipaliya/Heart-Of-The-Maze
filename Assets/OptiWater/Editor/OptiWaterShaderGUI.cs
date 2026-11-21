using UnityEngine;
using UnityEditor;

namespace Smartomano.OptiWater.Editor
{
    public class OptiWaterShaderGUI : ShaderGUI
    {
        private bool showColor = true;
        private bool showWave = true;
        private bool showNormal = true;
        private bool showDepth = true;
        private bool showCaustics = true;
        private bool showFoam = true;
        private bool showCrestGlow = true;
        private bool showReflection = true;
        private bool showShoreWave = true;
        private bool showDeepFoam = true;
        private bool showBottomDistort = true;
        private bool showPerf = true;
        private bool showWaveA = true, showWaveB = true, showWaveC = true, showWaveD = true;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            showColor = EditorGUILayout.Foldout(showColor, "Water Color & Specular", true, EditorStyles.foldoutHeader);
            if (showColor)
            {
                DrawProps(materialEditor, properties, "_WaterColor", "_DeepWaterColor", "_ShallowDeepBlendDepth", "_AlphaClearDepth", "_AlphaFullDepth", "_AlphaFalloffPower", "_AlphaEdgeWidth", "_AlphaEdgeMaxDepth", "_Smoothness", "_SpecularIntensity", "_SunGlitterRoughness", "_SunGlitterStrength", "_SunGlitterSparkle");
                EditorGUILayout.HelpBox(
                    "Water base color / alpha / specular. Water Color to Deep Water Color interpolated by depth (ShallowDeepBlendDepth = transition depth m). " +
                    "Alpha* based on real-time depth reconstructed water depth: EdgeWidth/EdgeMaxDepth control shore/cliff soft edges, FalloffPower controls edge hardness. " +
                    "Smoothness/Specular/Sun Glitter below are sun specular: Sun Glitter Width controls spot width, Strength brightness, Sparkle break-up.",
                    MessageType.Info);
            }

            showWave = EditorGUILayout.Foldout(showWave, "Wave Settings", true, EditorStyles.foldoutHeader);
            if (showWave)
            {
                MaterialProperty gerstnerToggle = FindProperty("_GerstnerWave", properties, false);
                if (gerstnerToggle != null) materialEditor.ShaderProperty(gerstnerToggle, "Enable Gerstner Wave");
                DrawProps(materialEditor, properties, "_WaveFrequency", "_WaveAmplitude", "_WaveSpeed", "_WaveSteepness");
                EditorGUILayout.HelpBox(
                    "Gerstner wave (fragment analytic, no mesh subdivision). Frequency/Amplitude/Speed/Steepness are global multipliers. " +
                    "Wave Layers A-D are four independent waves: Direction, Amplitude, Frequency; WaveDirSpeed rotates direction over time, WaveFreqMod/WaveFreqSpeed breathe frequency for non-repeating sea.",
                    MessageType.Info);
            }

            showNormal = EditorGUILayout.Foldout(showNormal, "Normal Map", true, EditorStyles.foldoutHeader);
            if (showNormal)
            {
                DrawTextureWithProps(materialEditor, properties, "_NormalMap", "_NormalStrength", "_NormalBlend", "_NormalWorldScale");
                MaterialProperty microToggle = FindProperty("_MicroNormal", properties, false);
                if (microToggle != null) materialEditor.ShaderProperty(microToggle, "Enable Micro Normal");
                MaterialProperty normalPerturbToggle = FindProperty("_NormalPerturb", properties, false);
                if (normalPerturbToggle != null) materialEditor.ShaderProperty(normalPerturbToggle, "Enable Normal Perturbation");
                EditorGUILayout.HelpBox(
                    "Normal map detail overlaid on Gerstner macro wave. NormalMap is detail normal; NormalStrength/Blend/WorldScale control strength, mix ratio, world scale. " +
                    "Micro Normal adds micro perturbation via screen derivatives; Normal Perturbation is the master toggle for detail normals.",
                    MessageType.Info);
            }

            showDepth = EditorGUILayout.Foldout(showDepth, "Shoreline Depth", true, EditorStyles.foldoutHeader);
            if (showDepth)
            {
                DrawProps(materialEditor, properties, "_ShorelineAlphaFalloff", "_ShorelineDepthFade");
                EditorGUILayout.HelpBox(
                    "Shoreline depth anchoring (driven by real-time depth reconstruction, no separate toggle). Provides shoreline gradient for foam/shore wave: " +
                    "ShorelineDepthFade is shore-to-deep transition world distance (m); ShorelineAlphaFalloff controls foam decay hardness (high = sharper).",
                    MessageType.Info);
            }

            showCaustics = EditorGUILayout.Foldout(showCaustics, "Caustics", true, EditorStyles.foldoutHeader);
            if (showCaustics)
            {
                MaterialProperty causticsToggle = FindProperty("_Caustics", properties, false);
                if (causticsToggle != null) materialEditor.ShaderProperty(causticsToggle, "Enable Caustics");
                DrawTextureWithProps(materialEditor, properties, "_CausticsTex", "_CausticsStrength", "_CausticsSpeed");
                EditorGUILayout.HelpBox(
                    "Caustics (underwater light net). CausticsTex is caustic texture; CausticsStrength intensity, CausticsSpeed flow speed. " +
                    "Caustics attenuate with water path length, visible only in shallow water.",
                    MessageType.Info);
            }

            showFoam = EditorGUILayout.Foldout(showFoam, "Foam", true, EditorStyles.foldoutHeader);
            if (showFoam)
            {
                MaterialProperty foamToggle = FindProperty("_Foam", properties, false);
                if (foamToggle != null) materialEditor.ShaderProperty(foamToggle, "Enable Foam");
                DrawTextureWithProps(materialEditor, properties, "_FoamTex", "_FoamIntensity", "_FoamDepthThreshold", "_FoamShorelineBoost", "_FoamPulseSpeed", "_WaveFoamIntensity", "_WaveFoamThreshold");
                EditorGUILayout.HelpBox(
                    "Foam (based on real shoreline factor). FoamTex foam texture; FoamIntensity total; FoamDepthThreshold how deep offshore foam persists; " +
                    "FoamShorelineBoost shoreline foam sharpness; FoamPulseSpeed/ScalePulse breathing scale. WaveFoamIntensity/Threshold control wave-crest foam.",
                    MessageType.Info);
            }

            showShoreWave = EditorGUILayout.Foldout(showShoreWave, "Shore Wave", true, EditorStyles.foldoutHeader);
            if (showShoreWave)
            {
                MaterialProperty shoreWaveToggle = FindProperty("_ShoreWave", properties, false);
                if (shoreWaveToggle != null) materialEditor.ShaderProperty(shoreWaveToggle, "Enable Shore Wave");
                MaterialProperty shoreWaveNormalToggle = FindProperty("_ShoreWaveNormal", properties, false);
                if (shoreWaveNormalToggle != null) materialEditor.ShaderProperty(shoreWaveNormalToggle, "Enable Shore Wave Normal Peak");
                DrawProps(materialEditor, properties, "_ShoreWaveFrequency", "_ShoreWaveSpeed", "_ShoreWaveWidth", "_ShoreWaveStart", "_ShoreWaveRange", "_ShoreWaveFalloff", "_ShoreWaveMix", "_ShoreWaveFoamStrength", "_ShoreWaveNormalStrength", "_ShoreWaveSlopeReach", "_ShoreWaveSlopeRef", "_ShoreWaveFoamTexTiling", "_ShoreWaveFoamMaskSpeed", "_ShoreWaveFoamMaskFloor", "_ShoreWaveFoamMaskPower");
                EditorGUILayout.HelpBox(
                    "Advancing shore wave: procedural sine band as wave line, broken by two-layer foam mask. Enable = master toggle; Normal Peak = wave-line normal ridge. " +
                    "Frequency = line density; Speed = advance speed; Mix = elevation-diff vs distance-field blend. Foam Strength = line weight (cap x100); " +
                    "Normal Strength = ridge strength; Width = line width; Start = offshore start distance; Range = offshore reach; Falloff = shore weighting; " +
                    "SlopeReach/SlopeRef = shift start by terrain slope; Foam Tiling = mask sparsity; Mask Speed = mask drift; Mask Floor = visible floor; Mask Contrast Power = contrast.",
                    MessageType.Info);
            }

            showDeepFoam = EditorGUILayout.Foldout(showDeepFoam, "Deep Water Foam", true, EditorStyles.foldoutHeader);
            if (showBottomDistort)
            {
                DrawProps(materialEditor, properties, "_DeepFoamStart", "_DeepFoamFade", "_DeepFoamIntensity");
                EditorGUILayout.HelpBox(
                    "Deep water random foam: reuses shore wave foam mask + foam texture noise, floats in deep water. No separate toggle, requires Shore Wave and Foam on. " +
                    "Start Depth = depth to start; Fade Range = fade-in distance; Intensity = strength (0 = off).",
                    MessageType.Info);
            }

            showBottomDistort = EditorGUILayout.Foldout(showBottomDistort, "Shallow Bottom Distortion", true, EditorStyles.foldoutHeader);
            if (showBottomDistort)
            {
                MaterialProperty bottomDistortToggle = FindProperty("_BottomDistort", properties, false);
                if (bottomDistortToggle != null)
                {
                    Material bottomMat = materialEditor.target as Material;
                    materialEditor.ShaderProperty(bottomDistortToggle, "Enable Shallow Bottom Distortion");
                    if (bottomMat != null)
                    {
                        if (bottomDistortToggle.floatValue > 0.5f) bottomMat.EnableKeyword("_BOTTOM_DISTORT");
                        else bottomMat.DisableKeyword("_BOTTOM_DISTORT");
                    }
                }
                DrawProps(materialEditor, properties, "_BottomDistortStrength", "_BottomDistortDepth", "_BottomDistortSpeed", "_BottomDistortTint");
                EditorGUILayout.HelpBox(
                    "Shallow bottom distortion (shore refraction): only in shore transition band where depth to 0, ripples the bed seen through thin water. " +
                    "Enable = master toggle (default on). Strength = screen UV distortion; Depth = offshore depth; Speed = ripple speed; Tint = water color tint. " +
                    "Requires URP Opaque Texture enabled.",
                    MessageType.Info);
            }

            showCrestGlow = EditorGUILayout.Foldout(showCrestGlow, "Crest Glow", true, EditorStyles.foldoutHeader);
            if (showCrestGlow)
            {
                MaterialProperty crestGlowToggle = FindProperty("_CrestGlow", properties, false);
                if (crestGlowToggle != null) materialEditor.ShaderProperty(crestGlowToggle, "Enable Crest Glow");
                DrawProps(materialEditor, properties, "_CrestGlowColor", "_CrestGlowThreshold", "_CrestGlowIntensity", "_CrestGlowPower");
                EditorGUILayout.HelpBox(
                    "Crest glow (wave sparkle). Uses Gerstner crest or high-freq detail normal slope as crest mask. " +
                    "Color; Threshold (below this normal.y glows); Intensity; Power (falloff hardness).",
                    MessageType.Info);
            }

            showReflection = EditorGUILayout.Foldout(showReflection, "Reflection (Planar only)", true, EditorStyles.foldoutHeader);
            if (showReflection)
            {
                EditorGUI.indentLevel++;
                MaterialProperty planarToggle = FindProperty("_PlanarReflection", properties, false);
                if (planarToggle != null)
                {
                    materialEditor.ShaderProperty(planarToggle, "Enable Planar Reflection");
                }
                EditorGUILayout.HelpBox(
                    "Planar reflection rendered by global OptiWaterPlanarReflectionRenderer into _ReflectionTex, providing _MirrorVP/_PlanePosWS/_PlaneNormalWS. " +
                    "Controller syncs _ReflectionTex into MaterialPropertyBlock each frame. ReflectionIntensity; Fresnel Power/Bias; Distortion = normal perturbation of reflection UV.",
                    MessageType.Info);
                DrawProps(materialEditor, properties, "_ReflectionIntensity", "_FresnelPower", "_FresnelBias");
                EditorGUI.indentLevel--;
            }

            showPerf = EditorGUILayout.Foldout(showPerf, "Performance & Culling", true, EditorStyles.foldoutHeader);
            if (showPerf)
            {
                DrawProps(materialEditor, properties, "_WaterClipThreshold");
                EditorGUILayout.HelpBox(
                    "Water Clip Threshold = early-return elevation bias (m). " +
                    "Terrain above water+threshold returns white and skips all water computation. 0 = threshold equals water height; positive clips higher.",
                    MessageType.Info);
            }

            showWaveA = DrawWaveLayer(materialEditor, properties, showWaveA, 'A');
            showWaveB = DrawWaveLayer(materialEditor, properties, showWaveB, 'B');
            showWaveC = DrawWaveLayer(materialEditor, properties, showWaveC, 'C');
            showWaveD = DrawWaveLayer(materialEditor, properties, showWaveD, 'D');
        }

        private bool DrawWaveLayer(MaterialEditor editor, MaterialProperty[] props, bool fold, char c)
        {
            fold = EditorGUILayout.Foldout(fold, $"Wave Layer {c}", true, EditorStyles.foldoutHeader);
            if (fold)
            {
                EditorGUI.indentLevel++;
                DrawVector2(editor, props, $"_{c}_Direction", "Direction");
                DrawProps(editor, props, $"_{c}_Amplitude", $"_{c}_Frequency");
                EditorGUI.indentLevel--;
            }
            return fold;
        }

        private void DrawVector2(MaterialEditor editor, MaterialProperty[] props, string name, string label)
        {
            MaterialProperty prop = FindProperty(name, props, false);
            if (prop == null) return;
            Vector4 v = prop.vectorValue;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            EditorGUIUtility.labelWidth = 20;
            v.x = EditorGUILayout.FloatField("X", v.x);
            v.y = EditorGUILayout.FloatField("Y", v.y);
            EditorGUIUtility.labelWidth = 0;
            EditorGUILayout.EndHorizontal();
            prop.vectorValue = v;
        }

        private void DrawTextures(MaterialEditor editor, MaterialProperty[] props, string texName)
        {
            MaterialProperty texProp = FindProperty(texName, props, false);
            if (texProp == null) return;
            editor.TexturePropertySingleLine(new GUIContent(texProp.displayName), texProp);
            editor.TextureScaleOffsetProperty(texProp);
        }

        private void DrawTextureWithProps(MaterialEditor editor, MaterialProperty[] props, string texName, params string[] floatNames)
        {
            EditorGUI.indentLevel++;
            DrawTextures(editor, props, texName);
            foreach (string fn in floatNames)
            {
                MaterialProperty fp = FindProperty(fn, props, false);
                if (fp != null) editor.ShaderProperty(fp, fp.displayName);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        private void DrawProps(MaterialEditor editor, MaterialProperty[] props, params string[] names)
        {
            EditorGUI.indentLevel++;
            foreach (string n in names)
            {
                MaterialProperty p = FindProperty(n, props, false);
                if (p != null) editor.ShaderProperty(p, p.displayName);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }
    }
}
