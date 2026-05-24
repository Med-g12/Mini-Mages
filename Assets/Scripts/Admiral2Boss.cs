using UnityEngine;

public class Admiral2Boss : MonoBehaviour
{
    public float moveSpeed = 2.5f;
    public float jumpForce = 28f;
    public float attackInterval = 0.75f;
    public float woundedAttackInterval = 0.45f;
    [Range(0.1f, 0.9f)] public float woundedHealthThreshold = 0.5f;
    public float groundPoundGapCloseDistance = 7f;
    public float groundPoundHorizontalSpeed = 8f;
    public float groundPoundRiseDuration = 0.65f;
    public float groundPoundFallSpeed = 38f;
    public float groundPoundTakeoffGrace = 0.2f;
    public float groundPoundMaxDuration = 3.5f;
    public float groundPoundRadius = 2.4f;
    public float groundPoundShakeDuration = 0.28f;
    public float groundPoundShakeStrength = 0.35f;
    public float groundPoundAnimationSpeed = 1.2f;
    public float groundPoundImpactAnimationHold = 0.25f;
    public float groundPoundAirborneMaxFrame = 0.68f;
    public float groundPoundImpactFrame = 0.78f;
    public float knockdownDuration = 1f;
    public float groundPoundDamage = 24f;
    public float blowRadius = 9f;
    public float blowForce = 34f;
    public float blowDamage = 15f;
    public float blowPushDuration = 0.75f;
    public float contactDamage = 12f;
    public float contactKnockbackForce = 10f;
    public float contactDamageCooldown = 0.8f;
    public float incomingDamageMultiplier = 0.55f;
    public float healAmount = 40f;
    public float attackAnimationSpeed = 0.6f;
    public int laughSummonCount = 15;
    public float laughSummonSpacing = 1.5f;
    public float laughSummonOutsideMargin = 2.5f;
    public float laughSummonVerticalSpacing = 1.25f;
    public float laughSummonAboveMapHeight = 5f;
    public float laughSummonRandomXSpread = 4f;
    public float laughSummonRandomYSpread = 5f;
    public float windSummonScaleMultiplier = 1.45f;
    public float waterSummonScaleMultiplier = 0.7f;
    public Vector3 bigScale = new Vector3(5.4f, 5.4f, 1f);
    public float bossColliderRadius = 0.25f;
    public float physicalColliderRadius = 0.1f;
    public float groundSnapDistance = 20f;
    public bool requireBaktinDefeatBeforeDamage = true;
    public Color guardedInvulnerableColor = new Color(0.25f, 1f, 0.35f, 1f);
    public Vector2 shieldIconOffset = new Vector2(0f, 0.05f);
    public float shieldIconScale = 0.18f;
    public float shieldIconOrbitWidth = 0.28f;
    public float shieldIconOrbitHeight = 0.08f;
    public float shieldIconOrbitSpeed = 180f;
    public float shieldIconDepthScale = 0.35f;
    public LayerMask groundLayerMask;
    public GameObject[] laughSummonPrefabs;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer[] spriteRenderers;
    private Color[] normalSpriteColors;
    private EnemyHealth enemyHealth;
    private CircleCollider2D physicalCollider;
    private GameObject shieldIconObject;
    private SpriteRenderer shieldIconRenderer;
    private Transform player;
    private float nextAttackTime;
    private float maxHealth;
    private float groundPoundStartTime;
    private float nextContactDamageTime;
    private bool isGrounded;
    private bool wasGrounded;
    private bool isJumping;
    private bool isAttacking;
    private bool hasPlayedJumpAnimation;
    private bool hasPlayedGroundPoundAnimation;
    private bool hasHitPlayerDuringGroundPoundDrop;
    private bool woundedNextMoveShouldHeal = true;
    private bool attackInvulnerable;
    private bool guardInvulnerable;
    private bool showingInvulnerableColor;
    private Admiral2AttackType lastAttackType = Admiral2AttackType.None;
    private Coroutine returnToIdleRoutine;
    private Vector3 lockedLandingPosition;
    private readonly System.Collections.Generic.List<Collider2D> ignoredGroundPoundPlatforms = new System.Collections.Generic.List<Collider2D>();

    private enum Admiral2AttackType
    {
        None,
        GroundPound,
        Laugh,
        Blow,
        Heal
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        normalSpriteColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            normalSpriteColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
        }

        enemyHealth = GetComponent<EnemyHealth>();
    }

    void Start()
    {
        if (enemyHealth != null)
        {
            enemyHealth.allowBuiltInMovement = false;
            enemyHealth.damageReceivedMultiplier = incomingDamageMultiplier;
        }

        transform.localScale = bigScale;
        if (groundLayerMask.value == 0)
        {
            groundLayerMask = LayerMask.GetMask("Platforms", "Default");
        }

        CircleCollider2D bossCollider = GetComponent<CircleCollider2D>();
        if (bossCollider == null)
        {
            bossCollider = gameObject.AddComponent<CircleCollider2D>();
        }
        bossCollider.radius = bossColliderRadius;
        bossCollider.offset = GetBodyColliderOffset();
        bossCollider.isTrigger = true;

        physicalCollider = gameObject.AddComponent<CircleCollider2D>();
        physicalCollider.radius = physicalColliderRadius;
        physicalCollider.offset = GetFeetColliderOffset();
        physicalCollider.isTrigger = false;

        SnapToGroundBelow();

        maxHealth = enemyHealth != null ? enemyHealth.health : 400f;
        nextAttackTime = Time.time + attackInterval;

        if (animator != null)
        {
            animator.Play("Admiral2_Idle", 0, 0f);
            animator.speed = 1f;
        }

        EnsureShieldIcon();
        UpdateGuardInvulnerability();
    }

    void Update()
    {
        UpdateGuardInvulnerability();

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
            return;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = player.position.x < transform.position.x;
        }

        UpdateShieldIconPosition();
    }

    void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        UpdateGroundedState();

        if (isGrounded && !isAttacking && Time.time >= nextAttackTime)
        {
            StartRandomAttack();
        }

        if (isJumping && Time.time - groundPoundStartTime > groundPoundMaxDuration)
        {
            rb.linearVelocity = Vector2.zero;
            SnapToGroundBelow();
            FinishAttackNow();
            return;
        }

        if (isJumping && !isGrounded)
        {
            UpdateGroundPoundFallAnimation();
            MoveTowardLandingPosition();
            KeepGroundPoundImpactFrameForLanding();
        }
    }

    private void UpdateGroundedState()
    {
        wasGrounded = isGrounded;

        if (rb == null)
        {
            isGrounded = false;
            return;
        }

        if (isJumping && Time.time - groundPoundStartTime < groundPoundTakeoffGrace)
        {
            isGrounded = false;
            return;
        }

        Vector2 groundCheckOrigin = GetFeetPosition() + Vector2.up * 0.1f;
        RaycastHit2D hit = GetGroundHit(
            groundCheckOrigin,
            Vector2.down,
            0.35f,
            isJumping
        );

        isGrounded = hit.collider != null;

        if (!wasGrounded && isGrounded && isJumping)
        {
            isJumping = false;
            PoundImpact();
        }
    }

    public void SetLaughSummonPrefabs(GameObject[] summonPrefabs)
    {
        laughSummonPrefabs = summonPrefabs;
    }

    private void StartRandomAttack()
    {
        Admiral2AttackType attackType = ChooseNextAttackType();
        StartAttack(attackType);
    }

    private Admiral2AttackType ChooseNextAttackType()
    {
        if (ShouldUseWoundedPattern())
        {
            if (woundedNextMoveShouldHeal && lastAttackType != Admiral2AttackType.Heal)
            {
                woundedNextMoveShouldHeal = false;
                return Admiral2AttackType.Heal;
            }

            woundedNextMoveShouldHeal = true;
            return ChooseNonHealAttack();
        }

        woundedNextMoveShouldHeal = true;
        return ChooseAnyAttack();
    }

    private Admiral2AttackType ChooseAnyAttack()
    {
        Admiral2AttackType[] attacks =
        {
            Admiral2AttackType.GroundPound,
            Admiral2AttackType.Laugh,
            Admiral2AttackType.Blow,
            Admiral2AttackType.Heal
        };

        return ChooseAttackFrom(attacks);
    }

    private Admiral2AttackType ChooseNonHealAttack()
    {
        bool shouldGapClose = player != null &&
            Mathf.Abs(player.position.x - transform.position.x) >= groundPoundGapCloseDistance &&
            lastAttackType != Admiral2AttackType.GroundPound;

        if (shouldGapClose)
        {
            return Admiral2AttackType.GroundPound;
        }

        Admiral2AttackType[] attacks =
        {
            Admiral2AttackType.GroundPound,
            Admiral2AttackType.Laugh,
            Admiral2AttackType.Blow
        };

        return ChooseAttackFrom(attacks);
    }

    private Admiral2AttackType ChooseAttackFrom(Admiral2AttackType[] attacks)
    {
        if (attacks == null || attacks.Length == 0)
        {
            return Admiral2AttackType.GroundPound;
        }

        int validCount = 0;
        for (int i = 0; i < attacks.Length; i++)
        {
            if (attacks[i] != lastAttackType)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return attacks[0];
        }

        int roll = Random.Range(0, validCount);
        for (int i = 0; i < attacks.Length; i++)
        {
            if (attacks[i] == lastAttackType)
            {
                continue;
            }

            if (roll == 0)
            {
                return attacks[i];
            }

            roll--;
        }

        return attacks[0];
    }

    private void StartAttack(Admiral2AttackType attackType)
    {
        if (attackType == Admiral2AttackType.GroundPound)
        {
            StartGroundPound();
        }
        else if (attackType == Admiral2AttackType.Laugh)
        {
            StartCoroutine(PerformLaughAttack());
        }
        else if (attackType == Admiral2AttackType.Blow)
        {
            StartCoroutine(PerformBlowAttack());
        }
        else
        {
            StartCoroutine(PerformHealAttack());
        }
    }

    private void StartGroundPound()
    {
        if (!isGrounded || rb == null || player == null)
        {
            ScheduleNextAttack();
            return;
        }

        lastAttackType = Admiral2AttackType.GroundPound;
        lockedLandingPosition = player.position;
        groundPoundStartTime = Time.time;
        rb.position += Vector2.up * 0.08f;
        rb.linearVelocity = new Vector2(0f, jumpForce);
        isJumping = true;
        isAttacking = true;
        isGrounded = false;
        hasPlayedJumpAnimation = false;
        hasPlayedGroundPoundAnimation = false;
        hasHitPlayerDuringGroundPoundDrop = false;
        IgnorePlatformCollisionsForGroundPound(true);
        PlayJumpAnimation();
    }

    private void PoundImpact()
    {
        rb.linearVelocity = Vector2.zero;
        SnapToGroundBelow();
        PlayGroundPoundImpactFrame();
        ScreenShake.Shake(groundPoundShakeDuration, groundPoundShakeStrength);

        if (returnToIdleRoutine != null)
        {
            StopCoroutine(returnToIdleRoutine);
        }

        returnToIdleRoutine = StartCoroutine(FinishAttackAfterDelay(groundPoundImpactAnimationHold));

        Vector3 impactPoint = GetFeetPosition();
        Collider2D[] nearbyPlayers = Physics2D.OverlapCircleAll(impactPoint, groundPoundRadius);
        foreach (Collider2D nearby in nearbyPlayers)
        {
            if (nearby == null || nearby == GetComponent<Collider2D>())
            {
                continue;
            }

            PlayerResources resources = nearby.GetComponent<PlayerResources>();
            if (resources == null)
            {
                resources = nearby.GetComponentInParent<PlayerResources>();
            }

            if (resources != null)
            {
                resources.TakeNonlethalDamage(groundPoundDamage);
                resources.ApplyKnockdown(impactPoint, knockdownDuration);
            }
        }
    }

    private System.Collections.IEnumerator PerformLaughAttack()
    {
        lastAttackType = Admiral2AttackType.Laugh;
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;
        SetDamageInvulnerable(true);

        if (animator != null)
        {
            animator.speed = attackAnimationSpeed;
            animator.Play("Admiral2_Laugh", 0, 0f);
        }

        yield return new WaitForSeconds(0.45f);
        GameObject[] spawnedEnemies = SpawnLaughSummons();
        while (HasLivingSummons(spawnedEnemies))
        {
            yield return null;
        }

        yield return FinishAttackAfterDelay(0.2f);
        SetDamageInvulnerable(false);
    }

    private System.Collections.IEnumerator PerformBlowAttack()
    {
        lastAttackType = Admiral2AttackType.Blow;
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;

        if (animator != null)
        {
            animator.speed = attackAnimationSpeed;
            animator.Play("Admiral2_Blow", 0, 0f);
        }

        yield return new WaitForSeconds(0.25f);
        BlowPlayerAway();
        yield return FinishAttackAfterDelay(0.5f);
    }

    private System.Collections.IEnumerator PerformHealAttack()
    {
        lastAttackType = Admiral2AttackType.Heal;
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;
        SetDamageInvulnerable(true);

        if (animator != null)
        {
            animator.speed = attackAnimationSpeed;
            animator.Play("Admiral2_Heal", 0, 0f);
        }

        if (enemyHealth != null)
        {
            enemyHealth.Heal(healAmount);
        }

        yield return FinishAttackAfterDelay(0.9f);
        SetDamageInvulnerable(false);
    }

    private System.Collections.IEnumerator FinishAttackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        IgnorePlatformCollisionsForGroundPound(false);

        if (animator != null)
        {
            animator.speed = 1f;
            animator.Play("Admiral2_Idle", 0, 0f);
        }

        isJumping = false;
        isAttacking = false;
        ScheduleNextAttack();
        returnToIdleRoutine = null;
    }

    private void FinishAttackNow()
    {
        if (returnToIdleRoutine != null)
        {
            StopCoroutine(returnToIdleRoutine);
            returnToIdleRoutine = null;
        }

        IgnorePlatformCollisionsForGroundPound(false);
        SetDamageInvulnerable(false);

        if (animator != null)
        {
            animator.speed = 1f;
            animator.Play("Admiral2_Idle", 0, 0f);
        }

        isJumping = false;
        isAttacking = false;
        ScheduleNextAttack();
    }

    private void ScheduleNextAttack()
    {
        nextAttackTime = Time.time + GetCurrentAttackInterval();
    }

    private float GetCurrentAttackInterval()
    {
        return ShouldUseWoundedPattern() ? woundedAttackInterval : attackInterval;
    }

    private bool ShouldUseWoundedPattern()
    {
        return enemyHealth != null &&
            maxHealth > 0f &&
            enemyHealth.health <= maxHealth * woundedHealthThreshold;
    }

    private void SetDamageInvulnerable(bool invulnerable)
    {
        attackInvulnerable = invulnerable;
        ApplyDamageInvulnerability();
    }

    private void UpdateGuardInvulnerability()
    {
        bool shouldBeGuarded = requireBaktinDefeatBeforeDamage && HasLivingBaktinNiAdmirals();
        if (guardInvulnerable == shouldBeGuarded)
        {
        UpdateInvulnerabilityVisual();
        return;
    }

        guardInvulnerable = shouldBeGuarded;
        ApplyDamageInvulnerability();
        UpdateInvulnerabilityVisual();
    }

    private void ApplyDamageInvulnerability()
    {
        if (enemyHealth != null)
        {
            enemyHealth.isInvulnerable = attackInvulnerable || guardInvulnerable;
        }

        UpdateInvulnerabilityVisual();
    }

    private bool HasLivingBaktinNiAdmirals()
    {
        PlatformPatrolEnemy[] baktins = FindObjectsByType<PlatformPatrolEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < baktins.Length; i++)
        {
            if (baktins[i] == null || !baktins[i].isActiveAndEnabled)
            {
                continue;
            }

            if (baktins[i].gameObject.name.Contains("BaktinNiAdmiral") ||
                baktins[i].nameTagText.Contains("Baktin"))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateInvulnerabilityVisual()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return;
        }

        if (guardInvulnerable || attackInvulnerable)
        {
            SetBossSpriteColors(guardedInvulnerableColor);
            SetShieldIconVisible(true);
            showingInvulnerableColor = true;
            return;
        }

        if (showingInvulnerableColor)
        {
            RestoreBossSpriteColors();
            SetShieldIconVisible(false);
            showingInvulnerableColor = false;
        }
    }

    private void EnsureShieldIcon()
    {
        if (shieldIconObject != null)
        {
            return;
        }

        shieldIconObject = new GameObject("ShieldIcon");
        shieldIconObject.transform.SetParent(transform, false);
        shieldIconRenderer = shieldIconObject.AddComponent<SpriteRenderer>();
        shieldIconRenderer.sprite = CreateShieldIconSprite();
        shieldIconRenderer.color = guardedInvulnerableColor;
        shieldIconRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 5 : 35;
        shieldIconObject.transform.localScale = Vector3.one * shieldIconScale;
        SetShieldIconVisible(false);
        UpdateShieldIconPosition();
    }

    private void UpdateShieldIconPosition()
    {
        if (shieldIconObject == null || !shieldIconObject.activeSelf)
        {
            return;
        }

        float angle = Time.time * shieldIconOrbitSpeed * Mathf.Deg2Rad;
        float depth = Mathf.Sin(angle);
        Vector2 orbitOffset = new Vector2(
            Mathf.Cos(angle) * shieldIconOrbitWidth,
            depth * shieldIconOrbitHeight
        );

        shieldIconObject.transform.localPosition = shieldIconOffset + orbitOffset;
        float depthScale = 1f + depth * shieldIconDepthScale;
        shieldIconObject.transform.localScale = Vector3.one * shieldIconScale * depthScale;

        if (shieldIconRenderer != null && spriteRenderer != null)
        {
            shieldIconRenderer.sortingOrder = spriteRenderer.sortingOrder + (depth < 0f ? -1 : 5);
        }
    }

    private void SetShieldIconVisible(bool visible)
    {
        EnsureShieldIcon();
        if (shieldIconObject != null && shieldIconObject.activeSelf != visible)
        {
            shieldIconObject.SetActive(visible);
        }
    }

    private Sprite CreateShieldIconSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color fill = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        Vector2[] points =
        {
            new Vector2(16f, 30f),
            new Vector2(26f, 25f),
            new Vector2(24f, 11f),
            new Vector2(16f, 3f),
            new Vector2(8f, 11f),
            new Vector2(6f, 25f)
        };

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Length];
            DrawLine(texture, start, end, fill, 2);
        }

        DrawLine(texture, new Vector2(16f, 25f), new Vector2(16f, 8f), fill, 1);
        DrawLine(texture, new Vector2(11f, 18f), new Vector2(21f, 18f), fill, 1);
        texture.filterMode = FilterMode.Point;
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void DrawLine(Texture2D texture, Vector2 start, Vector2 end, Color color, int thickness)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(start, end));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(start, end, steps == 0 ? 0f : i / (float)steps);
            DrawPixelBlock(texture, Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), color, thickness);
        }
    }

    private void DrawPixelBlock(Texture2D texture, int centerX, int centerY, Color color, int thickness)
    {
        for (int y = -thickness; y <= thickness; y++)
        {
            for (int x = -thickness; x <= thickness; x++)
            {
                int pixelX = centerX + x;
                int pixelY = centerY + y;
                if (pixelX >= 0 && pixelX < texture.width && pixelY >= 0 && pixelY < texture.height)
                {
                    texture.SetPixel(pixelX, pixelY, color);
                }
            }
        }
    }

    private void SetBossSpriteColors(Color color)
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = color;
            }
        }
    }

    private void RestoreBossSpriteColors()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = i < normalSpriteColors.Length ? normalSpriteColors[i] : Color.white;
            }
        }
    }

    private GameObject[] SpawnLaughSummons()
    {
        System.Collections.Generic.List<GameObject> spawnedEnemies = new System.Collections.Generic.List<GameObject>();
        if (laughSummonPrefabs == null || laughSummonPrefabs.Length == 0)
        {
            return spawnedEnemies.ToArray();
        }

        Bounds mapBounds = GetSummonMapBounds();
        for (int i = 0; i < laughSummonCount; i++)
        {
            GameObject prefab = GetRandomSummonPrefab();
            if (prefab == null)
            {
                continue;
            }

            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 spawnPosition = GetOutsideMapSummonPosition(mapBounds, side);

            GameObject spawnedEnemy = Instantiate(prefab, spawnPosition, Quaternion.identity);
            if (spawnedEnemy != null)
            {
                ApplySummonSize(spawnedEnemy);
                spawnedEnemies.Add(spawnedEnemy);
            }
        }

        return spawnedEnemies.ToArray();
    }

    private Vector3 GetOutsideMapSummonPosition(Bounds mapBounds, float side)
    {
        float xOutsideOffset = laughSummonOutsideMargin + Random.Range(0f, laughSummonRandomXSpread);
        float xPosition = side < 0f
            ? mapBounds.min.x - xOutsideOffset
            : mapBounds.max.x + xOutsideOffset;

        float yPosition = mapBounds.max.y + laughSummonAboveMapHeight + Random.Range(0f, laughSummonRandomYSpread);

        return new Vector3(xPosition, yPosition, 0f);
    }

    private Bounds GetSummonMapBounds()
    {
        int platformLayer = LayerMask.NameToLayer("Platforms");
        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        Bounds mapBounds = new Bounds(transform.position, Vector3.one);
        bool hasBounds = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null ||
                collider.isTrigger ||
                (platformLayer >= 0 && collider.gameObject.layer != platformLayer))
            {
                continue;
            }

            if (!hasBounds)
            {
                mapBounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                mapBounds.Encapsulate(collider.bounds);
            }
        }

        if (!hasBounds)
        {
            mapBounds = new Bounds(transform.position, new Vector3(24f, 10f, 1f));
        }

        return mapBounds;
    }

    private void ApplySummonSize(GameObject spawnedEnemy)
    {
        EnemyHealth health = spawnedEnemy.GetComponent<EnemyHealth>();
        EnemyMovement movement = spawnedEnemy.GetComponent<EnemyMovement>();

        ElementType element = movement != null ? movement.enemyElement :
            health != null ? health.enemyElement :
            ElementType.Earth;

        if (element == ElementType.Wind)
        {
            spawnedEnemy.transform.localScale *= windSummonScaleMultiplier;
        }
        else if (element == ElementType.Water)
        {
            spawnedEnemy.transform.localScale *= waterSummonScaleMultiplier;
        }

        if (movement != null)
        {
            movement.RefreshBaseScale();
        }
    }

    private bool HasLivingSummons(GameObject[] spawnedEnemies)
    {
        if (spawnedEnemies == null || spawnedEnemies.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < spawnedEnemies.Length; i++)
        {
            if (spawnedEnemies[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private GameObject GetRandomSummonPrefab()
    {
        int validCount = 0;
        for (int i = 0; i < laughSummonPrefabs.Length; i++)
        {
            if (laughSummonPrefabs[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int roll = Random.Range(0, validCount);
        for (int i = 0; i < laughSummonPrefabs.Length; i++)
        {
            if (laughSummonPrefabs[i] == null)
            {
                continue;
            }

            if (roll == 0)
            {
                return laughSummonPrefabs[i];
            }

            roll--;
        }

        return null;
    }

    private void BlowPlayerAway()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, blowRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            PlayerResources resources = hits[i].GetComponent<PlayerResources>();
            if (resources == null)
            {
                resources = hits[i].GetComponentInParent<PlayerResources>();
            }

            if (resources == null)
            {
                continue;
            }

            DamageAndKnockbackPlayer(resources, blowDamage, blowForce, true);
            return;
        }
    }

    private void MoveTowardLandingPosition()
    {
        float xDifference = lockedLandingPosition.x - rb.position.x;
        float xVelocity = Mathf.Clamp(
            xDifference / Time.fixedDeltaTime,
            -groundPoundHorizontalSpeed,
            groundPoundHorizontalSpeed
        );

        float yVelocity = rb.linearVelocity.y;
        if (Time.time - groundPoundStartTime >= groundPoundRiseDuration)
        {
            PlayGroundPoundAnimation();
            yVelocity = -groundPoundFallSpeed;
        }
        else if (yVelocity <= 0f)
        {
            PlayGroundPoundAnimation();
            yVelocity = -groundPoundFallSpeed;
        }

        rb.linearVelocity = new Vector2(xVelocity, yVelocity);
    }

    private void UpdateGroundPoundFallAnimation()
    {
        if (rb == null)
        {
            return;
        }

        bool shouldFallNow =
            Time.time - groundPoundStartTime >= groundPoundRiseDuration ||
            rb.linearVelocity.y <= 0f;

        if (shouldFallNow)
        {
            PlayGroundPoundAnimation();
        }
    }

    private void KeepGroundPoundImpactFrameForLanding()
    {
        if (!hasPlayedGroundPoundAnimation || animator == null)
        {
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName("Admiral2_GroundPound"))
        {
            return;
        }

        float currentFrame = stateInfo.normalizedTime % 1f;
        if (currentFrame > groundPoundAirborneMaxFrame)
        {
            animator.Play("Admiral2_GroundPound", 0, groundPoundAirborneMaxFrame);
        }
    }

    private void PlayJumpAnimation()
    {
        if (hasPlayedJumpAnimation || animator == null)
        {
            return;
        }

        animator.speed = attackAnimationSpeed;
        animator.Play("Admiral2_Jump", 0, 0f);
        hasPlayedJumpAnimation = true;
    }

    private void PlayGroundPoundAnimation()
    {
        if (hasPlayedGroundPoundAnimation || animator == null)
        {
            return;
        }

        animator.speed = groundPoundAnimationSpeed;
        animator.Play("Admiral2_GroundPound", 0, 0f);
        hasPlayedGroundPoundAnimation = true;
    }

    private void PlayGroundPoundImpactFrame()
    {
        if (animator == null)
        {
            return;
        }

        animator.speed = 0f;
        animator.Play("Admiral2_GroundPound", 0, groundPoundImpactFrame);
        animator.Update(0f);
        hasPlayedGroundPoundAnimation = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryApplyContactHit(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryApplyContactHit(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyContactHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyContactHit(other);
    }

    private void TryApplyContactHit(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerResources resources = GetPlayerResources(other);
        if (resources == null)
        {
            return;
        }

        if (TryApplyGroundPoundDropHit(resources))
        {
            nextContactDamageTime = Time.time + contactDamageCooldown;
            return;
        }

        if (Time.time < nextContactDamageTime)
        {
            return;
        }

        DamageAndKnockbackPlayer(resources, contactDamage, contactKnockbackForce);
        nextContactDamageTime = Time.time + contactDamageCooldown;
    }

    private bool TryApplyGroundPoundDropHit(PlayerResources resources)
    {
        if (!isJumping ||
            hasHitPlayerDuringGroundPoundDrop ||
            rb == null ||
            rb.linearVelocity.y > 0f)
        {
            return false;
        }

        hasHitPlayerDuringGroundPoundDrop = true;
        resources.TakeNonlethalDamage(groundPoundDamage);
        resources.ApplyKnockdown(transform.position, knockdownDuration);
        return true;
    }

    private void DamageAndKnockbackPlayer(PlayerResources resources, float damage, float force, bool useFacingDirection = false)
    {
        resources.TakeDamage(damage);

        Rigidbody2D playerRb = resources.GetComponent<Rigidbody2D>();
        if (playerRb == null)
        {
            playerRb = resources.GetComponentInParent<Rigidbody2D>();
        }

        if (playerRb == null)
        {
            return;
        }

        float direction = useFacingDirection ? GetFacingDirection() : GetKnockbackDirection(resources.transform);
        float upwardVelocity = force >= blowForce ? 1f : 4f;
        float upwardImpulse = force >= blowForce ? 0.5f : 2f;
        Vector2 knockbackVelocity = new Vector2(direction * force, upwardVelocity);

        PlayerController playerController = resources.GetComponent<PlayerController>();
        if (playerController == null)
        {
            playerController = resources.GetComponentInParent<PlayerController>();
        }

        if (playerController != null && force >= blowForce)
        {
            playerController.ApplyExternalKnockback(knockbackVelocity, blowPushDuration);
        }
        else
        {
            playerRb.linearVelocity = knockbackVelocity;
        }

        playerRb.AddForce(new Vector2(direction * force, upwardImpulse), ForceMode2D.Impulse);

        if (force >= blowForce)
        {
            StartCoroutine(PushPlayerForDuration(playerRb, direction, force, blowPushDuration));
        }
    }

    private float GetKnockbackDirection(Transform target)
    {
        float xDifference = target.position.x - transform.position.x;
        if (Mathf.Abs(xDifference) > 0.15f)
        {
            return Mathf.Sign(xDifference);
        }

        return GetFacingDirection();
    }

    private float GetFacingDirection()
    {
        if (spriteRenderer != null)
        {
            return spriteRenderer.flipX ? -1f : 1f;
        }

        return 1f;
    }

    private System.Collections.IEnumerator PushPlayerForDuration(Rigidbody2D playerRb, float direction, float force, float duration)
    {
        float endTime = Time.time + duration;
        while (playerRb != null && Time.time < endTime)
        {
            yield return new WaitForFixedUpdate();
            playerRb.linearVelocity = new Vector2(direction * force, Mathf.Clamp(playerRb.linearVelocity.y, -8f, 2f));
        }
    }

    private PlayerResources GetPlayerResources(Collider2D other)
    {
        PlayerResources resources = other.GetComponent<PlayerResources>();
        if (resources == null)
        {
            resources = other.GetComponentInParent<PlayerResources>();
        }

        return resources;
    }

    private Vector2 GetBodyColliderOffset()
    {
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Bounds spriteBounds = spriteRenderer.sprite.bounds;
            return new Vector2(0f, spriteBounds.center.y - spriteBounds.extents.y * 0.15f);
        }

        return Vector2.zero;
    }

    private Vector2 GetFeetColliderOffset()
    {
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            float localBottom = spriteRenderer.sprite.bounds.min.y;
            return new Vector2(0f, localBottom + physicalColliderRadius);
        }

        return new Vector2(0f, -bossColliderRadius + physicalColliderRadius);
    }

    private Vector2 GetFeetPosition()
    {
        if (physicalCollider != null)
        {
            return new Vector2(physicalCollider.bounds.center.x, physicalCollider.bounds.min.y);
        }

        return transform.position;
    }

    private void SnapToGroundBelow()
    {
        if (physicalCollider == null)
        {
            return;
        }

        RaycastHit2D hit = GetGroundHit(
            transform.position + Vector3.up,
            Vector2.down,
            groundSnapDistance,
            isJumping
        );

        if (hit.collider == null)
        {
            return;
        }

        float feetToPivot = transform.position.y - physicalCollider.bounds.min.y;
        transform.position = new Vector3(transform.position.x, hit.point.y + feetToPivot + 0.02f, transform.position.z);

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }
    }

    private RaycastHit2D GetGroundHit(Vector2 origin, Vector2 direction, float distance, bool ignorePlatforms = false)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance, groundLayerMask);
        RaycastHit2D bestHit = new RaycastHit2D();
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null ||
                hitCollider.isTrigger ||
                (ignorePlatforms && hitCollider.gameObject.layer == LayerMask.NameToLayer("Platforms")) ||
                hitCollider.transform.root == transform.root ||
                hitCollider.CompareTag("Player") ||
                hitCollider.GetComponentInParent<PlayerResources>() != null ||
                hitCollider.GetComponentInParent<EnemyHealth>() != null)
            {
                continue;
            }

            if (hits[i].distance < bestDistance)
            {
                bestHit = hits[i];
                bestDistance = hits[i].distance;
            }
        }

        return bestHit;
    }

    private void IgnorePlatformCollisionsForGroundPound(bool ignore)
    {
        if (physicalCollider == null)
        {
            return;
        }

        if (!ignore)
        {
            for (int i = 0; i < ignoredGroundPoundPlatforms.Count; i++)
            {
                if (ignoredGroundPoundPlatforms[i] != null)
                {
                    Physics2D.IgnoreCollision(physicalCollider, ignoredGroundPoundPlatforms[i], false);
                }
            }

            ignoredGroundPoundPlatforms.Clear();
            return;
        }

        ignoredGroundPoundPlatforms.Clear();
        int platformLayer = LayerMask.NameToLayer("Platforms");
        if (platformLayer < 0)
        {
            return;
        }

        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D platformCollider = colliders[i];
            if (platformCollider == null ||
                platformCollider.isTrigger ||
                platformCollider.gameObject.layer != platformLayer)
            {
                continue;
            }

            Physics2D.IgnoreCollision(physicalCollider, platformCollider, true);
            ignoredGroundPoundPlatforms.Add(platformCollider);
        }
    }
}
