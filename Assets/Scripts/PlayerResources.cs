using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerResources : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;
    public float maxMana = 100f;
    public float currentMana;
    public float manaRegenRate = 10f;

    public Slider healthSlider;
    public Slider manaSlider;

    [Header("Enemy Contact")]
    public float enemyContactDamage = 10f;
    public float contactDamageCooldown = 1f;
    public float enemyContactRadius = 0.95f;
    public float bossContactKnockbackForce = 12f;
    public float bossContactKnockbackUpwardVelocity = 4f;
    public float bossContactKnockbackDuration = 0.22f;
    public float damageFlashDuration = 0.12f;
    public int damageFlashBlinks = 3;

    [Header("Auto UI")]
    public bool createHealthBarIfMissing = true;
    public Vector2 healthBarPosition = new Vector2(48f, -16f);
    public Vector2 healthBarSize = new Vector2(240f, 22f);
    public Vector2 manaBarPosition = new Vector2(28f, -58f);
    public Vector2 manaBarSize = new Vector2(240f, 22f);
    public float gameOverDelay = 1f;
    [Range(0.01f, 1f)] public float lowHealthBorderThreshold = 0.3f;
    public float lowHealthBorderBlinkSpeed = 7f;
    public float lowHealthBorderMaxAlpha = 0.75f;
    public float lowHealthBorderThickness = 16f;

    private float nextContactDamageTime;
    private bool isDead;
    private bool isKnockedDown;
    private Coroutine knockdownRoutine;
    private Coroutine damageFlashRoutine;
    private GameObject gameOverPanel;
    private RectTransform healthFillRect;
    private RectTransform manaFillRect;
    private Image[] lowHealthBorderImages;
    private SpriteRenderer[] spriteRenderers;
    private Color[] baseSpriteColors;

    void Start()
    {
        manaBarSize.y = healthBarSize.y; // Force mana bar height to match health bar height
        CacheSpriteRenderers();
        currentHealth = maxHealth;
        currentMana = maxMana;
        EnsureHealthBar();
        UpdateHealthBar();
        UpdateManaBar();
        EnsureLowHealthBorder();
    }

    void Update()
    {
        UpdateLowHealthBorder();

        if (isDead || isKnockedDown) return;

        if (currentMana < maxMana)
        {
            currentMana += manaRegenRate * Time.deltaTime;
            UpdateManaBar();
        }

        CheckNearbyEnemies();
    }

    public bool SpendMana(float amount)
    {
        if (currentMana >= amount)
        {
            currentMana -= amount;
            UpdateManaBar();
            return true;
        }
        return false;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        FlashRed();
        UpdateHealthBar();
        if (currentHealth <= 0) Die();
    }

    public void AddHealth(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        UpdateHealthBar();
    }

    public void AddMana(float amount)
    {
        if (isDead) return;

        currentMana = Mathf.Clamp(currentMana + amount, 0f, maxMana);
        UpdateManaBar();
    }

    public void TakeNonlethalDamage(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 1f, maxHealth);
        FlashRed();
        UpdateHealthBar();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead || isKnockedDown) return;
        TryTakeEnemyContactDamage(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead || isKnockedDown) return;
        TryTakeEnemyContactDamage(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead || isKnockedDown) return;
        TryTakeEnemyContactDamage(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isDead || isKnockedDown) return;
        TryTakeEnemyContactDamage(other.gameObject);
    }

    private void CheckNearbyEnemies()
    {
        if (Time.time < nextContactDamageTime) return;

        EnemyMovement[] enemies = FindObjectsByType<EnemyMovement>();
        foreach (EnemyMovement enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;

            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance <= enemyContactRadius)
            {
                TakeEnemyContactDamage(enemy);
                return;
            }
        }
    }

    private void TryTakeEnemyContactDamage(GameObject other)
    {
        EnemyMovement enemy = other.GetComponent<EnemyMovement>();
        if (enemy == null)
        {
            enemy = other.GetComponentInParent<EnemyMovement>();
        }

        if (enemy != null)
        {
            TakeEnemyContactDamage(enemy);
        }
    }

    private void TakeEnemyContactDamage(EnemyMovement enemy)
    {
        if (Time.time < nextContactDamageTime) return;

        float damage = enemy != null ? enemy.GetContactDamage() : enemyContactDamage;
        TakeDamage(damage);

        if (enemy != null)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
            {
                enemyHealth = enemy.GetComponentInParent<EnemyHealth>();
            }

            if (enemyHealth != null && enemyHealth.isBoss)
            {
                ApplyBossContactKnockback(enemy.transform.position);
            }
            else
            {
                enemy.ApplyKnockbackFrom(transform.position);
            }
        }

        nextContactDamageTime = Time.time + contactDamageCooldown;
    }

    public void ApplyBossContactKnockback(Vector3 sourcePosition)
    {
        if (isDead) return;

        float direction = transform.position.x >= sourcePosition.x ? 1f : -1f;
        Vector2 knockbackVelocity = new Vector2(
            direction * bossContactKnockbackForce,
            bossContactKnockbackUpwardVelocity
        );

        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.ApplyExternalKnockback(knockbackVelocity, bossContactKnockbackDuration);
            return;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = knockbackVelocity;
        }
    }

    private void CacheSpriteRenderers()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        baseSpriteColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            baseSpriteColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
        }
    }

    private void FlashRed()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            CacheSpriteRenderers();
        }

        if (damageFlashRoutine != null)
        {
            StopCoroutine(damageFlashRoutine);
        }

        damageFlashRoutine = StartCoroutine(DamageFlashRoutine());
    }

    private IEnumerator DamageFlashRoutine()
    {
        int blinkCount = Mathf.Max(1, damageFlashBlinks);
        float blinkStepDuration = damageFlashDuration / blinkCount;

        for (int i = 0; i < blinkCount; i++)
        {
            SetSpriteColors(Color.red);
            yield return new WaitForSeconds(blinkStepDuration * 0.5f);
            RestoreSpriteColors();
            yield return new WaitForSeconds(blinkStepDuration * 0.5f);
        }

        RestoreSpriteColors();
        damageFlashRoutine = null;
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
                spriteRenderers[i].color = i < baseSpriteColors.Length ? baseSpriteColors[i] : Color.white;
            }
        }
    }

    public void ApplyKnockdown(Vector3 sourcePosition, float duration)
    {
        if (isDead || duration <= 0f) return;

        if (knockdownRoutine != null)
        {
            StopCoroutine(knockdownRoutine);
        }

        knockdownRoutine = StartCoroutine(KnockdownRoutine(sourcePosition, duration));
    }

    private IEnumerator KnockdownRoutine(Vector3 sourcePosition, float duration)
    {
        isKnockedDown = true;

        PlayerController controller = GetComponent<PlayerController>();
        WeaponManager weaponManager = GetComponent<WeaponManager>();
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (controller != null)
        {
            controller.enabled = false;
        }

        if (weaponManager != null)
        {
            weaponManager.CancelHeldBasicFire();
            weaponManager.enabled = false;
        }

        if (rb != null)
        {
            float dir = transform.position.x >= sourcePosition.x ? 1f : -1f;
            rb.linearVelocity = new Vector2(dir * 6f, 4f);
        }

        if (animator != null)
        {
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.Play("Player_Death", 0, 0f);
        }

        yield return new WaitForSeconds(duration);

        if (animator != null)
        {
            animator.Play("Player_Idle", 0, 0f);
        }

        if (controller != null)
        {
            controller.enabled = true;
        }

        if (weaponManager != null)
        {
            weaponManager.enabled = true;
        }

        isKnockedDown = false;
        knockdownRoutine = null;
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        currentHealth = 0f;
        UpdateHealthBar();
        Debug.Log("Player Died!");

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = Mathf.Max(rb.gravityScale, 1f);
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        IgnoreEnemyCollisionsAfterDeath();

        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        WeaponManager weaponManager = GetComponent<WeaponManager>();
        if (weaponManager != null)
        {
            weaponManager.CancelHeldBasicFire();
            weaponManager.enabled = false;
        }

        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.Play("Player_Death", 0, 0f);
        }

        StartCoroutine(ShowGameOverAfterDelay());
    }

    private void IgnoreEnemyCollisionsAfterDeath()
    {
        Collider2D[] playerColliders = GetComponentsInChildren<Collider2D>();
        EnemyMovement[] enemies = FindObjectsByType<EnemyMovement>();

        foreach (Collider2D playerCollider in playerColliders)
        {
            foreach (EnemyMovement enemy in enemies)
            {
                if (enemy == null) continue;

                Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
                if (enemyCollider != null)
                {
                    Physics2D.IgnoreCollision(playerCollider, enemyCollider, true);
                }
            }
        }
    }

    private IEnumerator ShowGameOverAfterDelay()
    {
        yield return new WaitForSeconds(gameOverDelay);
        ShowGameOver();
    }

    private void EnsureHealthBar()
    {
        if (!createHealthBarIfMissing) return;
        if (healthSlider != null && manaSlider != null) return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("PlayerHUDCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (healthSlider == null)
        {
            healthSlider = CreateResourceBar(
                canvas.transform,
                "PlayerHealthBar",
                healthBarPosition,
                healthBarSize,
                new Color(0.12f, 0.08f, 0.08f, 0.88f),
                new Color(0.88f, 0.12f, 0.16f, 1f),
                out healthFillRect
            );
        }

        if (manaSlider == null)
        {
            manaSlider = CreateResourceBar(
                canvas.transform,
                "PlayerManaBar",
                manaBarPosition,
                manaBarSize,
                new Color(0.06f, 0.08f, 0.16f, 0.88f),
                new Color(0.16f, 0.42f, 1f, 1f),
                out manaFillRect
            );
        }
    }

    private Slider CreateResourceBar(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        Color backgroundColor,
        Color fillColor,
        out RectTransform fillRect)
    {
        GameObject root = new GameObject(objectName);
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = position;
        rootRect.sizeDelta = size;

        Image background = root.AddComponent<Image>();
        background.color = backgroundColor;

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(root.transform, false);

        fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        fillRect.pivot = new Vector2(0f, 0.5f);

        Image fill = fillObject.AddComponent<Image>();
        fill.color = fillColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;

        Slider slider = root.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.fillRect = fillRect;
        slider.targetGraphic = fill;

        return slider;
    }

    private void ShowGameOver()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("GameOverCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        EnsureEventSystem();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            return;
        }

        gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = gameOverPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image overlay = gameOverPanel.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.68f);

        GameObject titleObject = new GameObject("GameOverText");
        titleObject.transform.SetParent(gameOverPanel.transform, false);

        RectTransform titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 54f);
        titleRect.sizeDelta = new Vector2(420f, 80f);

        Text title = titleObject.AddComponent<Text>();
        title.text = "GAME OVER";
        title.alignment = TextAnchor.MiddleCenter;
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 44;
        title.color = Color.white;

        GameObject buttonObject = new GameObject("RestartButton");
        buttonObject.transform.SetParent(gameOverPanel.transform, false);

        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -32f);
        buttonRect.sizeDelta = new Vector2(190f, 48f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.9f, 0.16f, 0.18f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(RestartScene);

        GameObject labelObject = new GameObject("Text");
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObject.AddComponent<Text>();
        label.text = "Restart";
        label.alignment = TextAnchor.MiddleCenter;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.color = Color.white;

        GameObject continueButtonObject = new GameObject("ContinueButton");
        continueButtonObject.transform.SetParent(gameOverPanel.transform, false);

        RectTransform continueButtonRect = continueButtonObject.AddComponent<RectTransform>();
        continueButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
        continueButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
        continueButtonRect.pivot = new Vector2(0.5f, 0.5f);
        continueButtonRect.anchoredPosition = new Vector2(0f, -92f);
        continueButtonRect.sizeDelta = new Vector2(190f, 48f);

        Image continueButtonImage = continueButtonObject.AddComponent<Image>();
        continueButtonImage.color = new Color(0.16f, 0.42f, 0.9f, 1f);

        Button continueButton = continueButtonObject.AddComponent<Button>();
        continueButton.targetGraphic = continueButtonImage;
        continueButton.onClick.AddListener(ContinueFromGameOver);

        GameObject continueLabelObject = new GameObject("Text");
        continueLabelObject.transform.SetParent(continueButtonObject.transform, false);

        RectTransform continueLabelRect = continueLabelObject.AddComponent<RectTransform>();
        continueLabelRect.anchorMin = Vector2.zero;
        continueLabelRect.anchorMax = Vector2.one;
        continueLabelRect.offsetMin = Vector2.zero;
        continueLabelRect.offsetMax = Vector2.zero;

        Text continueLabel = continueLabelObject.AddComponent<Text>();
        continueLabel.text = "Continue";
        continueLabel.alignment = TextAnchor.MiddleCenter;
        continueLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        continueLabel.fontSize = 24;
        continueLabel.color = Color.white;
    }

    private void EnsureLowHealthBorder()
    {
        if (lowHealthBorderImages != null && lowHealthBorderImages.Length > 0)
        {
            return;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("PlayerHUDCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject borderRoot = new GameObject("LowHealthBorder");
        borderRoot.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = borderRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        lowHealthBorderImages = new Image[4];
        lowHealthBorderImages[0] = CreateBorderImage(borderRoot.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, lowHealthBorderThickness));
        lowHealthBorderImages[1] = CreateBorderImage(borderRoot.transform, "Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, lowHealthBorderThickness));
        lowHealthBorderImages[2] = CreateBorderImage(borderRoot.transform, "Left", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(lowHealthBorderThickness, 0f));
        lowHealthBorderImages[3] = CreateBorderImage(borderRoot.transform, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(lowHealthBorderThickness, 0f));
        SetLowHealthBorderAlpha(0f);
    }

    private Image CreateBorderImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
    {
        GameObject borderObject = new GameObject(name);
        borderObject.transform.SetParent(parent, false);

        RectTransform rect = borderObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta;

        Image image = borderObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = new Color(1f, 0f, 0f, 0f);
        return image;
    }

    private void UpdateLowHealthBorder()
    {
        EnsureLowHealthBorder();

        if (lowHealthBorderImages == null)
        {
            return;
        }

        float healthPercent = maxHealth > 0f ? currentHealth / maxHealth : 0f;
        if (isDead || healthPercent > lowHealthBorderThreshold)
        {
            SetLowHealthBorderAlpha(0f);
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * lowHealthBorderBlinkSpeed) + 1f) * 0.5f;
        SetLowHealthBorderAlpha(pulse * lowHealthBorderMaxAlpha);
    }

    private void SetLowHealthBorderAlpha(float alpha)
    {
        if (lowHealthBorderImages == null)
        {
            return;
        }

        for (int i = 0; i < lowHealthBorderImages.Length; i++)
        {
            if (lowHealthBorderImages[i] != null)
            {
                lowHealthBorderImages[i].color = new Color(1f, 0f, 0f, alpha);
            }
        }
    }

    private void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ContinueFromGameOver()
    {
        isDead = false;
        currentHealth = maxHealth;
        currentMana = maxMana;
        nextContactDamageTime = Time.time + contactDamageCooldown;
        UpdateHealthBar();
        UpdateManaBar();

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        RestoreEnemyCollisions();

        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = true;
        }

        WeaponManager weaponManager = GetComponent<WeaponManager>();
        if (weaponManager != null)
        {
            weaponManager.enabled = true;
        }

        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.Play("Player_Idle", 0, 0f);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private void RestoreEnemyCollisions()
    {
        // Enemies stay non-physical with the player; contact damage is handled by overlap checks.
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void UpdateHealthBar()
    {
        float healthPercent = maxHealth > 0f ? currentHealth / maxHealth : 0f;
        healthPercent = Mathf.Clamp01(healthPercent);

        if (healthSlider != null)
        {
            healthSlider.value = healthPercent;
        }

        if (healthFillRect != null)
        {
            healthFillRect.localScale = new Vector3(healthPercent, 1f, 1f);
        }
    }

    private void UpdateManaBar()
    {
        if (manaSlider != null)
        {
            manaSlider.value = maxMana > 0f ? Mathf.Clamp01(currentMana / maxMana) : 0f;
        }

        if (manaFillRect != null)
        {
            float manaPercent = maxMana > 0f ? Mathf.Clamp01(currentMana / maxMana) : 0f;
            manaFillRect.localScale = new Vector3(manaPercent, 1f, 1f);
        }
    }
}
