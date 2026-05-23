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

    private void Reset()
    {
        EnsurePickupPhysics();
    }

    private void Awake()
    {
        EnsurePickupPhysics();
    }

    private void Start()
    {
        TryPickupNearbyPlayer();
    }

    private void Update()
    {
        TryPickupNearbyPlayer();
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
                controller.AddSpeedBuff(speedToAdd, buffDuration);
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
