using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class SpiderEnemy : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("AI Ranges & Territory")]
    [Tooltip("Yahan ek BoxCollider ya SphereCollider drag karein jo spider ka area define karega. Iske andar spider ghumega aur player ko chase karega.")]
    public Collider territoryTrigger;
    
    public float attackRange = 2f;
    
    [Header("Combat Settings")]
    public float attackCooldown = 2f;
    public float damageToPlayer = 10f;
    
    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;

    [Header("References")]
    public Transform player;

    private NavMeshAgent agent;
    private Animator anim;
    private Vector3 startPos;
    private float lastAttackTime;
    
    private bool isDead = false;

    // Animator Parameter Hashes (Fast lookup)
    private int hashSpeed;
    private int hashAttack;
    private int hashHit;
    private int hashDie;
    private int hashJump;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
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

        // Agar Hit animation chal raha hai, toh enemy ko roko (stun effect)
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsTag("Hit"))
        {
            agent.isStopped = true;
            return;
        }

        // ── Off-Mesh Link (Jump over rocks/obstacles) ──
        if (agent.isOnOffMeshLink)
        {
            anim.SetTrigger(hashJump);
            agent.CompleteOffMeshLink();
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

        // Update Animator Speed parameter (Isse walk/run blend hoga)
        anim.SetFloat(hashSpeed, agent.velocity.magnitude);
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
        
        // Player ki taraf dekho
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0; // Tedi nahi honi chahiye
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            anim.SetTrigger(hashAttack);
            lastAttackTime = Time.time;
            
            // Damage player ko thode delay ke baad lagna chahiye taaki animation match ho
            StartCoroutine(DealDamageAfterDelay(0.5f));
        }
    }

    private System.Collections.IEnumerator DealDamageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Agar spider mar gaya ya player bahar chala gaya to damage na de
        if (isDead || player == null || Vector3.Distance(transform.position, player.position) > attackRange + 1f)
            yield break;

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null && !pc.IsDead)
        {
            pc.TakeDamage((int)damageToPlayer);
            Debug.Log("Spider attacked player for " + damageToPlayer + " damage!");
        }
    }

    // Player jab attack kare toh ye function call karega
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            anim.SetTrigger(hashHit);
        }
    }

    private void Die()
    {
        isDead = true;
        agent.isStopped = true;
        agent.enabled = false;
        anim.SetTrigger(hashDie);
        
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
