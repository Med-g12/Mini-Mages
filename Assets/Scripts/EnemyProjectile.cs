using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public float speed = 10f;
    public float damage = 15f;
    public float lifetime = 30f;
    public bool destroyOnHit = true;

    private Rigidbody2D rb;

    void Start()
    {
        if (GetComponent<EnemyHealth>() != null)
        {
            Debug.LogWarning("EnemyProjectile should only be attached to projectiles, not the boss itself! Removing component.");
            Destroy(this);
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        Destroy(gameObject, lifetime);
    }

    public void Launch(Vector2 direction)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = direction.normalized * speed;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerResources player = other.GetComponent<PlayerResources>();
            if (player == null) player = other.GetComponentInParent<PlayerResources>();
            
            if (player != null)
            {
                player.TakeDamage(damage);
                if (destroyOnHit)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
