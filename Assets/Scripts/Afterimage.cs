using UnityEngine;

namespace Platformer
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class Afterimage : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Color startColor = Color.white;
        private float lifetime = 0.15f;
        private float elapsed;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            spriteRenderer.color = color;

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        public void Initialize(Sprite sprite, Color color, Vector3 worldScale, float time, int sortingLayerId, string sortingLayerName, int sortingOrder, bool flipX, bool flipY, Material material)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = sprite;
            spriteRenderer.flipX = flipX;
            spriteRenderer.flipY = flipY;
            spriteRenderer.sortingLayerID = sortingLayerId;
            spriteRenderer.sortingLayerName = sortingLayerName;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            if (material != null)
            {
                spriteRenderer.sharedMaterial = material;
            }

            startColor = color;
            lifetime = Mathf.Max(0.01f, time);
            transform.localScale = worldScale;
            spriteRenderer.color = color;
        }

        public static void SpawnFromSprite(SpriteRenderer source, float time, float alpha)
        {
            if (source == null || source.sprite == null || alpha <= 0f)
            {
                return;
            }

            GameObject clone = new GameObject($"{source.gameObject.name}_Afterimage");
            clone.transform.position = source.transform.position;
            clone.transform.rotation = source.transform.rotation;
            clone.transform.localScale = source.transform.lossyScale;
            clone.layer = source.gameObject.layer;

            clone.AddComponent<SpriteRenderer>();
            Afterimage afterimage = clone.AddComponent<Afterimage>();

            Color color = source.color;
            color.a = alpha;

            afterimage.Initialize(
                source.sprite,
                color,
                source.transform.lossyScale,
                time,
                source.sortingLayerID,
                source.sortingLayerName,
                source.sortingOrder - 1,
                source.flipX,
                source.flipY,
                source.sharedMaterial);
        }
    }
}
