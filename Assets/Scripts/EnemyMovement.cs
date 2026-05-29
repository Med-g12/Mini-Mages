using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    public ElementType enemyElement = ElementType.Earth;
    public float moveSpeed = 2.5f;
    public float flyingSpeed = 3.5f;
    public bool flyAnywhere = false;
    public float contactDamage = 10f;
    public float damageCooldown = 1f;
    public float contactDamageRadius = 0.85f;
    public float knockbackForce = 9f;
    public float knockbackDuration = 0.18f;

    [Header("Flap Animation")]
    public float flapScaleAmount = 0.12f;
    public float flapSpeed = 8f;
    public float flapRotationAmount = 5f;

    [Header("Boss Settings")]
    public GameObject fireBossProjectilePrefab;

    [Header("Highlights & Polish")]
    public Color highlightColor = new Color(1f, 1f, 1f, 1f);
    public float highlightPulseAmount = 0.35f;
    public bool showVisibilityMarker = true;
    public Color markerColor = new Color(1f, 0.95f, 0.1f, 1f);
    public Vector3 markerOffset = new Vector3(0f, 0.68f, 0f);
    public float markerSize = 0.14f;

    private Rigidbody2D rb;
    private Transform player;
    private PlayerResources playerResources;
    private Collider2D playerCollider;
    private SpriteRenderer spriteRenderer;
    private Color baseSpriteColor;
    private Collider2D enemyCollider;
    public Vector3 baseScale;
    private Transform visibilityMarker;
    private float nextDamageTime;
    private float knockbackEndTime;
    private EnemyHealth enemyHealth;
    private float speedMultiplier = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        enemyCollider = GetComponent<Collider2D>();
        enemyHealth = GetComponent<EnemyHealth>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            baseSpriteColor = spriteRenderer.color;
            spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, 30);
        }
        baseScale = transform.localScale;
    }

    void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerResources = playerObject.GetComponent<PlayerResources>();
            playerCollider = playerObject.GetComponent<Collider2D>();
        }

        rb.gravityScale = ShouldFly() ? 0f : rb.gravityScale;
        rb.freezeRotation = true;

        IgnoreEnemyOnlyObstacles();
        IgnorePlayerCollisions();

        EnsureVisibilityMarker();

        if (enemyHealth != null && enemyHealth.isBoss && 
            (enemyElement == ElementType.Fire || enemyHealth.enemyElement == ElementType.Fire || gameObject.name.Contains("Fire_Boss")))
        {
            if (gameObject.GetComponent<FireBossHeatWave>() == null)
            {
                gameObject.AddComponent<FireBossHeatWave>();
            }
            
            FireBossProjectileAttack projAttack = gameObject.GetComponent<FireBossProjectileAttack>();
            if (projAttack == null)
            {
                projAttack = gameObject.AddComponent<FireBossProjectileAttack>();
            }
            
            if (projAttack != null && fireBossProjectilePrefab != null)
            {
                projAttack.projectilePrefab = fireBossProjectilePrefab;
            }
        }
        else if (enemyHealth != null && enemyHealth.isBoss && 
                 (enemyElement == ElementType.Earth || enemyHealth.enemyElement == ElementType.Earth || gameObject.name.Contains("Earth_Boss")))
        {
            if (gameObject.GetComponent<EarthBossThrowAttack>() == null)
            {
                gameObject.AddComponent<EarthBossThrowAttack>();
            }
        }
    }

    void Update()
    {
        AnimateFlap();
        PulseHighlight();
        AnimateVisibilityMarker();
        FacePlayer();
    }

    void FixedUpdate()
    {
        if (player == null) return;
        if (Time.time < knockbackEndTime) return;

        Vector2 direction = player.position - transform.position;
        TryDamagePlayerByDistance();

        if (ShouldFly())
        {
            rb.linearVelocity = direction.normalized * flyingSpeed * speedMultiplier;
        }
        else
        {
            rb.linearVelocity = new Vector2(Mathf.Sign(direction.x) * moveSpeed * speedMultiplier, rb.linearVelocity.y);
        }
    }

    private void AnimateFlap()
    {
        float flap = Mathf.Sin(Time.time * flapSpeed);
        float yScale = 1f + flap * flapScaleAmount;
        transform.localScale = new Vector3(baseScale.x, baseScale.y * yScale, baseScale.z);
        transform.rotation = Quaternion.Euler(0f, 0f, flap * flapRotationAmount);
    }

    private bool ShouldFly()
    {
        return flyAnywhere || enemyElement == ElementType.Fire;
    }

    private void FacePlayer()
    {
        if (player == null || spriteRenderer == null) return;
        spriteRenderer.flipX = player.position.x < transform.position.x;
    }

    private void PulseHighlight()
    {
        if (spriteRenderer == null) return;
        if (enemyHealth != null && enemyHealth.IsHitFlashing) return;

        float pulse = (Mathf.Sin(Time.time * 7f) + 1f) * 0.5f * highlightPulseAmount;
        spriteRenderer.color = Color.Lerp(baseSpriteColor, highlightColor, pulse);
    }

    private void EnsureVisibilityMarker()
    {
        if (enemyHealth != null && enemyHealth.isBoss)
        {
            Transform existingMarker = transform.Find("EnemyMarker");
            if (existingMarker != null)
            {
                Destroy(existingMarker.gameObject);
            }

            return;
        }

        if (!showVisibilityMarker || transform.Find("EnemyMarker") != null) return;

        GameObject markerObject = new GameObject("EnemyMarker");
        markerObject.transform.SetParent(transform, false);
        markerObject.transform.localPosition = markerOffset;
        visibilityMarker = markerObject.transform;

        TextMesh marker = markerObject.AddComponent<TextMesh>();
        marker.text = "!";
        marker.anchor = TextAnchor.MiddleCenter;
        marker.alignment = TextAlignment.Center;
        marker.characterSize = markerSize;
        marker.fontSize = 36;
        marker.color = markerColor;

        MeshRenderer renderer = markerObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 60;
        }
    }

    private void AnimateVisibilityMarker()
    {
        if (visibilityMarker == null) return;

        float bob = Mathf.Sin(Time.time * 6f) * 0.08f;
        float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.12f;
        visibilityMarker.localPosition = markerOffset + new Vector3(0f, bob, 0f);
        visibilityMarker.localScale = new Vector3(pulse, pulse, 1f);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDamagePlayer(collision.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamagePlayer(collision.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other.gameObject);
    }

    private void TryDamagePlayer(GameObject target)
    {
        if (Time.time < nextDamageTime) return;

        PlayerResources resources = target.GetComponent<PlayerResources>();
        if (resources == null)
        {
            resources = target.GetComponentInParent<PlayerResources>();
        }

        if (resources != null)
        {
            DamagePlayer(resources);
        }
    }

    private void TryDamagePlayerByDistance()
    {
        if (Time.time < nextDamageTime || player == null) return;

        if (enemyCollider != null && playerCollider != null)
        {
            ColliderDistance2D distance = enemyCollider.Distance(playerCollider);
            if (distance.isOverlapped || distance.distance <= 0.05f)
            {
                DamagePlayer(playerResources);
                return;
            }
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer <= contactDamageRadius)
        {
            DamagePlayer(playerResources);
        }
    }

    private void DamagePlayer(PlayerResources resources)
    {
        if (resources == null) return;

        resources.TakeDamage(contactDamage);
        if (enemyHealth != null && enemyHealth.isBoss)
        {
            resources.ApplyBossContactKnockback(transform.position);
        }
        else
        {
            ApplyKnockbackFrom(resources.transform.position);
        }

        nextDamageTime = Time.time + damageCooldown;
    }

    public void ApplyKnockbackFrom(Vector3 sourcePosition)
    {
        if (enemyHealth != null && enemyHealth.isBoss)
        {
            return;
        }

        Vector2 away = transform.position - sourcePosition;
        if (away.sqrMagnitude < 0.01f)
        {
            away = Vector2.up;
        }

        rb.linearVelocity = away.normalized * knockbackForce;
        knockbackEndTime = Time.time + knockbackDuration;
    }

    public void ApplyKnockback(Vector2 direction, float force, float duration)
    {
        if (enemyHealth != null && enemyHealth.isBoss)
        {
            return;
        }

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector2.up;
        }

        rb.linearVelocity = direction.normalized * force;
        knockbackEndTime = Time.time + duration;
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0f, multiplier);
    }

    public float GetContactDamage()
    {
        return contactDamage;
    }

    public void RefreshBaseScale()
    {
        baseScale = transform.localScale;
    }

    private void IgnoreEnemyOnlyObstacles()
    {
        if (enemyCollider == null) return;

        int platformLayer = LayerMask.NameToLayer("Platforms");
        Collider2D[] colliders = FindObjectsByType<Collider2D>();
        foreach (Collider2D other in colliders)
        {
            if (other == enemyCollider) continue;

            if ((ShouldFly() && other.gameObject.layer == platformLayer) ||
                IsBoundaryCollider(other))
            {
                Physics2D.IgnoreCollision(enemyCollider, other, true);
            }
        }
    }

    private void IgnorePlayerCollisions()
    {
        if (enemyCollider == null)
        {
            return;
        }

        PlayerResources playerResources = FindAnyObjectByType<PlayerResources>();
        if (playerResources == null)
        {
            return;
        }

        Collider2D[] playerColliders = playerResources.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (playerColliders[i] != null)
            {
                Physics2D.IgnoreCollision(enemyCollider, playerColliders[i], true);
            }
        }
    }

    private bool IsBoundaryCollider(Collider2D other)
    {
        string objectName = other.gameObject.name.ToLowerInvariant();
        return objectName.Contains("wall") ||
               objectName.Contains("boundary") ||
               objectName.Contains("bound");
    }
}
