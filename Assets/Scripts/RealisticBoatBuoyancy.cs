using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Realistic multi-point boat buoyancy and physics simulation for Unity 6 (URP).
/// Uses Unity 6 Rigidbody API (linearVelocity, linearDamping, angularDamping).
/// Samples GerstnerWaveMath to ensure exact physical-to-visual wave synchrony.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RealisticBoatBuoyancy : MonoBehaviour
{
    [Header("Hull Float Points (World Transforms)")]
    [Tooltip("Assign 4-6 transforms placed at bow, stern, port, starboard, and center.")]
    [SerializeField] private Transform[] floatPoints;
    [SerializeField] private float pointVolume = 0.65f;
    [SerializeField] private float maxImmersionDepth = 0.8f;
    [SerializeField] private float waterDensity = 1025f;

    [Header("Linear & Angular Water Damping")]
    [SerializeField] private float waterLinearDamping = 1.8f;
    [SerializeField] private float waterAngularDamping = 3.2f;

    [Header("River Current & Spline Drift")]
    [SerializeField] private SplineContainer riverSpline;
    [SerializeField] private float riverCurrentSpeed = 2.4f;

    [Header("Player Engine & Steering Controls")]
    public float engineThrust = 3200f;
    public float reverseThrust = 1600f;
    public float steerTorque = 950f;
    [Range(0f, 1f)] public float controlAuthority = 0f; // 1 when player is driving, 0 when empty

    [Header("Visual Effects & Wake")]
    [SerializeField] private ParticleSystem bowSprayVFX;
    [SerializeField] private TrailRenderer wakeTrailPort;
    [SerializeField] private TrailRenderer wakeTrailStarboard;

    private Rigidbody _rb;
    private float _throttleInput;
    private float _steerInput;

    public Rigidbody BoatRigidbody => _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        
        // Unity 6 Rigidbody damping properties (replacing drag / angularDrag)
        _rb.linearDamping = 0.05f;
        _rb.angularDamping = 0.05f;
        _rb.centerOfMass = new Vector3(0f, -0.45f, 0f); // Low center of mass prevents flipping
    }

    public void SetPlayerInput(float throttle, float steer)
    {
        _throttleInput = Mathf.Clamp(throttle, -1f, 1f);
        _steerInput = Mathf.Clamp(steer, -1f, 1f);
    }

    private void FixedUpdate()
    {
        float time = Time.time;
        int submergedCount = 0;

        // 1. Multi-point Buoyancy sampling identical Gerstner wave math
        if (floatPoints != null && floatPoints.Length > 0)
        {
            for (int i = 0; i < floatPoints.Length; i++)
            {
                Transform pt = floatPoints[i];
                if (pt == null) continue;

                Vector3 ptPos = pt.position;
                float waterHeight = GerstnerWaveMath.SampleWaterHeight(ptPos, time);
                float depth = waterHeight - ptPos.y;

                if (depth > 0f)
                {
                    submergedCount++;
                    float immersion01 = Mathf.Clamp01(depth / maxImmersionDepth);
                    float buoyantForceMagnitude = pointVolume * waterDensity * Mathf.Abs(Physics.gravity.y) * immersion01;

                    Vector3 buoyantForce = Vector3.up * buoyantForceMagnitude;
                    _rb.AddForceAtPosition(buoyantForce, ptPos, ForceMode.Force);

                    // Hydrodynamic point drag (resist vertical oscillation)
                    Vector3 ptVelocity = _rb.GetPointVelocity(ptPos);
                    Vector3 verticalDamping = -Vector3.up * (ptVelocity.y * 550f * immersion01);
                    _rb.AddForceAtPosition(verticalDamping, ptPos, ForceMode.Force);
                }
            }
        }

        // Apply water-surface damping when submerged
        if (submergedCount > 0)
        {
            float subRatio = (float)submergedCount / floatPoints.Length;
            
            // Unity 6: linearVelocity and angularVelocity
            _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, new Vector3(_rb.linearVelocity.x, _rb.linearVelocity.y * 0.96f, _rb.linearVelocity.z), waterLinearDamping * subRatio * Time.fixedDeltaTime);
            _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, Vector3.zero, waterAngularDamping * subRatio * Time.fixedDeltaTime);

            // 2. River Current Spline Push
            ApplyRiverCurrent();

            // 3. Engine Throttle & Steering
            if (controlAuthority > 0.01f)
            {
                ApplyControls();
            }
        }

        UpdateWakeVFX();
    }

    private void ApplyRiverCurrent()
    {
        Vector3 currentDirection = transform.forward;
        if (riverSpline != null && riverSpline.Splines != null && riverSpline.Splines.Count > 0)
        {
            // Evaluate tangent at closest point on spline
            Unity.Mathematics.float3 localPos = riverSpline.transform.InverseTransformPoint(transform.position);
            SplineUtility.GetNearestPoint(riverSpline.Spline, localPos, out Unity.Mathematics.float3 nearest, out float t);
            riverSpline.Spline.Evaluate(t, out _, out Unity.Mathematics.float3 tan, out _);
            currentDirection = riverSpline.transform.TransformDirection(tan).normalized;
        }

        Vector3 riverForce = currentDirection * (riverCurrentSpeed * waterDensity * 0.5f);
        _rb.AddForce(riverForce, ForceMode.Force);
    }

    private void ApplyControls()
    {
        // Forward / Reverse Throttle
        float thrust = _throttleInput >= 0f ? _throttleInput * engineThrust : _throttleInput * reverseThrust;
        Vector3 forwardThrust = transform.forward * (thrust * controlAuthority);
        _rb.AddForce(forwardThrust, ForceMode.Force);

        // Steering torque
        if (Mathf.Abs(_steerInput) > 0.01f)
        {
            // Boat can only steer effectively when it has forward or current momentum
            float speedFactor = Mathf.Clamp01(_rb.linearVelocity.magnitude / 2.5f);
            Vector3 torque = Vector3.up * (_steerInput * steerTorque * controlAuthority * speedFactor);
            _rb.AddTorque(torque, ForceMode.Force);
        }
    }

    private void UpdateWakeVFX()
    {
        float speed = _rb.linearVelocity.magnitude;
        bool isMoving = speed > 0.8f;

        if (bowSprayVFX != null)
        {
            var emission = bowSprayVFX.emission;
            emission.rateOverTime = isMoving ? speed * 12f : 0f;
        }

        if (wakeTrailPort != null) wakeTrailPort.emitting = isMoving;
        if (wakeTrailStarboard != null) wakeTrailStarboard.emitting = isMoving;
    }
}
