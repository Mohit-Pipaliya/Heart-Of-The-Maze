using UnityEngine;

namespace MazeSystem
{
    /// <summary>
    /// Marks one of the eight maze blocks. Stores its original transform so the maze can
    /// recover safely and so the optional "restore layout" pass has a target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MazeBlock : MonoBehaviour
    {
        [Header("── Identity ─────────────────────────────────")]
        [SerializeField] [Range(1, 8)] private int blockNumber = 1;

        [Header("── Main Entrance ────────────────────────────")]
        [SerializeField] private Transform mainEntrance;
        [SerializeField] private MazeMainDoor mainDoor;

        [Tooltip("Used only when no door or entrance marker is assigned.")]
        [SerializeField] private Vector3 fallbackLocalOutward = Vector3.forward;

        [Header("── Contents Controller ──────────────────────")]
        [SerializeField] private MazeBlockContents contents;

        [Header("── Original Layout (read-only) ──────────────")]
        [SerializeField] private Vector3 originalPosition;
        [SerializeField] private Quaternion originalRotation = Quaternion.identity;
        [SerializeField] private Vector3 originalScale = Vector3.one;
        [SerializeField] private Transform originalParent;
        [SerializeField] private bool originalActive = true;
        [SerializeField] private bool captured;
        [SerializeField] private Vector3 footprintCenterLocal;
        [SerializeField] private Vector3 footprintSize = new Vector3(1000f, 100f, 1000f);

        public int BlockNumber => blockNumber;
        public Transform MainEntrance => mainEntrance;
        public MazeMainDoor MainDoor => mainDoor;
        public IMazeDoor Door => (IMazeDoor)mainDoor ?? (IMazeDoor)GetComponentInChildren<IMazeDoor>(true);
        public MazeBlockContents Contents => contents != null ? contents : (contents = GetComponent<MazeBlockContents>() ?? gameObject.AddComponent<MazeBlockContents>());
        public bool Captured => captured;
        public Vector3 OriginalPosition => originalPosition;
        public Quaternion OriginalRotation => originalRotation;
        public Vector3 OriginalScale => originalScale;
        public bool OriginalActive => originalActive;
        public Vector3 FootprintCenterLocal => footprintCenterLocal;
        public Vector3 FootprintSize => footprintSize;

        public Transform DoorTransform => mainDoor != null
            ? mainDoor.transform
            : (mainEntrance != null ? mainEntrance : null);

        public Vector3 DoorOutwardDirection
        {
            get
            {
                if (mainDoor != null)
                    return mainDoor.transform.forward;
                if (mainEntrance != null)
                    return mainEntrance.forward;
                return transform.TransformDirection(fallbackLocalOutward.normalized);
            }
        }

        private void Awake()
        {
            if (contents == null)
                contents = GetComponent<MazeBlockContents>() ?? gameObject.AddComponent<MazeBlockContents>();

            if (!captured)
                CaptureOriginalState();
        }

        public void Configure(int number, Transform entrance, MazeMainDoor door)
        {
            blockNumber = Mathf.Clamp(number, 1, 8);
            mainEntrance = entrance;
            mainDoor = door;
            if (contents == null)
                contents = GetComponent<MazeBlockContents>() ?? gameObject.AddComponent<MazeBlockContents>();

            if (!captured)
                CaptureOriginalState();
        }

        public void CaptureOriginalState()
        {
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            originalScale = transform.lossyScale;
            originalParent = transform.parent;
            originalActive = gameObject.activeSelf;

            CaptureFootprint();
            captured = true;
        }

        public void CaptureFootprint()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds union = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer)
                    continue;

                if (!hasBounds)
                {
                    union = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    union.Encapsulate(r.bounds);
                }
            }

            if (hasBounds)
            {
                footprintCenterLocal = transform.InverseTransformPoint(union.center);
                footprintSize = union.size;
            }
        }

        public Bounds GetFootprintWorldBounds()
        {
            Vector3 center = transform.TransformPoint(footprintCenterLocal);
            return new Bounds(center, footprintSize);
        }

        public void RestoreOriginalState()
        {
            if (!captured)
                return;

            transform.SetParent(originalParent, true);
            transform.SetPositionAndRotation(originalPosition, originalRotation);
            transform.localScale = originalScale;
            gameObject.SetActive(originalActive);
        }

        public bool TryGetDoorLocalPose(out Vector3 localPosition, out Quaternion localRotation)
        {
            Transform door = DoorTransform;
            if (door == null)
            {
                localPosition = Vector3.zero;
                localRotation = Quaternion.identity;
                return false;
            }

            localPosition = transform.InverseTransformPoint(door.position);
            localRotation = Quaternion.Inverse(transform.rotation) * door.rotation;
            return true;
        }

        public void SetTransformPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            transform.SetPositionAndRotation(worldPosition, worldRotation);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (blockNumber < 1) blockNumber = 1;
            if (blockNumber > 8) blockNumber = 8;
        }
#endif
    }
}
