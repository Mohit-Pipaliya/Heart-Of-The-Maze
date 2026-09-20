using UnityEditor;
using UnityEngine;

public class TorchFireSetup
{
    [MenuItem("GameObject/Effects/Realistic Torch Fire VFX", false, 10)]
    public static void CreateTorchFire(MenuCommand menuCommand)
    {
        // 1. Create Root Object
        GameObject fireRoot = new GameObject("RealisticTorchFire");
        GameObjectUtility.SetParentAndAlign(fireRoot, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(fireRoot, "Create Realistic Torch Fire");

        // Add Controller Script
        TorchFireController controller = fireRoot.AddComponent<TorchFireController>();

        // 2. Setup Material
        string matPath = "Assets/Materials/VFX";
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(matPath)) AssetDatabase.CreateFolder("Assets/Materials", "VFX");

        Material fireMat = AssetDatabase.LoadAssetAtPath<Material>(matPath + "/M_TorchFire.mat");
        if (fireMat == null)
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpShader != null)
            {
                fireMat = new Material(urpShader);
                fireMat.SetFloat("_Surface", 1); // Transparent
                fireMat.SetFloat("_BlendMode", 2); // Additive
                fireMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                fireMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                fireMat.SetInt("_ZWrite", 0);
                fireMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                // Fallback for non-URP
                fireMat = new Material(Shader.Find("Particles/Standard Unlit"));
                fireMat.SetFloat("_Mode", 3); // Additive
            }
            AssetDatabase.CreateAsset(fireMat, matPath + "/M_TorchFire.mat");
        }

        // 3. Main Flame Particle System
        GameObject mainFlame = new GameObject("MainFlame");
        mainFlame.transform.SetParent(fireRoot.transform);
        mainFlame.transform.localPosition = Vector3.zero;
        ParticleSystem psMain = mainFlame.AddComponent<ParticleSystem>();
        
        var main = psMain.main;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = -0.1f; // Slight upward pull
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = psMain.emission;
        emission.rateOverTime = 40f;

        var shape = psMain.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.05f;

        var colOverLifetime = psMain.colorOverLifetime;
        colOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(1f, 0.6f, 0.1f), 0.2f), new GradientColorKey(new Color(1f, 0.2f, 0f), 0.6f), new GradientColorKey(new Color(0.2f, 0f, 0f), 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1.0f) }
        );
        colOverLifetime.color = grad;

        var sizeOverLifetime = psMain.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = psMain.noise;
        noise.enabled = true;
        noise.strength = 0.2f;
        noise.frequency = 1.5f;
        noise.scrollSpeed = 1f; // Creates wind-like movement

        var renderer = psMain.GetComponent<ParticleSystemRenderer>();
        renderer.material = fireMat;

        // 4. Core Flame Particle System (Brighter inner part)
        GameObject coreFlame = new GameObject("CoreFlame");
        coreFlame.transform.SetParent(fireRoot.transform);
        coreFlame.transform.localPosition = Vector3.up * 0.1f;
        ParticleSystem psCore = coreFlame.AddComponent<ParticleSystem>();
        
        var cMain = psCore.main;
        cMain.duration = 1f;
        cMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        cMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
        cMain.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        cMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        cMain.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var cEmission = psCore.emission;
        cEmission.rateOverTime = 20f;

        var cShape = psCore.shape;
        cShape.shapeType = ParticleSystemShapeType.Cone;
        cShape.angle = 5f;
        cShape.radius = 0.02f;

        var cColOverLifetime = psCore.colorOverLifetime;
        cColOverLifetime.enabled = true;
        Gradient cGrad = new Gradient();
        cGrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0.5f), new GradientColorKey(new Color(1f, 0.4f, 0f), 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1.0f) }
        );
        cColOverLifetime.color = cGrad;

        var cSizeOverLifetime = psCore.sizeOverLifetime;
        cSizeOverLifetime.enabled = true;
        cSizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var cRenderer = psCore.GetComponent<ParticleSystemRenderer>();
        cRenderer.material = fireMat;

        // 5. Embers/Sparks
        GameObject embers = new GameObject("Embers");
        embers.transform.SetParent(fireRoot.transform);
        embers.transform.localPosition = Vector3.zero;
        ParticleSystem psEmbers = embers.AddComponent<ParticleSystem>();
        
        var eMain = psEmbers.main;
        eMain.duration = 1f;
        eMain.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.5f);
        eMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        eMain.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        eMain.gravityModifier = -0.2f;
        eMain.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var eEmission = psEmbers.emission;
        eEmission.rateOverTime = 15f;

        var eShape = psEmbers.shape;
        eShape.shapeType = ParticleSystemShapeType.Cone;
        eShape.angle = 25f;
        eShape.radius = 0.1f;

        var eColOverLifetime = psEmbers.colorOverLifetime;
        eColOverLifetime.enabled = true;
        Gradient eGrad = new Gradient();
        eGrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.8f, 0f), 0.0f), new GradientColorKey(new Color(1f, 0.2f, 0f), 0.6f), new GradientColorKey(Color.black, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0.0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1.0f) }
        );
        eColOverLifetime.color = eGrad;

        var eNoise = psEmbers.noise;
        eNoise.enabled = true;
        eNoise.strength = 0.5f;
        eNoise.frequency = 2f;
        eNoise.scrollSpeed = 1.5f;

        var eRenderer = psEmbers.GetComponent<ParticleSystemRenderer>();
        eRenderer.material = fireMat;
        eRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        eRenderer.lengthScale = 2f;

        // 6. Point Light Setup
        GameObject lightObj = new GameObject("TorchLight");
        lightObj.transform.SetParent(fireRoot.transform);
        lightObj.transform.localPosition = Vector3.up * 0.2f;
        Light pointLight = lightObj.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = new Color(1f, 0.6f, 0.2f);
        pointLight.range = 8f;
        pointLight.intensity = 3f;

        // Assign light to controller
        controller.torchLight = pointLight;

        // Select the new object in Hierarchy
        Selection.activeObject = fireRoot;
        Debug.Log("Realistic Torch Fire VFX created successfully! You can adjust settings on the TorchFireController script.");
    }
}
