using System;
using UnityEngine;

public class EnemyDropTable : MonoBehaviour
{
    [Serializable]
    public class DropEntry
    {
        public GameObject prefab;
        [Range(0f, 100f)] public float dropChancePercent = 25f;
        public Vector2 dropOffset = Vector2.zero;
    }

    public DropEntry[] drops;

    public void TryDropItems()
    {
        if (drops == null || drops.Length == 0) return;

        float totalChance = 0f;
        foreach (var drop in drops)
        {
            if (drop != null && drop.prefab != null)
            {
                totalChance += Mathf.Clamp(drop.dropChancePercent, 0f, 100f);
            }
        }

        if (totalChance <= 0f) return;

        float roll = UnityEngine.Random.Range(0f, 100f);
        float cumulative = 0f;
        Vector3 baseDropPosition = GetDropPosition();

        foreach (var drop in drops)
        {
            if (drop == null || drop.prefab == null) continue;
            
            float chance = Mathf.Clamp(drop.dropChancePercent, 0f, 100f);
            cumulative += chance;
            
            if (roll <= cumulative)
            {
                Vector3 dropPosition = baseDropPosition + (Vector3)drop.dropOffset;
                GameObject droppedItem = Instantiate(drop.prefab, dropPosition, Quaternion.identity);
                
                PickupableItem pickup = droppedItem.GetComponent<PickupableItem>();
                if (pickup == null) pickup = droppedItem.GetComponentInChildren<PickupableItem>();
                if (pickup != null) pickup.TryPickupNearbyPlayer();
                
                return; // Exit after dropping exactly ONE item
            }
        }
    }

    private Vector3 GetDropPosition()
    {
        Collider2D enemyCollider = GetComponent<Collider2D>();
        if (enemyCollider == null)
        {
            enemyCollider = GetComponentInChildren<Collider2D>();
        }

        if (enemyCollider != null)
        {
            Vector3 center = enemyCollider.bounds.center;
            center.z = transform.position.z;
            return center;
        }

        Rigidbody2D enemyRigidbody = GetComponent<Rigidbody2D>();
        if (enemyRigidbody != null)
        {
            return enemyRigidbody.position;
        }

        return transform.position;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        drops = new DropEntry[3];
        
        drops[0] = new DropEntry();
        drops[0].prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Health_Potion.prefab");
        drops[0].dropChancePercent = 10f;
        
        drops[1] = new DropEntry();
        drops[1].prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mana_Potion.prefab");
        drops[1].dropChancePercent = 10f;
        
        drops[2] = new DropEntry();
        drops[2].prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buff_Potion.prefab");
        drops[2].dropChancePercent = 2.5f;
    }
#endif
}
