using System;
using UnityEngine;
using UnityEngine.Events;

namespace MazeSystem
{
    /// <summary>
    /// Invisible blocker that seals the Glade entrance while doors are moving,
    /// the nightly swap is running, or a transition has failed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GladeEntranceGate : MonoBehaviour
    {
        [Header("── Blocker ──────────────────────────────────")]
        [SerializeField] private Collider blocker;

        [Header("── Optional Player Lock ─────────────────────")]
        [Tooltip("Freeze the player's movement while they stand inside the gate volume. " +
                 "Off by default so the gate can never trap the player; the collider alone already blocks passage.")]
        [SerializeField] private bool freezePlayerWhileInside;
        [SerializeField] private float freezeCheckRadius = 1.5f;

        [Header("── Events ───────────────────────────────────")]
        [SerializeField] private UnityEvent onBlocked = new UnityEvent();
        [SerializeField] private UnityEvent onOpened = new UnityEvent();

        public event Action<bool> BlockedChanged;

        public bool IsBlocked { get; private set; }

        private Transform _player;
        private Component _playerComponent;
        private System.Reflection.FieldInfo _frozenField;

        private void Awake()
        {
            if (blocker != null)
                blocker.enabled = false;

            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
            {
                _player = playerObject.transform;
                _playerComponent = playerObject.GetComponent("PlayerController");
                if (_playerComponent != null)
                    _frozenField = _playerComponent.GetType().GetField("isFrozen");
            }
        }

        /// <summary>Editor setup hook.</summary>
        public void EditorConfigure(Collider gateCollider)
        {
            blocker = gateCollider;
            if (blocker != null)
                blocker.enabled = false;
        }

        public void SetBlocked(bool blocked)
        {
            if (IsBlocked == blocked)
                return;

            IsBlocked = blocked;
            if (blocker != null)
                blocker.enabled = blocked;

            if (!blocked)
                SetPlayerFrozen(false);

            if (blocked)
                onBlocked.Invoke();
            else
                onOpened.Invoke();

            BlockedChanged?.Invoke(blocked);
        }

        private void Update()
        {
            if (!freezePlayerWhileInside || !IsBlocked || _player == null)
                return;

            bool inside = Vector3.Distance(_player.position, transform.position) <= freezeCheckRadius;
            SetPlayerFrozen(inside);
        }

        private void OnDisable()
        {
            SetPlayerFrozen(false);
        }

        private void SetPlayerFrozen(bool frozen)
        {
            if (_frozenField == null || _playerComponent == null)
                return;

            object current = _frozenField.GetValue(_playerComponent);
            if (current is bool b && b != frozen)
                _frozenField.SetValue(_playerComponent, frozen);
        }
    }
}
