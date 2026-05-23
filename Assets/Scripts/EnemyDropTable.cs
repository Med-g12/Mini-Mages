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
        if (drops == null || drops.Length == 0)
        {
            return;
        }

        Vector3 baseDropPosition = GetDropPosition();
        foreach (DropEntry drop in drops)
        {
            if (drop == null || drop.prefab == null)
            {
                continue;
            }

            float chance = Mathf.Clamp(drop.dropChancePercent, 0f, 100f);
            if (UnityEngine.Random.value * 100f > chance)
            {
                continue;
            }

            Vector3 dropPosition = baseDropPosition + (Vector3)drop.dropOffset;
            GameObject droppedItem = Instantiate(drop.prefab, dropPosition, Quaternion.identity);
            PickupableItem pickup = droppedItem.GetComponent<PickupableItem>();
            if (pickup == null)
            {
                pickup = droppedItem.GetComponentInChildren<PickupableItem>();
            }

            if (pickup != null)
            {
                pickup.TryPickupNearbyPlayer();
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
}
