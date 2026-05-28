using UnityEngine;

public class BadgePickup : MonoBehaviour
{
    [SerializeField] private WandData badge;
    private bool collected;

    [Header("Visual Effects")]
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
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite == null && badge != null)
        {
            spriteRenderer.sprite = badge.elementIcon;
        }
    }

    private void Start()
    {
        startPosition = transform.position;

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            GameObject auraObj = new GameObject("Aura");
            auraObj.transform.SetParent(transform, false);
            auraObj.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            
            auraRenderer = auraObj.AddComponent<SpriteRenderer>();
            auraRenderer.sprite = spriteRenderer.sprite;
            auraRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        }
    }

    private void Update()
    {
        if (collected) return;

        float newY = startPosition.y + Mathf.Sin(Time.time * bounceSpeed) * bounceAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        float pulse = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;
        float scale = Mathf.Lerp(minGlowScale, maxGlowScale, pulse);
        transform.localScale = new Vector3(scale, scale, 1f);

        if (spriteRenderer != null)
        {
            float brightness = Mathf.Lerp(0.85f, 1.25f, pulse);
            spriteRenderer.color = new Color(brightness, brightness, brightness, 1f);
        }

        if (auraRenderer != null)
        {
            float auraPulse = (Mathf.Sin(Time.time * glowPulseSpeed * 1.5f) + 1f) * 0.5f;
            float auraScale = Mathf.Lerp(1.0f, maxAuraScale, auraPulse);
            auraRenderer.transform.localScale = new Vector3(auraScale, auraScale, 1f);
            
            float alpha = Mathf.Lerp(0.6f, 0f, auraPulse);
            auraRenderer.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    public void SetBadge(WandData badgeData)
    {
        badge = badgeData;

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
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

        GameDirector director = FindAnyObjectByType<GameDirector>();
        if (director != null)
        {
            director.OnBossBadgeCollected(badge);
        }

        Destroy(gameObject);
    }
}
