using UnityEngine;

namespace TerritoryIO.Gameplay
{
    /// <summary>Kinds of prizes that can appear on the map. Add new ones here.</summary>
    public enum PickupKind { Crown }

    /// <summary>
    /// A prize lying on the map. It only knows how to look (bob, blink, edge arrow) and how long it lives.
    /// GameManager decides where it spawns, who can grab it and what it does.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        public PickupKind Kind { get; private set; }
        /// <summary>World position (1 unit = 1 cell).</summary>
        public Vector2 Position { get; private set; }
        /// <summary>Seconds until it disappears.</summary>
        public float TimeLeft { get; private set; }

        private float blinkTime;
        private float size;
        private float age;
        private SpriteRenderer body;
        private SpriteRenderer marker;

        /// <param name="lifetime">Seconds it stays on the map.</param>
        /// <param name="blinkSeconds">It blinks during this many last seconds.</param>
        /// <param name="arrowSprite">Arrow shown at the screen edge (pointing right) while the pickup is off screen.</param>
        public void Setup(PickupKind kind, Sprite sprite, Sprite arrowSprite, Vector2 position, float worldSize,
            float lifetime, float blinkSeconds)
        {
            Kind = kind;
            Position = position;
            TimeLeft = lifetime;
            blinkTime = blinkSeconds;
            size = worldSize;
            transform.position = new Vector3(position.x, position.y, 0f);

            body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = sprite;
            body.sortingOrder = 8; // above land and trails, below heads
            transform.localScale = Vector3.one * worldSize;

            // Arrow at the screen edge pointing toward the prize while it is off screen.
            // (An arrow, not a copy of the prize, so it never looks like the prize itself.)
            var m = new GameObject(name + "_Arrow");
            marker = m.AddComponent<SpriteRenderer>();
            marker.sprite = arrowSprite;
            marker.sortingOrder = 20;
            marker.enabled = false;
        }

        /// <summary>Advances time. Returns false when the pickup has expired.</summary>
        public bool Tick(float dt)
        {
            TimeLeft -= dt;
            age += dt;
            if (TimeLeft <= 0f) return false;

            // Gentle bob + pulse so it catches the eye.
            float pulse = 1f + 0.08f * Mathf.Sin(age * 6f);
            transform.localScale = Vector3.one * size * pulse;
            transform.position = new Vector3(Position.x, Position.y + 0.1f * size * Mathf.Sin(age * 3f), 0f);

            // Blink faster and faster during the last seconds.
            bool visible = true;
            if (TimeLeft <= blinkTime)
            {
                float period = Mathf.Lerp(0.15f, 0.4f, TimeLeft / Mathf.Max(0.01f, blinkTime));
                visible = Mathf.Repeat(TimeLeft, period) > period * 0.4f;
            }
            body.enabled = visible;
            return true;
        }

        // After the camera has moved this frame.
        private void LateUpdate()
        {
            if (body != null) UpdateMarker(body.enabled);
        }

        /// <summary>Shows an arrow at the edge of the screen pointing to the pickup when it is off screen.</summary>
        private void UpdateMarker(bool visible)
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic) { marker.enabled = false; return; }

            Vector2 camPos = cam.transform.position;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector2 d = Position - camPos;
            bool onScreen = Mathf.Abs(d.x) < halfW && Mathf.Abs(d.y) < halfH;
            marker.enabled = !onScreen && visible;
            if (onScreen) return;

            float iconSize = halfH * 0.09f;
            float limitX = halfW - iconSize * 1.5f, limitY = halfH - iconSize * 1.5f;
            float k = Mathf.Min(limitX / Mathf.Max(0.001f, Mathf.Abs(d.x)), limitY / Mathf.Max(0.001f, Mathf.Abs(d.y)));
            Vector2 p = camPos + d * k;
            marker.transform.position = new Vector3(p.x, p.y, 0f);
            marker.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            marker.transform.localScale = Vector3.one * iconSize * (1f + 0.15f * Mathf.Sin(age * 8f));
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }
    }
}
