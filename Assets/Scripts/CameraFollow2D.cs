using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    private Rigidbody2D targetRb;
    private PlayerMovement playerMovement;

    [Header("Deadzone")]
    [SerializeField] private Vector2 deadZoneSize = new Vector2(4f, 3f);

    [Header("Look Ahead (Hollow Knight-style)")]
    [SerializeField] private float lookAheadDistance = 1.5f;
    [SerializeField] private float lookAheadSmoothing = 5f;

    [Header("Fall Bias")]
    [SerializeField] private float fallBiasAmount = 1.2f;
    [SerializeField] private float fallBiasThreshold = -5f;

    [Header("Offsets")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.5f, -10f);

    [Header("Smooth Damp")]
    [SerializeField] private float smoothTime = 0.12f; // mer responsiv än 0.2

    private Vector3 currentVelocity;
    private Vector3 lookAheadOffset;
    private Vector3 fixedCameraTarget; // NY: smooth target från FixedUpdate

    private void Awake()
    {
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody2D>();
            playerMovement = target.GetComponent<PlayerMovement>();
        }
    }

    // ─────────────────────────────────────────────────────
    // FIXEDUPDATE — PHYSICS-SYNKAD POSITION BERÄKNAS HÄR
    // ─────────────────────────────────────────────────────
    private void FixedUpdate()
    {
        if (target == null)
            return;

        // Beräkna fokuspunkt baserat på spelaren
        Vector3 focusPoint = target.position + worldOffset;

        ApplyLookAhead(ref focusPoint);
        ApplyFallBias(ref focusPoint);

        // Deadzone appliceras på physics-positionen
        fixedCameraTarget = ApplyDeadZone(transform.position, focusPoint);
    }

    // ─────────────────────────────────────────────────────
    // LATEUPDATE — MJUK KAMERARÖRELSE MOT FIXEDUPDATE-MÅLET
    // ─────────────────────────────────────────────────────
    private void LateUpdate()
    {
        if (target == null)
            return;

        // Smootha mot den physics-stabila positionen
        transform.position = Vector3.SmoothDamp(
            transform.position,
            fixedCameraTarget,
            ref currentVelocity,
            smoothTime
        );
    }

    // ─────────────────────────────────────────────────────
    // LOOK AHEAD (Hollow Knight-style)
    // ─────────────────────────────────────────────────────
    private void ApplyLookAhead(ref Vector3 focusPoint)
    {
        if (targetRb == null) return;

        float horizontalSpeed = targetRb.linearVelocity.x;
        float direction = Mathf.Sign(horizontalSpeed);

        float targetLook = Mathf.Abs(horizontalSpeed) > 0.1f
            ? direction * lookAheadDistance
            : 0f;

        lookAheadOffset.x = Mathf.Lerp(
            lookAheadOffset.x,
            targetLook,
            Time.deltaTime * lookAheadSmoothing
        );

        focusPoint.x += lookAheadOffset.x;
    }

    // ─────────────────────────────────────────────────────
    // FALL BIAS
    // ─────────────────────────────────────────────────────
    private void ApplyFallBias(ref Vector3 focusPoint)
    {
        if (targetRb == null) return;

        if (targetRb.linearVelocity.y < fallBiasThreshold)
        {
            focusPoint.y -= fallBiasAmount;
        }
        else if (playerMovement != null && playerMovement.IsGrounded)
        {
            focusPoint.y = Mathf.Lerp(
                focusPoint.y,
                target.position.y + worldOffset.y,
                Time.deltaTime * 5f
            );
        }
    }

    // ─────────────────────────────────────────────────────
    // DEADZONE
    // ─────────────────────────────────────────────────────
    private Vector3 ApplyDeadZone(Vector3 camPos, Vector3 focusPoint)
    {
        Vector2 half = deadZoneSize * 0.5f;
        Vector3 delta = focusPoint - camPos;

        if (delta.x > half.x)
            camPos.x = focusPoint.x - half.x;
        else if (delta.x < -half.x)
            camPos.x = focusPoint.x + half.x;

        if (delta.y > half.y)
            camPos.y = focusPoint.y - half.y;
        else if (delta.y < -half.y)
            camPos.y = focusPoint.y + half.y;

        return camPos;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;

        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.3f);
        Gizmos.DrawWireCube(transform.position, new Vector3(deadZoneSize.x, deadZoneSize.y, 0f));
    }
#endif
}
