using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxRunSpeed = 7.5f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float deceleration = 70f;

    [Header("Air Movement")]
    [SerializeField] private float airAcceleration = 50f;
    [SerializeField] private float airDeceleration = 60f;

    [Header("Inertia / Turn Weight (Heavy Sack Feel)")]
    [SerializeField] private float turnInertia = 0.12f;
    [SerializeField] private float stopThreshold = 0.2f;

    [Header("Jumping")]
    [SerializeField] private float jumpHeight = 4f;
    [SerializeField] private float timeToApex = 0.3f;

    [Header("Jump Assistance")]
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Gravity Multipliers")]
    [SerializeField] private float fallMultiplier = 2f;
    [SerializeField] private float jumpCutMultiplier = 2.5f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.6f, 0.15f);
    [SerializeField] private LayerMask groundMask;

    private Rigidbody2D rb;
    private Vector2 input;
    private bool movementSuppressed;

    private bool isGrounded;
    public bool IsGrounded => isGrounded;

    private Vector2 groundNormal = Vector2.up;

    private bool isJumpCut;
    private float gravityStrength;
    private float jumpVelocity;
    private float coyoteCounter;
    private float jumpBufferCounter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        CalculateJumpVariables();
    }

    private void Update()
    {
        if (!movementSuppressed)
        {
            input.x = Input.GetAxisRaw("Horizontal");

            HandleJumpInput();
        }

        HandleJumpTimers();
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();
        ApplyRunMovement();
        ApplyGravityModifiers();
        TryJump();
    }

    // ------------------------------------------------------------
    //  JUMP CALCULATION
    // ------------------------------------------------------------
    private void CalculateJumpVariables()
    {
        gravityStrength = -(2 * jumpHeight) / (timeToApex * timeToApex);
        jumpVelocity = Mathf.Abs(gravityStrength) * timeToApex;

        rb.gravityScale = gravityStrength / Physics2D.gravity.y;
    }

    private void HandleJumpInput()
    {
        if (Input.GetButtonDown("Jump"))   // SPACEBAR WORKS HERE
            jumpBufferCounter = jumpBufferTime;

        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0)
            isJumpCut = true;
    }

    private void HandleJumpTimers()
    {
        if (isGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter = Mathf.Max(coyoteCounter - Time.deltaTime, 0f);

        jumpBufferCounter = Mathf.Max(jumpBufferCounter - Time.deltaTime, 0f);
    }

    private void TryJump()
    {
        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);

            // Reset
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            isJumpCut = false;
        }
    }

    // ------------------------------------------------------------
    //  SLOPE-AWARE GRAVITY
    // ------------------------------------------------------------
    private void ApplyGravityModifiers()
    {
        float baseGravity = gravityStrength / Physics2D.gravity.y;

        if (isGrounded)
        {
            rb.gravityScale = Mathf.Lerp(rb.gravityScale, baseGravity, Time.deltaTime * 15f);
            return;
        }


        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = baseGravity * fallMultiplier;
        }
        else if (isJumpCut)
        {
            rb.gravityScale = baseGravity * jumpCutMultiplier;
        }
        else
        {
            rb.gravityScale = baseGravity;
        }
    }

    // ------------------------------------------------------------
    //  SLOPE-AWARE GROUNDCHECK
    // ------------------------------------------------------------
    private void UpdateGroundedState()
    {
        Collider2D hit = Physics2D.OverlapBox(
            groundCheckPoint.position,
            groundCheckSize,
            0f,
            groundMask
        );

        if (hit != null)
        {
            isGrounded = true;

            // get ground normal
            RaycastHit2D normalHit = Physics2D.Raycast(
                groundCheckPoint.position + Vector3.up * 0.05f,
                Vector2.down,
                0.3f,
                groundMask
);


            if (normalHit.collider != null)
                groundNormal = normalHit.normal;
            else
                groundNormal = Vector2.up;

            isJumpCut = false;
            coyoteCounter = coyoteTime;
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector2.up;
        }
    }

    // ------------------------------------------------------------
    //  RUN MOVEMENT + SLOPE SUPPORT + INERTIA
    // ------------------------------------------------------------
    private void ApplyRunMovement()
    {
        if (movementSuppressed)
            return;

        float targetSpeed = input.x * maxRunSpeed;
        float currentX = rb.linearVelocity.x;

        bool hasInput = Mathf.Abs(input.x) > 0.01f;
        bool changingDirection =
            hasInput &&
            Mathf.Abs(currentX) > 0.1f &&
            Mathf.Sign(currentX) != Mathf.Sign(input.x);

        // inertia brake
        if (changingDirection)
        {
            currentX = Mathf.MoveTowards(
                currentX,
                0f,
                turnInertia * maxRunSpeed * Time.fixedDeltaTime
            );
        }

        float accel =
            Mathf.Abs(targetSpeed) > 0.01f
                ? (isGrounded ? acceleration : airAcceleration)
                : (isGrounded ? deceleration : airDeceleration);

        float newX = Mathf.MoveTowards(
            currentX,
            targetSpeed,
            accel * Time.fixedDeltaTime
        );

        // snap-to-zero on ground
        if (isGrounded && !hasInput && Mathf.Abs(newX) < stopThreshold)
            newX = Mathf.Lerp(newX, 0f, Time.fixedDeltaTime * 12f);


        // --- SLOPE MOVEMENT ---
        if (isGrounded && groundNormal != Vector2.up)
        {
            Vector2 moveDir = Vector2.Perpendicular(groundNormal).normalized * Mathf.Sign(input.x);
            rb.linearVelocity = new Vector2(moveDir.x * Mathf.Abs(newX), moveDir.y * Mathf.Abs(newX));
        }
        else
        {
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        }
    }

    // ------------------------------------------------------------
    //  DASH CONTROLLER INTEGRATION
    // ------------------------------------------------------------
    public void SetMovementSuppressed(bool suppressed)
    {
        movementSuppressed = suppressed;

        if (suppressed)
        {
            input.x = 0f;
        }
        else
        {
            rb.gravityScale = gravityStrength / Physics2D.gravity.y;
        }
    }

    // ------------------------------------------------------------
    //  DEBUG DRAW
    // ------------------------------------------------------------
    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(groundCheckPoint.position, groundCheckSize);
    }
}
