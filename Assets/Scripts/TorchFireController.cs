using UnityEngine;

public class TorchFireController : MonoBehaviour
{
    [Header("Fire Controls")]
    [Tooltip("Overall intensity of the fire and light")]
    [Range(0.1f, 3f)] public float fireIntensity = 1f;
    [Tooltip("How fast the light flickers")]
    [Range(0.1f, 10f)] public float flickerSpeed = 4f;

    [Header("Light Settings")]
    public Light torchLight;
    public float minLightIntensity = 1.5f;
    public float maxLightIntensity = 4f;

    private float randomOffset;
    private ParticleSystem[] fireParticles;
    private float[] initialEmissionRates;

    void Start()
    {
        randomOffset = Random.Range(0f, 100f);
        fireParticles = GetComponentsInChildren<ParticleSystem>();
        
        if (fireParticles != null && fireParticles.Length > 0)
        {
            initialEmissionRates = new float[fireParticles.Length];
            for (int i = 0; i < fireParticles.Length; i++)
            {
                initialEmissionRates[i] = fireParticles[i].emission.rateOverTime.constant;
            }
        }
    }

    void Update()
    {
        // 1. Flicker the point light for realism
        if (torchLight != null)
        {
            // Perlin noise creates smooth, natural random flickering
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, randomOffset);
            torchLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, noise) * fireIntensity;
        }

        // 2. Adjust particle emission rate based on the fireIntensity parameter
        if (fireParticles != null && initialEmissionRates != null)
        {
            for (int i = 0; i < fireParticles.Length; i++)
            {
                var emission = fireParticles[i].emission;
                emission.rateOverTime = initialEmissionRates[i] * fireIntensity;
            }
        }
    }
}
