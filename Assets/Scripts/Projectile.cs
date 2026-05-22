using UnityEngine;

public enum ElementType { Wind, Water, Earth, Fire }

public class Projectile : MonoBehaviour
{
    public ElementType element;
    public float speed = 12f;
    public float damage = 10f;
    public float lifetime = 3f;
    public float spinSpeed = 720f;
    public bool isHeldByPlayer;

    [Header("Trail")]
    public bool showTrail = true;
    public float trailTime = 0.18f;
    public float trailStartWidth = 0.16f;
    public float trailEndWidth = 0.02f;
    public Color trailStartColor = new Color(0.85f, 1f, 1f, 0.75f);
    public Color trailEndColor = new Color(0.85f, 1f, 1f, 0f);

    private Rigidbody2D rb;
    private static Material trailMaterial;

    void Awake()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sortingOrder < 10)
        {
            spriteRenderer.sortingOrder = 10;
        }

    }

    void Start()
    {
        SetupTrail();

        rb = GetComponent<Rigidbody2D>();
        if (rb != null && !isHeldByPlayer)
        {
            rb.linearVelocity = transform.right * speed;
        }

        if (!isHeldByPlayer)
        {
            Destroy(gameObject, lifetime);
        }
    }

    void Update()
    {
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }

    public void HoldInPlace()
    {
        isHeldByPlayer = true;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    public void Launch(Vector2 direction)
    {
        isHeldByPlayer = false;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb != null)
        {
            rb.linearVelocity = direction.normalized * speed;
        }

        Destroy(gameObject, lifetime);
    }

    private void SetupTrail()
    {
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
        }

        Color elementColor = GetElementTrailColor();
        trail.material = GetTrailMaterial();

        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = trailEndWidth;
        trail.startColor = new Color(
            elementColor.r,
            elementColor.g,
            elementColor.b,
            trailStartColor.a
        );
        trail.endColor = new Color(
            elementColor.r,
            elementColor.g,
            elementColor.b,
            trailEndColor.a
        );
        trail.sortingOrder = 9;
        trail.autodestruct = false;
        trail.emitting = showTrail;
    }

    private Color GetElementTrailColor()
    {
        switch (element)
        {
            case ElementType.Water:
                return new Color(0.25f, 0.65f, 1f);
            case ElementType.Earth:
                return new Color(0.45f, 0.9f, 0.35f);
            case ElementType.Fire:
                return new Color(1f, 0.35f, 0.12f);
            default:
                return new Color(0.82f, 0.84f, 0.86f);
        }
    }

    private static Material GetTrailMaterial()
    {
        if (trailMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            trailMaterial = new Material(shader);
        }

        return trailMaterial;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy") || other.CompareTag("Boss"))
        {
            EnemyHealth enemy = other.GetComponent<EnemyHealth>();
            if (enemy != null)
            {
                if (element == ElementType.Wind) enemy.TakeDamage(damage);
                if (element == ElementType.Water) enemy.ApplySlow(0.4f, 3f);
                if (element == ElementType.Earth) enemy.TakeDamage(damage * 2.5f);
                if (element == ElementType.Fire) enemy.ApplyBurn(damage * 0.3f, 2f);
            }
            Destroy(gameObject);
        }
    }
}
