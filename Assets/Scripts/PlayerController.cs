using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    [Tooltip("Prevents wall friction from holding the player in place while airborne.")]
    public PhysicsMaterial2D frictionlessMovementMaterial;

    [Header("Jumping Mechanics")]
    public float jumpForce = 12f;
    public float doubleJumpForce = 10f;

    [Tooltip("Total number of jumps allowed (e.g., 2 for double jump)")]
    public int maxJumps = 2;

    private int jumpCount = 0;
    private bool isGrounded;
    private bool wasGrounded;
    private float lastJumpTime = 0f;
    private float groundedCooldown = 0.15f;

    [Header("Grounded Check")]
    [Tooltip("The layers that count as solid ground/platforms")]
    public LayerMask platformLayerMask;

    [Header("Better Physics / Fast Falling")]
    public float fallMultiplier = 4f;
    public float lowJumpMultiplier = 3f;

    [Header("Elemental Combat Systems")]
    [Tooltip("Drag the current WandData ScriptableObject here to change elements")]
    public WandData currentElementData;

    [Tooltip("The point on the player where basic attacks originate")]
    public Transform firePoint;
    public float projectileSpeed = 15f;
    public float basicFireCooldown = 0.1f;

    [Header("Overhead Indicator")]
    [Tooltip("Drag the ElementIndicator container object here")]
    public Transform elementIndicator;
    [Tooltip("Drag the IndicatorVisual child object with the SpriteRenderer here")]
    public SpriteRenderer indicatorSpriteRenderer;

    private float normalGravityScale;

    private Rigidbody2D rb;
    private float horizontalInput;
    private float externalKnockbackUntil;
    private Collider2D playerCollider;
    private Collider2D[] playerColliders;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Camera mainCamera;
    private WeaponManager weaponManager;
    private float nextBasicFireTime = 0f;

    // Buff tracking
    private float speedBuffEndTime = 0f;
    private float activeSpeedBonus = 0f;
    private float buffMaxDuration = 1f;
    private Sprite currentBuffIcon;

    // Buff Visuals & UI
    private SpriteRenderer buffShadeRenderer;
    private SpriteRenderer[] buffBorderRenderers;
    private GameObject buffUIContainer;
    private Image buffUITimerFill;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        playerColliders = GetComponentsInChildren<Collider2D>();
        weaponManager = GetComponent<WeaponManager>();

        normalGravityScale = rb.gravityScale;

        // Cache the main camera for mouse position mapping
        mainCamera = Camera.main;

        // SpriteRenderer
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // Animator
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // Fallback ground layers
        if (platformLayerMask.value == 0)
        {
            platformLayerMask = Physics2D.AllLayers;
        }

        if (firePoint == null)
        {
            Transform foundFirePoint = transform.Find("WandPivot/FirePoint");
            if (foundFirePoint == null)
            {
                foundFirePoint = transform.Find("FirePoint");
            }

            firePoint = foundFirePoint;
        }

        // Self-colliders are filtered out per raycast so floors can share the
        // player's layer while you are still setting up project layers.
        ApplyFrictionlessMovementMaterial();
    }

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        EnsureElementIndicator();
        UpdateActiveElementUI();
    }

    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // Handle weapon firing and mouse aiming mechanics
        HandleFireInput();

        // Flip sprite based on movement (only runs if not forcing a face direction from firing)
        UpdateSpriteDirection();

        // Ground detection
        CheckGroundedWithLaser();

        // Jump input
        if (Input.GetKeyDown(KeyCode.W))
        {
            if (isGrounded || jumpCount < maxJumps)
            {
                ExecuteInstantJump();
            }
        }

        // Better gravity
        ApplyBetterGravity();

        // Drop-through platforms
        if (Input.GetKeyDown(KeyCode.S))
        {
            CheckAndDrop();
        }

        // Animations
        UpdateAnimations();

        // Buff Logic
        UpdateBuffs();
    }

    void FixedUpdate()
    {
        if (Time.time < externalKnockbackUntil)
        {
            return;
        }

        rb.linearVelocity = new Vector2(
            horizontalInput * moveSpeed,
            rb.linearVelocity.y
        );
    }

    public void ApplyExternalKnockback(Vector2 velocity, float duration)
    {
        if (weaponManager != null)
        {
            weaponManager.CancelHeldBasicFire();
        }

        externalKnockbackUntil = Mathf.Max(externalKnockbackUntil, Time.time + duration);
        rb.linearVelocity = velocity;
    }

    private void HandleFireInput()
    {
        if (weaponManager != null && weaponManager.HasBasicAttackReady())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (Time.time < nextBasicFireTime)
            {
                return;
            }

            if (animator != null)
            {
                animator.SetTrigger("Fire");
            }

            Vector3 mousePosition = GetMouseWorldPosition();

            FireBasicProjectile(mousePosition);

            if (spriteRenderer != null)
            {
                SetSpriteFacing(mousePosition.x < transform.position.x);
            }

            nextBasicFireTime = Time.time + basicFireCooldown;
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return transform.position;
        }

        Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;
        return mousePosition;
    }

    private void FireBasicProjectile(Vector3 mousePosition)
    {
        if (currentElementData == null ||
            currentElementData.basicProjectilePrefab == null ||
            firePoint == null)
        {
            return;
        }

        Vector2 shootDirection =
            (mousePosition - firePoint.position).normalized;

        float angle =
            Mathf.Atan2(shootDirection.y, shootDirection.x) *
            Mathf.Rad2Deg;

        GameObject bullet = Instantiate(
            currentElementData.basicProjectilePrefab,
            firePoint.position,
            Quaternion.Euler(0f, 0f, angle)
        );

        Projectile projectile = bullet.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.element = currentElementData.elementType;
        }

        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        if (bulletRb != null)
        {
            bulletRb.linearVelocity = shootDirection * projectileSpeed;
        }
    }

    private void EnsureElementIndicator()
    {
        if (elementIndicator == null)
        {
            GameObject indicatorObject = new GameObject("ElementIndicator");
            indicatorObject.transform.SetParent(transform);
            indicatorObject.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            indicatorObject.transform.localRotation = Quaternion.identity;
            indicatorObject.transform.localScale = Vector3.one;
            elementIndicator = indicatorObject.transform;
        }

        if (indicatorSpriteRenderer == null)
        {
            GameObject visualObject = new GameObject("IndicatorVisual");
            visualObject.transform.SetParent(elementIndicator);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = new Vector3(0.52f, 0.52f, 1f);

            indicatorSpriteRenderer = visualObject.AddComponent<SpriteRenderer>();
            indicatorSpriteRenderer.sortingOrder = 20;
        }
    }

    public void UpdateActiveElementUI()
    {
        EnsureElementIndicator();

        if (currentElementData != null &&
            indicatorSpriteRenderer != null)
        {
            indicatorSpriteRenderer.sprite = currentElementData.elementIcon;
            indicatorSpriteRenderer.enabled = currentElementData.elementIcon != null;
        }
    }

    public void SetActiveElement(WandData elementData)
    {
        currentElementData = elementData;
        UpdateActiveElementUI();
    }

    public void FaceToward(Vector3 targetPosition)
    {
        SetSpriteFacing(targetPosition.x < transform.position.x);
    }

    public void AddSpeedBuff(float speedAmount, float duration, Sprite icon = null)
    {
        if (duration <= 0f)
        {
            moveSpeed += speedAmount;
            return;
        }

        if (Time.time >= speedBuffEndTime)
        {
            // Apply new buff
            activeSpeedBonus = speedAmount;
            moveSpeed += activeSpeedBonus;
        }
        else if (speedAmount > activeSpeedBonus)
        {
            // Upgrade existing buff
            moveSpeed -= activeSpeedBonus;
            activeSpeedBonus = speedAmount;
            moveSpeed += activeSpeedBonus;
        }

        speedBuffEndTime = Time.time + duration;
        buffMaxDuration = duration;
        if (icon != null)
        {
            currentBuffIcon = icon;
        }

        EnsureBuffAura();
        EnsureBuffUI();
    }

    private void UpdateBuffs()
    {
        bool speedBuffActive = Time.time < speedBuffEndTime;
        bool damageBuffActive = weaponManager != null && weaponManager.HasActiveDamageBuff();
        bool buffActive = speedBuffActive || damageBuffActive;

        if (!speedBuffActive && activeSpeedBonus > 0f)
        {
            // Expire buff
            moveSpeed -= activeSpeedBonus;
            activeSpeedBonus = 0f;
        }

        if (buffActive)
        {
            EnsureBuffAura();
            EnsureBuffUI();
        }

        // Aura logic
        if (buffShadeRenderer != null)
        {
            if (buffActive)
            {
                buffShadeRenderer.enabled = true;
                buffShadeRenderer.sprite = spriteRenderer.sprite; // Sync animation frame
                buffShadeRenderer.flipX = spriteRenderer.flipX;
                buffShadeRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
                buffShadeRenderer.color = new Color(1f, 1f, 0f, 0.75f); // Yellow shade (more visible)

                float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                float borderAlpha = Mathf.Lerp(0.7f, 1f, pulse);
                Color borderColor = new Color(1f, 0.8f, 0f, borderAlpha); // Gold/Yellow

                for (int i = 0; i < buffBorderRenderers.Length; i++)
                {
                    buffBorderRenderers[i].enabled = true;
                    buffBorderRenderers[i].sprite = spriteRenderer.sprite;
                    buffBorderRenderers[i].flipX = spriteRenderer.flipX;
                    buffBorderRenderers[i].sortingOrder = spriteRenderer.sortingOrder - 1;
                    buffBorderRenderers[i].color = borderColor;
                }
            }
            else
            {
                buffShadeRenderer.enabled = false;
                for (int i = 0; i < buffBorderRenderers.Length; i++)
                {
                    if (buffBorderRenderers[i] != null)
                        buffBorderRenderers[i].enabled = false;
                }
            }
        }

        // UI logic
        if (buffUIContainer != null)
        {
            if (buffActive)
            {
                buffUIContainer.SetActive(true);
                if (buffUITimerFill != null)
                {
                    float remaining = 0f;
                    float maxDur = 1f;

                    if (speedBuffActive && damageBuffActive)
                    {
                        float speedRemaining = speedBuffEndTime - Time.time;
                        float damageRemaining = weaponManager.GetDamageBuffRemainingTime();
                        remaining = Mathf.Max(speedRemaining, damageRemaining);
                        maxDur = Mathf.Max(buffMaxDuration, weaponManager.GetDamageBuffMaxDuration());
                    }
                    else if (speedBuffActive)
                    {
                        remaining = speedBuffEndTime - Time.time;
                        maxDur = buffMaxDuration;
                    }
                    else if (damageBuffActive)
                    {
                        remaining = weaponManager.GetDamageBuffRemainingTime();
                        maxDur = weaponManager.GetDamageBuffMaxDuration();
                    }

                    buffUITimerFill.fillAmount = Mathf.Clamp01(remaining / maxDur);
                }
            }
            else
            {
                buffUIContainer.SetActive(false);
            }
        }
    }

    private void EnsureBuffAura()
    {
        if (buffShadeRenderer != null) return;
        if (spriteRenderer == null) return;

        GameObject auraContainer = new GameObject("BuffAuraContainer");
        auraContainer.transform.SetParent(spriteRenderer.transform, false);
        auraContainer.transform.localPosition = Vector3.zero;

        GameObject shadeObj = new GameObject("BuffShade");
        shadeObj.transform.SetParent(auraContainer.transform, false);
        shadeObj.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        
        buffShadeRenderer = shadeObj.AddComponent<SpriteRenderer>();
        buffShadeRenderer.sprite = spriteRenderer.sprite;
        buffShadeRenderer.sortingOrder = spriteRenderer.sortingOrder + 1; // On top
        buffShadeRenderer.enabled = false;

        buffBorderRenderers = new SpriteRenderer[8];
        Vector3[] offsets = new Vector3[]
        {
            new Vector3(0.12f, 0f, 0f),
            new Vector3(-0.12f, 0f, 0f),
            new Vector3(0f, 0.12f, 0f),
            new Vector3(0f, -0.12f, 0f),
            new Vector3(0.085f, 0.085f, 0f),
            new Vector3(-0.085f, 0.085f, 0f),
            new Vector3(0.085f, -0.085f, 0f),
            new Vector3(-0.085f, -0.085f, 0f)
        };

        for (int i = 0; i < 8; i++)
        {
            GameObject borderObj = new GameObject("BuffBorder_" + i);
            borderObj.transform.SetParent(auraContainer.transform, false);
            borderObj.transform.localPosition = offsets[i] + new Vector3(0f, 0f, 0.1f);
            
            SpriteRenderer sr = borderObj.AddComponent<SpriteRenderer>();
            sr.sprite = spriteRenderer.sprite;
            sr.sortingOrder = spriteRenderer.sortingOrder - 1; // Behind player
            sr.enabled = false;
            buffBorderRenderers[i] = sr;
        }
    }

    private void EnsureBuffUI()
    {
        if (buffUIContainer != null) return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // Container
        buffUIContainer = new GameObject("BuffTimerUI");
        buffUIContainer.transform.SetParent(canvas.transform, false);
        RectTransform containerRect = buffUIContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(1f, 1f);
        containerRect.anchorMax = new Vector2(1f, 1f);
        containerRect.pivot = new Vector2(1f, 1f);
        containerRect.anchoredPosition = new Vector2(-20f, -20f);
        containerRect.sizeDelta = new Vector2(80f, 100f);

        // Icon
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(buffUIContainer.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.2f);
        iconRect.anchorMax = new Vector2(1f, 1f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.preserveAspect = true;
        if (currentBuffIcon != null) 
        {
            iconImage.sprite = currentBuffIcon;
        }

        // Bar Background
        GameObject bgObj = new GameObject("BarBackground");
        bgObj.transform.SetParent(buffUIContainer.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(1f, 0.15f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        // Bar Fill
        GameObject fillObj = new GameObject("BarFill");
        fillObj.transform.SetParent(bgObj.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        buffUITimerFill = fillObj.AddComponent<Image>();
        buffUITimerFill.color = new Color(1f, 0.8f, 0f, 1f);
        
        // Setup Fill behavior
        // To use fillMethod, image needs a sprite, but we can use a blank texture
        Texture2D blankTex = new Texture2D(1, 1);
        blankTex.SetPixel(0, 0, Color.white);
        blankTex.Apply();
        Sprite blankSprite = Sprite.Create(blankTex, new Rect(0,0,1,1), new Vector2(0.5f, 0.5f));
        buffUITimerFill.sprite = blankSprite;
        buffUITimerFill.type = Image.Type.Filled;
        buffUITimerFill.fillMethod = Image.FillMethod.Horizontal;
        buffUITimerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        buffUITimerFill.fillAmount = 1f;
    }

    private void CheckGroundedWithLaser()
    {
        wasGrounded = isGrounded;

        // Prevent instant re-grounding after jumping
        if (Time.time - lastJumpTime < groundedCooldown)
        {
            isGrounded = false;
            return;
        }

        // Ignore while moving upward
        if (rb.linearVelocity.y > 0.1f)
        {
            isGrounded = false;
            return;
        }

        Vector2 laserStartPoint = new Vector2(
            transform.position.x,
            playerCollider.bounds.min.y + 0.02f
        );

        float rayLength = 0.2f;

        RaycastHit2D hit = GetGroundHit(
            laserStartPoint,
            rayLength
        );

        Debug.DrawRay(
            laserStartPoint,
            Vector2.down * rayLength,
            hit.collider != null ? Color.green : Color.red
        );

        if (hit.collider != null)
        {
            isGrounded = true;
            if (!wasGrounded)
            {
                jumpCount = 0;
            }
        }
        else
        {
            isGrounded = false;
        }
    }

    private void ExecuteInstantJump()
    {
        // Reset vertical velocity before jumping
        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            0f
        );

        float appliedForce =
            (jumpCount == 0) ? jumpForce : doubleJumpForce;

        rb.AddForce(
            Vector2.up * appliedForce,
            ForceMode2D.Impulse
        );

        jumpCount++;

        isGrounded = false;
        lastJumpTime = Time.time;
    }

    private void ApplyBetterGravity()
    {
        // Falling
        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = fallMultiplier;
        }
        // Rising but jump released
        else if (rb.linearVelocity.y > 0 &&
                 !Input.GetKey(KeyCode.W))
        {
            rb.gravityScale = lowJumpMultiplier;
        }
        else
        {
            rb.gravityScale = normalGravityScale;
        }
    }

    private void CheckAndDrop()
    {
        Vector2 laserStartPoint = new Vector2(
            transform.position.x,
            playerCollider.bounds.min.y + 0.1f
        );

        RaycastHit2D hit = GetDropThroughPlatformHit(
            laserStartPoint,
            1f
        );

        if (hit.collider != null)
        {
            StartCoroutine(
                DropThroughPlatform(hit.collider)
            );
        }
    }

    private IEnumerator DropThroughPlatform(
        Collider2D platformCollider)
    {
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Physics2D.IgnoreCollision(
                playerColliders[i],
                platformCollider,
                true
            );
        }

        isGrounded = false;
        lastJumpTime = Time.time;

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            -5f
        );

        float timeout = 1f;
        float timer = 0f;

        while (timer < timeout &&
               playerCollider.bounds.max.y >
               platformCollider.bounds.min.y)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Physics2D.IgnoreCollision(
                playerColliders[i],
                platformCollider,
                false
            );
        }
    }

    private RaycastHit2D GetDropThroughPlatformHit(
        Vector2 startPoint,
        float rayLength)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            startPoint,
            Vector2.down,
            rayLength,
            platformLayerMask
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;

            if (hitCollider == null ||
                hitCollider.isTrigger ||
                IsPlayerCollider(hitCollider))
            {
                continue;
            }

            if (hitCollider.GetComponent<PlatformEffector2D>() != null ||
                hitCollider.GetComponentInParent<PlatformEffector2D>() != null)
            {
                return hits[i];
            }
        }

        return new RaycastHit2D();
    }

    private RaycastHit2D GetGroundHit(
        Vector2 startPoint,
        float rayLength)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            startPoint,
            Vector2.down,
            rayLength,
            platformLayerMask
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;

            if (hitCollider != null &&
                !hitCollider.isTrigger &&
                !IsPlayerCollider(hitCollider))
            {
                return hits[i];
            }
        }

        return new RaycastHit2D();
    }

    private bool IsPlayerCollider(Collider2D colliderToCheck)
    {
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (colliderToCheck == playerColliders[i])
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyFrictionlessMovementMaterial()
    {
        if (frictionlessMovementMaterial == null)
        {
            frictionlessMovementMaterial = new PhysicsMaterial2D("Player Frictionless Movement")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        for (int i = 0; i < playerColliders.Length; i++)
        {
            playerColliders[i].sharedMaterial = frictionlessMovementMaterial;
        }
    }

    private void UpdateSpriteDirection()
    {
        // Only flip based on movement vector if player isn't actively forcing a directional look via clicking
        if (!Input.GetMouseButton(0) && Mathf.Abs(horizontalInput) > 0.01f)
        {
            bool shouldFlip = horizontalInput < 0f;

            if (spriteRenderer != null)
            {
                SetSpriteFacing(shouldFlip);
            }
        }
    }

    private void SetSpriteFacing(bool shouldFlip)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = shouldFlip;
        }

        if (elementIndicator != null)
        {
            elementIndicator.localScale = Vector3.one;
        }
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        // Run speed
        animator.SetFloat(
            "Speed",
            Mathf.Abs(horizontalInput)
        );

        // Grounded state
        animator.SetBool(
            "IsGrounded",
            isGrounded
        );

        // Vertical movement
        animator.SetFloat(
            "VerticalVelocity",
            rb.linearVelocity.y
        );

        // Falling bool (optional)
        foreach (var p in animator.parameters)
        {
            if (p.name == "IsFalling" &&
                p.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(
                    "IsFalling",
                    rb.linearVelocity.y < -0.05f
                );

                break;
            }
        }
    }
}
