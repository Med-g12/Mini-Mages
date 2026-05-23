using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemySpawnData
    {
        public GameObject enemyPrefab;
        [Min(0f)] public float spawnWeight = 1f;
    }

    [Header("Enemy Variety")]
    [SerializeField] private List<EnemySpawnData> enemies = new List<EnemySpawnData>();

    [Header("Spawner Settings")]
    [SerializeField, Min(0.01f)] private float spawnRate = 2f;
    [SerializeField, Min(1)] private int maxEnemiesAllowed = 20;

    [Header("Spawn Area")]
    [SerializeField] private BoxCollider2D spawnArea;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private Coroutine spawnCoroutine;

    private void Awake()
    {
        if (spawnArea == null)
        {
            spawnArea = GetComponent<BoxCollider2D>();
        }
    }

    private void OnEnable()
    {
        spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(spawnRate);

        while (true)
        {
            CleanupDestroyedEnemies();

            if (activeEnemies.Count < maxEnemiesAllowed)
            {
                SpawnEnemy();
            }

            yield return wait;
        }
    }

    private void SpawnEnemy()
    {
        GameObject enemyPrefab = GetRandomEnemyPrefab();
        if (enemyPrefab == null)
        {
            return;
        }

        GameObject enemy = Instantiate(enemyPrefab, GetSpawnPosition(), Quaternion.identity);
        activeEnemies.Add(enemy);
    }

    private GameObject GetRandomEnemyPrefab()
    {
        float totalWeight = 0f;

        foreach (EnemySpawnData enemy in enemies)
        {
            if (enemy.enemyPrefab != null && enemy.spawnWeight > 0f)
            {
                totalWeight += enemy.spawnWeight;
            }
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        // Pick a random value within the total weight. Each enemy owns a slice
        // of that range equal to its weight, so higher weights are more likely.
        float randomWeight = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (EnemySpawnData enemy in enemies)
        {
            if (enemy.enemyPrefab == null || enemy.spawnWeight <= 0f)
            {
                continue;
            }

            currentWeight += enemy.spawnWeight;
            if (randomWeight <= currentWeight)
            {
                return enemy.enemyPrefab;
            }
        }

        return null;
    }

    private Vector3 GetSpawnPosition()
    {
        if (spawnArea == null)
        {
            return transform.position;
        }

        Bounds bounds = spawnArea.bounds;
        float randomX = Random.Range(bounds.min.x, bounds.max.x);
        float randomY = Random.Range(bounds.min.y, bounds.max.y);

        return new Vector3(randomX, randomY, transform.position.z);
    }

    private void CleanupDestroyedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
            {
                activeEnemies.RemoveAt(i);
            }
        }
    }
}
