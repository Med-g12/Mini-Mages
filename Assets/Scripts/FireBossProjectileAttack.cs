using UnityEngine;
using System.Collections;

public class FireBossProjectileAttack : MonoBehaviour
{
    public float attackInterval = 4f;
    public int projectileCount = 6;
    public float projectileSpeed = 8f;
    public float projectileDamage = 15f;
    public GameObject projectilePrefab;
    
    private float nextAttackTime;
    private Transform player;
    private Sprite fireballSprite;
    
    void Start()
    {
        fireballSprite = CreateFireballSprite(64);
        
        if (projectilePrefab == null)
        {
            WandData[] wands = Resources.FindObjectsOfTypeAll<WandData>();
            foreach (WandData wand in wands)
            {
                if (wand.elementType == ElementType.Fire && wand.basicProjectilePrefab != null)
                {
                    projectilePrefab = wand.basicProjectilePrefab;
                    break;
                }
            }
        }
        
        nextAttackTime = Time.time + attackInterval + Random.Range(1f, 2.5f);
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }
    
    void Update()
    {
        if (Time.time >= nextAttackTime)
        {
            StartCoroutine(PerformAttack());
            nextAttackTime = Time.time + attackInterval;
        }
    }
    
    IEnumerator PerformAttack()
    {
        if (player == null) yield break;
        
        Vector3 targetPos = player.position;
        
        for (int i = 0; i < projectileCount; i++)
        {
            Vector3 spawnPos = GetEdgeSpawnPosition();
            GameObject proj = CreateProjectile();
            proj.transform.position = spawnPos;
            
            // Aim at the player's position at the time of summoning
            Vector2 dir = (targetPos - spawnPos).normalized;
            
            // Point the projectile in the direction of travel
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            proj.transform.rotation = Quaternion.Euler(0, 0, angle);
            
            EnemyProjectile ep = proj.GetComponent<EnemyProjectile>();
            ep.speed = projectileSpeed;
            ep.damage = projectileDamage;
            ep.Launch(dir);
            
            yield return new WaitForSeconds(0.15f); // Stagger summons
        }
    }
    
    Vector3 GetEdgeSpawnPosition()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            float height = cam.orthographicSize;
            float width = height * cam.aspect;
            
            int edge = Random.Range(0, 4);
            Vector3 camPos = cam.transform.position;
            float x = 0, y = 0;
            switch (edge)
            {
                case 0: // Top
                    x = Random.Range(-width, width);
                    y = height + 1f;
                    break;
                case 1: // Bottom
                    x = Random.Range(-width, width);
                    y = -height - 1f;
                    break;
                case 2: // Left
                    x = -width - 1f;
                    y = Random.Range(-height, height);
                    break;
                case 3: // Right
                    x = width + 1f;
                    y = Random.Range(-height, height);
                    break;
            }
            return new Vector3(camPos.x + x, camPos.y + y, 0);
        }
        
        return transform.position + (Vector3)Random.insideUnitCircle.normalized * 15f;
    }
    
    GameObject CreateProjectile()
    {
        if (projectilePrefab != null)
        {
            GameObject proj = Instantiate(projectilePrefab);
            
            // Remove the player's Projectile script so it doesn't behave like a player attack
            Projectile playerProj = proj.GetComponent<Projectile>();
            if (playerProj != null)
            {
                playerProj.enabled = false;
                Destroy(playerProj);
            }
            
            // Add the EnemyProjectile script
            EnemyProjectile ep = proj.GetComponent<EnemyProjectile>();
            if (ep == null) ep = proj.AddComponent<EnemyProjectile>();
            
            ep.speed = projectileSpeed;
            ep.damage = projectileDamage;
            ep.lifetime = 30f;
            
            // Ensure sorting order is high enough to be seen
            SpriteRenderer sr = proj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 25;
            
            return proj;
        }
        else
        {
            GameObject proj = new GameObject("FireBossProjectile");
            SpriteRenderer sr = proj.AddComponent<SpriteRenderer>();
            sr.sprite = fireballSprite;
            sr.sortingOrder = 25;
            
            CircleCollider2D col = proj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.2f;
            
            Rigidbody2D rb = proj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Dynamic;
            
            EnemyProjectile ep = proj.AddComponent<EnemyProjectile>();
            ep.speed = projectileSpeed;
            ep.damage = projectileDamage;
            ep.lifetime = 30f;
            
            return proj;
        }
    }
    
    Sprite CreateFireballSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        Color centerColor = Color.yellow;
        Color edgeColor = Color.red;
        
        float center = size / 2f;
        float radius = size / 2f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= radius)
                {
                    float t = dist / radius;
                    Color color = Color.Lerp(centerColor, edgeColor, t);
                    color.a = 1f - Mathf.Pow(t, 2f); // Soft outer fade
                    tex.SetPixel(x, y, color);
                }
                else
                {
                    tex.SetPixel(x, y, clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
