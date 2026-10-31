using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class SpiderEnemy : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 60f;
    private float currentHealth;

    [Header("AI Ranges & Territory")]
    [Tooltip("Yahan ek BoxCollider ya SphereCollider drag karein jo spider ka area define karega. Iske andar spider ghumega aur player ko chase karega.")]
    public Collider territoryTrigger;
    
    public float attackRange = 3.5f;         // Slightly larger attack zone for natural feel
    
    [Header("Combat Settings")]
    public float attackCooldown = 2.2f;      // Natural attack cycle
    public float damageToPlayer = 10f;

    [Header("Audio SFX")]
    public AudioClip attackSound;
    public AudioClip biteHitSound;
    
    [Header("Movement Settings")]
    public float walkSpeed = 1.8f;           // Natural, calm spider patrol speed
    public float runSpeed = 4.2f;            // Natural chase speed (no supersonic rushing)
    public float rotationSpeed = 6f;         // Smooth look-at speed during attack

    [Header("References")]
    public Transform player;

    private NavMeshAgent agent;
    private Animator anim;
    private AudioSource audioSource;
    private Vector3 startPos;
    private float lastAttackTime;
    private float _animSpeedTarget = 0f;     // Smooth animator Speed blending
    private float _animSpeedCurrent = 0f;
    
    private bool isDead = false;
    private bool _isAttacking;
    private float _hitStunUntil;
    private bool _offMeshJumping;
    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;

    // Animator Parameter Hashes (Fast lookup)
    private int hashSpeed;
    private int hashAttack;
    private int hashHit;
    private int hashDie;
    private int hashJump;

    private void Awake()
    {
        // Collider strictly required for bullets (Physics.Raycast) and sword (Physics.OverlapSphere)
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            CapsuleCollider cap = gameObject.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 1.0f, 0f);
            cap.radius = 2.2f;
            cap.height = 2.5f;
            cap.direction = 1; // Y-axis
        }
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.4f;
        }

        if (attackSound == null)
            attackSound = GameAudio.GetClip(GameAudio.SoundType.SpiderAttack);
        if (biteHitSound == null)
            biteHitSound = GameAudio.GetClip(GameAudio.SoundType.SpiderHurt);

        currentHealth = maxHealth;
        startPos = transform.position;

        // Automatically find player if not assigned
        if (player == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) player = pObj.transform;
        }

        hashSpeed = Animator.StringToHash("Speed");
        hashAttack = Animator.StringToHash("Attack");
        hashHit = Animator.StringToHash("Hit");
        hashDie = Animator.StringToHash("Die");
        hashJump = Animator.StringToHash("Jump");
    }

    void Update()
    {
        if (isDead) return;

        if (anim != null)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsTag("Hit") || Time.time < _hitStunUntil)
            {
                if (agent != null && agent.enabled) agent.isStopped = true;
                return;
            }
        }

        if (agent != null && agent.enabled && agent.isOnOffMeshLink)
        {
            if (!_offMeshJumping)
                StartCoroutine(CompleteOffMeshJump());
            return;
        }

        // Distance aur territory check
        float distanceToPlayer = player != null ? Vector3.Distance(transform.position, player.position) : Mathf.Infinity;
        bool isPlayerInTerritory = territoryTrigger != null && player != null && territoryTrigger.bounds.Contains(player.position);

        if (distanceToPlayer <= attackRange)
        {
            AttackPlayer();
        }
        else if (isPlayerInTerritory)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }

        _animSpeedTarget = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        _animSpeedCurrent = Mathf.Lerp(_animSpeedCurrent, _animSpeedTarget, Time.deltaTime * 8f);
        if (anim != null) anim.SetFloat(hashSpeed, _animSpeedCurrent);
    }

    private System.Collections.IEnumerator CompleteOffMeshJump()
    {
        _offMeshJumping = true;
        if (anim != null)
        {
            anim.ResetTrigger(hashJump);
            anim.SetTrigger(hashJump);
        }
        yield return new WaitForSeconds(0.35f);
        if (agent != null && agent.enabled && agent.isOnOffMeshLink)
            agent.CompleteOffMeshLink();
        _offMeshJumping = false;
    }

    private void Patrol()
    {
        agent.isStopped = false;
        agent.speed = walkSpeed;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            Vector3 randomDir;
            
            if (territoryTrigger != null)
            {
                // Trigger area ke andar ek random jagah chuno
                Bounds b = territoryTrigger.bounds;
                randomDir = new Vector3(
                    Random.Range(b.min.x, b.max.x),
                    transform.position.y,
                    Random.Range(b.min.z, b.max.z)
                );
            }
            else
            {
                // Fallback agar koi area assign na kiya ho
                randomDir = startPos + Random.insideUnitSphere * 15f;
            }
            
            if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    private void ChasePlayer()
    {
        if (player == null) return;
        agent.isStopped = false;
        agent.speed = runSpeed;
        agent.SetDestination(player.position);
    }

    private void AttackPlayer()
    {
        agent.isStopped = true; // Attack ke time ruk jao
        
        // Player ki taraf smooth rotation (natural head tracking)
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0;
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }

        if (!_isAttacking && Time.time >= lastAttackTime + attackCooldown)
        {
            if (anim != null)
            {
                anim.ResetTrigger(hashAttack);
                anim.SetTrigger(hashAttack);
            }
            lastAttackTime = Time.time;
            GameAudio.PlaySpiderAttack(transform.position, audioSource);
            StartCoroutine(DealDamageAfterDelay(0.4f));
        }
    }

    private System.Collections.IEnumerator DealDamageAfterDelay(float delay)
    {
        _isAttacking = true;
        yield return new WaitForSeconds(delay);

        if (!isDead && player != null && Vector3.Distance(transform.position, player.position) <= attackRange + 1f)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null && !pc.IsDead)
            {
                pc.TakeDamage((int)damageToPlayer);
                GameAudio.PlayBulletHit(player.position, true);
                VFXUtil.Burst(player.position + Vector3.up * 1.1f, transform.forward,
                    new Color(0.8f, 0.08f, 0.05f, 1f), 12, 4f, 10f, 0.06f, 0.16f, 0.15f, 0.3f, true, true, 1.5f, 30f);
            }
        }
        _isAttacking = false;
    }

    // Player jab attack kare toh ye function call karega
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log($"<color=#FF2222><b>[Spider Hit!]</b></color> Damage: {amount} | HP: {Mathf.Max(0f, currentHealth)}/{maxHealth}");

        GameAudio.PlaySpiderHurt(transform.position, audioSource, 1.2f);
        VFXUtil.Burst(transform.position + Vector3.up * 0.8f, Vector3.up,
            new Color(0.7f, 0.12f, 0.08f, 1f), 16, 3f, 9f, 0.08f, 0.2f, 0.15f, 0.35f, true, true, 1.8f, 40f);
        _hitStunUntil = Time.time + 0.45f;

        if (currentHealth <= 0)
        {
            Die();
        }
        else if (anim != null)
        {
            anim.ResetTrigger(hashHit);
            anim.SetTrigger(hashHit);
        }
    }

    private void Die()
    {
        isDead = true;
        agent.isStopped = true;
        agent.enabled = false;
        if (anim != null)
        {
            anim.ResetTrigger(hashDie);
            anim.SetTrigger(hashDie);
        }
        
        // Collider band kardo taaki player iss se na takraye
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 5 second baad body gayab kardo
        Destroy(gameObject, 5f);
    }
    
    // Editor me Attack Area dekhne ke liye (Visual Debug)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawWireSphere(transform.position, attackRange);  // Attack area
    }
}
