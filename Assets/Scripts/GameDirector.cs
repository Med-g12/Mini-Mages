using UnityEngine;

public class GameDirector : MonoBehaviour
{
    public int killCount = 0;
    public int targetKillsForBoss = 30;

    [Header("Prefabs")]
    public GameObject normalEnemyPrefab;
    public GameObject[] bosses;
    private int currentStageIndex = 0;

    [Header("Dynamic Spawn Settings")]
    public float minSpawnDistance = 8f;
    public float maxSpawnDistance = 18f;
    private Transform playerTransform;

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
        InvokeRepeating(nameof(SpawnEnemyDynamically), 2f, 3.5f);
    }

    void SpawnEnemyDynamically()
    {
        if (killCount >= targetKillsForBoss || GameObject.FindGameObjectWithTag("Boss") != null) return;
        if (playerTransform == null) return;

        Vector2 randomDir = Random.insideUnitCircle.normalized;
        float randomDist = Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 spawnPosition = playerTransform.position + (Vector3)(randomDir * randomDist);

        Instantiate(normalEnemyPrefab, spawnPosition, Quaternion.identity);
    }

    public void OnNormalEnemyDefeated()
    {
        killCount++;
        if (killCount >= targetKillsForBoss && currentStageIndex < bosses.Length)
        {
            SpawnBoss();
        }
    }

    void SpawnBoss()
    {
        if (playerTransform == null) return;
        Vector3 bossSpawnPos = playerTransform.position + new Vector3(Random.Range(-10f, 10f), 5f, 0f);
        Instantiate(bosses[currentStageIndex], bossSpawnPos, Quaternion.identity);
    }

    public void OnBossDefeated(int tier)
    {
        WeaponManager wm = FindFirstObjectByType<WeaponManager>();
        if (wm != null) wm.UnlockWand(tier);
        killCount = 0;
        currentStageIndex++;
    }
}
