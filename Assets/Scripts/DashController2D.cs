using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Platformer
{
    /// <summary>
    /// DashController2D
    /// -----------------
    /// 1. Add this component to the same GameObject as the Rigidbody2D/PlayerMovement.
    /// 2. Assign the optional ParticleSystem prefab (DashTrail) and SpriteRenderer references in the Inspector.
    /// 3. Tune dash speed, duration, cooldown, and air-dash counts from the serialized fields below.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class DashController2D : MonoBehaviour
    {
        [Header("Dash Settings")]
        [SerializeField] private float dashSpeed = 18f;
        [SerializeField, Min(0f)] private float dashTime = 0.18f;
        [SerializeField, Min(0f)] private float dashCooldown = 0.25f;
        [SerializeField, Min(0)] private int maxAirDashes = 1;
        [SerializeField, Min(0f)] private float coyoteTime = 0.1f;
        [SerializeField, Min(0f)] private float endLagDuration = 0.05f;

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundMask = 1;
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);
        [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -1f);

        [Header("Visuals")]
        [SerializeField] private ParticleSystem dashTrail;
        [SerializeField, Tooltip("Sprite used for afterimages. Defaults to the player's SpriteRenderer.")]
        private SpriteRenderer playerSprite;
        [SerializeField, Min(0.005f)] private float afterimageSpawnInterval = 0.02f;
        [SerializeField, Min(0.01f)] private float afterimageLifetime = 0.15f;
        [SerializeField, Range(0f, 1f)] private float afterimageAlpha = 0.45f;
        [SerializeField, Tooltip("How many particles to emit when the dash starts.")]
        private int dashTrailBurstAmount = 16;

        [Header("Integration")]
        [SerializeField] private PlayerMovement cachedPlayerMovement;

        public event Action OnDash;

        private Rigidbody2D rb;
        private PlayerMovement playerMovement;

        private Vector2 dashDirection = Vector2.right;
        private Vector2 lastAimDirection = Vector2.right;

        private float dashTimer;
        private float dashCooldownTimer;
        private float coyoteTimer;
        private float endLagTimer;
        private float afterimageTimer;
        private float originalGravityScale;

        private int dashesRemaining;

        private bool isGrounded;
        private bool wasGrounded;
        private bool isDashing;
        private bool isInEndLag;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            playerMovement = cachedPlayerMovement != null ? cachedPlayerMovement : GetComponent<PlayerMovement>();
            if (playerSprite == null)
            {
                playerSprite = GetComponent<SpriteRenderer>();
            }

            PrepareDashTrailInstance();

            dashesRemaining = Mathf.Max(0, maxAirDashes);
            lastAimDirection = Vector2.right;
        }

        private void PrepareDashTrailInstance()
        {
            if (dashTrail == null)
            {
                return;
            }

            if (dashTrail.gameObject.scene.rootCount == 0)
            {
                string originalName = dashTrail.name;
                dashTrail = Instantiate(dashTrail, transform.position, Quaternion.identity);
                dashTrail.name = originalName;
            }

            dashTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = dashTrail.main;
            main.stopAction = ParticleSystemStopAction.None;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.playOnAwake = false;

            var emission = dashTrail.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            var trails = dashTrail.trails;
            trails.enabled = true;
            trails.lifetime = 0.2f;
            trails.dieWithParticles = true;
            trails.ratio = 1f;
            trails.worldSpace = true;
            trails.inheritParticleColor = true;

            var colorOverLifetime = dashTrail.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.3f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            UpdateGroundedState(deltaTime);
            UpdateCooldowns(deltaTime);

            Vector2 input = GetDirectionalInput();
            if (input.sqrMagnitude > 0.01f)
            {
                lastAimDirection = SnapToEightDirections(input.normalized);
            }
            else if (rb.linearVelocity.sqrMagnitude > 0.01f)
            {
                lastAimDirection = SnapToEightDirections(rb.linearVelocity.normalized);
            }

            if (!isDashing && !isInEndLag && GetDashPressed())
            {
                TryStartDash(input);
            }

            if (isDashing)
            {
                UpdateDash(deltaTime);
            }
            else if (isInEndLag)
            {
                UpdateEndLag(deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (!isDashing)
            {
                return;
            }

            rb.linearVelocity = dashDirection * dashSpeed;
            if (dashTrail != null)
            {
                dashTrail.transform.position = transform.position;
            }
        }

        private void OnDisable()
        {
            if (isDashing)
            {
                EndDash();
            }

            if (dashTrail != null)
            {
                dashTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (playerMovement != null)
            {
                playerMovement.SetMovementSuppressed(false);
            }

            isInEndLag = false;
        }

        private void UpdateGroundedState(float deltaTime)
        {
            wasGrounded = isGrounded;
            Vector2 origin = (Vector2)transform.position + groundCheckOffset;
            RaycastHit2D hit = Physics2D.BoxCast(origin, groundCheckSize, 0f, Vector2.down, 0f, groundMask);
            isGrounded = hit.collider != null;

            if (isGrounded)
            {
                coyoteTimer = coyoteTime;
                if (!wasGrounded)
                {
                    dashesRemaining = Mathf.Max(0, maxAirDashes);
                }
            }
            else
            {
                coyoteTimer = Mathf.Max(0f, coyoteTimer - deltaTime);
            }
        }

        private void UpdateCooldowns(float deltaTime)
        {
            if (dashCooldownTimer > 0f)
            {
                dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - deltaTime);
            }
        }

        private void TryStartDash(Vector2 input)
        {
            if (!CanDash())
            {
                return;
            }

            Vector2 desiredDirection = DetermineDashDirection(input);
            BeginDash(desiredDirection);
        }

        private bool CanDash()
        {
            if (dashCooldownTimer > 0f)
            {
                return false;
            }

            if (isGrounded || coyoteTimer > 0f)
            {
                return true;
            }

            return dashesRemaining > 0;
        }

        private Vector2 DetermineDashDirection(Vector2 input)
        {
            Vector2 snappedInput = SnapToEightDirections(input);

            if (snappedInput.sqrMagnitude < 0.01f)
            {
                snappedInput = lastAimDirection.sqrMagnitude > 0.01f
                    ? lastAimDirection
                    : Vector2.right;
            }

            lastAimDirection = snappedInput;
            return snappedInput;
        }

        private static Vector2 SnapToEightDirections(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            direction.Normalize();
            float angle = Mathf.Atan2(direction.y, direction.x);
            float step = Mathf.PI / 4f;
            int index = Mathf.RoundToInt(angle / step);
            float snappedAngle = index * step;
            return new Vector2(Mathf.Cos(snappedAngle), Mathf.Sin(snappedAngle)).normalized;
        }

        private void BeginDash(Vector2 direction)
        {
            dashDirection = direction;
            isDashing = true;
            isInEndLag = false;
            dashTimer = dashTime;
            dashCooldownTimer = dashCooldown;
            afterimageTimer = 0f;

            originalGravityScale = rb.gravityScale;
            rb.gravityScale = 0f;
            rb.linearVelocity = direction * dashSpeed;

            if (!isGrounded && coyoteTimer <= 0f && dashesRemaining > 0)
            {
                dashesRemaining--;
            }

            if (playerMovement != null)
            {
                playerMovement.SetMovementSuppressed(true);
            }

            PlayDashTrail();
            OnDash?.Invoke();
            // If using Cinemachine ImpulseSource, you can trigger it here for a camera shake.
        }

        private void UpdateDash(float deltaTime)
        {
            dashTimer -= deltaTime;
            if (dashTimer <= 0f)
            {
                EndDash();
                return;
            }

            afterimageTimer -= deltaTime;
            if (afterimageTimer <= 0f)
            {
                SpawnAfterimage();
                afterimageTimer = afterimageSpawnInterval;
            }
        }

        private void EndDash()
        {
            if (!isDashing)
            {
                return;
            }

            isDashing = false;
            rb.gravityScale = originalGravityScale;

            if (dashTrail != null)
            {
                dashTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (endLagDuration > 0f)
            {
                isInEndLag = true;
                endLagTimer = endLagDuration;
            }
            else if (playerMovement != null)
            {
                playerMovement.SetMovementSuppressed(false);
            }
        }

        private void UpdateEndLag(float deltaTime)
        {
            endLagTimer -= deltaTime;
            if (endLagTimer > 0f)
            {
                return;
            }

            isInEndLag = false;
            if (playerMovement != null)
            {
                playerMovement.SetMovementSuppressed(false);
            }
        }

        private void PlayDashTrail()
        {
            if (dashTrail == null)
            {
                return;
            }

            dashTrail.transform.position = transform.position;
            dashTrail.Clear(true);
            dashTrail.Play(true);

            if (dashTrailBurstAmount > 0)
            {
                dashTrail.Emit(dashTrailBurstAmount);
            }
        }

        private void SpawnAfterimage()
        {
            if (playerSprite == null)
            {
                return;
            }

            Afterimage.SpawnFromSprite(playerSprite, afterimageLifetime, afterimageAlpha);
        }

        private Vector2 GetDirectionalInput()
        {
            Vector2 input = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Gamepad.current != null)
            {
                input += Gamepad.current.leftStick.ReadValue();
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                {
                    input.x -= 1f;
                }

                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                {
                    input.x += 1f;
                }

                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                {
                    input.y += 1f;
                }

                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                {
                    input.y -= 1f;
                }
            }
#endif
            input += new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);
            return input;
        }

        private bool GetDashPressed()
        {
            bool pressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.JoystickButton0);
#if ENABLE_INPUT_SYSTEM
            if (!pressed && Keyboard.current != null)
            {
                pressed |= Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame;
            }

            if (!pressed && Gamepad.current != null)
            {
                pressed |= Gamepad.current.buttonSouth.wasPressedThisFrame;
            }
#endif
            return pressed;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 origin = transform.position + (Vector3)groundCheckOffset;
            Gizmos.DrawWireCube(origin, groundCheckSize);
        }
    }
}
