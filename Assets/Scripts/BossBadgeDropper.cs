using UnityEngine;

public class BossBadgeDropper : MonoBehaviour
{
    public WandData badgeToDrop;
    public float pickupRadius = 0.6f;
    public Vector2 dropOffset = new Vector2(0f, 0.75f);

    public BadgePickup DropBadge(Vector3 position)
    {
        if (badgeToDrop == null)
        {
            return null;
        }

        GameObject pickupObject = new GameObject(badgeToDrop.wandName + " Badge Pickup");
        pickupObject.transform.position = position + (Vector3)dropOffset;

        SpriteRenderer spriteRenderer = pickupObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = badgeToDrop.elementIcon;
        spriteRenderer.sortingOrder = 20;

        CircleCollider2D collider = pickupObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = pickupRadius;

        Rigidbody2D rigidbody = pickupObject.AddComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.gravityScale = 0f;

        BadgePickup pickup = pickupObject.AddComponent<BadgePickup>();
        pickup.SetBadge(badgeToDrop);

        return pickup;
    }
}
