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
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    // Tracks whether we are currently airborne
    private bool isJumping = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        normalGravityScale = rb.gravityScale;

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

        // Ignore player's own layer
        platformLayerMask &= ~(1 << gameObject.layer);
    }

    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // Flip sprite
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

        RaycastHit2D hit = Physics2D.Raycast(
            laserStartPoint,
            Vector2.down,
            rayLength,
            platformLayerMask
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

        RaycastHit2D hit = Physics2D.Raycast(
            laserStartPoint,
            Vector2.down,
            0.5f,
            platformLayerMask
        );

        if (hit.collider != null)
        {
            PlatformEffector2D effector =
                hit.collider.GetComponent<PlatformEffector2D>();

            if (effector != null)
            {
                StartCoroutine(
                    DropThroughPlatform(effector)
                );
            }
        }
    }

    private IEnumerator DropThroughPlatform(
        PlatformEffector2D effector)
    {
        effector.rotationalOffset = 180f;

        yield return new WaitForSeconds(0.4f);

        effector.rotationalOffset = 0f;
    }

    private void UpdateSpriteDirection()
    {
        if (Mathf.Abs(horizontalInput) > 0.01f)
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
