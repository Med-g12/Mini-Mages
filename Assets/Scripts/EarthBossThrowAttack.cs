using UnityEngine;
using System.Collections;

public class EarthBossThrowAttack : MonoBehaviour
{
    public float attackInterval = 4f;
    public float projectileSpeed = 10f;
    public float projectileDamage = 20f;
    public float throwDelay = 0.25f; // Delay to sync with throwing animation
    public GameObject boulderPrefab;

    private float nextAttackTime;
    private Transform player;
    private Animator animator;
    private Vector3 originalScale;

    void Start()
    {
        animator = GetComponent<Animator>();
        originalScale = transform.localScale;
        nextAttackTime = Time.time + attackInterval + Random.Range(1f, 2.5f);
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        // Try to automatically find the Earth Wand's projectile if not assigned manually
        if (boulderPrefab == null)
        {
            WandData[] wands = Resources.FindObjectsOfTypeAll<WandData>();
            foreach (WandData wand in wands)
            {
                if (wand.elementType == ElementType.Earth && wand.basicProjectilePrefab != null)
                {
                    boulderPrefab = wand.basicProjectilePrefab;
                    break;
                }
            }
        }
    }

    void Update()
    {
        if (Time.time >= nextAttackTime)
        {
            StartCoroutine(PerformAttack());
            nextAttackTime = Time.time + attackInterval;
        }
    }

    IEnumerator PerformAttack()
    {
        if (player == null) yield break;

        // Play the throwing animation
        if (animator != null)
        {
            animator.Play("EarthBoss_Throwing", -1, 0f);
        }
        
        // Change scale to precisely 1, 1, 1 during throwing by telling the movement script about it
        EnemyMovement movement = GetComponent<EnemyMovement>();
        if (movement != null)
        {
            movement.baseScale = new Vector3(2.5f, 2.5f, 1f);
        }
        else
        {
            transform.localScale = new Vector3(2.5f, 2.5f, 1f);
        }

        // Wait for the throwing animation to fully finish before shooting
        if (animator != null)
        {
            yield return null; // Wait one frame for the animator to transition
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("EarthBoss_Throwing"))
            {
                yield return new WaitForSeconds(stateInfo.length);
            }
            else
            {
                yield return new WaitForSeconds(throwDelay);
            }
        }
        else
        {
            yield return new WaitForSeconds(throwDelay);
        }
        
        if (player == null) yield break;

        // Spawn boulder
        Vector3 targetPos = player.position;
        // Calculate spawn position in front of the boss and centered vertically
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        float facingDir = (sr != null && sr.flipX) ? -1f : 1f;
        
        // Offset by 1.5 units forward, and 0 units vertically (assuming transform.position is roughly center-mass)
        // If transform.position is at the feet, we can add a small Y offset like 0.5f. 
        // Let's use 1.5x and a slight 0.5y to align with the extended arms.
        Vector3 spawnPos = transform.position + new Vector3(facingDir * 2.0f, 0.5f, 0f);

        GameObject proj = CreateProjectile();
        proj.transform.position = spawnPos;

        // Calculate direction and add an upward arc to compensate for gravity
        Vector2 rawDir = (targetPos - spawnPos);
        float distance = rawDir.magnitude;
        Vector2 dir = rawDir.normalized;
        dir.y += distance * 0.08f; // Aim higher the further away the player is
        dir = dir.normalized;

        // Give the boulder a spinning effect
        Rigidbody2D rb = proj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.angularVelocity = -400f * Mathf.Sign(dir.x);
        }

        EnemyProjectile ep = proj.GetComponent<EnemyProjectile>();
        if (ep != null)
        {
            ep.Launch(dir);
        }
        
        // Wait a bit for the animation to finish before reverting scale
        yield return new WaitForSeconds(0.5f);
        if (movement != null)
        {
            movement.baseScale = originalScale;
        }
        else
        {
            transform.localScale = originalScale;
        }
    }

    GameObject CreateProjectile()
    {
        GameObject proj;
        if (boulderPrefab != null)
        {
            proj = Instantiate(boulderPrefab);
            
            // The original player boulder has an offset for orbiting. We need to center it for a straight throw.
            Transform spriteChild = proj.transform.Find("Boulder_0");
            if (spriteChild != null)
            {
                spriteChild.localPosition = Vector3.zero;
            }
            
            // The prefab might not have a collider at the root level, so we must add one to ensure it hits the player
            if (proj.GetComponent<Collider2D>() == null)
            {
                CircleCollider2D col = proj.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.5f;
            }
        }
        else
        {
            // Fallback placeholder if no prefab is found
            proj = new GameObject("FallbackBoulder");
            SpriteRenderer sr = proj.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.6f, 0.4f, 0.2f); // Brown fallback
            proj.AddComponent<CircleCollider2D>().isTrigger = true;
        }

        // Strip the player damage logic and immediately disable it to prevent it from overriding our velocity
        Projectile playerProj = proj.GetComponent<Projectile>();
        if (playerProj != null)
        {
            playerProj.enabled = false;
            Destroy(playerProj);
        }

        // Add Enemy Projectile logic
        EnemyProjectile ep = proj.GetComponent<EnemyProjectile>();
        if (ep == null) ep = proj.AddComponent<EnemyProjectile>();

        ep.speed = projectileSpeed;
        ep.damage = projectileDamage;
        ep.lifetime = 15f; // 15 seconds is enough to leave the screen
        ep.destroyOnHit = true; 

        Rigidbody2D projRb = proj.GetComponent<Rigidbody2D>();
        if (projRb == null) projRb = proj.AddComponent<Rigidbody2D>();
        projRb.bodyType = RigidbodyType2D.Dynamic;
        projRb.gravityScale = 0.65f; // Give it heavy lobbing physics like the player's earth spell
        projRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Re-add the TrailRenderer since we stripped the Player's Projectile script
        TrailRenderer trail = proj.GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = proj.AddComponent<TrailRenderer>();
        }
        trail.time = 0.2f;
        trail.startWidth = 0.35f;
        trail.endWidth = 0.05f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(0.4f, 0.25f, 0.1f, 0.7f); // Earthy brown
        trail.endColor = new Color(0.4f, 0.25f, 0.1f, 0f);
        trail.sortingOrder = 9;

        return proj;
    }
}
