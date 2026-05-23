using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public float health = 30f;
    public float baseSpeed = 2.5f;
    public bool isBoss = false;
    public int bossTier = 1;
    public ElementType enemyElement = ElementType.Earth;
    public float hitFlashDuration = 0.12f;

    [Header("Boss UI")]
    public Vector3 bossHealthBarOffset = new Vector3(0f, 0.25f, 0f);
    public Vector2 bossHealthBarSize = new Vector2(1.6f, 0.14f);

    private float maxHealth;
    private float currentSpeed;
    private SpriteRenderer sr;
    private SpriteRenderer[] spriteRenderers;
    private Color[] baseColors;
    private Rigidbody2D rb;
    private Collider2D enemyCollider;
    private EnemyMovement enemyMovement;
    private GameObject currentPlatform;
    private float externalKnockbackEndTime;
    private Coroutine hitFlashRoutine;
    private Coroutine slowRoutine;
    private GameObject bossHealthBarObject;
    private Transform bossHealthFill;
    private bool isHitFlashing;
    private static Sprite bossHealthBarSprite;

    public bool IsHitFlashing => isHitFlashing;

    void Start()
    {
        maxHealth = Mathf.Max(health, 1f);
        currentSpeed = baseSpeed;
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        baseColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            baseColors[i] = spriteRenderers[i].color;
        }

        rb = GetComponent<Rigidbody2D>();
        enemyCollider = GetComponent<Collider2D>();
        enemyMovement = GetComponent<EnemyMovement>();

        if (isBoss)
        {
            EnsureBossHealthBar();
            UpdateBossHealthBar();
        }
    }

    void Update()
    {
        if (enemyMovement != null) return;
        if (Time.time < externalKnockbackEndTime) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        Vector3 direction = (player.transform.position - transform.position).normalized;
        rb.linearVelocity = new Vector2(direction.x * currentSpeed, rb.linearVelocity.y);

        float yDifference = player.transform.position.y - transform.position.y;

        if (yDifference < -1.5f && currentPlatform != null)
        {
            StartCoroutine(EnemyDropThroughPlatform());
        }

        if (yDifference > 2f && Mathf.Abs(rb.linearVelocity.y) < 0.01f && Random.Range(0, 100) < 2)
        {
            rb.AddForce(Vector2.up * 8f, ForceMode2D.Impulse);
        }
    }

    void LateUpdate()
    {
        if (!isBoss || bossHealthBarObject == null)
        {
            return;
        }

        bossHealthBarObject.transform.position = GetBossHealthBarPosition();
        bossHealthBarObject.transform.rotation = Quaternion.identity;
    }

    private IEnumerator EnemyDropThroughPlatform()
    {
        if (currentPlatform != null)
        {
            Collider2D platformCollider = currentPlatform.GetComponent<Collider2D>();
            if (platformCollider != null)
            {
                Physics2D.IgnoreCollision(enemyCollider, platformCollider, true);
                yield return new WaitForSeconds(0.5f);
                Physics2D.IgnoreCollision(enemyCollider, platformCollider, false);
            }
        }
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        FlashRed();
        UpdateBossHealthBar();
        if (health <= 0) Die();
    }

    public void ApplySlow(float multiplier, float duration)
    {
        if (slowRoutine != null)
        {
            StopCoroutine(slowRoutine);
        }

        slowRoutine = StartCoroutine(SlowRoutine(multiplier, duration));
    }

    private IEnumerator SlowRoutine(float mult, float dur)
    {
        currentSpeed = baseSpeed * mult;
        if (enemyMovement != null)
        {
            enemyMovement.SetSpeedMultiplier(mult);
        }

        if (sr != null && !isHitFlashing)
        {
            sr.color = Color.blue;
        }

        yield return new WaitForSeconds(dur);
        currentSpeed = baseSpeed;
        if (enemyMovement != null)
        {
            enemyMovement.SetSpeedMultiplier(1f);
        }

        if (sr != null && !isHitFlashing)
        {
            sr.color = Color.white;
        }

        slowRoutine = null;
    }

    public void ApplyBurn(float damagePerTick, float duration) => StartCoroutine(BurnRoutine(damagePerTick, duration));
    private IEnumerator BurnRoutine(float dmg, float dur)
    {
        float elapsed = 0; sr.color = Color.red;
        while (elapsed < dur) { TakeDamage(dmg); elapsed += 0.5f; yield return new WaitForSeconds(0.5f); }
        sr.color = Color.white;
    }

    public void ApplyExternalKnockback(Vector2 direction, float force, float duration)
    {
        if (isBoss)
        {
            return;
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb == null || force <= 0f)
        {
            return;
        }

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector2.up;
        }

        rb.linearVelocity = direction.normalized * force;
        externalKnockbackEndTime = Time.time + duration;
    }

    private void FlashRed()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return;
        }

        if (hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
        }

        hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        isHitFlashing = true;
        SetSpriteColors(Color.red);
        yield return new WaitForSeconds(hitFlashDuration);
        RestoreSpriteColors();
        isHitFlashing = false;
        hitFlashRoutine = null;
    }

    private void SetSpriteColors(Color color)
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = color;
            }
        }
    }

    private void RestoreSpriteColors()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = i < baseColors.Length ? baseColors[i] : Color.white;
            }
        }
    }

    private void EnsureBossHealthBar()
    {
        if (bossHealthBarObject != null)
        {
            return;
        }

        bossHealthBarObject = new GameObject(gameObject.name + "_HealthBar");
        bossHealthBarObject.transform.position = GetBossHealthBarPosition();

        SpriteRenderer background = bossHealthBarObject.AddComponent<SpriteRenderer>();
        background.sprite = GetBossHealthBarSprite();
        background.color = new Color(0.18f, 0.13f, 0.02f, 0.9f);
        background.sortingOrder = 95;
        bossHealthBarObject.transform.localScale =
            new Vector3(bossHealthBarSize.x, bossHealthBarSize.y, 1f);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(bossHealthBarObject.transform, false);
        fillObject.transform.localPosition = new Vector3(-0.5f, 0f, -0.01f);
        fillObject.transform.localScale = Vector3.one;
        bossHealthFill = fillObject.transform;

        SpriteRenderer fill = fillObject.AddComponent<SpriteRenderer>();
        fill.sprite = GetBossHealthBarSprite();
        fill.color = new Color(1f, 0.78f, 0.08f, 1f);
        fill.sortingOrder = 96;
    }

    private void UpdateBossHealthBar()
    {
        if (!isBoss)
        {
            return;
        }

        float healthPercent = Mathf.Clamp01(health / maxHealth);
        if (bossHealthFill != null)
        {
            bossHealthFill.localScale = new Vector3(healthPercent, 0.72f, 1f);
            bossHealthFill.localPosition = new Vector3(-0.5f + healthPercent * 0.5f, 0f, -0.01f);
        }
    }

    private Vector3 GetBossHealthBarPosition()
    {
        if (sr != null)
        {
            Bounds bounds = sr.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, transform.position.z) +
                   bossHealthBarOffset;
        }

        return transform.position + bossHealthBarOffset;
    }

    private static Sprite GetBossHealthBarSprite()
    {
        if (bossHealthBarSprite != null)
        {
            return bossHealthBarSprite;
        }

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        bossHealthBarSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );

        return bossHealthBarSprite;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Platforms")) currentPlatform = collision.gameObject;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Platforms")) currentPlatform = null;
    }

    void Die()
    {
        if (bossHealthBarObject != null)
        {
            Destroy(bossHealthBarObject);
        }

        GameDirector director = FindFirstObjectByType<GameDirector>();
        if (director != null)
        {
            if (isBoss) director.OnBossDefeated(this);
            else director.OnNormalEnemyDefeated();
        }
        Destroy(gameObject);
    }
}
