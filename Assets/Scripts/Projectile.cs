using UnityEngine;

public enum ElementType { Wind, Water, Earth, Fire }

public class Projectile : MonoBehaviour
{
    public ElementType element;
    public float speed = 12f;
    public float damage = 10f;
    public float lifetime = 3f;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.linearVelocity = transform.right * speed;
        Destroy(gameObject, lifetime);
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
