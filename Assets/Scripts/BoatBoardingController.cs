using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Controls player boarding/exiting, CharacterController handoff,
/// and smooth Cinemachine 3 camera priority transitions.
/// </summary>
public class BoatBoardingController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RealisticBoatBuoyancy boatBuoyancy;
    [SerializeField] private Transform seatPoint;
    [SerializeField] private Transform dockExitPoint;

    [Header("Cinemachine 3 Cameras")]
    [Tooltip("Normal on-foot player third-person camera.")]
    [SerializeField] private CinemachineCamera playerCinemachineCam;
    [Tooltip("Behind-boat follow camera.")]
    [SerializeField] private CinemachineCamera boatCinemachineCam;

    [Header("Prompt & Range")]
    [SerializeField] private float boardDistance = 3.5f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    private bool _isBoarded = false;
    private CharacterController _playerCC;
    private Transform _playerTransform;

    private void Start()
    {
        if (boatCinemachineCam != null) boatCinemachineCam.Priority = 0;
        if (playerCinemachineCam != null) playerCinemachineCam.Priority = 10;
        if (boatBuoyancy != null) boatBuoyancy.controlAuthority = 0f;
    }

    private void Update()
    {
        if (!_isBoarded)
        {
            // Detect player proximity
            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    _playerTransform = player.transform;
                    _playerCC = player.GetComponent<CharacterController>();
                }
            }

            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(_playerTransform.position, transform.position);
                if (dist <= boardDistance && Input.GetKeyDown(interactionKey))
                {
                    BoardBoat();
                }
            }
        }
        else
        {
            // Drive boat input
            float throttle = Input.GetAxis("Vertical");   // W / S
            float steer = Input.GetAxis("Horizontal");    // A / D
            if (boatBuoyancy != null) boatBuoyancy.SetPlayerInput(throttle, steer);

            // Exit boat
            if (Input.GetKeyDown(interactionKey))
            {
                ExitBoat();
            }
        }
    }

    private void BoardBoat()
    {
        _isBoarded = true;

        if (_playerCC != null) _playerCC.enabled = false;
        
        // Parent player to boat seat
        if (_playerTransform != null && seatPoint != null)
        {
            _playerTransform.SetParent(seatPoint);
            _playerTransform.localPosition = Vector3.zero;
            _playerTransform.localRotation = Quaternion.identity;
        }

        // Grant boat control
        if (boatBuoyancy != null) boatBuoyancy.controlAuthority = 1f;

        // Blend Cinemachine 3 cameras
        if (boatCinemachineCam != null) boatCinemachineCam.Priority = 20;
    }

    private void ExitBoat()
    {
        _isBoarded = false;
        if (boatBuoyancy != null)
        {
            boatBuoyancy.controlAuthority = 0f;
            boatBuoyancy.SetPlayerInput(0f, 0f);
        }

        // Reposition player to safe dock/shore
        if (_playerTransform != null)
        {
            _playerTransform.SetParent(null);
            Vector3 exitPos = dockExitPoint != null ? dockExitPoint.position : transform.position + transform.right * 2.5f + Vector3.up * 0.5f;
            _playerTransform.position = exitPos;
            if (_playerCC != null) _playerCC.enabled = true;
        }

        // Blend back to player camera
        if (boatCinemachineCam != null) boatCinemachineCam.Priority = 0;
    }
}

