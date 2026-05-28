using UnityEngine;

public class PickupableItem : MonoBehaviour
{
    [Header("Instant Gain")]
    [Min(0f)] public float healthToAdd = 0f;
    [Min(0f)] public float manaToAdd = 0f;

    [Header("Buff")]
    public bool isBuff = false;
    [Min(0f)] public float speedToAdd = 0f;
    [Min(0f)] public float damageToAdd = 0f;
    [Min(0f)] public float buffDuration = 5f;

    [Header("Pickup")]
    [Min(0.05f)] public float pickupRadius = 0.75f;
    public bool destroyOnPickup = true;

    private bool pickedUp;
    private Collider2D pickupCollider;

    [Header("Visual Effects")]
    public bool enableVisualEffects = true;
    public float bounceAmplitude = 0.15f;
    public float bounceSpeed = 3f;
    public float glowPulseSpeed = 4f;
    public float minGlowScale = 0.9f;
    public float maxGlowScale = 1.15f;
    public float maxAuraScale = 1.65f;

    private Vector3 startPosition;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer auraRenderer;

    private void Reset()
    {
        EnsurePickupPhysics();
    }

    private void Awake()
    {
        EnsurePickupPhysics();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Start()
    {
        TryPickupNearbyPlayer();

        startPosition = transform.position;

        if (enableVisualEffects && spriteRenderer != null && spriteRenderer.sprite != null)
        {
            GameObject auraObj = new GameObject("Aura");
            auraObj.transform.SetParent(spriteRenderer.transform, false);
            auraObj.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            
            auraRenderer = auraObj.AddComponent<SpriteRenderer>();
            auraRenderer.sprite = spriteRenderer.sprite;
            auraRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        }
    }

    private void Update()
    {
        TryPickupNearbyPlayer();

        if (!pickedUp && enableVisualEffects)
        {
            float newY = startPosition.y + Mathf.Sin(Time.time * bounceSpeed) * bounceAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            float pulse = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;
            float scale = Mathf.Lerp(minGlowScale, maxGlowScale, pulse);
            
            if (spriteRenderer != null)
            {
                spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                float brightness = Mathf.Lerp(0.85f, 1.25f, pulse);
                spriteRenderer.color = new Color(brightness, brightness, brightness, 1f);
            }
            else
            {
                transform.localScale = new Vector3(scale, scale, 1f);
            }

            if (auraRenderer != null)
            {
                float auraPulse = (Mathf.Sin(Time.time * glowPulseSpeed * 1.5f) + 1f) * 0.5f;
                // Since aura is a child of spriteRenderer, we might need to adjust relative scale. 
                // But spriteRenderer is scaling. We'll just scale auraRenderer locally.
                float auraScale = Mathf.Lerp(1.0f, maxAuraScale, auraPulse) / scale; 
                auraRenderer.transform.localScale = new Vector3(auraScale, auraScale, 1f);
                
                float alpha = Mathf.Lerp(0.6f, 0f, auraPulse);
                auraRenderer.color = new Color(1f, 1f, 1f, alpha);
            }
        }
    }

    private void EnsurePickupPhysics()
    {
        pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider == null)
        {
            pickupCollider = gameObject.AddComponent<CircleCollider2D>();
        }

        pickupCollider.isTrigger = true;

        Rigidbody2D pickupRigidbody = GetComponent<Rigidbody2D>();
        if (pickupRigidbody == null)
        {
            pickupRigidbody = gameObject.AddComponent<Rigidbody2D>();
        }

        pickupRigidbody.bodyType = RigidbodyType2D.Kinematic;
        pickupRigidbody.gravityScale = 0f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryPickup(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryPickup(other);
    }

    public void TryPickupNearbyPlayer()
    {
        if (pickedUp)
        {
            return;
        }

        Collider2D[] results = Physics2D.OverlapCircleAll(GetPickupCenter(), pickupRadius);
        for (int i = 0; i < results.Length; i++)
        {
            TryPickup(results[i]);
            if (pickedUp)
            {
                return;
            }
        }
    }

    private Vector2 GetPickupCenter()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            return spriteRenderer.bounds.center;
        }

        if (pickupCollider != null)
        {
            return pickupCollider.bounds.center;
        }

        return transform.position;
    }

    private void TryPickup(Collider2D other)
    {
        if (pickedUp)
        {
            return;
        }

        PlayerResources resources = other.GetComponent<PlayerResources>();
        if (resources == null)
        {
            resources = other.GetComponentInParent<PlayerResources>();
        }

        PlayerController controller = other.GetComponent<PlayerController>();
        if (controller == null)
        {
            controller = other.GetComponentInParent<PlayerController>();
        }

        WeaponManager weaponManager = other.GetComponent<WeaponManager>();
        if (weaponManager == null)
        {
            weaponManager = other.GetComponentInParent<WeaponManager>();
        }

        if (resources == null && controller == null && weaponManager == null)
        {
            return;
        }

        if (!ApplyEffect(resources, controller, weaponManager))
        {
            return;
        }

        pickedUp = true;

        if (destroyOnPickup)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private bool ApplyEffect(
        PlayerResources resources,
        PlayerController controller,
        WeaponManager weaponManager)
    {
        bool applied = false;

        if (resources != null && healthToAdd > 0f)
        {
            resources.AddHealth(healthToAdd);
            applied = true;
        }

        if (resources != null && manaToAdd > 0f)
        {
            resources.AddMana(manaToAdd);
            applied = true;
        }

        if (isBuff)
        {
            if (controller != null && speedToAdd > 0f)
            {
                Sprite icon = spriteRenderer != null ? spriteRenderer.sprite : null;
                controller.AddSpeedBuff(speedToAdd, buffDuration, icon);
                applied = true;
            }

            if (weaponManager != null && damageToAdd > 0f)
            {
                weaponManager.AddDamageBuff(damageToAdd, buffDuration);
                applied = true;
            }
        }

        return applied;
    }
}
