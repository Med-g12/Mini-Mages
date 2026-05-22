using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;

    [Header("Jumping Mechanics")]
    public float jumpForce = 12f;
    public float doubleJumpForce = 10f;

    [Tooltip("Total number of jumps allowed (e.g., 2 for double jump)")]
    public int maxJumps = 2;

    private int jumpCount = 0;
    private bool isGrounded;
    private float lastJumpTime = 0f;
    private float groundedCooldown = 0.15f;

    [Header("Grounded Check")]
    [Tooltip("The layers that count as solid ground/platforms")]
    public LayerMask platformLayerMask;

    [Header("Better Physics / Fast Falling")]
    public float fallMultiplier = 4f;
    public float lowJumpMultiplier = 3f;

    private float normalGravityScale;

    private Rigidbody2D rb;
    private float horizontalInput;
    private Collider2D playerCollider;
    private Collider2D[] playerColliders;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Camera mainCamera;

    // Tracks whether we are currently airborne
    private bool isJumping = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        playerColliders = GetComponentsInChildren<Collider2D>();

        normalGravityScale = rb.gravityScale;

        // Cache the main camera for mouse position mapping
        mainCamera = Camera.main;

        // SpriteRenderer
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // Animator
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // Fallback ground layers
        if (platformLayerMask.value == 0)
        {
            platformLayerMask = Physics2D.AllLayers;
        }

        // Self-colliders are filtered out per raycast so floors can share the
        // player's layer while you are still setting up project layers.
    }

    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // Handle weapon firing and mouse aiming mechanics
        HandleFireInput();

        // Flip sprite based on movement (only runs if not forcing a face direction from firing)
        UpdateSpriteDirection();

        // Ground detection
        CheckGroundedWithLaser();

        // Jump input
        if (Input.GetKeyDown(KeyCode.W))
        {
            if (isGrounded || jumpCount < maxJumps)
            {
                ExecuteInstantJump();
            }
        }

        // Better gravity
        ApplyBetterGravity();

        // Drop-through platforms
        if (Input.GetKeyDown(KeyCode.S))
        {
            CheckAndDrop();
        }

        // Animations
        UpdateAnimations();
    }

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(
            horizontalInput * moveSpeed,
            rb.linearVelocity.y
        );
    }

    private void HandleFireInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // 1. Trigger the animator's Fire parameter
            if (animator != null)
            {
                animator.SetTrigger("Fire");
            }

            // 2. Map mouse position to world coordinates to orient the character flip state
            if (mainCamera != null && spriteRenderer != null)
            {
                Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);

                // If the mouse is to the left of the player's center point, flip left
                if (mousePosition.x < transform.position.x)
                {
                    spriteRenderer.flipX = true;
                }
                // If the mouse is to the right of the player's center point, look right
                else
                {
                    spriteRenderer.flipX = false;
                }
            }
        }
    }

    private void CheckGroundedWithLaser()
    {
        // Prevent instant re-grounding after jumping
        if (Time.time - lastJumpTime < groundedCooldown)
        {
            isGrounded = false;
            return;
        }

        // Ignore while moving upward
        if (rb.linearVelocity.y > 0.1f)
        {
            isGrounded = false;
            return;
        }

        Vector2 laserStartPoint = new Vector2(
            transform.position.x,
            playerCollider.bounds.min.y + 0.02f
        );

        float rayLength = 0.2f;

        RaycastHit2D hit = GetGroundHit(
            laserStartPoint,
            rayLength
        );

        Debug.DrawRay(
            laserStartPoint,
            Vector2.down * rayLength,
            hit.collider != null ? Color.green : Color.red
        );

        if (hit.collider != null)
        {
            isGrounded = true;
            jumpCount = 0;
            isJumping = false;
        }
        else
        {
            isGrounded = false;
        }
    }

    private void ExecuteInstantJump()
    {
        // Reset vertical velocity before jumping
        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            0f
        );

        float appliedForce =
            (jumpCount == 0) ? jumpForce : doubleJumpForce;

        rb.AddForce(
            Vector2.up * appliedForce,
            ForceMode2D.Impulse
        );

        jumpCount++;

        isGrounded = false;
        isJumping = true;
        lastJumpTime = Time.time;
    }

    private void ApplyBetterGravity()
    {
        // Falling
        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = fallMultiplier;
        }
        // Rising but jump released
        else if (rb.linearVelocity.y > 0 &&
                 !Input.GetKey(KeyCode.W))
        {
            rb.gravityScale = lowJumpMultiplier;
        }
        else
        {
            rb.gravityScale = normalGravityScale;
        }
    }

    private void CheckAndDrop()
    {
        Vector2 laserStartPoint = new Vector2(
            transform.position.x,
            playerCollider.bounds.min.y + 0.1f
        );

        RaycastHit2D hit = GetDropThroughPlatformHit(
            laserStartPoint,
            1f
        );

        if (hit.collider != null)
        {
            StartCoroutine(
                DropThroughPlatform(hit.collider)
            );
        }
    }

    private IEnumerator DropThroughPlatform(
        Collider2D platformCollider)
    {
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Physics2D.IgnoreCollision(
                playerColliders[i],
                platformCollider,
                true
            );
        }

        isGrounded = false;
        lastJumpTime = Time.time;

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            -5f
        );

        float timeout = 1f;
        float timer = 0f;

        while (timer < timeout &&
               playerCollider.bounds.max.y >
               platformCollider.bounds.min.y)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Physics2D.IgnoreCollision(
                playerColliders[i],
                platformCollider,
                false
            );
        }
    }

    private RaycastHit2D GetDropThroughPlatformHit(
        Vector2 startPoint,
        float rayLength)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            startPoint,
            Vector2.down,
            rayLength,
            platformLayerMask
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;

            if (hitCollider == null ||
                hitCollider.isTrigger ||
                IsPlayerCollider(hitCollider))
            {
                continue;
            }

            if (hitCollider.GetComponent<PlatformEffector2D>() != null ||
                hitCollider.GetComponentInParent<PlatformEffector2D>() != null)
            {
                return hits[i];
            }
        }

        return new RaycastHit2D();
    }

    private RaycastHit2D GetGroundHit(
        Vector2 startPoint,
        float rayLength)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            startPoint,
            Vector2.down,
            rayLength,
            platformLayerMask
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;

            if (hitCollider != null &&
                !IsPlayerCollider(hitCollider))
            {
                return hits[i];
            }
        }

        return new RaycastHit2D();
    }

    private bool IsPlayerCollider(Collider2D colliderToCheck)
    {
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (colliderToCheck == playerColliders[i])
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateSpriteDirection()
    {
        // Only flip based on movement vector if player isn't actively forcing a directional look via clicking
        if (!Input.GetMouseButton(0) && Mathf.Abs(horizontalInput) > 0.01f)
        {
            bool shouldFlip = horizontalInput < 0f;

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = shouldFlip;
            }
        }
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        // Run speed
        animator.SetFloat(
            "Speed",
            Mathf.Abs(horizontalInput)
        );

        // Grounded state
        animator.SetBool(
            "IsGrounded",
            isGrounded
        );

        // Vertical movement
        animator.SetFloat(
            "VerticalVelocity",
            rb.linearVelocity.y
        );

        // Falling bool (optional)
        foreach (var p in animator.parameters)
        {
            if (p.name == "IsFalling" &&
                p.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(
                    "IsFalling",
                    rb.linearVelocity.y < -0.05f
                );

                break;
            }
        }
    }
}
