using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class WaterBossDash : MonoBehaviour
{
    public float approachSpeed = 3.5f;
    public float dashInterval = 4f;
    public float dashWindup = 0.35f;
    public float dashDuration = 0.55f;
    public float dashSpeed = 40f;
    public float dashDistance = 22f;
    public float dashDamage = 18f;
    public float dashKnockbackForce = 14f;
    public float dashKnockbackUpwardVelocity = 3f;
    public float dashKnockbackDuration = 0.25f;
    public float minDashDistance = 2f;
    public Color windupColor = new Color(0.35f, 0.9f, 1f, 1f);

    private Rigidbody2D rb;
    private EnemyMovement enemyMovement;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;
    private Transform player;
    private Collider2D playerCollider;
    private Collider2D bossCollider;
    private Collider2D[] dashColliders;
    private bool[] originalColliderTriggerStates;
    private float dashHitRadius = 0.5f;
    private float nextDashTime;
    private float windupEndTime;
    private float dashEndTime;
    private Vector2 dashStartPosition;
    private bool isDashing;
    private bool isWindingUp;
    private bool hitPlayerThisDash;
    private Vector2 dashDirection = Vector2.right;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        enemyMovement = GetComponent<EnemyMovement>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        bossCollider = GetComponent<Collider2D>();
        if (bossCollider == null)
        {
            bossCollider = GetComponentInChildren<Collider2D>();
        }

        dashColliders = GetComponents<Collider2D>();
        if (dashColliders == null || dashColliders.Length == 0)
        {
            dashColliders = GetComponentsInChildren<Collider2D>();
        }

        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
        }
    }

    private void Start()
    {
        if (enemyMovement != null)
        {
            enemyMovement.enabled = false;
        }

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerCollider = playerObject.GetComponent<Collider2D>();
            if (playerCollider == null)
            {
                playerCollider = playerObject.GetComponentInChildren<Collider2D>();
            }
        }

        if (dashColliders != null && dashColliders.Length > 0)
        {
            originalColliderTriggerStates = new bool[dashColliders.Length];
            for (int i = 0; i < dashColliders.Length; i++)
            {
                originalColliderTriggerStates[i] = dashColliders[i].isTrigger;
            }
        }

        if (bossCollider != null)
        {
            dashHitRadius = Mathf.Max(bossCollider.bounds.extents.x, bossCollider.bounds.extents.y) + 0.1f;
        }

        nextDashTime = Time.time + dashInterval;
    }

    private void Update()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }
    }

    private void FixedUpdate()
    {
        if (player == null)
        {
            return;
        }

        if (isWindingUp)
        {
            rb.linearVelocity = Vector2.zero;
            if (Time.time >= windupEndTime)
            {
                BeginDash();
            }

            return;
        }

        if (isDashing)
        {
            rb.linearVelocity = dashDirection * dashSpeed;
            CheckDashHit();
            float traveledDistance = Vector2.Distance(dashStartPosition, transform.position);
            if (traveledDistance >= dashDistance || Time.time >= dashEndTime)
            {
                EndDash();
            }

            return;
        }

        if (Time.time >= nextDashTime &&
            Vector2.Distance(transform.position, player.position) >= minDashDistance)
        {
            BeginWindup();
            return;
        }

        if (Time.time >= nextDashTime)
        {
            nextDashTime = Time.time + 0.5f;
        }

        Vector2 approachDirection = GetDirectionToPlayer();
        rb.linearVelocity = approachDirection * approachSpeed;
        FaceDashDirection(approachDirection);
    }

    private void BeginWindup()
    {
        isWindingUp = true;
        hitPlayerThisDash = false;
        windupEndTime = Time.time + dashWindup;
        rb.linearVelocity = Vector2.zero;
        SetSpriteColor(windupColor);
    }

    private void BeginDash()
    {
        isWindingUp = false;
        isDashing = true;
        dashDirection = GetDirectionToPlayer();
        dashStartPosition = transform.position;
        dashEndTime = Time.time + (dashSpeed > 0f ? dashDistance / dashSpeed : dashDuration);
        SetDashCollidersTrigger(true);
        FaceDashDirection(dashDirection);
    }

    private void EndDash()
    {
        isDashing = false;
        rb.linearVelocity = Vector2.zero;
        SetDashCollidersTrigger(false);
        SetSpriteColor(baseColor);
        nextDashTime = Time.time + dashInterval;
    }

    private Vector2 GetDirectionToPlayer()
    {
        if (player == null)
        {
            return Vector2.right;
        }

        Vector2 direction = player.position - transform.position;
        if (direction.sqrMagnitude < 0.01f)
        {
            return Vector2.right;
        }

        return direction.normalized;
    }

    private void FaceDashDirection(Vector2 direction)
    {
        if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.01f)
        {
            spriteRenderer.flipX = direction.x < 0f;
        }
    }

    private void CheckDashHit()
    {
        if (!isDashing || hitPlayerThisDash || bossCollider == null)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, dashHitRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit == bossCollider || hit.attachedRigidbody == rb)
            {
                continue;
            }

            TryDashHit(hit);
            if (hitPlayerThisDash)
            {
                return;
            }
        }
    }

    private void SetDashCollidersTrigger(bool isTrigger)
    {
        if (dashColliders == null || originalColliderTriggerStates == null)
        {
            return;
        }

        for (int i = 0; i < dashColliders.Length; i++)
        {
            if (dashColliders[i] == null)
            {
                continue;
            }

            if (isTrigger)
            {
                dashColliders[i].isTrigger = true;
            }
            else
            {
                dashColliders[i].isTrigger = originalColliderTriggerStates[i];
            }
        }
    }

    private void SetSpriteColor(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDashHit(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDashHit(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDashHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDashHit(other);
    }

    private void TryDashHit(Collider2D other)
    {
        if (!isDashing || hitPlayerThisDash || other == null)
        {
            return;
        }

        PlayerResources resources = other.GetComponent<PlayerResources>();
        if (resources == null)
        {
            resources = other.GetComponentInParent<PlayerResources>();
        }

        if (resources == null)
        {
            return;
        }

        hitPlayerThisDash = true;
        resources.TakeDamage(dashDamage);

        PlayerController playerController = resources.GetComponent<PlayerController>();
        if (playerController == null)
        {
            playerController = resources.GetComponentInParent<PlayerController>();
        }

        Vector2 knockback = GetDirectionToPlayer() * dashKnockbackForce;
        knockback.y = dashKnockbackUpwardVelocity;
        if (playerController != null)
        {
            playerController.ApplyExternalKnockback(knockback, dashKnockbackDuration);
            return;
        }

        Rigidbody2D playerRb = resources.GetComponent<Rigidbody2D>();
        if (playerRb == null)
        {
            playerRb = resources.GetComponentInParent<Rigidbody2D>();
        }

        if (playerRb != null)
        {
            playerRb.linearVelocity = knockback;
        }
    }
}
