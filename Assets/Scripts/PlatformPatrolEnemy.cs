using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlatformPatrolEnemy : MonoBehaviour
{
    public float walkSpeed = 2.2f;
    public float edgeCheckDistance = 0.35f;
    public float wallCheckDistance = 0.25f;
    public float contactDamage = 10f;
    public float contactDamageCooldown = 0.8f;
    public float playerKnockbackForce = 10f;
    public float playerKnockbackUpwardVelocity = 4f;
    public float playerKnockbackDuration = 0.22f;
    public string nameTagText = "Baktin ni Admiral";
    public Vector3 nameTagOffset = new Vector3(0f, 0.30f, 0f);
    public float nameTagSize = 0.02f;
    public Color nameTagColor = Color.white;
    public LayerMask groundLayerMask;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private float direction = 1f;
    private float nextContactDamageTime;
    private bool hasPatrolBounds;
    private float patrolMinX;
    private float patrolMaxX;
    private float lockedZ;
    private float patrolX;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        if (groundLayerMask.value == 0)
        {
            groundLayerMask = LayerMask.GetMask("Platforms", "Default");
        }

        rb.freezeRotation = true;
        rb.mass = 1000f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.sharedMaterial = CreateFrictionlessMaterial();
        lockedZ = transform.position.z;
        patrolX = transform.position.x;
        IgnoreEnemyCollisions();
        IgnorePlayerCollisions();
        EnsureNameTag();
    }

    private void FixedUpdate()
    {
        TryDamageNearbyPlayer();

        if (ShouldTurnAround())
        {
            direction *= -1f;
        }

        patrolX += direction * walkSpeed * Time.fixedDeltaTime;

        if (hasPatrolBounds)
        {
            patrolX = Mathf.Clamp(patrolX, patrolMinX, patrolMaxX);
        }

        Vector2 targetPosition = new Vector2(patrolX, rb.position.y);
        rb.MovePosition(targetPosition);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = direction < 0f;
        }
    }

    private void TryDamageNearbyPlayer()
    {
        if (Time.time < nextContactDamageTime || bodyCollider == null)
        {
            return;
        }

        PlayerResources resources = FindAnyObjectByType<PlayerResources>();
        if (resources == null)
        {
            return;
        }

        Collider2D playerCollider = resources.GetComponent<Collider2D>();
        if (playerCollider == null)
        {
            playerCollider = resources.GetComponentInChildren<Collider2D>();
        }

        if (playerCollider != null)
        {
            ColliderDistance2D distance = bodyCollider.Distance(playerCollider);
            if (distance.isOverlapped || distance.distance <= 0.05f)
            {
                resources.TakeDamage(contactDamage);
                KnockbackPlayer(resources);
                nextContactDamageTime = Time.time + contactDamageCooldown;
            }
        }
    }

    private bool ShouldTurnAround()
    {
        Bounds bounds = bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one);
        if (hasPatrolBounds)
        {
            return (direction > 0f && patrolX >= patrolMaxX) ||
                   (direction < 0f && patrolX <= patrolMinX);
        }

        Vector2 frontFoot = new Vector2(
            direction > 0f ? bounds.max.x : bounds.min.x,
            bounds.min.y + 0.05f
        );

        RaycastHit2D groundAhead = Physics2D.Raycast(frontFoot, Vector2.down, edgeCheckDistance, groundLayerMask);
        if (groundAhead.collider == null || groundAhead.collider.isTrigger)
        {
            return true;
        }

        Vector2 wallOrigin = new Vector2(
            direction > 0f ? bounds.max.x : bounds.min.x,
            bounds.center.y
        );
        RaycastHit2D wallAhead = Physics2D.Raycast(wallOrigin, Vector2.right * direction, wallCheckDistance, groundLayerMask);
        return wallAhead.collider != null && !wallAhead.collider.isTrigger;
    }

    public void SetPatrolBounds(float minX, float maxX)
    {
        patrolMinX = Mathf.Min(minX, maxX);
        patrolMaxX = Mathf.Max(minX, maxX);
        patrolX = Mathf.Clamp(transform.position.x, patrolMinX, patrolMaxX);
        hasPatrolBounds = true;
    }

    public void SetDirection(float newDirection)
    {
        direction = newDirection < 0f ? -1f : 1f;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryIgnoreNonPlayerNonPlatform(collision.collider);
        TryDamagePlayer(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryIgnoreNonPlayerNonPlatform(collision.collider);
        TryDamagePlayer(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryIgnoreNonPlayerNonPlatform(other);
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryIgnoreNonPlayerNonPlatform(other);
        TryDamagePlayer(other);
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (Time.time < nextContactDamageTime || other == null)
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

        resources.TakeDamage(contactDamage);
        KnockbackPlayer(resources);
        nextContactDamageTime = Time.time + contactDamageCooldown;
    }

    private void KnockbackPlayer(PlayerResources resources)
    {
        Rigidbody2D playerRb = resources.GetComponent<Rigidbody2D>();
        if (playerRb == null)
        {
            playerRb = resources.GetComponentInParent<Rigidbody2D>();
        }

        if (playerRb == null)
        {
            return;
        }

        float knockbackDirection = resources.transform.position.x >= transform.position.x ? 1f : -1f;
        Vector2 knockbackVelocity = new Vector2(
            knockbackDirection * playerKnockbackForce,
            playerKnockbackUpwardVelocity
        );

        PlayerController playerController = resources.GetComponent<PlayerController>();
        if (playerController == null)
        {
            playerController = resources.GetComponentInParent<PlayerController>();
        }

        if (playerController != null)
        {
            playerController.ApplyExternalKnockback(knockbackVelocity, playerKnockbackDuration);
        }
        else
        {
            playerRb.linearVelocity = knockbackVelocity;
        }
    }

    private void IgnoreEnemyCollisions()
    {
        if (bodyCollider == null)
        {
            return;
        }

        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        for (int i = 0; i < colliders.Length; i++)
        {
            TryIgnoreNonPlayerNonPlatform(colliders[i]);
        }
    }

    private void IgnorePlayerCollisions()
    {
        if (bodyCollider == null)
        {
            return;
        }

        PlayerResources resources = FindAnyObjectByType<PlayerResources>();
        if (resources == null)
        {
            return;
        }

        Collider2D[] playerColliders = resources.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (playerColliders[i] != null)
            {
                Physics2D.IgnoreCollision(bodyCollider, playerColliders[i], true);
            }
        }
    }

    private void TryIgnoreNonPlayerNonPlatform(Collider2D other)
    {
        if (bodyCollider == null ||
            other == null ||
            other == bodyCollider ||
            other.GetComponentInParent<PlayerResources>() != null)
        {
            return;
        }

        int platformLayer = LayerMask.NameToLayer("Platforms");
        if (platformLayer >= 0 && other.gameObject.layer == platformLayer)
        {
            return;
        }

        Physics2D.IgnoreCollision(bodyCollider, other, true);
    }

    private PhysicsMaterial2D CreateFrictionlessMaterial()
    {
        PhysicsMaterial2D material = new PhysicsMaterial2D("BaktinFrictionless");
        material.friction = 0f;
        material.bounciness = 0f;
        return material;
    }

    private void EnsureNameTag()
    {
        if (transform.Find("NameTag") != null)
        {
            return;
        }

        GameObject nameTagObject = new GameObject("NameTag");
        nameTagObject.transform.SetParent(transform, false);
        nameTagObject.transform.localPosition = nameTagOffset;

        TextMesh text = nameTagObject.AddComponent<TextMesh>();
        text.text = nameTagText;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = nameTagSize;
        text.fontSize = 32;
        text.color = nameTagColor;

        MeshRenderer renderer = nameTagObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 80;
        }
    }
}
