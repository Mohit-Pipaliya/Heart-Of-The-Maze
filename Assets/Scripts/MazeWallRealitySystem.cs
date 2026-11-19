using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class MazeWallRealitySystem : MonoBehaviour
{
    [Header("Wall VFX")]
    [SerializeField, Min(1)] private int targetWallCount = 12;
    [SerializeField, Min(1)] private int glowLightCount = 5;
    [SerializeField, Min(0.05f)] private float moteInterval = 0.12f;
    [SerializeField, Min(1)] private int motesPerBurst = 5;
    [SerializeField, Min(1f)] private float glowRange = 9f;
    [SerializeField, Min(0f)] private float glowIntensity = 1.4f;

    private const int MaxParticles = 240;

    private readonly List<Renderer> _targetWalls = new List<Renderer>(16);
    private readonly List<WallGlow> _wallGlows = new List<WallGlow>(4);
    private MazeManager _mazeManager;
    private ParticleSystem _motes;
    private Material _moteMaterial;
    private Material _crackMaterial;
    private Texture2D _moteTexture;
    private GameObject _targetBlock;
    private float _nextTargetRefresh;
    private float _nextMoteBurst;
    private bool _reportedMissingWalls;
    private bool _reportedActiveWalls;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToMazeManager()
    {
        MazeManager manager = FindFirstObjectByType<MazeManager>();
        if (manager == null) return;

        if (manager.GetComponent<MazeWallRealitySystem>() == null)
            manager.gameObject.AddComponent<MazeWallRealitySystem>();
    }

    private void Awake()
    {
        _mazeManager = GetComponent<MazeManager>();
        CreateMoteSystem();
        CreateCrackMaterial();
    }

    private void Update()
    {
        if (_mazeManager == null)
            _mazeManager = FindFirstObjectByType<MazeManager>();

        PlayerController player = PlayerController.Instance;
        if (_mazeManager == null || player == null || _motes == null) return;

        _motes.transform.position = player.transform.position;

        if (_targetBlock == null || Time.time >= _nextTargetRefresh)
        {
            RefreshTargets(player.transform.position);
            _nextTargetRefresh = Time.time + 4f;
        }

        UpdateWallGlow();

        if (_targetWalls.Count > 0 && Time.time >= _nextMoteBurst)
        {
            EmitMotes(player.transform.position);
            _nextMoteBurst = Time.time + moteInterval;
        }
    }

    private void RefreshTargets(Vector3 playerPosition)
    {
        GameObject closestBlock = null;
        float closestWallDistance = float.PositiveInfinity;
        GameObject[] blocks = _mazeManager.mazeBlocks;

        if (blocks == null) return;

        for (int i = 0; i < blocks.Length; i++)
        {
            GameObject block = blocks[i];
            if (block == null) continue;

            Renderer[] renderers = block.GetComponentsInChildren<Renderer>(true);
            for (int j = 0; j < renderers.Length; j++)
            {
                Renderer wall = renderers[j];
                if (!IsMazeWall(wall)) continue;

                float distance = wall.bounds.SqrDistance(playerPosition);
                if (distance < closestWallDistance)
                {
                    closestWallDistance = distance;
                    closestBlock = block;
                }
            }
        }

        if (closestBlock == null)
        {
            if (!_reportedMissingWalls)
            {
                Debug.LogWarning("[MazeWallRealitySystem] No enabled renderers named 'wall...' were found in mazeBlocks.");
                _reportedMissingWalls = true;
            }
            return;
        }

        Renderer[] blockRenderers = closestBlock.GetComponentsInChildren<Renderer>(true);
        List<WallDistance> nearbyWalls = new List<WallDistance>(blockRenderers.Length);
        for (int i = 0; i < blockRenderers.Length; i++)
        {
            Renderer wall = blockRenderers[i];
            if (IsMazeWall(wall))
            {
                nearbyWalls.Add(new WallDistance(wall, wall.bounds.SqrDistance(playerPosition)));
            }
        }

        nearbyWalls.Sort((a, b) => a.distance.CompareTo(b.distance));
        int selectedCount = Mathf.Min(targetWallCount, nearbyWalls.Count);
        bool targetsChanged = closestBlock != _targetBlock || selectedCount != _targetWalls.Count;
        if (!targetsChanged)
        {
            for (int i = 0; i < selectedCount; i++)
            {
                if (_targetWalls[i] != nearbyWalls[i].renderer)
                {
                    targetsChanged = true;
                    break;
                }
            }
        }

        _targetBlock = closestBlock;
        _targetWalls.Clear();
        for (int i = 0; i < selectedCount; i++)
            _targetWalls.Add(nearbyWalls[i].renderer);

        if (targetsChanged)
            RebuildWallGlows(playerPosition);

        if (!_reportedActiveWalls && _targetWalls.Count > 0)
        {
            Debug.Log($"[MazeWallRealitySystem] Wall VFX active on {_targetWalls.Count} wall sections.");
            _reportedActiveWalls = true;
        }
    }

    private static bool IsMazeWall(Renderer renderer)
    {
        return renderer != null && renderer.enabled &&
               renderer.gameObject.name.StartsWith("wall", StringComparison.OrdinalIgnoreCase);
    }

    private void CreateMoteSystem()
    {
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                ?? Shader.Find("Particles/Standard Unlit")
                                ?? Shader.Find("Sprites/Default");
        if (particleShader == null)
        {
            Debug.LogError("[MazeWallRealitySystem] No compatible particle shader was found.");
            enabled = false;
            return;
        }

        _moteTexture = CreateSoftMoteTexture();
        _moteMaterial = new Material(particleShader)
        {
            name = "Maze Wall Motes (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        if (_moteMaterial.HasProperty("_BaseMap"))
            _moteMaterial.SetTexture("_BaseMap", _moteTexture);
        if (_moteMaterial.HasProperty("_MainTex"))
            _moteMaterial.SetTexture("_MainTex", _moteTexture);
        if (_moteMaterial.HasProperty("_BaseColor"))
            _moteMaterial.SetColor("_BaseColor", Color.white);
        if (_moteMaterial.HasProperty("_Surface"))
            _moteMaterial.SetFloat("_Surface", 1f);
        if (_moteMaterial.HasProperty("_Blend"))
            _moteMaterial.SetFloat("_Blend", 0f);
        if (_moteMaterial.HasProperty("_SrcBlend"))
            _moteMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (_moteMaterial.HasProperty("_DstBlend"))
            _moteMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (_moteMaterial.HasProperty("_ZWrite"))
            _moteMaterial.SetFloat("_ZWrite", 0f);
        _moteMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _moteMaterial.renderQueue = (int)RenderQueue.Transparent;

        GameObject moteObject = new GameObject("Maze Wall Dust VFX");
        moteObject.transform.SetParent(transform, false);
        _motes = moteObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = _motes.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = MaxParticles;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startSpeed = 0f;
        main.startColor = Color.white;

        ParticleSystem.EmissionModule emission = _motes.emission;
        emission.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _motes.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.18f),
                new GradientAlphaKey(0.55f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        ParticleSystemRenderer particleRenderer = moteObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.material = _moteMaterial;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.localBounds = new Bounds(Vector3.zero, new Vector3(80f, 40f, 80f));

        _motes.Play();
    }

    private void EmitMotes(Vector3 playerPosition)
    {
        for (int i = 0; i < motesPerBurst; i++)
        {
            Renderer wall = _targetWalls[UnityEngine.Random.Range(0, _targetWalls.Count)];
            if (wall == null) continue;

            Bounds bounds = wall.bounds;
            Vector3 position = GetWallSurfacePoint(bounds, playerPosition, out Vector3 outward);
            float lifetime = UnityEngine.Random.Range(2.2f, 3.8f);
            Color color = UnityEngine.Random.value < 0.65f
                ? new Color(1f, 0.55f, 0.22f, 0.9f)
                : new Color(0.43f, 0.9f, 0.55f, 0.8f);

            ParticleSystem.EmitParams parameters = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.up * UnityEngine.Random.Range(0.12f, 0.34f)
                           + outward * UnityEngine.Random.Range(0.03f, 0.1f),
                startColor = color,
                startSize = UnityEngine.Random.Range(0.08f, 0.18f),
                startLifetime = lifetime
            };
            _motes.Emit(parameters, 1);
        }
    }

    private void RebuildWallGlows(Vector3 playerPosition)
    {
        ClearWallGlows();

        int count = Mathf.Min(glowLightCount, _targetWalls.Count);
        for (int i = 0; i < count; i++)
        {
            Renderer wall = _targetWalls[i];
            Vector3 position = GetWallSurfacePoint(wall.bounds, playerPosition, out _);

            GameObject glowObject = new GameObject("Ancient Wall Glow");
            glowObject.transform.SetParent(wall.transform, true);
            glowObject.transform.position = position;

            Light glow = glowObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = (i % 2 == 0)
                ? new Color(1f, 0.48f, 0.18f)
                : new Color(0.32f, 0.82f, 0.48f);
            glow.intensity = glowIntensity;
            glow.range = glowRange;
            glow.shadows = LightShadows.None;

            Color crackColor = (i % 2 == 0)
                ? new Color(1.8f, 0.48f, 0.08f, 1f)
                : new Color(0.22f, 1.5f, 0.52f, 1f);
            LineRenderer crack = _crackMaterial != null
                ? CreateEnergyCrack(wall, playerPosition, crackColor, _crackMaterial)
                : null;
            _wallGlows.Add(new WallGlow(
                glow,
                crack,
                glowIntensity,
                crackColor,
                UnityEngine.Random.Range(0f, 6.28f)));
        }
    }

    private void CreateCrackMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("[MazeWallRealitySystem] No compatible shader was found for wall energy cracks.");
            return;
        }

        _crackMaterial = new Material(shader)
        {
            name = "Maze Wall Energy Cracks (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        if (_crackMaterial.HasProperty("_BaseColor"))
            _crackMaterial.SetColor("_BaseColor", Color.white);
        if (_crackMaterial.HasProperty("_Color"))
            _crackMaterial.SetColor("_Color", Color.white);
        if (_crackMaterial.HasProperty("_Surface"))
            _crackMaterial.SetFloat("_Surface", 1f);
        if (_crackMaterial.HasProperty("_Blend"))
            _crackMaterial.SetFloat("_Blend", 0f);
        if (_crackMaterial.HasProperty("_SrcBlend"))
            _crackMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (_crackMaterial.HasProperty("_DstBlend"))
            _crackMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (_crackMaterial.HasProperty("_ZWrite"))
            _crackMaterial.SetFloat("_ZWrite", 0f);
        _crackMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _crackMaterial.renderQueue = (int)RenderQueue.Transparent;
    }

    private static LineRenderer CreateEnergyCrack(
        Renderer wall,
        Vector3 playerPosition,
        Color color,
        Material material)
    {
        Bounds bounds = wall.bounds;
        bool xIsThin = bounds.size.x <= bounds.size.z;
        Vector3 outward = xIsThin ? Vector3.right : Vector3.forward;
        if (Vector3.Dot(playerPosition - bounds.center, outward) < 0f)
            outward = -outward;

        Vector3 horizontal = xIsThin ? Vector3.forward : Vector3.right;
        float wallDepth = xIsThin ? bounds.extents.x : bounds.extents.z;
        float horizontalExtent = xIsThin ? bounds.extents.z : bounds.extents.x;
        float horizontalShift = UnityEngine.Random.Range(-horizontalExtent * 0.3f, horizontalExtent * 0.3f);
        Vector3 surfaceCenter = bounds.center
                                + outward * (wallDepth + 0.08f)
                                + horizontal * horizontalShift;

        GameObject crackObject = new GameObject("Ancient Wall Energy Crack");
        crackObject.transform.SetParent(wall.transform, false);

        LineRenderer line = crackObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.sharedMaterial = material;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 5;
        line.startWidth = Mathf.Clamp(bounds.size.y * 0.012f, 0.055f, 0.11f);
        line.endWidth = line.startWidth * 0.45f;
        line.startColor = color;
        line.endColor = color;

        const int pointCount = 8;
        line.positionCount = pointCount;
        float bottom = bounds.min.y + bounds.size.y * 0.08f;
        float top = bounds.max.y - bounds.size.y * 0.08f;
        float zigzag = Mathf.Min(horizontalExtent * 0.12f, 0.22f);
        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            float sideOffset = i == 0 || i == pointCount - 1
                ? 0f
                : (i % 2 == 0 ? -zigzag : zigzag);
            Vector3 worldPoint = surfaceCenter
                                 + Vector3.up * (Mathf.Lerp(bottom, top, t) - bounds.center.y)
                                 + horizontal * sideOffset;
            line.SetPosition(i, wall.transform.InverseTransformPoint(worldPoint));
        }

        return line;
    }

    private void UpdateWallGlow()
    {
        for (int i = 0; i < _wallGlows.Count; i++)
        {
            WallGlow glow = _wallGlows[i];
            if (glow.light == null && glow.crack == null) continue;
            float pulse = 0.82f + Mathf.Sin(Time.time * 2.1f + glow.phase) * 0.18f;
            if (glow.light != null)
                glow.light.intensity = glow.baseIntensity * pulse;
            if (glow.crack != null)
            {
                Color pulseColor = glow.crackColor * pulse;
                glow.crack.startColor = pulseColor;
                glow.crack.endColor = pulseColor;
            }
        }
    }

    private void ClearWallGlows()
    {
        for (int i = 0; i < _wallGlows.Count; i++)
        {
            if (_wallGlows[i].light != null)
                Destroy(_wallGlows[i].light.gameObject);
            if (_wallGlows[i].crack != null)
                Destroy(_wallGlows[i].crack.gameObject);
        }
        _wallGlows.Clear();
    }

    private static Vector3 GetWallSurfacePoint(Bounds bounds, Vector3 playerPosition, out Vector3 outward)
    {
        Vector3 size = bounds.size;
        bool xIsThin = size.x <= size.z;
        outward = xIsThin ? Vector3.right : Vector3.forward;
        float playerSide = Vector3.Dot(playerPosition - bounds.center, outward) >= 0f ? 1f : -1f;
        outward *= playerSide;

        float horizontalHalfExtent = xIsThin ? size.z * 0.35f : size.x * 0.35f;
        float verticalOffset = UnityEngine.Random.Range(-size.y * 0.28f, size.y * 0.28f);
        Vector3 point = bounds.center + outward * ((xIsThin ? size.x : size.z) * 0.5f + 0.12f);
        point.y = bounds.center.y + verticalOffset;
        point += xIsThin
            ? Vector3.forward * UnityEngine.Random.Range(-horizontalHalfExtent, horizontalHalfExtent)
            : Vector3.right * UnityEngine.Random.Range(-horizontalHalfExtent, horizontalHalfExtent);
        return point;
    }

    private static Texture2D CreateSoftMoteTexture()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Maze Wall Soft Mote (Runtime)",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Pow(Mathf.Clamp01(1f - distance), 2.4f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        ClearWallGlows();
        if (_moteMaterial != null) Destroy(_moteMaterial);
        if (_crackMaterial != null) Destroy(_crackMaterial);
        if (_moteTexture != null) Destroy(_moteTexture);
    }

    private readonly struct WallDistance
    {
        public readonly Renderer renderer;
        public readonly float distance;

        public WallDistance(Renderer renderer, float distance)
        {
            this.renderer = renderer;
            this.distance = distance;
        }
    }

    private readonly struct WallGlow
    {
        public readonly Light light;
        public readonly LineRenderer crack;
        public readonly float baseIntensity;
        public readonly Color crackColor;
        public readonly float phase;

        public WallGlow(Light light, LineRenderer crack, float baseIntensity, Color crackColor, float phase)
        {
            this.light = light;
            this.crack = crack;
            this.baseIntensity = baseIntensity;
            this.crackColor = crackColor;
            this.phase = phase;
        }
    }
}