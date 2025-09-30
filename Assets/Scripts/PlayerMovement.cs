using UnityEngine;

namespace Platformer
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float maxRunSpeed = 8f;
        [SerializeField] private float acceleration = 75f;
        [SerializeField] private float deceleration = 90f;
        [SerializeField] private float airAcceleration = 65f;
        [SerializeField] private float airDeceleration = 55f;

        [Header("Jumping")]
        [SerializeField] private float jumpHeight = 4.5f;
        [SerializeField] private float jumpTimeToApex = 0.35f;
        [SerializeField] private float coyoteTime = 0.055f;
        [SerializeField] private float jumpBufferTime = 0.12f;
        [SerializeField] private float fallGravityMultiplier = 1.8f;
        [SerializeField] private float shortHopGravityMultiplier = 2.2f;

        [Header("Air Options")]
        [SerializeField] private bool enableDoubleJump = true;
        [Min(0)]
        [SerializeField] private int maxAirJumps = 1;

        [Header("Crouch Charge Jump")]
        [SerializeField] private bool enableCrouchCharge = true;
        [SerializeField] private float chargeTime = 0.35f;
        [SerializeField] private float minJumpMultiplier = 1f;
        [SerializeField] private float maxJumpMultiplier = 1.6f;

        [Header("Wall Interaction")]
        [SerializeField] private bool enableWallSlide = false;
        [SerializeField] private float wallSlideSpeed = 3f;
        [SerializeField] private float wallCheckDistance = 0.08f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
        [SerializeField] private LayerMask groundLayers = 1;

        [Header("Optional Settings")]
        [Tooltip("If true gravity will be scaled by fallGravityMultiplier while falling.")]
        [SerializeField] private bool useFallGravityMultiplier = true;

        private const float GroundCheckDistance = 0.05f;

        private Rigidbody2D body;
        private CapsuleCollider2D capsule;
        private Vector2 currentVelocity;

        private float baseGravityScale;
        private float gravityStrength;
        private float jumpVelocity;

        private float inputX;
        private float coyoteCounter;
        private float jumpBufferCounter;
        private bool isJumpCut;
        private bool movementSuppressed;
        private bool isCrouching;
        private bool isWallSliding;
        private bool isTouchingWall;
        private int wallDirection;
        private float crouchChargeTimer;
        private int airJumpsRemaining;

        public bool IsGrounded { get; private set; }
        public Vector2 Velocity => body.linearVelocity;
        public bool MovementSuppressed => movementSuppressed;
        public bool IsCrouching => isCrouching;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            capsule = GetComponent<CapsuleCollider2D>();
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            baseGravityScale = body.gravityScale;
            CalculateGravity();
            ResetAirJumpCount();
        }

        private void CalculateGravity()
        {
            gravityStrength = -(2f * jumpHeight) / (jumpTimeToApex * jumpTimeToApex);
            jumpVelocity = Mathf.Abs(gravityStrength) * jumpTimeToApex;
            body.gravityScale = gravityStrength / Physics2D.gravity.y;
            baseGravityScale = body.gravityScale;
        }

        private void Update()
        {
            inputX = movementSuppressed ? 0f : Input.GetAxisRaw("Horizontal");
            currentVelocity = body.linearVelocity;

            UpdateCrouchState();
            UpdateTimers();
            if (!movementSuppressed)
            {
                HandleJumpInput();
            }
        }

        private void FixedUpdate()
        {
            RefreshContactStates();

            if (movementSuppressed)
            {
                return;
            }

            ApplyHorizontalMovement();
            HandleWallSlide();
            ApplyGravityModifiers();
        }

        private void UpdateTimers()
        {
            if (IsGrounded)
            {
                coyoteCounter = coyoteTime;
            }
            else
            {
                coyoteCounter = Mathf.Max(coyoteCounter - Time.deltaTime, 0f);
            }

            jumpBufferCounter = Mathf.Max(jumpBufferCounter - Time.deltaTime, 0f);
        }

        private void HandleJumpInput()
        {
            bool jumpPressed = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W);
            bool jumpReleased = Input.GetButtonUp("Jump") || Input.GetKeyUp(KeyCode.W);

            if (jumpPressed)
            {
                jumpBufferCounter = jumpBufferTime;
            }

            if (TryConsumeJump(out bool groundedJump, out float jumpMultiplier))
            {
                PerformJump(groundedJump, jumpMultiplier);
            }

            if (jumpReleased)
            {
                if (currentVelocity.y > 0f)
                {
                    isJumpCut = true;
                }
            }
        }

        private void ApplyHorizontalMovement()
        {
            if (movementSuppressed)
            {
                return;
            }

            float targetSpeed = inputX * maxRunSpeed;
            if (isCrouching)
            {
                targetSpeed = 0f;
            }
            float speedDifference = targetSpeed - body.linearVelocity.x;

            float accelRate = Mathf.Abs(targetSpeed) > 0.01f
                ? (IsGrounded ? acceleration : airAcceleration)
                : (IsGrounded ? deceleration : airDeceleration);

            float movement = accelRate * speedDifference * Time.fixedDeltaTime;
            float newVelocityX = body.linearVelocity.x + movement;

            if (IsGrounded && Mathf.Abs(inputX) < 0.01f && Mathf.Abs(body.linearVelocity.x) < 0.2f)
            {
                newVelocityX = 0f;
            }

            body.linearVelocity = new Vector2(newVelocityX, body.linearVelocity.y);
        }

        /// <summary>
        /// Consumes a buffered jump if we are within the coyote window or have air jumps remaining.
        /// Returns true when a jump should be performed and outputs whether it counts as a grounded jump
        /// plus the multiplier that should be applied to the jump velocity.
        /// </summary>
        private bool TryConsumeJump(out bool groundedJump, out float jumpMultiplier)
        {
            groundedJump = false;
            jumpMultiplier = 1f;

            if (jumpBufferCounter <= 0f)
            {
                return false;
            }

            if (coyoteCounter > 0f)
            {
                groundedJump = true;
                jumpMultiplier = isCrouching ? GetChargedJumpMultiplier() : 1f;
                return true;
            }

            if (enableDoubleJump && airJumpsRemaining > 0)
            {
                airJumpsRemaining--;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Applies the jump velocity and resets any state tied to the start of a jump.
        /// </summary>
        private void PerformJump(bool groundedJump, float jumpMultiplier)
        {
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            isJumpCut = false;

            float appliedJumpVelocity = jumpVelocity * jumpMultiplier;
            body.linearVelocity = new Vector2(body.linearVelocity.x, appliedJumpVelocity);

            if (groundedJump)
            {
                ResetAirJumpCount();
                if (enableCrouchCharge)
                {
                    crouchChargeTimer = 0f;
                }
            }

            isCrouching = false;
        }

        private void ApplyGravityModifiers()
        {
            if (movementSuppressed)
            {
                return;
            }

            if (!useFallGravityMultiplier)
            {
                body.gravityScale = baseGravityScale;
                return;
            }

            if (isWallSliding)
            {
                body.gravityScale = baseGravityScale;
                return;
            }

            if (body.linearVelocity.y < 0f)
            {
                body.gravityScale = baseGravityScale * fallGravityMultiplier;
            }
            else if (isJumpCut)
            {
                body.gravityScale = baseGravityScale * shortHopGravityMultiplier;
            }
            else
            {
                body.gravityScale = baseGravityScale;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null)
            {
                return;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }

        public void SetMovementSuppressed(bool suppressed)
        {
            movementSuppressed = suppressed;

            if (suppressed)
            {
                inputX = 0f;
                isCrouching = false;
                crouchChargeTimer = 0f;
            }
            else
            {
                body.gravityScale = baseGravityScale;
            }
        }

        /// <summary>
        /// Polls the environment for ground and wall contact information so the movement state stays in sync with physics.
        /// </summary>
        private void RefreshContactStates()
        {
            UpdateGroundState();
            UpdateWallState();
        }

        /// <summary>
        /// Uses a downward box cast below the ground check to confirm grounded state and refresh the air jump counter.
        /// </summary>
        private void UpdateGroundState()
        {
            if (groundCheck == null)
            {
                return;
            }

            RaycastHit2D hit = Physics2D.BoxCast(groundCheck.position, groundCheckSize, 0f, Vector2.down, GroundCheckDistance, groundLayers);
            IsGrounded = hit.collider != null && Vector2.Angle(hit.normal, Vector2.up) <= 60f;

            if (IsGrounded)
            {
                isJumpCut = false;
                ResetAirJumpCount();
            }
        }

        /// <summary>
        /// Checks for walls directly beside the capsule so we can decide if wall sliding should activate.
        /// </summary>
        private void UpdateWallState()
        {
            isTouchingWall = false;
            wallDirection = 0;

            if (capsule == null || IsGrounded)
            {
                return;
            }

            Bounds bounds = capsule.bounds;
            Vector2 origin = bounds.center;
            Vector2 size = new Vector2(bounds.size.x * 0.9f, bounds.size.y * 0.9f);

            RaycastHit2D leftHit = Physics2D.BoxCast(origin, size, 0f, Vector2.left, wallCheckDistance, groundLayers);
            if (leftHit.collider != null && leftHit.normal.x > 0.1f)
            {
                isTouchingWall = true;
                wallDirection = -1;
                return;
            }

            RaycastHit2D rightHit = Physics2D.BoxCast(origin, size, 0f, Vector2.right, wallCheckDistance, groundLayers);
            if (rightHit.collider != null && rightHit.normal.x < -0.1f)
            {
                isTouchingWall = true;
                wallDirection = 1;
            }
        }

        /// <summary>
        /// Applies a gentle downward clamp when wall sliding is enabled and the player pushes into a wall while falling.
        /// </summary>
        private void HandleWallSlide()
        {
            isWallSliding = false;

            if (!enableWallSlide || IsGrounded || !isTouchingWall)
            {
                return;
            }

            if (Mathf.Abs(inputX) < 0.01f || Mathf.RoundToInt(Mathf.Sign(inputX)) != wallDirection)
            {
                return;
            }

            if (body.linearVelocity.y < 0f)
            {
                isWallSliding = true;
                float clampedY = Mathf.Max(body.linearVelocity.y, -Mathf.Abs(wallSlideSpeed));
                body.linearVelocity = new Vector2(body.linearVelocity.x, clampedY);
            }
        }

        /// <summary>
        /// Tracks whether the crouch input is held and builds jump charge while grounded.
        /// </summary>
        private void UpdateCrouchState()
        {
            bool crouchInputHeld = !movementSuppressed && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow));
            bool canCrouch = IsGrounded && crouchInputHeld;

            isCrouching = canCrouch;

            if (!enableCrouchCharge)
            {
                crouchChargeTimer = 0f;
                return;
            }

            if (isCrouching)
            {
                crouchChargeTimer = Mathf.Min(crouchChargeTimer + Time.deltaTime, chargeTime);
            }
            else
            {
                crouchChargeTimer = Mathf.MoveTowards(crouchChargeTimer, 0f, Time.deltaTime * 2f);
            }
        }

        /// <summary>
        /// Converts the current crouch charge timer into a multiplier that scales the base jump velocity.
        /// </summary>
        private float GetChargedJumpMultiplier()
        {
            if (!enableCrouchCharge)
            {
                return 1f;
            }

            if (chargeTime <= Mathf.Epsilon)
            {
                return maxJumpMultiplier;
            }

            float t = Mathf.Clamp01(crouchChargeTimer / chargeTime);
            return Mathf.Lerp(minJumpMultiplier, maxJumpMultiplier, t);
        }

        /// <summary>
        /// Resets the pool of air jumps to match the inspector configuration.
        /// </summary>
        private void ResetAirJumpCount()
        {
            airJumpsRemaining = enableDoubleJump ? maxAirJumps : 0;
        }

        private void OnValidate()
        {
            jumpTimeToApex = Mathf.Max(0.01f, jumpTimeToApex);
            maxAirJumps = Mathf.Max(0, maxAirJumps);
            chargeTime = Mathf.Max(0.01f, chargeTime);
            minJumpMultiplier = Mathf.Max(0f, minJumpMultiplier);
            maxJumpMultiplier = Mathf.Max(minJumpMultiplier, maxJumpMultiplier);
            wallSlideSpeed = Mathf.Max(0.1f, wallSlideSpeed);
            wallCheckDistance = Mathf.Max(0.01f, wallCheckDistance);

            if (!Application.isPlaying)
            {
                body = GetComponent<Rigidbody2D>();
                capsule = GetComponent<CapsuleCollider2D>();
            }

            if (body != null)
            {
                CalculateGravity();
            }

            ResetAirJumpCount();
        }
    }
}
