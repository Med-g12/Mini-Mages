using UnityEngine;

public class BadgePickup : MonoBehaviour
{
    [SerializeField] private WandData badge;
    private bool collected;

    private void Reset()
    {
        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        if (circleCollider == null)
        {
            circleCollider = gameObject.AddComponent<CircleCollider2D>();
        }

        circleCollider.isTrigger = true;

        Rigidbody2D rigidbody = GetComponent<Rigidbody2D>();
        if (rigidbody == null)
        {
            rigidbody = gameObject.AddComponent<Rigidbody2D>();
        }

        rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.gravityScale = 0f;
    }

    private void Awake()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite == null && badge != null)
        {
            spriteRenderer.sprite = badge.elementIcon;
        }
    }

    public void SetBadge(WandData badgeData)
    {
        badge = badgeData;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && badge != null)
        {
            spriteRenderer.sprite = badge.elementIcon;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
        {
            return;
        }

        WeaponManager weaponManager = other.GetComponent<WeaponManager>();
        if (weaponManager == null)
        {
            weaponManager = other.GetComponentInParent<WeaponManager>();
        }

        if (weaponManager == null || badge == null)
        {
            return;
        }

        collected = true;
        weaponManager.UnlockWand(badge);

        GameDirector director = FindFirstObjectByType<GameDirector>();
        if (director != null)
        {
            director.OnBossBadgeCollected(badge);
        }

        Destroy(gameObject);
    }
}
