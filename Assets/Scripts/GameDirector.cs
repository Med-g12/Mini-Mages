using UnityEngine;

public class GameDirector : MonoBehaviour
{
    public int killCount = 0;
    public int targetKillsForBoss = 75;

    [Header("Prefabs")]
    public GameObject normalEnemyPrefab;
    public GameObject earthEnemyPrefab;
    public GameObject waterEnemyPrefab;
    public GameObject fireEnemyPrefab;
    public GameObject[] bosses;
    public WandData[] bossBadgeDrops;
    private int currentStageIndex = 0;

    [Header("Wave Settings")]
    public int enemiesPerWave = 20;
    public float spawnInterval = 1f;
    public float timeBetweenWaves = 4f;

    [Header("Dynamic Spawn Settings")]
    public float minSpawnDistance = 8f;
    public float maxSpawnDistance = 18f;
    public float minAirSpawnHeightAbovePlayer = 2f;
    public float maxAirSpawnHeightAbovePlayer = 7f;
    public Transform[] floorSpawnPoints;
    private Transform playerTransform;
    private int currentWaveIndex = 0;
    private int enemiesSpawnedThisWave = 0;
    private int enemiesDefeatedThisWave = 0;
    private float nextSpawnTime = 0f;
    private bool waitingForNextWave = false;
    private bool waitingForBossBadgePickup = false;

    private readonly ElementType[] waveOrder =
    {
        ElementType.Earth,
        ElementType.Water,
        ElementType.Earth
    };

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
        CacheFloorSpawnPoints();
        nextSpawnTime = Time.time + 2f;
    }

    void Update()
    {
        if (waitingForBossBadgePickup) return;
        if (killCount >= targetKillsForBoss || GameObject.FindGameObjectWithTag("Boss") != null) return;
        if (playerTransform == null) return;
        if (currentWaveIndex >= waveOrder.Length) return;
        if (waitingForNextWave || enemiesSpawnedThisWave >= enemiesPerWave) return;
        if (CountActiveNormalEnemies() >= enemiesPerWave) return;
        if (Time.time < nextSpawnTime) return;

        if (SpawnEnemyDynamically(waveOrder[currentWaveIndex]))
        {
            enemiesSpawnedThisWave++;
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    bool SpawnEnemyDynamically(ElementType element)
    {
        GameObject prefab = GetEnemyPrefab(element);
        if (prefab == null) return false;

        Vector3 spawnPosition = GetSpawnPosition(element);
        GameObject enemy = Instantiate(prefab, spawnPosition, Quaternion.identity);

        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.enemyElement = element;
        }

        EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
        if (movement != null)
        {
            movement.enemyElement = element;
        }

        return true;
    }

    private GameObject GetEnemyPrefab(ElementType element)
    {
        switch (element)
        {
            case ElementType.Earth:
                return earthEnemyPrefab != null ? earthEnemyPrefab : normalEnemyPrefab;
            case ElementType.Water:
                return waterEnemyPrefab != null ? waterEnemyPrefab : normalEnemyPrefab;
            case ElementType.Fire:
                return fireEnemyPrefab != null ? fireEnemyPrefab : normalEnemyPrefab;
            default:
                return normalEnemyPrefab;
        }
    }

    private Vector3 GetSpawnPosition(ElementType element)
    {
        if (!ShouldSpawnInAir(element) && floorSpawnPoints != null && floorSpawnPoints.Length > 0)
        {
            Transform floor = floorSpawnPoints[Random.Range(0, floorSpawnPoints.Length)];
            Collider2D floorCollider = floor.GetComponent<Collider2D>();
            if (floorCollider != null)
            {
                Bounds bounds = floorCollider.bounds;
                return new Vector3(
                    Random.Range(bounds.min.x + 0.5f, bounds.max.x - 0.5f),
                    bounds.max.y + 0.75f,
                    0f
                );
            }

            return floor.position + Vector3.up * 0.75f;
        }

        float side = Random.value < 0.5f ? -1f : 1f;
        float xOffset = Random.Range(minSpawnDistance, maxSpawnDistance) * side;
        float yOffset = Random.Range(minAirSpawnHeightAbovePlayer, maxAirSpawnHeightAbovePlayer);
        float minY = GetLowestPlatformTopY() + 1.5f;

        Vector3 spawnPosition = playerTransform.position + new Vector3(xOffset, yOffset, 0f);
        if (spawnPosition.y < minY)
        {
            spawnPosition.y = minY;
        }

        return spawnPosition;
    }

    private bool ShouldSpawnInAir(ElementType element)
    {
        return element == ElementType.Earth || element == ElementType.Fire;
    }

    private float GetLowestPlatformTopY()
    {
        float lowestTop = float.PositiveInfinity;

        if (floorSpawnPoints != null)
        {
            foreach (Transform floor in floorSpawnPoints)
            {
                if (floor == null) continue;

                Collider2D floorCollider = floor.GetComponent<Collider2D>();
                float topY = floorCollider != null ? floorCollider.bounds.max.y : floor.position.y;
                if (topY < lowestTop)
                {
                    lowestTop = topY;
                }
            }
        }

        return float.IsPositiveInfinity(lowestTop) ? playerTransform.position.y : lowestTop;
    }

    private int CountActiveNormalEnemies()
    {
        EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        int count = 0;

        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy != null && !enemy.isBoss)
            {
                count++;
            }
        }

        return count;
    }

    private void CacheFloorSpawnPoints()
    {
        if (floorSpawnPoints != null && floorSpawnPoints.Length > 0) return;

        GameObject[] platforms = FindObjectsByLayerName("Platforms");
        floorSpawnPoints = new Transform[platforms.Length];
        for (int i = 0; i < platforms.Length; i++)
        {
            floorSpawnPoints[i] = platforms[i].transform;
        }
    }

    private GameObject[] FindObjectsByLayerName(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) return new GameObject[0];

        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        System.Collections.Generic.List<GameObject> matches = new System.Collections.Generic.List<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj.layer == layer && obj.GetComponent<Collider2D>() != null)
            {
                matches.Add(obj);
            }
        }

        return matches.ToArray();
    }

    public void OnNormalEnemyDefeated()
    {
        killCount++;
        enemiesDefeatedThisWave++;

        if (enemiesDefeatedThisWave >= enemiesPerWave && enemiesSpawnedThisWave >= enemiesPerWave)
        {
            AdvanceWave();
        }

        if (killCount >= targetKillsForBoss && currentStageIndex < bosses.Length)
        {
            SpawnBoss();
        }
    }

    private void AdvanceWave()
    {
        currentWaveIndex++;
        enemiesSpawnedThisWave = 0;
        enemiesDefeatedThisWave = 0;

        if (currentWaveIndex < waveOrder.Length)
        {
            waitingForNextWave = true;
            Invoke(nameof(StartNextWave), timeBetweenWaves);
        }
    }

    private void StartNextWave()
    {
        waitingForNextWave = false;
        nextSpawnTime = Time.time;
    }

    void SpawnBoss()
    {
        if (playerTransform == null) return;
        Vector3 bossSpawnPos = playerTransform.position + new Vector3(Random.Range(-10f, 10f), 5f, 0f);
        GameObject boss = Instantiate(bosses[currentStageIndex], bossSpawnPos, Quaternion.identity);
        BossBadgeDropper dropper = boss.GetComponent<BossBadgeDropper>();
        if (dropper == null)
        {
            dropper = boss.AddComponent<BossBadgeDropper>();
        }

        if (dropper.badgeToDrop == null)
        {
            dropper.badgeToDrop = GetBossBadgeDrop(currentStageIndex, null);
        }
    }

    public void OnBossDefeated(int tier)
    {
        CompleteBossStage();
    }

    public void OnBossDefeated(EnemyHealth boss)
    {
        if (boss == null)
        {
            CompleteBossStage();
            return;
        }

        BossBadgeDropper dropper = boss.GetComponent<BossBadgeDropper>();
        if (dropper == null)
        {
            dropper = boss.gameObject.AddComponent<BossBadgeDropper>();
        }

        if (dropper.badgeToDrop == null)
        {
            dropper.badgeToDrop = GetBossBadgeDrop(currentStageIndex, boss);
        }

        BadgePickup pickup = dropper != null ? dropper.DropBadge(boss.transform.position) : null;

        if (pickup != null)
        {
            waitingForBossBadgePickup = true;
        }
        else
        {
            CompleteBossStage();
        }
    }

    public void OnBossBadgeCollected(WandData badge)
    {
        if (!waitingForBossBadgePickup)
        {
            return;
        }

        CompleteBossStage();
    }

    private WandData GetBossBadgeDrop(int stageIndex, EnemyHealth boss)
    {
        if (bossBadgeDrops != null &&
            stageIndex >= 0 &&
            stageIndex < bossBadgeDrops.Length &&
            bossBadgeDrops[stageIndex] != null)
        {
            return bossBadgeDrops[stageIndex];
        }

        WeaponManager weaponManager = FindFirstObjectByType<WeaponManager>();
        if (weaponManager == null || weaponManager.allWands == null)
        {
            return null;
        }

        if (boss != null)
        {
            foreach (WandData wand in weaponManager.allWands)
            {
                if (wand != null && wand.elementType == boss.enemyElement)
                {
                    return wand;
                }
            }

            if (boss.bossTier >= 0 && boss.bossTier < weaponManager.allWands.Length)
            {
                return weaponManager.allWands[boss.bossTier];
            }
        }

        return null;
    }

    private void CompleteBossStage()
    {
        waitingForBossBadgePickup = false;
        killCount = 0;
        currentStageIndex++;
        currentWaveIndex = 0;
        enemiesSpawnedThisWave = 0;
        enemiesDefeatedThisWave = 0;
        waitingForNextWave = false;
        nextSpawnTime = Time.time + timeBetweenWaves;
    }
}
