using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MazeSystem
{
    /// <summary>
    /// Manages the contents of a maze block (enemies, NavMeshAgents, pickups, puzzle pieces, lights).
    /// Safely freezes AI and physics before relocation and warps agents/restores state upon arrival.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MazeBlockContents : MonoBehaviour
    {
        private readonly List<NavMeshAgent> _agents = new List<NavMeshAgent>();
        private readonly List<Rigidbody> _rigidbodies = new List<Rigidbody>();
        private readonly List<bool> _rbKinematicCached = new List<bool>();
        private readonly List<MonoBehaviour> _enemyAIs = new List<MonoBehaviour>();

        private bool _isFrozen;

        /// <summary>
        /// Discovers and caches all dynamic contents currently inside this block.
        /// </summary>
        public void ScanContents()
        {
            _agents.Clear();
            _rigidbodies.Clear();
            _rbKinematicCached.Clear();
            _enemyAIs.Clear();

            // Find all NavMeshAgents
            GetComponentsInChildren(true, _agents);

            // Find all Rigidbodies
            GetComponentsInChildren(true, _rigidbodies);
            for (int i = 0; i < _rigidbodies.Count; i++)
            {
                _rbKinematicCached.Add(_rigidbodies[i] != null && _rigidbodies[i].isKinematic);
            }

            // Find known enemy AI components (SpiderEnemy, Zombie locomotion, etc.)
            var allComponents = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < allComponents.Length; i++)
            {
                var comp = allComponents[i];
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                if (typeName.Contains("Enemy") || typeName.Contains("Zombie") || typeName.Contains("Spider"))
                {
                    _enemyAIs.Add(comp);
                }
            }
        }

        /// <summary>
        /// Freezes AI and physics before the block begins moving.
        /// </summary>
        public void FreezeBeforeMove()
        {
            if (_isFrozen) return;
            ScanContents();

            // 1. Freeze NavMeshAgents
            for (int i = 0; i < _agents.Count; i++)
            {
                var agent = _agents[i];
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
            }

            // 2. Set Rigidbodies kinematic and zero velocity
            for (int i = 0; i < _rigidbodies.Count; i++)
            {
                var rb = _rigidbodies[i];
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }
            }

            // 3. Disable enemy AI
            for (int i = 0; i < _enemyAIs.Count; i++)
            {
                var ai = _enemyAIs[i];
                if (ai != null)
                {
                    ai.enabled = false;
                }
            }

            _isFrozen = true;
        }

        /// <summary>
        /// Unfreezes and warps all NavMeshAgents to their new world positions upon arrival.
        /// </summary>
        public void UnfreezeAfterArrival()
        {
            if (!_isFrozen) return;

            // 1. Warp and restore NavMeshAgents
            for (int i = 0; i < _agents.Count; i++)
            {
                var agent = _agents[i];
                if (agent != null && agent.enabled)
                {
                    agent.Warp(agent.transform.position);
                    agent.isStopped = false;
                }
            }

            // 2. Restore Rigidbodies
            for (int i = 0; i < _rigidbodies.Count; i++)
            {
                var rb = _rigidbodies[i];
                if (rb != null && i < _rbKinematicCached.Count)
                {
                    rb.isKinematic = _rbKinematicCached[i];
                }
            }

            // 3. Re-enable enemy AI
            for (int i = 0; i < _enemyAIs.Count; i++)
            {
                var ai = _enemyAIs[i];
                if (ai != null)
                {
                    ai.enabled = true;
                }
            }

            _isFrozen = false;
        }
    }
}
