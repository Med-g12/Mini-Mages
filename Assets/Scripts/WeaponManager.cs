using UnityEngine;
using UnityEngine.UI;

public class WeaponManager : MonoBehaviour
{
    public Transform wandPivot;
    public Transform firePoint;
    public WandData[] allWands;
    public bool[] unlockedWands = { true, false, false, false };
    public float basicFireCooldown = 0.35f;
    public float earthBasicFireCooldown = 1.5f;
    public float controlledProjectileFollowSpeed = 28f;
    public float heldProjectileSpawnTravelSpeed = 10f;
    public float bonusProjectileDamage = 0f;
    public float streamHoldDelay = 0.25f;
    public float windChargeTimeToMax = 1.5f;
    public float windMaxChargeScaleMultiplier = 2.2f;
    public float windMinChargedKnockbackForce = 8f;
    public float windMaxChargedKnockbackForce = 90f;
    public float windMinChargedKnockbackDuration = 0.2f;
    public float windMaxChargedKnockbackDuration = 0.8f;
    public float windChargeSpinSpeed = 720f;
    public float windMinImpactAreaRadius = 1.25f;
    public float windMaxImpactAreaRadius = 3.25f;
    public string playerFireStateName = "Player_Fire";
    public Vector2 streamMuzzleOffset = Vector2.zero;

    [Header("Element Inventory UI")]
    public bool showElementInventory = true;
    public Vector2 inventorySlotSize = new Vector2(48f, 48f);
    public Vector2 inventorySpacing = new Vector2(8f, 0f);

    private int activeWandIndex = 0;
    private float nextBasicFireTime = 0f;
    private bool isHoldingBasicFire;
    private bool didCreateHeldProjectile;
    private float basicFireHoldStartTime;
    private bool isElementStreamActive;
    private GameObject heldProjectileObject;
    private Projectile heldProjectile;
    private Vector3 heldProjectileBaseScale = Vector3.one;
    private float windChargeRotation;
    private PlayerResources resources;
    private Animator animator;
    private PlayerController playerController;
    private Image[] inventorySlotBackgrounds;
    private Image[] inventorySlotIcons;

    private float damageBuffEndTime = 0f;
    private float activeDamageBonus = 0f;

    void Start()
    {
        resources = GetComponent<PlayerResources>();
        playerController = GetComponent<PlayerController>();

        if (playerController != null &&
            playerController.currentElementData != null)
        {
            EnsureStartingWand(playerController.currentElementData);
        }

        // Find the Animator component (on self or children)
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        BuildElementInventoryUI();
        RefreshElementInventoryUI();
    }

    void Update()
    {
        if (wandPivot == null || firePoint == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        Vector3 lookDir = mousePos - wandPivot.position;
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;
        wandPivot.rotation = Quaternion.Euler(0, 0, angle);

        if (Input.GetKeyDown(KeyCode.Alpha1)) SetActiveWand(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetActiveWand(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetActiveWand(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetActiveWand(3);

        WandData activeWand = GetActiveWand();
        if (activeWand == null) return;

        HandleBasicFireInput(activeWand, mousePos);

        if (Input.GetKeyDown(KeyCode.Q))
        {
            FacePlayerToward(mousePos);
            FireProjectile(activeWand, activeWand.qProjectilePrefab, activeWand.qManaCost);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            FacePlayerToward(mousePos);
            FireProjectile(activeWand, activeWand.eProjectilePrefab, activeWand.eManaCost);
        }

        UpdateDamageBuff();
    }

    void OnDisable()
    {
        CancelHeldBasicFire();
    }

    private void FireProjectile(WandData wand, GameObject prefab, float manaCost)
    {
        if (prefab != null && CanSpendMana(manaCost))
        {
            GameObject projectileObject = Instantiate(prefab, firePoint.position, wandPivot.rotation);
            Projectile projectile = projectileObject.GetComponent<Projectile>();
            if (projectile != null && wand != null)
            {
                projectile.element = wand.elementType;
                projectile.damage += bonusProjectileDamage;
            }

            TriggerFireAnimation();
        }
    }

    private void HandleBasicFireInput(WandData activeWand, Vector3 mousePos)
    {
        if (activeWand.elementType == ElementType.Water ||
            activeWand.elementType == ElementType.Fire)
        {
            HandleElementStreamBasicFireInput(activeWand, mousePos);
            return;
        }

        if (activeWand.elementType == ElementType.Earth)
        {
            HandleInstantBasicFireInput(activeWand, mousePos);
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            isHoldingBasicFire = true;
            didCreateHeldProjectile = false;
            basicFireHoldStartTime = Time.time;
            windChargeRotation = 0f;
            CreateHeldProjectile(activeWand, mousePos);
        }

        if (didCreateHeldProjectile)
        {
            if (IsChargingWindProjectile())
            {
                MoveWindChargeProjectile(mousePos);
            }
            else
            {
                MoveHeldProjectile(mousePos);
            }

            UpdateWindCharge();
        }

        if (isHoldingBasicFire && Input.GetMouseButtonUp(0))
        {
            if (didCreateHeldProjectile)
            {
                LaunchHeldProjectile(mousePos);
            }
            else
            {
                LaunchHeldProjectile(mousePos);
            }

            isHoldingBasicFire = false;
        }
    }

    private void HandleInstantBasicFireInput(WandData activeWand, Vector3 mousePos)
    {
        if (!Input.GetMouseButtonDown(0) ||
            Time.time < nextBasicFireTime ||
            activeWand == null ||
            activeWand.basicProjectilePrefab == null)
        {
            return;
        }

        FacePlayerToward(mousePos);
        FireProjectile(activeWand, activeWand.basicProjectilePrefab, activeWand.basicManaCost);
        nextBasicFireTime = Time.time + GetBasicFireCooldown(activeWand);
    }

    private void HandleElementStreamBasicFireInput(WandData activeWand, Vector3 mousePos)
    {
        if (Input.GetMouseButtonDown(0))
        {
            isHoldingBasicFire = true;
            didCreateHeldProjectile = false;
            isElementStreamActive = false;
            basicFireHoldStartTime = Time.time;
        }

        if (isHoldingBasicFire &&
            !isElementStreamActive &&
            Input.GetMouseButton(0) &&
            Time.time - basicFireHoldStartTime >= streamHoldDelay)
        {
            CreateElementStream(activeWand, mousePos);
        }

        if (isElementStreamActive)
        {
            MoveElementStream(mousePos);
            KeepPlayerInFirePose();
        }

        if (isHoldingBasicFire && Input.GetMouseButtonUp(0))
        {
            if (isElementStreamActive)
            {
                DestroyHeldProjectile();
                nextBasicFireTime = Time.time + GetBasicFireCooldown(activeWand);
            }
            else
            {
                if (Time.time >= nextBasicFireTime)
                {
                    FireProjectile(activeWand, activeWand.basicProjectilePrefab, activeWand.basicManaCost);
                    nextBasicFireTime = Time.time + GetBasicFireCooldown(activeWand);
                }
            }

            isHoldingBasicFire = false;
            isElementStreamActive = false;
        }
    }

    private void CreateElementStream(WandData activeWand, Vector3 mousePos)
    {
        if (Time.time < nextBasicFireTime ||
            activeWand == null ||
            !CanSpendMana(activeWand.basicManaCost))
        {
            isHoldingBasicFire = false;
            return;
        }

        GameObject prefab =
            activeWand.heldBasicProjectilePrefab != null
                ? activeWand.heldBasicProjectilePrefab
                : activeWand.basicProjectilePrefab;

        if (prefab == null)
        {
            isHoldingBasicFire = false;
            return;
        }

        FacePlayerToward(mousePos);
        TriggerFireAnimation();
        KeepPlayerInFirePose();

        heldProjectileObject = Instantiate(
            prefab,
            GetElementStreamPosition(prefab.GetComponent<Projectile>()),
            wandPivot.rotation
        );

        heldProjectile = heldProjectileObject.GetComponent<Projectile>();
        if (heldProjectile != null)
        {
            heldProjectile.element = activeWand.elementType;
            heldProjectile.HoldInPlace();
        }

        isElementStreamActive = true;
        didCreateHeldProjectile = true;
        MoveElementStream(mousePos);
    }

    private void MoveElementStream(Vector3 mousePos)
    {
        if (heldProjectileObject == null)
        {
            return;
        }

        FacePlayerToward(mousePos);
        heldProjectileObject.transform.rotation = wandPivot.rotation;
        heldProjectileObject.transform.position = GetElementStreamPosition(heldProjectile);
    }

    private Vector3 GetElementStreamPosition(Projectile projectile)
    {
        Vector2 attachOffset =
            projectile != null
                ? projectile.heldAttachOffset
                : Vector2.zero;

        return firePoint.position +
               wandPivot.TransformVector(attachOffset + streamMuzzleOffset);
    }

    private void DestroyHeldProjectile()
    {
        if (heldProjectileObject != null)
        {
            Destroy(heldProjectileObject);
        }

        heldProjectileObject = null;
        heldProjectile = null;
        heldProjectileBaseScale = Vector3.one;
        didCreateHeldProjectile = false;
    }

    public void CancelHeldBasicFire()
    {
        DestroyHeldProjectile();
        isHoldingBasicFire = false;
        isElementStreamActive = false;
        windChargeRotation = 0f;
    }

    private void KeepPlayerInFirePose()
    {
        if (animator == null || string.IsNullOrEmpty(playerFireStateName))
        {
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsName(playerFireStateName))
        {
            animator.Play(playerFireStateName, 0, 0.35f);
        }
        else
        {
            animator.Play(playerFireStateName, 0, Mathf.Clamp01(state.normalizedTime));
        }
    }

    private void CreateHeldProjectile(WandData activeWand, Vector3 mousePos)
    {
        if (Time.time < nextBasicFireTime ||
            activeWand == null ||
            activeWand.basicProjectilePrefab == null ||
            !CanSpendMana(activeWand.basicManaCost))
        {
            isHoldingBasicFire = false;
            return;
        }

        FacePlayerToward(mousePos);
        TriggerFireAnimation();

        heldProjectileObject = Instantiate(
            activeWand.basicProjectilePrefab,
            firePoint.position,
            Quaternion.identity
        );

        heldProjectile = heldProjectileObject.GetComponent<Projectile>();
        if (heldProjectile != null)
        {
            heldProjectile.element = activeWand.elementType;
            heldProjectile.damage += bonusProjectileDamage;
            heldProjectile.HoldInPlace();
        }

        heldProjectileBaseScale = heldProjectileObject.transform.localScale;
        UpdateWindCharge();
        didCreateHeldProjectile = true;
    }

    private void MoveHeldProjectile(Vector3 mousePos)
    {
        if (heldProjectileObject == null)
        {
            return;
        }

        float followSpeed =
            Vector3.Distance(heldProjectileObject.transform.position, mousePos) > 0.25f
                ? heldProjectileSpawnTravelSpeed
                : controlledProjectileFollowSpeed;

        heldProjectileObject.transform.position = Vector3.MoveTowards(
            heldProjectileObject.transform.position,
            mousePos,
            followSpeed * Time.deltaTime
        );
    }

    private bool IsChargingWindProjectile()
    {
        return heldProjectile != null &&
               heldProjectile.element == ElementType.Wind &&
               heldProjectileObject != null;
    }

    private void MoveWindChargeProjectile(Vector3 mousePos)
    {
        if (heldProjectileObject == null)
        {
            return;
        }

        FacePlayerToward(mousePos);
        KeepPlayerInFirePose();
        windChargeRotation += windChargeSpinSpeed * Time.deltaTime;
        heldProjectileObject.transform.rotation =
            wandPivot.rotation * Quaternion.Euler(0f, 0f, windChargeRotation);
        heldProjectileObject.transform.position = firePoint.position;
    }

    private void UpdateWindCharge()
    {
        if (heldProjectile == null ||
            heldProjectile.element != ElementType.Wind ||
            heldProjectileObject == null)
        {
            return;
        }

        float chargePercent = GetWindChargePercent();
        float maxScaleMultiplier = Mathf.Max(1f, windMaxChargeScaleMultiplier);
        float scaleMultiplier = Mathf.Lerp(1f, maxScaleMultiplier, chargePercent);
        heldProjectileObject.transform.localScale = heldProjectileBaseScale * scaleMultiplier;
    }

    private float GetWindChargePercent()
    {
        if (windChargeTimeToMax <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp01((Time.time - basicFireHoldStartTime) / windChargeTimeToMax);
    }

    private float GetChargedWindKnockbackForce()
    {
        float minForce = Mathf.Min(windMinChargedKnockbackForce, windMaxChargedKnockbackForce);
        float maxForce = Mathf.Max(windMinChargedKnockbackForce, windMaxChargedKnockbackForce, 90f);

        return Mathf.Lerp(
            minForce,
            maxForce,
            GetWindChargePercent()
        );
    }

    private float GetChargedWindKnockbackDuration()
    {
        float minDuration = Mathf.Min(windMinChargedKnockbackDuration, windMaxChargedKnockbackDuration);
        float maxDuration = Mathf.Max(windMinChargedKnockbackDuration, windMaxChargedKnockbackDuration, 0.8f);

        return Mathf.Lerp(
            minDuration,
            maxDuration,
            GetWindChargePercent()
        );
    }

    private float GetChargedWindAreaRadius()
    {
        float minRadius = Mathf.Min(windMinImpactAreaRadius, windMaxImpactAreaRadius);
        float maxRadius = Mathf.Max(windMinImpactAreaRadius, windMaxImpactAreaRadius);

        return Mathf.Lerp(
            minRadius,
            maxRadius,
            GetWindChargePercent()
        );
    }

    private void LaunchHeldProjectile(Vector3 mousePos)
    {
        if (heldProjectileObject == null)
        {
            heldProjectile = null;
            heldProjectileBaseScale = Vector3.one;
            nextBasicFireTime = Time.time + GetBasicFireCooldown(GetActiveWand());
            return;
        }

        Vector2 launchDirection =
            mousePos - transform.position;
        if (launchDirection.sqrMagnitude < 0.01f)
        {
            launchDirection = Vector2.right;
        }

        float angle =
            Mathf.Atan2(launchDirection.y, launchDirection.x) *
            Mathf.Rad2Deg;

        heldProjectileObject.transform.rotation =
            Quaternion.Euler(0f, 0f, angle);

        if (heldProjectile != null)
        {
            if (heldProjectile.element == ElementType.Wind)
            {
                heldProjectile.knockbackForce = GetChargedWindKnockbackForce();
                heldProjectile.knockbackDuration = GetChargedWindKnockbackDuration();
                heldProjectile.areaRadius = GetChargedWindAreaRadius();
                heldProjectile.windImpactAoE = true;
            }

            heldProjectile.Launch(launchDirection);
        }

        heldProjectileObject = null;
        heldProjectile = null;
        heldProjectileBaseScale = Vector3.one;
        didCreateHeldProjectile = false;
        nextBasicFireTime = Time.time + GetBasicFireCooldown(GetActiveWand());
    }

    private void TriggerFireAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger("Fire");
        }
    }

    private void EnsureStartingWand(WandData startingWand)
    {
        if (allWands == null || allWands.Length == 0)
        {
            allWands = new WandData[] { startingWand };
        }
        else if (allWands[0] == null)
        {
            allWands[0] = startingWand;
        }

        if (unlockedWands == null || unlockedWands.Length < allWands.Length)
        {
            bool[] resizedUnlocks = new bool[allWands.Length];
            for (int i = 0; i < resizedUnlocks.Length; i++)
            {
                resizedUnlocks[i] =
                    unlockedWands != null &&
                    i < unlockedWands.Length &&
                    unlockedWands[i];
            }

            unlockedWands = resizedUnlocks;
        }

        unlockedWands[0] = true;
        SetActiveWand(0);
    }

    private WandData GetActiveWand()
    {
        if (allWands == null || allWands.Length == 0)
        {
            return null;
        }

        activeWandIndex = Mathf.Clamp(activeWandIndex, 0, allWands.Length - 1);
        return allWands[activeWandIndex];
    }

    private bool IsWandUnlocked(int index)
    {
        return unlockedWands != null &&
               index >= 0 &&
               index < unlockedWands.Length &&
               unlockedWands[index] &&
               allWands != null &&
               index < allWands.Length &&
               allWands[index] != null;
    }

    private void SetActiveWand(int index)
    {
        if (!IsWandUnlocked(index))
        {
            return;
        }

        if (index != activeWandIndex)
        {
            CancelHeldBasicFire();
        }

        activeWandIndex = index;

        if (playerController != null)
        {
            playerController.SetActiveElement(allWands[activeWandIndex]);
        }

        RefreshElementInventoryUI();
    }

    private bool CanSpendMana(float manaCost)
    {
        return resources == null || resources.SpendMana(manaCost);
    }

    private float GetBasicFireCooldown(WandData wand)
    {
        if (wand != null && wand.elementType == ElementType.Earth)
        {
            return Mathf.Max(earthBasicFireCooldown, 1.5f);
        }

        return Mathf.Max(basicFireCooldown, 0.35f);
    }

    private void FacePlayerToward(Vector3 targetPosition)
    {
        if (playerController != null)
        {
            playerController.FaceToward(targetPosition);
        }
    }

    public bool HasBasicAttackReady()
    {
        WandData activeWand = GetActiveWand();

        return enabled &&
               wandPivot != null &&
               firePoint != null &&
               activeWand != null &&
               activeWand.basicProjectilePrefab != null;
    }

    public void UnlockWand(int index)
    {
        if (unlockedWands == null || index < 0 || index >= unlockedWands.Length)
        {
            return;
        }

        unlockedWands[index] = true;
        SetActiveWand(index);
        RefreshElementInventoryUI();
    }

    public void UnlockWand(WandData wand)
    {
        if (wand == null || allWands == null)
        {
            return;
        }

        for (int i = 0; i < allWands.Length; i++)
        {
            if (allWands[i] == wand || allWands[i].elementType == wand.elementType)
            {
                UnlockWand(i);
                return;
            }
        }
    }

    public void AddDamageBuff(float damageAmount, float duration)
    {
        if (duration <= 0f)
        {
            bonusProjectileDamage += damageAmount;
            return;
        }

        if (Time.time >= damageBuffEndTime)
        {
            activeDamageBonus = damageAmount;
            bonusProjectileDamage += activeDamageBonus;
        }
        else if (damageAmount > activeDamageBonus)
        {
            bonusProjectileDamage -= activeDamageBonus;
            activeDamageBonus = damageAmount;
            bonusProjectileDamage += activeDamageBonus;
        }

        damageBuffEndTime = Time.time + duration;
    }

    private void UpdateDamageBuff()
    {
        if (Time.time >= damageBuffEndTime && activeDamageBonus > 0f)
        {
            bonusProjectileDamage -= activeDamageBonus;
            activeDamageBonus = 0f;
        }
    }

    private void BuildElementInventoryUI()
    {
        if (!showElementInventory ||
            allWands == null ||
            allWands.Length == 0 ||
            inventorySlotIcons != null)
        {
            return;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("ElementInventoryCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject inventoryObject = new GameObject("ElementInventory");
        inventoryObject.transform.SetParent(canvas.transform, false);

        RectTransform inventoryRect =
            inventoryObject.AddComponent<RectTransform>();
        inventoryRect.anchorMin = new Vector2(0.5f, 0f);
        inventoryRect.anchorMax = new Vector2(0.5f, 0f);
        inventoryRect.pivot = new Vector2(0.5f, 0f);
        inventoryRect.anchoredPosition = new Vector2(0f, 18f);

        HorizontalLayoutGroup layout =
            inventoryObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = inventorySpacing.x;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            inventoryObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        inventorySlotBackgrounds = new Image[allWands.Length];
        inventorySlotIcons = new Image[allWands.Length];

        for (int i = 0; i < allWands.Length; i++)
        {
            CreateInventorySlot(inventoryObject.transform, i);
        }
    }

    private void CreateInventorySlot(Transform parent, int index)
    {
        GameObject slotObject = new GameObject("ElementSlot_" + (index + 1));
        slotObject.transform.SetParent(parent, false);

        RectTransform slotRect = slotObject.AddComponent<RectTransform>();
        slotRect.sizeDelta = inventorySlotSize;

        LayoutElement layoutElement = slotObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = inventorySlotSize.x;
        layoutElement.preferredHeight = inventorySlotSize.y;

        Image slotBackground = slotObject.AddComponent<Image>();
        slotBackground.color = new Color(0f, 0f, 0f, 0.45f);
        inventorySlotBackgrounds[index] = slotBackground;

        GameObject iconObject = new GameObject("Icon");
        iconObject.transform.SetParent(slotObject.transform, false);

        RectTransform iconRect = iconObject.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(6f, 6f);
        iconRect.offsetMax = new Vector2(-6f, -6f);

        Image icon = iconObject.AddComponent<Image>();
        icon.preserveAspect = true;
        inventorySlotIcons[index] = icon;
    }

    private void RefreshElementInventoryUI()
    {
        if (!showElementInventory)
        {
            return;
        }

        if (inventorySlotIcons == null)
        {
            BuildElementInventoryUI();
        }

        if (inventorySlotIcons == null)
        {
            return;
        }

        for (int i = 0; i < inventorySlotIcons.Length; i++)
        {
            WandData wand =
                allWands != null && i < allWands.Length
                    ? allWands[i]
                    : null;

            bool isUnlocked = IsWandUnlocked(i);
            bool isActive = i == activeWandIndex;

            inventorySlotIcons[i].sprite =
                wand != null ? wand.elementIcon : null;
            inventorySlotIcons[i].enabled =
                wand != null && wand.elementIcon != null;
            inventorySlotIcons[i].color =
                isUnlocked ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.45f);

            inventorySlotBackgrounds[i].color =
                isActive
                    ? new Color(1f, 1f, 1f, 0.38f)
                    : new Color(0f, 0f, 0f, 0.45f);
        }
    }
}
