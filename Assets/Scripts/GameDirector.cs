using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameDirector : MonoBehaviour
{
    [Serializable]
    public class WaveConfig
    {
        public string waveName = "Wave";
        [Min(0)] public int enemiesToSpawn = 10;
        public GameObject[] enemyPrefabs;
        public ElementWaveConfig[] elementSpawns =
        {
            new ElementWaveConfig { element = ElementType.Wind },
            new ElementWaveConfig { element = ElementType.Water },
            new ElementWaveConfig { element = ElementType.Earth },
            new ElementWaveConfig { element = ElementType.Fire }
        };
    }

    [Serializable]
    public class ElementWaveConfig
    {
        public ElementType element = ElementType.Earth;
        [Min(0)] public int enemiesToSpawn = 0;
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

    [Header("Enemy Size")]
    public float windNormalEnemyScaleMultiplier = 1.45f;
    public float waterNormalEnemyScaleMultiplier = 0.7f;

    [Header("Admiral 2 Support")]
    public GameObject baktinNiAdmiralPrefab;
    public float baktinNiAdmiralScale = 3f;
    public float baktinNiAdmiralPlatformEdgePadding = 0.8f;
    public float baktinNiAdmiralSpawnHeight = 0.8f;

    private Transform playerTransform;
    private int currentStageIndex = 0;
    private int currentWaveIndex = 0;
    private int enemiesSpawnedThisWave = 0;
    private int enemiesDefeatedThisWave = 0;
    private int[] enemiesSpawnedByElementThisWave = new int[0];
    private float nextSpawnTime = 0f;
    private bool waitingForNextWave = false;
    private bool waitingForBossBadgePickup = false;
    private bool bossActiveOrPending = false;
    private bool skipShortcutTriggered = false;
    private bool gameWon = false;
    private string typedSkipInput = string.Empty;
    private GameObject victoryPanel;

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
        HandleSkipLevelShortcut();

        if (gameWon) return;
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

        EnsureElementSpawnCounters(wave);

        int enemyCount = GetWaveEnemyCount(wave);
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
        GameObject enemy = Instantiate(prefab, spawnPosition, Quaternion.identity);
        ApplyNormalEnemySize(enemy);
        return true;
    }

    private void ApplyNormalEnemySize(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
        if (health != null && health.isBoss)
        {
            return;
        }

        ElementType element = movement != null ? movement.enemyElement :
            health != null ? health.enemyElement :
            ElementType.Earth;

        if (element == ElementType.Wind)
        {
            enemy.transform.localScale *= windNormalEnemyScaleMultiplier;
        }
        else if (element == ElementType.Water)
        {
            enemy.transform.localScale *= waterNormalEnemyScaleMultiplier;
        }

        if (movement != null)
        {
            movement.RefreshBaseScale();
        }
    }

    private GameObject GetRandomEnemyPrefab(WaveConfig wave)
    {
        ElementWaveConfig elementConfig = GetRandomElementSpawnConfig(wave);
        if (elementConfig != null)
        {
            GameObject prefab = GetRandomPrefabFromArray(elementConfig.enemyPrefabs);
            if (prefab == null)
            {
                prefab = GetRandomEnemyPrefabForElement(wave.enemyPrefabs, elementConfig.element);
            }

            if (prefab != null)
            {
                MarkElementSpawned(wave, elementConfig);
                return prefab;
            }
        }

        return GetRandomPrefabFromArray(wave != null ? wave.enemyPrefabs : null);
    }

    private GameObject GetRandomPrefabFromArray(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return null;
        }

        int validCount = 0;
        foreach (GameObject prefab in prefabs)
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
        foreach (GameObject prefab in prefabs)
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

    private GameObject GetRandomEnemyPrefabForElement(GameObject[] prefabs, ElementType element)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return null;
        }

        System.Collections.Generic.List<GameObject> matches = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null && GetPrefabElement(prefabs[i]) == element)
            {
                matches.Add(prefabs[i]);
            }
        }

        return matches.Count > 0
            ? matches[UnityEngine.Random.Range(0, matches.Count)]
            : null;
    }

    private ElementWaveConfig GetRandomElementSpawnConfig(WaveConfig wave)
    {
        if (wave == null || !HasElementSpawnControls(wave))
        {
            return null;
        }

        EnsureElementSpawnCounters(wave);

        int remainingTotal = 0;
        for (int i = 0; i < wave.elementSpawns.Length; i++)
        {
            ElementWaveConfig config = wave.elementSpawns[i];
            int remaining = config != null ? Mathf.Max(0, config.enemiesToSpawn) - GetElementSpawnedCount(i) : 0;
            remainingTotal += Mathf.Max(0, remaining);
        }

        if (remainingTotal <= 0)
        {
            return null;
        }

        int roll = UnityEngine.Random.Range(0, remainingTotal);
        for (int i = 0; i < wave.elementSpawns.Length; i++)
        {
            ElementWaveConfig config = wave.elementSpawns[i];
            int remaining = config != null ? Mathf.Max(0, config.enemiesToSpawn) - GetElementSpawnedCount(i) : 0;
            if (remaining <= 0)
            {
                continue;
            }

            if (roll < remaining)
            {
                return config;
            }

            roll -= remaining;
        }

        return null;
    }

    private void MarkElementSpawned(WaveConfig wave, ElementWaveConfig elementConfig)
    {
        if (wave == null || elementConfig == null || wave.elementSpawns == null)
        {
            return;
        }

        EnsureElementSpawnCounters(wave);
        for (int i = 0; i < wave.elementSpawns.Length; i++)
        {
            if (wave.elementSpawns[i] == elementConfig)
            {
                enemiesSpawnedByElementThisWave[i]++;
                return;
            }
        }
    }

    private int GetElementSpawnedCount(int index)
    {
        return enemiesSpawnedByElementThisWave != null &&
               index >= 0 &&
               index < enemiesSpawnedByElementThisWave.Length
            ? enemiesSpawnedByElementThisWave[index]
            : 0;
    }

    private int GetWaveEnemyCount(WaveConfig wave)
    {
        if (wave == null)
        {
            return 0;
        }

        if (!HasElementSpawnControls(wave))
        {
            return Mathf.Max(0, wave.enemiesToSpawn);
        }

        int total = 0;
        for (int i = 0; i < wave.elementSpawns.Length; i++)
        {
            if (wave.elementSpawns[i] != null)
            {
                total += Mathf.Max(0, wave.elementSpawns[i].enemiesToSpawn);
            }
        }

        return total;
    }

    private bool HasElementSpawnControls(WaveConfig wave)
    {
        if (wave == null || wave.elementSpawns == null)
        {
            return false;
        }

        for (int i = 0; i < wave.elementSpawns.Length; i++)
        {
            if (wave.elementSpawns[i] != null && wave.elementSpawns[i].enemiesToSpawn > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureElementSpawnCounters(WaveConfig wave)
    {
        int count = wave != null && wave.elementSpawns != null ? wave.elementSpawns.Length : 0;
        if (enemiesSpawnedByElementThisWave == null ||
            enemiesSpawnedByElementThisWave.Length != count)
        {
            enemiesSpawnedByElementThisWave = new int[count];
        }
    }

    private ElementType GetPrefabElement(GameObject prefab)
    {
        EnemyMovement movement = prefab != null ? prefab.GetComponent<EnemyMovement>() : null;
        if (movement != null)
        {
            return movement.enemyElement;
        }

        EnemyHealth health = prefab != null ? prefab.GetComponent<EnemyHealth>() : null;
        return health != null ? health.enemyElement : ElementType.Earth;
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
            return movement.flyAnywhere ||
                   movement.enemyElement == ElementType.Earth ||
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

        GameObject[] allObjects = FindObjectsByType<GameObject>();
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
        ResetElementSpawnCounters();
        waitingForNextWave = true;
        Invoke(nameof(StartNextWave), timeBetweenWaves);
    }

    private void StartNextWave()
    {
        waitingForNextWave = false;
        enemiesSpawnedThisWave = 0;
        enemiesDefeatedThisWave = 0;
        ResetElementSpawnCounters();
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
        if (bossActiveOrPending)
        {
            return;
        }

        StageConfig stage = GetCurrentStage();
        if (playerTransform == null || stage == null) return;
        if (stage.bossPrefab == null)
        {
            CompleteBossStage();
            return;
        }

        bool isAdmiral2Boss = stage.bossPrefab.name.StartsWith("Admiral2_GroundPound");
        if (isAdmiral2Boss && FindAnyObjectByType<Admiral2Boss>() != null)
        {
            return;
        }

        bossActiveOrPending = true;
        Vector3 bossSpawnPos = playerTransform.position + new Vector3(UnityEngine.Random.Range(-10f, 10f), 8f, 0f);
        if (isAdmiral2Boss)
        {
            bossSpawnPos = GetBottomFloorBossSpawnPosition();
        }

        GameObject boss = Instantiate(stage.bossPrefab, bossSpawnPos, Quaternion.identity);

        if (boss == null)
        {
            CompleteBossStage();
            return;
        }

        EnemyHealth bossHealth = boss.GetComponent<EnemyHealth>();
        if (bossHealth == null)
        {
            bossHealth = boss.AddComponent<EnemyHealth>();
        }

        if (isAdmiral2Boss)
        {
            ConfigureAdmiral2Boss(boss);
            SpawnBaktinNiAdmiralsOnPlatforms();
        }
        else if (bossHealth != null && bossHealth.enemyElement == ElementType.Water)
        {
            ConfigureWaterBoss(boss);
        }

        BossBadgeDropper dropper = boss.GetComponent<BossBadgeDropper>();
        if (dropper == null)
        {
            dropper = boss.AddComponent<BossBadgeDropper>();
        }

        if (dropper.badgeToDrop == null ||
            (bossHealth != null && dropper.badgeToDrop.elementType != bossHealth.enemyElement))
        {
            dropper.badgeToDrop = GetBossBadgeDrop(bossHealth);
        }
    }

    private void ConfigureAdmiral2Boss(GameObject boss)
    {
        boss.tag = "Boss";

        Rigidbody2D bossRb = boss.GetComponent<Rigidbody2D>();
        if (bossRb == null)
        {
            bossRb = boss.AddComponent<Rigidbody2D>();
        }

        bossRb.bodyType = RigidbodyType2D.Dynamic;
        bossRb.gravityScale = 1f;
        bossRb.mass = 1000f;
        bossRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        bossRb.freezeRotation = true;

        CircleCollider2D bossCollider = boss.GetComponent<CircleCollider2D>();
        if (bossCollider == null)
        {
            bossCollider = boss.AddComponent<CircleCollider2D>();
        }

        bossCollider.radius = 0.75f;
        bossCollider.offset = Vector2.zero;

        SpriteRenderer bossRenderer = boss.GetComponent<SpriteRenderer>();
        if (bossRenderer != null)
        {
            bossRenderer.sortingOrder = Mathf.Max(bossRenderer.sortingOrder, 30);
        }

        Animator bossAnimator = boss.GetComponent<Animator>();
        if (bossAnimator == null)
        {
            bossAnimator = boss.AddComponent<Animator>();
        }

        RuntimeAnimatorController bossController = Resources.Load<RuntimeAnimatorController>("Admiral2BossAnimator");
        if (bossController != null)
        {
            bossAnimator.runtimeAnimatorController = bossController;
        }

        boss.transform.localScale = Vector3.one * 5.4f;

        EnemyHealth bossHealth = boss.GetComponent<EnemyHealth>();
        if (bossHealth == null)
        {
            bossHealth = boss.AddComponent<EnemyHealth>();
        }

        bossHealth.isBoss = true;
        bossHealth.health = 400f;
        bossHealth.baseSpeed = 2.5f;
        bossHealth.bossTier = 5;
        bossHealth.enemyElement = ElementType.Wind;
        bossHealth.allowBuiltInMovement = false;
        bossHealth.damageReceivedMultiplier = 0.55f;
        bossHealth.bossHealthBarOffset = new Vector3(0f, 0.60f, 0f);
        bossHealth.bossHealthBarSize = new Vector2(3.2f, 0.24f);

        Admiral2Boss admiral2Boss = boss.GetComponent<Admiral2Boss>();
        if (admiral2Boss == null)
        {
            admiral2Boss = boss.AddComponent<Admiral2Boss>();
        }

        StageConfig stage = GetCurrentStage();
        WaveConfig summonWave = stage != null && stage.waves != null && stage.waves.Length > 0
            ? stage.waves[stage.waves.Length - 1]
            : null;
        if (summonWave != null)
        {
            admiral2Boss.SetLaughSummonPrefabs(GetAllWaveEnemyPrefabs(summonWave));
        }

    }

    private GameObject[] GetAllWaveEnemyPrefabs(WaveConfig wave)
    {
        System.Collections.Generic.List<GameObject> prefabs = new System.Collections.Generic.List<GameObject>();
        AddPrefabs(prefabs, wave != null ? wave.enemyPrefabs : null);

        if (wave != null && wave.elementSpawns != null)
        {
            for (int i = 0; i < wave.elementSpawns.Length; i++)
            {
                AddPrefabs(prefabs, wave.elementSpawns[i] != null ? wave.elementSpawns[i].enemyPrefabs : null);
            }
        }

        return prefabs.ToArray();
    }

    private void AddPrefabs(System.Collections.Generic.List<GameObject> destination, GameObject[] prefabs)
    {
        if (destination == null || prefabs == null)
        {
            return;
        }

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null && !destination.Contains(prefabs[i]))
            {
                destination.Add(prefabs[i]);
            }
        }
    }

    private void ConfigureWaterBoss(GameObject boss)
    {
        if (boss == null)
        {
            return;
        }

        WaterBossDash dash = boss.GetComponent<WaterBossDash>();
        if (dash == null)
        {
            dash = boss.AddComponent<WaterBossDash>();
        }

        Rigidbody2D bossRb = boss.GetComponent<Rigidbody2D>();
        if (bossRb != null)
        {
            bossRb.gravityScale = 0f;
            bossRb.freezeRotation = true;
            bossRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    private void SpawnBaktinNiAdmiralsOnPlatforms()
    {
        GameObject prefab = baktinNiAdmiralPrefab;
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>("BaktinNiAdmiral2_0");
        }

        if (prefab == null)
        {
            Debug.LogWarning("BaktinNiAdmiral2_0 prefab was not found in Resources.");
            return;
        }

        DestroyExistingBaktinNiAdmirals();

        Collider2D[] platforms = GetBaktinPatrolPlatforms();
        if (platforms.Length == 0)
        {
            Debug.LogWarning("No platforms were found for BaktinNiAdmiral patrol spawns.");
            return;
        }

        for (int i = 0; i < platforms.Length; i++)
        {
            Bounds platformBounds = platforms[i].bounds;
            float spawnFromLeft = i % 2 == 0 ? 1f : -1f;
            float spawnX = spawnFromLeft > 0f
                ? platformBounds.min.x + baktinNiAdmiralPlatformEdgePadding
                : platformBounds.max.x - baktinNiAdmiralPlatformEdgePadding;
            Vector3 spawnPosition = new Vector3(
                spawnX,
                platformBounds.max.y + baktinNiAdmiralSpawnHeight,
                0f
            );

            GameObject baktin = Instantiate(prefab, spawnPosition, Quaternion.identity);
            ConfigureBaktinNiAdmiral(baktin, platformBounds, spawnFromLeft);
        }
    }

    private void DestroyExistingBaktinNiAdmirals()
    {
        PlatformPatrolEnemy[] existingBaktins = FindObjectsByType<PlatformPatrolEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < existingBaktins.Length; i++)
        {
            if (existingBaktins[i] != null)
            {
                Destroy(existingBaktins[i].gameObject);
            }
        }
    }

    private Collider2D[] GetBaktinPatrolPlatforms()
    {
        int platformLayer = LayerMask.NameToLayer("Platforms");
        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        System.Collections.Generic.List<Collider2D> platforms = new System.Collections.Generic.List<Collider2D>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null ||
                collider.isTrigger ||
                collider.gameObject.layer != platformLayer ||
                collider.bounds.size.x < 3f ||
                collider.CompareTag("Player") ||
                collider.GetComponentInParent<EnemyHealth>() != null)
            {
                continue;
            }

            platforms.Add(collider);
        }

        return platforms.ToArray();
    }

    private void ConfigureBaktinNiAdmiral(GameObject baktin, Bounds platformBounds, float initialDirection)
    {
        if (baktin == null)
        {
            return;
        }

        baktin.name = "BaktinNiAdmiral";
        baktin.tag = "Enemy";
        baktin.transform.localScale = Vector3.one * baktinNiAdmiralScale;

        Rigidbody2D rb = baktin.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = baktin.AddComponent<Rigidbody2D>();
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1f;
        rb.mass = 1000f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        Collider2D collider = baktin.GetComponent<Collider2D>();
        if (collider == null)
        {
            BoxCollider2D box = baktin.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.6f, 0.4f);
            box.offset = Vector2.zero;
        }

        SpriteRenderer renderer = baktin.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 30);
        }

        EnemyHealth health = baktin.GetComponent<EnemyHealth>();
        if (health == null)
        {
            health = baktin.AddComponent<EnemyHealth>();
        }

        health.health = 80f;
        health.baseSpeed = 2.2f;
        health.enemyElement = ElementType.Wind;
        health.isBoss = false;
        health.allowBuiltInMovement = false;

        PlatformPatrolEnemy patrol = baktin.GetComponent<PlatformPatrolEnemy>();
        if (patrol == null)
        {
            patrol = baktin.AddComponent<PlatformPatrolEnemy>();
        }

        patrol.walkSpeed = 2.2f;
        patrol.contactDamage = 12f;
        patrol.playerKnockbackForce = 10f;
        patrol.playerKnockbackUpwardVelocity = 4f;
        patrol.playerKnockbackDuration = 0.22f;
        patrol.groundLayerMask = LayerMask.GetMask("Platforms", "Default");
        patrol.SetPatrolBounds(
            platformBounds.min.x + baktinNiAdmiralPlatformEdgePadding,
            platformBounds.max.x - baktinNiAdmiralPlatformEdgePadding
        );
        patrol.SetDirection(initialDirection);
    }

    private Vector3 GetBottomFloorBossSpawnPosition()
    {
        Collider2D bottomFloor = null;
        float lowestTop = float.PositiveInfinity;

        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        foreach (Collider2D collider in colliders)
        {
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            GameObject colliderObject = collider.gameObject;
            if (colliderObject.CompareTag("Player") ||
                colliderObject.GetComponent<EnemyHealth>() != null ||
                colliderObject.GetComponentInParent<EnemyHealth>() != null)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            if (bounds.size.x < 4f)
            {
                continue;
            }

            if (bounds.max.y < lowestTop)
            {
                lowestTop = bounds.max.y;
                bottomFloor = collider;
            }
        }

        if (bottomFloor == null)
        {
            return playerTransform.position + new Vector3(0f, 8.5f, 0f);
        }

        Bounds floorBounds = bottomFloor.bounds;
        float spawnX = Mathf.Clamp(playerTransform.position.x, floorBounds.min.x + 1f, floorBounds.max.x - 1f);
        return new Vector3(spawnX, floorBounds.max.y + 2f, 0f);
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

        if (boss.GetComponent<Admiral2Boss>() != null)
        {
            HandleFinalBossDefeat();
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

        WeaponManager weaponManager = FindAnyObjectByType<WeaponManager>();
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

    private void HandleSkipLevelShortcut()
    {
        if (skipShortcutTriggered || gameWon || string.IsNullOrEmpty(Input.inputString))
        {
            return;
        }

        foreach (char typedChar in Input.inputString)
        {
            if (typedChar == '\b')
            {
                if (typedSkipInput.Length > 0)
                {
                    typedSkipInput = typedSkipInput.Substring(0, typedSkipInput.Length - 1);
                }

                continue;
            }

            if (!char.IsLetterOrDigit(typedChar) && !char.IsWhiteSpace(typedChar))
            {
                continue;
            }

            typedSkipInput += char.ToLowerInvariant(typedChar);
            if (typedSkipInput.Length > 10)
            {
                typedSkipInput = typedSkipInput.Substring(typedSkipInput.Length - 10);
            }

            string normalized = NormalizeShortcutInput(typedSkipInput);
            string target = NormalizeShortcutInput("admiral 2");
            if (normalized == target)
            {
                SkipToFinalLevel();
                skipShortcutTriggered = true;
                return;
            }

            if (!target.StartsWith(normalized))
            {
                typedSkipInput = string.Empty;
            }
        }
    }

    private string NormalizeShortcutInput(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        string normalized = string.Empty;
        foreach (char c in input)
        {
            if (!char.IsWhiteSpace(c))
            {
                normalized += char.ToLowerInvariant(c);
            }
        }

        return normalized;
    }

    private void SkipToFinalLevel()
    {
        if (stages == null || stages.Length == 0)
        {
            return;
        }

        int finalStageIndex = stages.Length - 1;
        ClearExistingCombatants();

        currentStageIndex = finalStageIndex;
        currentWaveIndex = 0;
        enemiesSpawnedThisWave = 0;
        enemiesDefeatedThisWave = 0;
        ResetElementSpawnCounters();
        waitingForNextWave = false;
        waitingForBossBadgePickup = false;
        bossActiveOrPending = false;
        gameWon = false;
        victoryPanel = null;
        nextSpawnTime = Time.time;

        StageConfig stage = GetCurrentStage();
        if (stage == null)
        {
            return;
        }

        if (stage.waves == null || stage.waves.Length == 0)
        {
            SpawnBoss();
            return;
        }

        currentWaveIndex = stage.waves.Length;
        SpawnBoss();
    }

    private void ClearExistingCombatants()
    {
        EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>();
        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy == null || enemy.gameObject == null)
            {
                continue;
            }

            if (enemy.gameObject.CompareTag("Player"))
            {
                continue;
            }

            Destroy(enemy.gameObject);
        }
    }

    private void HandleFinalBossDefeat()
    {
        bossActiveOrPending = false;
        waitingForBossBadgePickup = false;
        gameWon = true;
        ClearExistingCombatants();
        ShowVictoryPanel();
        Time.timeScale = 0f;
    }

    private void ShowVictoryPanel()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            return;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("VictoryCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        victoryPanel = new GameObject("VictoryPanel");
        victoryPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = victoryPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image overlay = victoryPanel.AddComponent<Image>();
        overlay.color = new Color(0.06f, 0.22f, 0.12f, 0.82f);

        GameObject titleObject = new GameObject("VictoryText");
        titleObject.transform.SetParent(victoryPanel.transform, false);

        RectTransform titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 45f);
        titleRect.sizeDelta = new Vector2(540f, 130f);

        Text title = titleObject.AddComponent<Text>();
        title.text = "CONGRATULATIONS!\nYou defeated Admiral 2!";
        title.alignment = TextAnchor.MiddleCenter;
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 40;
        title.color = Color.white;

        GameObject buttonObject = new GameObject("PlayAgainButton");
        buttonObject.transform.SetParent(victoryPanel.transform, false);

        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -80f);
        buttonRect.sizeDelta = new Vector2(220f, 56f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.18f, 0.62f, 0.26f, 1f);

        Button playAgainButton = buttonObject.AddComponent<Button>();
        playAgainButton.targetGraphic = buttonImage;
        playAgainButton.onClick.AddListener(PlayAgain);

        GameObject buttonTextObject = new GameObject("Text");
        buttonTextObject.transform.SetParent(buttonObject.transform, false);

        RectTransform buttonTextRect = buttonTextObject.AddComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        Text buttonText = buttonTextObject.AddComponent<Text>();
        buttonText.text = "PLAY AGAIN";
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = 24;
        buttonText.color = Color.white;
    }

    private void PlayAgain()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    private void CompleteBossStage()
    {
        bossActiveOrPending = false;
        waitingForBossBadgePickup = false;
        currentStageIndex++;
        currentWaveIndex = 0;
        enemiesSpawnedThisWave = 0;
        enemiesDefeatedThisWave = 0;
        ResetElementSpawnCounters();
        waitingForNextWave = false;
        nextSpawnTime = Time.time + timeBetweenWaves;
    }

    private void ResetElementSpawnCounters()
    {
        enemiesSpawnedByElementThisWave = new int[0];
    }
}
