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

    [Header("Auto UI")]
    public bool createHealthBarIfMissing = true;
    public Vector2 healthBarPosition = new Vector2(28f, -28f);
    public Vector2 healthBarSize = new Vector2(240f, 22f);
    public float gameOverDelay = 1f;

    private float nextContactDamageTime;
    private bool isDead;
    private GameObject gameOverPanel;
    private RectTransform healthFillRect;

    void Start()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
        EnsureHealthBar();
        UpdateHealthBar();
        UpdateManaBar();
    }

    void Update()
    {
        if (isDead) return;

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
        UpdateHealthBar();
        if (currentHealth <= 0) Die();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;
        TryTakeEnemyContactDamage(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead) return;
        TryTakeEnemyContactDamage(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;
        TryTakeEnemyContactDamage(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isDead) return;
        TryTakeEnemyContactDamage(other.gameObject);
    }

    private void CheckNearbyEnemies()
    {
        if (Time.time < nextContactDamageTime) return;

        EnemyMovement[] enemies = FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None);
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

            if (enemyHealth == null || !enemyHealth.isBoss)
            {
                enemy.ApplyKnockbackFrom(transform.position);
            }
        }

        nextContactDamageTime = Time.time + contactDamageCooldown;
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
        EnemyMovement[] enemies = FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None);

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
        if (healthSlider != null || !createHealthBarIfMissing) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("PlayerHUDCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject root = new GameObject("PlayerHealthBar");
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = healthBarPosition;
        rootRect.sizeDelta = healthBarSize;

        Image background = root.AddComponent<Image>();
        background.color = new Color(0.12f, 0.08f, 0.08f, 0.88f);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(root.transform, false);

        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        healthFillRect = fillRect;

        Image fill = fillObject.AddComponent<Image>();
        fill.color = new Color(0.88f, 0.12f, 0.16f, 1f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;

        healthSlider = root.AddComponent<Slider>();
        healthSlider.transition = Selectable.Transition.None;
        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;
        healthSlider.value = 1f;
        healthSlider.fillRect = fillRect;
        healthSlider.targetGraphic = fill;
    }

    private void ShowGameOver()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
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
        Collider2D[] playerColliders = GetComponentsInChildren<Collider2D>();
        EnemyMovement[] enemies = FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None);

        foreach (Collider2D playerCollider in playerColliders)
        {
            foreach (EnemyMovement enemy in enemies)
            {
                if (enemy == null) continue;

                Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
                if (enemyCollider != null)
                {
                    Physics2D.IgnoreCollision(playerCollider, enemyCollider, false);
                }
            }
        }
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

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
            manaSlider.value = maxMana > 0f ? currentMana / maxMana : 0f;
        }
    }
}
