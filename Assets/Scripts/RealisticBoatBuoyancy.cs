using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// AAA Multi-point Boat Buoyancy & Physics Controller for Unity 6 (URP).
/// - Multi-point hull floatation with 4-6 sample points matching GerstnerWaveMath and TempleRunRiver.
/// - Believable heave, pitch, and roll with wave slope.
/// - Bank/lean into turns for dynamic runner gameplay feel.
/// - Heavy hydrodynamic drag preventing erratic oscillations.
/// - Downhill river current drift.
/// - Complete wake and spray VFX triggers.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RealisticBoatBuoyancy : MonoBehaviour
{
    [Header("Hull Float Points (6 Points Recommended)")]
    [Tooltip("Place transforms at: Bow, Stern, Port, Starboard, Center-Left, Center-Right")]
    [SerializeField] private Transform[] floatPoints;
    [SerializeField] private float pointVolume = 0.55f;
    [SerializeField] private float maxImmersionDepth = 0.75f;
    [SerializeField] private float waterDensity = 1025f;

    [Header("Hydrodynamic Damping (Heavy Water Feel)")]
    [SerializeField] private float waterLinearDamping = 1.9f;
    [SerializeField] private float waterAngularDamping = 3.6f;
    [SerializeField] private float verticalPointDamping = 620f;

    [Header("Engine & Steering (WASD Controls)")]
    public float forwardThrust = 4200f;
    public float reverseThrust = 2100f;
    public float steerTorque = 1200f;
    [Tooltip("How much the boat banks/leans into turns (Degrees/Torque ratio)")]
    public float turnBankingLean = 450f;
    [Range(0f, 1f)] public float controlAuthority = 0f;

    [Header("River Current & Downhill Drift")]
    [SerializeField] private SplineContainer riverSpline;
    public float riverCurrentDriftSpeed = 3.5f;

    [Header("Wake & Particle VFX")]
    [SerializeField] private ParticleSystem bowSplashVFX;
    [SerializeField] private TrailRenderer sternWakePort;
    [SerializeField] private TrailRenderer sternWakeStarboard;

    private Rigidbody _rb;
    private float _throttleInput;
    private float _steerInput;
    private bool _isSubmerged;

    public Rigidbody BoatRigidbody => _rb;
    public bool IsSubmerged => _isSubmerged;
    public float CurrentSpeed => _rb != null ? _rb.linearVelocity.magnitude : 0f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        
        // Unity 6 Rigidbody settings
        _rb.linearDamping = 0.05f;
        _rb.angularDamping = 0.05f;
        _rb.centerOfMass = new Vector3(0f, -0.42f, 0f); // Low center of gravity prevents tipping over
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

        // 1. Multi-point Buoyancy sampling shared Gerstner / Sloped River Math
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

                    // Buoyant lift
                    Vector3 buoyantForce = Vector3.up * buoyantForceMagnitude;
                    _rb.AddForceAtPosition(buoyantForce, ptPos, ForceMode.Force);

                    // Point water drag (dampens vertical bouncing and rocking)
                    Vector3 ptVelocity = _rb.GetPointVelocity(ptPos);
                    Vector3 verticalDamping = -Vector3.up * (ptVelocity.y * verticalPointDamping * immersion01);
                    _rb.AddForceAtPosition(verticalDamping, ptPos, ForceMode.Force);
                }
            }
        }

        _isSubmerged = submergedCount > 0;

        if (_isSubmerged)
        {
            float subRatio = (float)submergedCount / Mathf.Max(1, floatPoints.Length);

            // Water linear and angular drag (Unity 6 linearVelocity)
            Vector3 v = _rb.linearVelocity;
            _rb.linearVelocity = Vector3.Lerp(v, new Vector3(v.x * 0.98f, v.y * 0.94f, v.z * 0.98f), waterLinearDamping * subRatio * Time.fixedDeltaTime);
            _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, Vector3.zero, waterAngularDamping * subRatio * Time.fixedDeltaTime);

            // 2. River Current Drift (Carries boat downstream along slope/spline)
            ApplyRiverCurrent();

            // 3. Engine Throttle, Steering & Banking Lean
            if (controlAuthority > 0.01f)
            {
                ApplyControls();
            }
        }

        UpdateWakeVFX();
    }

    private void ApplyRiverCurrent()
    {
        Vector3 flowDirection = transform.forward;

        if (TempleRunRiver.Instance != null && TempleRunRiver.Instance.enabled)
        {
            flowDirection = TempleRunRiver.Instance.GetFlowDirection(transform.position);
        }
        else if (riverSpline != null && riverSpline.Splines != null && riverSpline.Splines.Count > 0)
        {
            Unity.Mathematics.float3 localPos = riverSpline.transform.InverseTransformPoint(transform.position);
            SplineUtility.GetNearestPoint(riverSpline.Spline, localPos, out _, out float t);
            riverSpline.Spline.Evaluate(t, out _, out Unity.Mathematics.float3 tan, out _);
            flowDirection = riverSpline.transform.TransformDirection(tan).normalized;
        }

        Vector3 riverForce = flowDirection * (riverCurrentDriftSpeed * waterDensity * 0.45f);
        _rb.AddForce(riverForce, ForceMode.Force);
    }

    private void ApplyControls()
    {
        // Throttle (Forward / Reverse)
        float thrust = _throttleInput >= 0f ? _throttleInput * forwardThrust : _throttleInput * reverseThrust;
        Vector3 thrustForce = transform.forward * (thrust * controlAuthority);
        _rb.AddForce(thrustForce, ForceMode.Force);

        // Steering torque
        if (Mathf.Abs(_steerInput) > 0.01f)
        {
            float speedFactor = Mathf.Clamp01(_rb.linearVelocity.magnitude / 2.0f);
            Vector3 steer = Vector3.up * (_steerInput * steerTorque * controlAuthority * speedFactor);
            _rb.AddTorque(steer, ForceMode.Force);

            // Bank / Roll lean into turns (Temple Run style dynamic lean)
            Vector3 bankingTorque = -transform.forward * (_steerInput * turnBankingLean * controlAuthority * speedFactor);
            _rb.AddTorque(bankingTorque, ForceMode.Force);
        }
    }

    private void UpdateWakeVFX()
    {
        float speed = _rb.linearVelocity.magnitude;
        bool isMoving = speed > 0.8f && _isSubmerged;

        if (bowSplashVFX != null)
        {
            var emission = bowSplashVFX.emission;
            emission.rateOverTime = isMoving ? speed * 15f : 0f;
        }

        if (sternWakePort != null) sternWakePort.emitting = isMoving;
        if (sternWakeStarboard != null) sternWakeStarboard.emitting = isMoving;
    }
}
