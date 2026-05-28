using UnityEngine;
using System.Collections.Generic;

public enum ElementType { Wind, Water, Earth, Fire }
public enum ProjectileImpactMode { SingleTarget, EarthAoE, WaterSpray, FireBreath }

public class Projectile : MonoBehaviour
{
    public ElementType element;
    public float speed = 12f;
    public float damage = 10f;
    public float lifetime = 3f;
    public float spinSpeed = 720f;
    public bool isHeldByPlayer;
    public ProjectileImpactMode impactMode = ProjectileImpactMode.SingleTarget;
    public float knockbackForce = 0f;
    public float knockbackDuration = 0.2f;
    public bool windImpactAoE = false;

    [Header("Area Effect")]
    public float areaRadius = 2.25f;
    public float areaDamageMultiplier = 1f;
    public LayerMask enemyLayers = ~0;
    public float tickInterval = 0.2f;
    public bool canDamageWhileHeld = false;
    public Vector2 heldAttachOffset = Vector2.zero;
    public float waterSlowMultiplier = 0.35f;
    public float waterSlowDuration = 1.5f;

    [Header("Animation")]
    public Sprite[] startupAnimationFrames;
    public Sprite[] animationFrames;
    public float framesPerSecond = 12f;

    [Header("Shatter")]
    public Sprite[] shatterSprites;
    public int shatterPieceCount = 10;
    public float shatterForce = 5f;
    public float shatterLifetime = 0.45f;
    public Vector2 shatterPieceScaleRange = new Vector2(0.7f, 1.15f);

    [Header("Trail")]
    public bool showTrail = true;
    public float trailTime = 0.18f;
    public float trailStartWidth = 0.16f;
    public float trailEndWidth = 0.02f;
    public Color trailStartColor = new Color(0.85f, 1f, 1f, 0.75f);
    public Color trailEndColor = new Color(0.85f, 1f, 1f, 0f);

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private float animationTimer;
    private int animationFrameIndex;
    private bool finishedStartupAnimation;
    private float nextAreaTickTime;
    private readonly HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();
    private static Material trailMaterial;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sortingOrder < 10)
        {
            spriteRenderer.sortingOrder = 10;
        }

    }

    void Start()
    {
        SetupTrail();

        rb = GetComponent<Rigidbody2D>();
        if (rb != null && !isHeldByPlayer)
        {
            rb.linearVelocity = transform.right * speed;
        }

        if (!isHeldByPlayer)
        {
            Destroy(gameObject, lifetime);
        }
    }

    void Update()
    {
        if (spinSpeed != 0f)
        {
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        }

        AnimateProjectile();

        if ((!isHeldByPlayer || canDamageWhileHeld) &&
            (impactMode == ProjectileImpactMode.WaterSpray ||
             impactMode == ProjectileImpactMode.FireBreath) &&
            Time.time >= nextAreaTickTime)
        {
            TickAreaDamage();
            nextAreaTickTime = Time.time + tickInterval;
        }
    }

    public void HoldInPlace()
    {
        isHeldByPlayer = true;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    public void Launch(Vector2 direction)
    {
        isHeldByPlayer = false;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb != null)
        {
            rb.linearVelocity = direction.normalized * speed;
        }

        Destroy(gameObject, lifetime);
    }

    private void SetupTrail()
    {
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
        }

        Color elementColor = GetElementTrailColor();
        trail.material = GetTrailMaterial();

        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = trailEndWidth;
        trail.startColor = new Color(
            elementColor.r,
            elementColor.g,
            elementColor.b,
            trailStartColor.a
        );
        trail.endColor = new Color(
            elementColor.r,
            elementColor.g,
            elementColor.b,
            trailEndColor.a
        );
        trail.sortingOrder = 9;
        trail.autodestruct = false;
        trail.emitting = showTrail;
    }

    private Color GetElementTrailColor()
    {
        switch (element)
        {
            case ElementType.Water:
                return new Color(0.25f, 0.65f, 1f);
            case ElementType.Earth:
                return new Color(0.45f, 0.9f, 0.35f);
            case ElementType.Fire:
                return new Color(1f, 0.35f, 0.12f);
            default:
                return new Color(0.82f, 0.84f, 0.86f);
        }
    }

    private static Material GetTrailMaterial()
    {
        if (trailMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            trailMaterial = new Material(shader);
        }

        return trailMaterial;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy") || other.CompareTag("Boss"))
        {
            EnemyHealth enemy = other.GetComponent<EnemyHealth>();
            if (enemy == null)
            {
                enemy = other.GetComponentInParent<EnemyHealth>();
            }

            if (enemy != null)
            {
                if (impactMode == ProjectileImpactMode.EarthAoE)
                {
                    ExplodeEarthAoE();
                    Destroy(gameObject);
                    return;
                }

                if (element == ElementType.Wind && windImpactAoE)
                {
                    ApplyWindImpactAoE();
                    Destroy(gameObject);
                    return;
                }

                ApplyElementHit(enemy);
                if (impactMode == ProjectileImpactMode.WaterSpray ||
                    impactMode == ProjectileImpactMode.FireBreath)
                {
                    nextAreaTickTime = Time.time + tickInterval;
                }
            }

            if (impactMode == ProjectileImpactMode.SingleTarget)
            {
                Destroy(gameObject);
            }
        }
    }

    private void AnimateProjectile()
    {
        if (spriteRenderer == null || framesPerSecond <= 0f)
        {
            return;
        }

        Sprite[] currentFrames = GetCurrentAnimationFrames();
        if (currentFrames == null || currentFrames.Length == 0)
        {
            return;
        }

        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = currentFrames[animationFrameIndex];
        }

        animationTimer += Time.deltaTime;
        float frameDuration = 1f / framesPerSecond;

        while (animationTimer >= frameDuration)
        {
            animationTimer -= frameDuration;

            animationFrameIndex++;
            if (animationFrameIndex >= currentFrames.Length)
            {
                if (!finishedStartupAnimation &&
                    startupAnimationFrames != null &&
                    startupAnimationFrames.Length > 0)
                {
                    finishedStartupAnimation = true;
                    animationFrameIndex = 0;
                    currentFrames = GetCurrentAnimationFrames();
                }
                else
                {
                    animationFrameIndex = 0;
                }
            }

            spriteRenderer.sprite = currentFrames[animationFrameIndex];
        }
    }

    private Sprite[] GetCurrentAnimationFrames()
    {
        if (!finishedStartupAnimation &&
            startupAnimationFrames != null &&
            startupAnimationFrames.Length > 0)
        {
            return startupAnimationFrames;
        }

        return animationFrames;
    }

    private void ExplodeEarthAoE()
    {
        SpawnShatterPieces();

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, areaRadius, enemyLayers);
        foreach (Collider2D hit in hits)
        {
            EnemyHealth enemy = hit.GetComponent<EnemyHealth>();
            if (enemy == null)
            {
                enemy = hit.GetComponentInParent<EnemyHealth>();
            }

            if (enemy == null || hitEnemies.Contains(enemy))
            {
                continue;
            }

            hitEnemies.Add(enemy);
            enemy.TakeDamage(damage * areaDamageMultiplier);
        }
    }

    private void SpawnShatterPieces()
    {
        if (shatterSprites == null || shatterSprites.Length == 0 || shatterPieceCount <= 0)
        {
            return;
        }

        for (int i = 0; i < shatterPieceCount; i++)
        {
            Sprite sprite = shatterSprites[Random.Range(0, shatterSprites.Length)];
            if (sprite == null)
            {
                continue;
            }

            GameObject piece = new GameObject("EarthShard");
            piece.transform.position = transform.position;
            piece.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            piece.transform.localScale = Vector3.one * Random.Range(
                shatterPieceScaleRange.x,
                shatterPieceScaleRange.y
            );

            SpriteRenderer pieceRenderer = piece.AddComponent<SpriteRenderer>();
            pieceRenderer.sprite = sprite;
            pieceRenderer.sortingOrder = 11;

            Rigidbody2D pieceRigidbody = piece.AddComponent<Rigidbody2D>();
            pieceRigidbody.gravityScale = 0f;
            pieceRigidbody.linearDamping = 3.5f;
            pieceRigidbody.angularDamping = 4f;

            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector2.up;
            }

            pieceRigidbody.linearVelocity = direction * Random.Range(shatterForce * 0.55f, shatterForce);
            pieceRigidbody.angularVelocity = Random.Range(-360f, 360f);

            Destroy(piece, shatterLifetime);
        }
    }

    private void TickAreaDamage()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, areaRadius, enemyLayers);
        foreach (Collider2D hit in hits)
        {
            EnemyHealth enemy = hit.GetComponent<EnemyHealth>();
            if (enemy == null)
            {
                enemy = hit.GetComponentInParent<EnemyHealth>();
            }

            if (enemy != null)
            {
                ApplyElementHit(enemy);
            }
        }
    }

    private void ApplyWindImpactAoE()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, areaRadius, enemyLayers);
        foreach (Collider2D hit in hits)
        {
            EnemyHealth enemy = hit.GetComponent<EnemyHealth>();
            if (enemy == null)
            {
                enemy = hit.GetComponentInParent<EnemyHealth>();
            }

            if (enemy == null || hitEnemies.Contains(enemy))
            {
                continue;
            }

            hitEnemies.Add(enemy);
            ApplyElementHit(enemy);
        }
    }

    private void ApplyElementHit(EnemyHealth enemy)
    {
        if (enemy == null)
        {
            return;
        }

        if (element == ElementType.Wind)
        {
            enemy.TakeDamage(damage);
            if (!enemy.isBoss)
            {
                ApplyKnockback(enemy);
            }
        }
        if (element == ElementType.Water)
        {
            enemy.TakeDamage(damage * areaDamageMultiplier);
            enemy.ApplySlow(waterSlowMultiplier, waterSlowDuration);
        }
        if (element == ElementType.Earth) enemy.TakeDamage(damage * 2.5f);
        if (element == ElementType.Fire)
        {
            enemy.TakeDamage(damage * areaDamageMultiplier);
            enemy.ApplyBurn(damage * 0.2f, 1.25f);
        }
    }

    private void ApplyKnockback(EnemyHealth enemy)
    {
        if (enemy == null || knockbackForce <= 0f)
        {
            return;
        }

        Vector2 direction = rb != null && rb.linearVelocity.sqrMagnitude > 0.01f
            ? rb.linearVelocity.normalized
            : (Vector2)transform.right;

        EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
        if (movement == null)
        {
            movement = enemy.GetComponentInParent<EnemyMovement>();
        }

        if (movement != null)
        {
            movement.ApplyKnockback(direction, knockbackForce, knockbackDuration);
            return;
        }

        enemy.ApplyExternalKnockback(direction, knockbackForce, knockbackDuration);
    }
}
