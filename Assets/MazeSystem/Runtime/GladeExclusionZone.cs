using System;
using UnityEngine;

namespace MazeSystem
{
    /// <summary>
    /// Glade Exclusion Zone volume.
    /// Strictly guarantees that NO moving maze block, collider, mesh bound, or travelling part
    /// may ever enter or intersect the Glade, its safe area, or the Glade entrance approach during a swap.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class GladeExclusionZone : MonoBehaviour
    {
        [Header("── Zone Volume ───────────────────────────────")]
        [Tooltip("BoxCollider defining the exclusion volume in world space. Auto-generated if null.")]
        public BoxCollider zoneCollider;

        [Tooltip("Additional safety padding (meters) added to each side of the exclusion bounds.")]
        [Range(0f, 50f)] public float paddingMargin = 12f;

        [Header("── Gizmo Visualization ───────────────────────")]
        public bool drawGizmos = true;
        public Color gizmoColor = new Color(1f, 0.2f, 0.2f, 0.35f);
        public Color wireColor = new Color(1f, 0.1f, 0.1f, 0.9f);

        private void Awake()
        {
            EnsureCollider();
        }

        private void EnsureCollider()
        {
            if (zoneCollider == null)
                zoneCollider = GetComponent<BoxCollider>();

            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }
        }

        /// <summary>
        /// Gets the expanded world-space bounds including the configured padding margin.
        /// </summary>
        public Bounds GetExpandedBounds()
        {
            EnsureCollider();
            if (zoneCollider == null)
            {
                // Fallback to transform position with 80m default size
                return new Bounds(transform.position, new Vector3(80f + paddingMargin * 2f, 150f, 80f + paddingMargin * 2f));
            }

            Bounds b = zoneCollider.bounds;
            b.Expand(paddingMargin * 2f);
            return b;
        }

        /// <summary>
        /// Fast, zero-allocation check whether arbitrary test bounds intersect the exclusion zone.
        /// </summary>
        public bool Intersects(Bounds testBounds)
        {
            Bounds exclusion = GetExpandedBounds();
            return exclusion.Intersects(testBounds);
        }

        /// <summary>
        /// Fast check whether a world point lies inside the exclusion zone.
        /// </summary>
        public bool Contains(Vector3 worldPoint)
        {
            Bounds exclusion = GetExpandedBounds();
            return exclusion.Contains(worldPoint);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            Bounds b = GetExpandedBounds();
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
