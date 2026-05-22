using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public float health = 30f;
    public float baseSpeed = 2.5f;
    public bool isBoss = false;
    public int bossTier = 1;
    public ElementType enemyElement = ElementType.Earth;

    private float currentSpeed;
    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private Collider2D enemyCollider;
    private EnemyMovement enemyMovement;
    private GameObject currentPlatform;

    void Start()
    {
        currentSpeed = baseSpeed;
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        enemyCollider = GetComponent<Collider2D>();
        enemyMovement = GetComponent<EnemyMovement>();
    }

    void Update()
    {
        if (enemyMovement != null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        Vector3 direction = (player.transform.position - transform.position).normalized;
        rb.linearVelocity = new Vector2(direction.x * currentSpeed, rb.linearVelocity.y);

        float yDifference = player.transform.position.y - transform.position.y;

        if (yDifference < -1.5f && currentPlatform != null)
        {
            StartCoroutine(EnemyDropThroughPlatform());
        }

        if (yDifference > 2f && Mathf.Abs(rb.linearVelocity.y) < 0.01f && Random.Range(0, 100) < 2)
        {
            rb.AddForce(Vector2.up * 8f, ForceMode2D.Impulse);
        }
    }

    private IEnumerator EnemyDropThroughPlatform()
    {
        if (currentPlatform != null)
        {
            Collider2D platformCollider = currentPlatform.GetComponent<Collider2D>();
            if (platformCollider != null)
            {
                Physics2D.IgnoreCollision(enemyCollider, platformCollider, true);
                yield return new WaitForSeconds(0.5f);
                Physics2D.IgnoreCollision(enemyCollider, platformCollider, false);
            }
        }
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0) Die();
    }

    public void ApplySlow(float multiplier, float duration) => StartCoroutine(SlowRoutine(multiplier, duration));
    private IEnumerator SlowRoutine(float mult, float dur)
    {
        currentSpeed = baseSpeed * mult; sr.color = Color.blue;
        yield return new WaitForSeconds(dur);
        currentSpeed = baseSpeed; sr.color = Color.white;
    }

    public void ApplyBurn(float damagePerTick, float duration) => StartCoroutine(BurnRoutine(damagePerTick, duration));
    private IEnumerator BurnRoutine(float dmg, float dur)
    {
        float elapsed = 0; sr.color = Color.red;
        while (elapsed < dur) { TakeDamage(dmg); elapsed += 0.5f; yield return new WaitForSeconds(0.5f); }
        sr.color = Color.white;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Platforms")) currentPlatform = collision.gameObject;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Platforms")) currentPlatform = null;
    }

    void Die()
    {
        GameDirector director = FindFirstObjectByType<GameDirector>();
        if (director != null)
        {
            if (isBoss) director.OnBossDefeated(bossTier);
            else director.OnNormalEnemyDefeated();
        }
        Destroy(gameObject);
    }
}
