using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public Transform wandPivot;
    public Transform firePoint;
    public WandData[] allWands;
    public bool[] unlockedWands = { true, false, false, false };

    private int activeWandIndex = 0;
    private PlayerResources resources;
    private Animator animator;

    void Start()
    {
        resources = GetComponent<PlayerResources>();
        
        // Find the Animator component (on self or children)
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    void Update()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3 lookDir = mousePos - wandPivot.position;
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;
        wandPivot.rotation = Quaternion.Euler(0, 0, angle);

        if (Input.GetKeyDown(KeyCode.Alpha1) && unlockedWands[0]) activeWandIndex = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2) && unlockedWands[1]) activeWandIndex = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3) && unlockedWands[2]) activeWandIndex = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4) && unlockedWands[3]) activeWandIndex = 3;

        WandData activeWand = allWands[activeWandIndex];
        if (activeWand == null) return;

        if (Input.GetMouseButtonDown(0)) FireProjectile(activeWand.basicProjectilePrefab, activeWand.basicManaCost);
        if (Input.GetKeyDown(KeyCode.Q)) FireProjectile(activeWand.qProjectilePrefab, activeWand.qManaCost);
        if (Input.GetKeyDown(KeyCode.E)) FireProjectile(activeWand.eProjectilePrefab, activeWand.eManaCost);
    }

    private void FireProjectile(GameObject prefab, float manaCost)
    {
        if (prefab != null && resources.SpendMana(manaCost))
        {
            Instantiate(prefab, firePoint.position, wandPivot.rotation);
            
            if (animator != null)
            {
                animator.SetTrigger("Fire");
            }
        }
    }

    public void UnlockWand(int index) => unlockedWands[index] = true;
}
