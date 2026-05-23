using System;
using UnityEngine;

public class GameDirector : MonoBehaviour
{
    [Serializable]
    public class WaveConfig
    {
        public string waveName = "Wave";
        [Min(0)] public int enemiesToSpawn = 10;
        public GameObject[] enemyPrefabs;
    }

    [Serializable]
    public class StageConfig
    {
        public string stageName = "Stage";
        public WaveConfig[] waves =
        {
            new WaveConfig()
        };
        public GameObject bossPrefab;
        public WandData badgeDrop;
    }

    [Header("Stages")]
    public StageConfig[] stages =
    {
        new StageConfig()
    };

    [Header("Timing")]
    public float spawnInterval = 1f;
    public float timeBetweenWaves = 4f;

    [Header("Spawn Area")]
    public float minSpawnDistance = 8f;
    public float maxSpawnDistance = 18f;
    public float minAirSpawnHeightAbovePlayer = 2f;
    public float maxAirSpawnHeightAbovePlayer = 7f;
    public Transform[] floorSpawnPoints;

    private Transform playerTransform;
    private int currentStageIndex = 0;
    private int currentWaveIndex = 0;
    private int enemiesSpawnedThisWave = 0;
    private int enemiesDefeatedThisWave = 0;
    private float nextSpawnTime = 0f;
    private bool waitingForNextWave = false;
    private bool waitingForBossBadgePickup = false;
    private bool bossActiveOrPending = false;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        CacheFloorSpawnPoints();
        nextSpawnTime = Time.time + 2f;
    }

    private void Update()
    {
        if (waitingForBossBadgePickup || bossActiveOrPending) return;
        if (waitingForNextWave) return;
        if (playerTransform == null) return;

        StageConfig stage = GetCurrentStage();
        if (stage == null) return;
        if (stage.waves == null || stage.waves.Length == 0)
        {
            SpawnBoss();
            return;
        }

        WaveConfig wave = GetCurrentWave();
        if (wave == null) return;

        int enemyCount = Mathf.Max(0, wave.enemiesToSpawn);
        if (enemyCount <= 0)
        {
            CompleteCurrentWave();
            return;
        }

        if (enemiesSpawnedThisWave >= enemyCount) return;
        if (Time.time < nextSpawnTime) return;

        if (SpawnEnemyFromWave(wave))
        {
            enemiesSpawnedThisWave++;
            nextSpawnTime = Time.time + spawnInterval;
        }
        else
        {
            Debug.LogWarning("GameDirector wave has enemies to spawn but no enemy prefab assigned.");
            CompleteCurrentWave();
        }
    }

    private bool SpawnEnemyFromWave(WaveConfig wave)
    {
        GameObject prefab = GetRandomEnemyPrefab(wave);
        if (prefab == null)
        {
            return false;
        }

        Vector3 spawnPosition = GetSpawnPosition(prefab);
        Instantiate(prefab, spawnPosition, Quaternion.identity);
        return true;
    }

    private GameObject GetRandomEnemyPrefab(WaveConfig wave)
    {
        if (wave == null || wave.enemyPrefabs == null || wave.enemyPrefabs.Length == 0)
        {
            return null;
        }

        int validCount = 0;
        foreach (GameObject prefab in wave.enemyPrefabs)
        {
            if (prefab != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int roll = UnityEngine.Random.Range(0, validCount);
        foreach (GameObject prefab in wave.enemyPrefabs)
        {
            if (prefab == null) continue;
            if (roll == 0)
            {
                return prefab;
            }

            roll--;
        }

        return null;
    }

    private Vector3 GetSpawnPosition(GameObject enemyPrefab)
    {
        if (!ShouldSpawnInAir(enemyPrefab) && floorSpawnPoints != null && floorSpawnPoints.Length > 0)
        {
            Transform floor = floorSpawnPoints[UnityEngine.Random.Range(0, floorSpawnPoints.Length)];
            Collider2D floorCollider = floor.GetComponent<Collider2D>();
            if (floorCollider != null)
            {
                Bounds bounds = floorCollider.bounds;
                return new Vector3(
                    UnityEngine.Random.Range(bounds.min.x + 0.5f, bounds.max.x - 0.5f),
                    bounds.max.y + 0.75f,
                    0f
                );
            }

            return floor.position + Vector3.up * 0.75f;
        }

        float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float xOffset = UnityEngine.Random.Range(minSpawnDistance, maxSpawnDistance) * side;
        float yOffset = UnityEngine.Random.Range(minAirSpawnHeightAbovePlayer, maxAirSpawnHeightAbovePlayer);
        float minY = GetLowestPlatformTopY() + 1.5f;

        Vector3 spawnPosition = playerTransform.position + new Vector3(xOffset, yOffset, 0f);
        if (spawnPosition.y < minY)
        {
            spawnPosition.y = minY;
        }

        return spawnPosition;
    }

    private bool ShouldSpawnInAir(GameObject enemyPrefab)
    {
        if (enemyPrefab == null)
        {
            return false;
        }

        EnemyMovement movement = enemyPrefab.GetComponent<EnemyMovement>();
        if (movement != null)
        {
            return movement.enemyElement == ElementType.Earth ||
                   movement.enemyElement == ElementType.Fire;
        }

        EnemyHealth health = enemyPrefab.GetComponent<EnemyHealth>();
        return health != null &&
               (health.enemyElement == ElementType.Earth ||
                health.enemyElement == ElementType.Fire);
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

        return float.IsPositiveInfinity(lowestTop) && playerTransform != null
            ? playerTransform.position.y
            : lowestTop;
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
        enemiesDefeatedThisWave++;

        WaveConfig wave = GetCurrentWave();
        int enemyCount = wave != null ? Mathf.Max(0, wave.enemiesToSpawn) : 0;
        if (enemiesDefeatedThisWave >= enemyCount &&
            enemiesSpawnedThisWave >= enemyCount)
        {
            CompleteCurrentWave();
        }
    }

    private void CompleteCurrentWave()
    {
        StageConfig stage = GetCurrentStage();
        if (stage == null) return;

        currentWaveIndex++;
        if (stage.waves == null || currentWaveIndex >= stage.waves.Length)
        {
            SpawnBoss();
            return;
        }

        enemiesSpawnedThisWave = 0;
        enemiesDefeatedThisWave = 0;
        waitingForNextWave = true;
        Invoke(nameof(StartNextWave), timeBetweenWaves);
    }

    private void StartNextWave()
    {
        waitingForNextWave = false;
        enemiesSpawnedThisWave = 0;
        enemiesDefeatedThisWave = 0;
        nextSpawnTime = Time.time;
    }

    private StageConfig GetCurrentStage()
    {
        if (stages == null ||
            currentStageIndex < 0 ||
            currentStageIndex >= stages.Length)
        {
            return null;
        }

        return stages[currentStageIndex];
    }

    private WaveConfig GetCurrentWave()
    {
        StageConfig stage = GetCurrentStage();
        if (stage == null ||
            stage.waves == null ||
            currentWaveIndex < 0 ||
            currentWaveIndex >= stage.waves.Length)
        {
            return null;
        }

        return stage.waves[currentWaveIndex];
    }

    private void SpawnBoss()
    {
        StageConfig stage = GetCurrentStage();
        if (playerTransform == null || stage == null) return;
        if (stage.bossPrefab == null)
        {
            CompleteBossStage();
            return;
        }

        bossActiveOrPending = true;
        Vector3 bossSpawnPos = playerTransform.position + new Vector3(UnityEngine.Random.Range(-10f, 10f), 5f, 0f);
        GameObject boss = Instantiate(stage.bossPrefab, bossSpawnPos, Quaternion.identity);

        BossBadgeDropper dropper = boss.GetComponent<BossBadgeDropper>();
        if (dropper == null)
        {
            dropper = boss.AddComponent<BossBadgeDropper>();
        }

        if (dropper.badgeToDrop == null ||
            dropper.badgeToDrop.elementType != boss.GetComponent<EnemyHealth>().enemyElement)
        {
            dropper.badgeToDrop = GetBossBadgeDrop(boss.GetComponent<EnemyHealth>());
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

        if (dropper.badgeToDrop == null ||
            dropper.badgeToDrop.elementType != boss.enemyElement)
        {
            dropper.badgeToDrop = GetBossBadgeDrop(boss);
        }

        BadgePickup pickup = dropper.DropBadge(boss.transform.position);
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

    private WandData GetBossBadgeDrop(EnemyHealth boss)
    {
        StageConfig stage = GetCurrentStage();
        if (stage != null &&
            stage.badgeDrop != null &&
            (boss == null || stage.badgeDrop.elementType == boss.enemyElement))
        {
            return stage.badgeDrop;
        }

        WeaponManager weaponManager = FindFirstObjectByType<WeaponManager>();
        if (weaponManager == null || weaponManager.allWands == null || boss == null)
        {
            return null;
        }

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

        return null;
    }

    private void CompleteBossStage()
    {
        bossActiveOrPending = false;
        waitingForBossBadgePickup = false;
        currentStageIndex++;
        currentWaveIndex = 0;
        enemiesSpawnedThisWave = 0;
        enemiesDefeatedThisWave = 0;
        waitingForNextWave = false;
        nextSpawnTime = Time.time + timeBetweenWaves;
    }
}
