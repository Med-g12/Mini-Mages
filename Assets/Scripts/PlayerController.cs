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
    private float groundedCooldown = 0.15f; // Prevent ground detection immediately after jumping

    [Header("Grounded Check")]
    [Tooltip("The layers that count as solid ground/platforms")]
    public LayerMask platformLayerMask;

    [Header("Better Physics / Fast Falling")]
    public float fallMultiplier = 4f;      // Heavy falling gravity scale
    public float lowJumpMultiplier = 3f;  // Tap lightly vs hold jump control
    private float normalGravityScale;

    private Rigidbody2D rb;
    private float horizontalInput;
    private Collider2D playerCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    // Tracks whether we are currently in a jump (airborne) – prevents premature ground resets.
    private bool isJumping = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        normalGravityScale = rb.gravityScale; // Save original gravity setting

        // Find the SpriteRenderer component (on self or children)
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // Find the Animator component (on self or children)
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // If the user didn't assign a mask, fall back to *all* layers – this guarantees we can detect any ground.
        // We also make sure the player's own layer is excluded so the ray never hits itself.
        if (platformLayerMask.value == 0)
        {
            platformLayerMask = Physics2D.AllLayers;
        }
        platformLayerMask &= ~(1 << gameObject.layer);
    }

    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // 1. Flip sprite based on horizontal movement (A/D)
        UpdateSpriteDirection();

        // 2. Check if we are touching a platform floor
        CheckGroundedWithLaser();

        // 3. Listen for Jump Inputs cleanly (W only)
        if (Input.GetKeyDown(KeyCode.W))
        {
            if (isGrounded || jumpCount < maxJumps)
            {
                ExecuteInstantJump();
            }
        }

        // 4. Apply heavy platformer fast‑fall gravity
        ApplyBetterGravity();

        // 5. Drop‑through platforms logic
        if (Input.GetKeyDown(KeyCode.S))
        {
            CheckAndDrop();
        }

        // 6. Update animation parameters
        UpdateAnimations();
    }

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
    }

    private void CheckGroundedWithLaser()
    {
        // Skip the check for a short window right after a jump – prevents the ray from hitting the ground too early.
        if (Time.time - lastJumpTime < groundedCooldown)
        {
            isGrounded = false;
            return;
        }

        // If we are still moving upward, ignore ground detection – this avoids the laser hitting the ceiling
        // (or the platform we just left) while the player is still rising.
        if (rb.linearVelocity.y > 0f)
        {
            isGrounded = false;
            return;
        }

        // Start the ray a tiny bit inside the collider so we never hit our own body.
        Vector2 laserStartPoint = new Vector2(
            transform.position.x,
            playerCollider.bounds.min.y + 0.01f);

        // Use a longer ray (0.7 units) to reliably reach the floor even if the player is tall or the platform is thin.
        float rayLength = 0.7f;

        RaycastHit2D hit = Physics2D.Raycast(laserStartPoint, Vector2.down, rayLength, platformLayerMask);
        Debug.DrawRay(laserStartPoint, Vector2.down * rayLength,
            hit.collider != null ? Color.green : Color.red);

        // Only consider the player grounded if we actually hit something **below** a small threshold.
        // This prevents the ray from instantly hitting the platform we just left when we are still very close to it.
        if (hit.collider != null && hit.distance > 0.05f)
        {
            isGrounded = true;
            jumpCount = 0; // reset jumps as soon as we touch something solid
            // We've landed – stop the jumping flag.
            isJumping = false;
        }
        else
        {
            isGrounded = false;
        }
    }

    private void ExecuteInstantJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        float appliedForce = (jumpCount == 0) ? jumpForce : doubleJumpForce;
        rb.AddForce(Vector2.up * appliedForce, ForceMode2D.Impulse);

        jumpCount++;
        isGrounded = false;
        lastJumpTime = Time.time;
        // Mark that we are now airborne – prevents early ground resets.
        isJumping = true;
    }

    private void ApplyBetterGravity()
    {
        // If falling down, apply a massive gravity pull for snappy heavy landing
        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = fallMultiplier;
        }
        // If rising but let go of the jump key, cut the jump short
        else if (rb.linearVelocity.y > 0 && !Input.GetKey(KeyCode.W))
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
        Vector2 laserStartPoint = new Vector2(transform.position.x, playerCollider.bounds.min.y + 0.1f);
        RaycastHit2D hit = Physics2D.Raycast(laserStartPoint, Vector2.down, 0.5f, platformLayerMask);

        if (hit.collider != null)
        {
            PlatformEffector2D effector = hit.collider.GetComponent<PlatformEffector2D>();
            if (effector != null)
            {
                StartCoroutine(DropThroughPlatform(effector));
            }
        }
    }

    private IEnumerator DropThroughPlatform(PlatformEffector2D effector)
    {
        effector.rotationalOffset = 180f;
        yield return new WaitForSeconds(0.4f);
        effector.rotationalOffset = 0f;
    }

    // UpdateFacingDirection method removed – using movement‑based flip instead.
    // Insert this method anywhere inside PlayerController (after UpdateFacingDirection is fine)
private void UpdateSpriteDirection()
{
    // Only flip when the player is actually moving; otherwise keep the previous orientation
    if (Mathf.Abs(horizontalInput) > 0.01f)
    {
        // When moving right (positive input) we want the sprite to face right → flipX = false
        // When moving left (negative input) we want the sprite to face left → flipX = true
        bool shouldFlip = horizontalInput < 0f;
        if (spriteRenderer != null)
            spriteRenderer.flipX = shouldFlip;
    }
}
    private void UpdateAnimations()
    {
        if (animator == null) return;

        // Horizontal speed for idle/run blending
        animator.SetFloat("Speed", Mathf.Abs(horizontalInput));

        // Grounded flag for landing logic
        animator.SetBool("IsGrounded", isGrounded);

        // Vertical velocity – useful for jump/fall conditions
        animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);

        // IsFalling flag – only set if the Animator defines it.
        foreach (var p in animator.parameters)
        {
            if (p.name == "IsFalling" && p.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool("IsFalling", rb.linearVelocity.y < -0.05f);
                break;
            }
        }

    }
}
