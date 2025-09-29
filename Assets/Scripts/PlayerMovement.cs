using UnityEngine;

namespace Platformer
{
    [RequireComponent(typeof(Rigidbody2D))]
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
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.12f;
        [SerializeField] private float fallGravityMultiplier = 1.8f;
        [SerializeField] private float shortHopGravityMultiplier = 2.2f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
        [SerializeField] private LayerMask groundLayers = 1;

        [Header("Optional Settings")]
        [Tooltip("If true gravity will be scaled by fallGravityMultiplier while falling.")]
        [SerializeField] private bool useFallGravityMultiplier = true;

        private Rigidbody2D body;
        private Vector2 currentVelocity;

        private float baseGravityScale;
        private float gravityStrength;
        private float jumpVelocity;

        private float inputX;
        private float coyoteCounter;
        private float jumpBufferCounter;
        private bool isJumpCut;
        private bool wasGroundedLastFrame;

        public bool IsGrounded { get; private set; }
        public Vector2 Velocity => body.velocity;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            baseGravityScale = body.gravityScale;
            CalculateGravity();
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
            inputX = Input.GetAxisRaw("Horizontal");
            currentVelocity = body.velocity;

            UpdateTimers();
            HandleJumpInput();
        }

        private void FixedUpdate()
        {
            ApplyHorizontalMovement();
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
                coyoteCounter -= Time.deltaTime;
            }

            jumpBufferCounter -= Time.deltaTime;
        }

        private void HandleJumpInput()
        {
            if (Input.GetButtonDown("Jump"))
            {
                jumpBufferCounter = jumpBufferTime;
            }

            if (CanJump())
            {
                PerformJump();
            }

            if (Input.GetButtonUp("Jump"))
            {
                if (currentVelocity.y > 0f)
                {
                    isJumpCut = true;
                }
            }
        }

        private void ApplyHorizontalMovement()
        {
            float targetSpeed = inputX * maxRunSpeed;
            float speedDifference = targetSpeed - body.velocity.x;

            float accelRate = Mathf.Abs(targetSpeed) > 0.01f
                ? (IsGrounded ? acceleration : airAcceleration)
                : (IsGrounded ? deceleration : airDeceleration);

            float movement = accelRate * speedDifference * Time.fixedDeltaTime;
            float newVelocityX = body.velocity.x + movement;

            if (IsGrounded && Mathf.Abs(inputX) < 0.01f && Mathf.Abs(body.velocity.x) < 0.2f)
            {
                newVelocityX = 0f;
            }

            body.velocity = new Vector2(newVelocityX, body.velocity.y);
        }

        private bool CanJump()
        {
            return jumpBufferCounter > 0f && coyoteCounter > 0f;
        }

        private void PerformJump()
        {
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            isJumpCut = false;

            body.velocity = new Vector2(body.velocity.x, jumpVelocity);
        }

        private void ApplyGravityModifiers()
        {
            if (!useFallGravityMultiplier)
            {
                body.gravityScale = baseGravityScale;
                return;
            }

            if (body.velocity.y < 0f)
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

        private void LateUpdate()
        {
            CheckGroundedState();
        }

        private void CheckGroundedState()
        {
            Collider2D collider = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayers);
            wasGroundedLastFrame = IsGrounded;
            IsGrounded = collider != null;

            if (IsGrounded && !wasGroundedLastFrame)
            {
                isJumpCut = false;
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
    }
}
