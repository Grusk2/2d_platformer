using UnityEngine;

namespace Platformer
{
    public class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 deadZoneSize = new Vector2(4f, 3f);
        [SerializeField] private float lookAheadDistance = 2f;
        [SerializeField] private float lookAheadSmoothing = 5f;
        [SerializeField] private float verticalFallBias = 1f;
        [SerializeField] private float fallVelocityThreshold = -2f;
        [SerializeField] private float followDampTime = 0.2f;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.5f, -10f);

        private Vector3 currentVelocity;
        private Vector3 lookAheadOffset;
        private Rigidbody2D targetBody;
        private PlayerMovement playerMovement;

        private void Awake()
        {
            if (target != null)
            {
                targetBody = target.GetComponent<Rigidbody2D>();
                playerMovement = target.GetComponent<PlayerMovement>();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = CalculateDesiredPosition();
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, followDampTime);
        }

        private Vector3 CalculateDesiredPosition()
        {
            Vector3 focusPosition = target.position + worldOffset;
            ApplyLookAhead(ref focusPosition);
            ApplyFallBias(ref focusPosition);

            Vector3 cameraPosition = transform.position;
            cameraPosition.z = focusPosition.z;

            Vector2 halfDeadZone = deadZoneSize * 0.5f;
            Vector3 delta = focusPosition - cameraPosition;

            if (delta.x > halfDeadZone.x)
            {
                cameraPosition.x = focusPosition.x - halfDeadZone.x;
            }
            else if (delta.x < -halfDeadZone.x)
            {
                cameraPosition.x = focusPosition.x + halfDeadZone.x;
            }

            if (delta.y > halfDeadZone.y)
            {
                cameraPosition.y = focusPosition.y - halfDeadZone.y;
            }
            else if (delta.y < -halfDeadZone.y)
            {
                cameraPosition.y = focusPosition.y + halfDeadZone.y;
            }

            return cameraPosition;
        }

        private void ApplyLookAhead(ref Vector3 focusPosition)
        {
            if (targetBody == null)
            {
                return;
            }

            float targetSpeed = targetBody.velocity.x;
            float direction = Mathf.Sign(targetSpeed);
            float targetLookAhead = Mathf.Abs(targetSpeed) > 0.1f ? lookAheadDistance * direction : 0f;

            lookAheadOffset.x = Mathf.Lerp(lookAheadOffset.x, targetLookAhead, Time.deltaTime * lookAheadSmoothing);
            focusPosition += new Vector3(lookAheadOffset.x, 0f, 0f);
        }

        private void ApplyFallBias(ref Vector3 focusPosition)
        {
            if (targetBody == null)
            {
                return;
            }

            if (targetBody.velocity.y < fallVelocityThreshold)
            {
                float t = Mathf.InverseLerp(0f, fallVelocityThreshold, targetBody.velocity.y);
                float bias = Mathf.Lerp(0f, -verticalFallBias, t);
                focusPosition.y += bias;
            }
            else if (playerMovement != null && playerMovement.IsGrounded)
            {
                focusPosition.y = Mathf.Lerp(focusPosition.y, target.position.y + worldOffset.y, Time.deltaTime * lookAheadSmoothing);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (target == null)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.3f);
            Vector3 center = transform.position;
            Vector3 size = new Vector3(deadZoneSize.x, deadZoneSize.y, 0f);
            Gizmos.DrawCube(center, size);
        }
#endif
    }
}
