using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Controls player boarding and exiting via interaction key (E or B).
/// - Disables CharacterController and locks player into seat transform.
/// - Passes driving control to RealisticBoatBuoyancy.
/// - Transitions between Player Camera and Boat Follow Camera (supporting both Cinemachine & BoatFollowCamera).
/// - Safe shore/dock dismount with collision check.
/// </summary>
public class BoatBoardingController : MonoBehaviour
{
    [Header("Boat Bindings")]
    [SerializeField] private RealisticBoatBuoyancy boatBuoyancy;
    [SerializeField] private Transform seatPoint;
    [SerializeField] private Transform dockExitPoint;

    [Header("Cameras (Assign Cinemachine OR BoatFollowCamera)")]
    [SerializeField] private CinemachineCamera playerCinemachineCam;
    [SerializeField] private CinemachineCamera boatCinemachineCam;
    [SerializeField] private BoatFollowCamera standaloneBoatCam;

    [Header("Interaction & Input")]
    public float interactionDistance = 3.5f;
    public KeyCode interactionKey = KeyCode.E;
    public KeyCode alternateKey = KeyCode.B;

    [Header("Animations")]
    public string sitStateName = "SitDown";
    public string standStateName = "StandUp";

    private bool _isBoarded = false;
    private CharacterController _playerCC;
    private Transform _playerTransform;
    private Animator _playerAnimator;

    public bool IsBoarded => _isBoarded;

    private void Start()
    {
        if (boatBuoyancy == null) boatBuoyancy = GetComponent<RealisticBoatBuoyancy>();
        if (boatCinemachineCam != null) boatCinemachineCam.Priority = 0;
        if (playerCinemachineCam != null) playerCinemachineCam.Priority = 10;
        if (standaloneBoatCam != null) standaloneBoatCam.enabled = false;
        if (boatBuoyancy != null) boatBuoyancy.controlAuthority = 0f;
    }

    private void Update()
    {
        if (!_isBoarded)
        {
            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    _playerTransform = player.transform;
                    _playerCC = player.GetComponent<CharacterController>();
                    _playerAnimator = player.GetComponentInChildren<Animator>();
                }
            }

            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(_playerTransform.position, transform.position);
                if (dist <= interactionDistance && (Input.GetKeyDown(interactionKey) || Input.GetKeyDown(alternateKey)))
                {
                    StartCoroutine(BoardRoutine());
                }
            }
        }
        else
        {
            // Driving inputs (WASD)
            float throttle = Input.GetAxis("Vertical");   // W / S
            float steer = Input.GetAxis("Horizontal");    // A / D
            if (boatBuoyancy != null) boatBuoyancy.SetPlayerInput(throttle, steer);

            // Exit input
            if (Input.GetKeyDown(interactionKey) || Input.GetKeyDown(alternateKey) || Input.GetKeyDown(KeyCode.F))
            {
                StartCoroutine(ExitRoutine());
            }
        }
    }

    private IEnumerator BoardRoutine()
    {
        _isBoarded = true;

        if (_playerCC != null) _playerCC.enabled = false;

        // Smooth seat transition (0.35s)
        Vector3 startPos = _playerTransform.position;
        Quaternion startRot = _playerTransform.rotation;
        Vector3 targetPos = seatPoint != null ? seatPoint.position : transform.position;
        Quaternion targetRot = seatPoint != null ? seatPoint.rotation : transform.rotation;

        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / 0.35f);
            if (_playerTransform != null)
            {
                _playerTransform.position = Vector3.Lerp(startPos, targetPos, t);
                _playerTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            }
            yield return null;
        }

        // Parent player to boat seat
        if (_playerTransform != null && seatPoint != null)
        {
            _playerTransform.SetParent(seatPoint);
            _playerTransform.localPosition = Vector3.zero;
            _playerTransform.localRotation = Quaternion.identity;
        }

        // Play sit animation if available
        if (_playerAnimator != null && !string.IsNullOrEmpty(sitStateName))
        {
            _playerAnimator.CrossFade(sitStateName, 0.15f);
        }

        // Hand over control authority to boat
        if (boatBuoyancy != null) boatBuoyancy.controlAuthority = 1f;

        // Camera activation
        if (boatCinemachineCam != null) boatCinemachineCam.Priority = 20;
        if (standaloneBoatCam != null)
        {
            standaloneBoatCam.enabled = true;
            standaloneBoatCam.SetTarget(transform, boatBuoyancy);
        }
    }

    private IEnumerator ExitRoutine()
    {
        _isBoarded = false;

        if (boatBuoyancy != null)
        {
            boatBuoyancy.controlAuthority = 0f;
            boatBuoyancy.SetPlayerInput(0f, 0f);
        }

        // Play stand animation
        if (_playerAnimator != null && !string.IsNullOrEmpty(standStateName))
        {
            _playerAnimator.CrossFade(standStateName, 0.15f);
        }

        yield return new WaitForSeconds(0.2f);

        // Disembark to safe shore/dock position
        if (_playerTransform != null)
        {
            _playerTransform.SetParent(null);
            Vector3 exitPos = dockExitPoint != null 
                ? dockExitPoint.position 
                : transform.position + transform.right * 2.8f + Vector3.up * 0.6f;
            _playerTransform.position = exitPos;
            if (_playerCC != null) _playerCC.enabled = true;
        }

        // Blend back to player camera
        if (boatCinemachineCam != null) boatCinemachineCam.Priority = 0;
        if (standaloneBoatCam != null) standaloneBoatCam.enabled = false;
    }
}
