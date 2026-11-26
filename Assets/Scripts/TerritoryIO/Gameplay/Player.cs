using System.Collections.Generic;
using TerritoryIO.View;
using UnityEngine;

namespace TerritoryIO.Gameplay
{
    /// <summary>
    /// One player head. Moves freely: a continuous Position and a Heading that turns at a limited
    /// rate, so it can drive curves and circles. The rules still run on the grid: whenever the head
    /// crosses into a new cell, GameManager processes that cell (trail, capture, collisions).
    /// </summary>
    public class Player : MonoBehaviour
    {
        public int Id { get; private set; }
        public string DisplayName { get; private set; }
        public Color32 Color { get; private set; }
        public bool IsHuman { get; private set; }
        public IPlayerBrain Brain { get; private set; }

        /// <summary>Cells per second.</summary>
        public float Speed;
        /// <summary>Degrees per second.</summary>
        public float TurnRate;

        public Vector2 Position;          // world position (1 unit = 1 cell)
        public Vector2 Heading = Vector2.right; // unit vector
        public Vector2Int Cell;           // grid cell the head is in
        public readonly List<Vector2Int> Trail = new List<Vector2Int>();

        public bool Alive { get; private set; }
        public int Kills;
        public float BestPercent;
        /// <summary>Seconds of crown mode left (0 = no crown). Kills during crown mode take the victim's land.</summary>
        public float CrownTimeLeft { get; private set; }
        public bool HasCrown => CrownTimeLeft > 0f;
        private SpriteRenderer crownIcon;

        private Transform visual;
        private LineRenderer trailLine;
        private readonly List<Vector3> linePoints = new List<Vector3>();
        private float linePointSpacing = 0.25f;
        private static Material lineMaterial;
        private bool rotateVisual = true;
        private float angleOffset;

        public void Setup(int id, string displayName, Color32 color, bool isHuman, IPlayerBrain brain,
            Sprite sprite, bool tintSprite, float headSize, bool rotateSprite, float spriteAngleOffset,
            float trailWidth)
        {
            Id = id;
            DisplayName = displayName;
            Color = color;
            IsHuman = isHuman;
            Brain = brain;
            rotateVisual = rotateSprite;
            angleOffset = spriteAngleOffset;

            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);

            if (sprite == null)
            {
                // Default look: outlined square with an "eye" showing the direction.
                var outline = SpriteFactory.CreateRoundedSquare("Outline", visual, Palette.HeadOutline, 9);
                outline.transform.localScale = Vector3.one * headSize * 1.15f;
                var body = SpriteFactory.CreateRoundedSquare("Body", visual, color, 10);
                body.transform.localScale = Vector3.one * headSize * 0.85f;
                var eye = SpriteFactory.CreateRoundedSquare("Eye", visual, Palette.HeadOutline, 11);
                eye.transform.localScale = Vector3.one * headSize * 0.28f;
                eye.transform.localPosition = new Vector3(headSize * 0.2f, 0f, 0f);
            }
            else
            {
                var sr = visual.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = tintSprite ? (UnityEngine.Color)color : UnityEngine.Color.white;
                sr.sortingOrder = 10;
                Vector3 size = sprite.bounds.size;
                float largest = Mathf.Max(size.x, size.y);
                if (largest > 0f) visual.localScale = Vector3.one * (headSize / largest);
            }

            if (trailWidth > 0f) CreateTrailLine(trailWidth);

            gameObject.SetActive(false);
        }

        /// <summary>Smooth trail: a line that follows the exact path of the head.</summary>
        private void CreateTrailLine(float width)
        {
            if (lineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                lineMaterial = new Material(shader);
            }

            // Separate object, not a child: the head moves, the trail must stay in the world.
            var go = new GameObject($"Trail_{DisplayName}");
            trailLine = go.AddComponent<LineRenderer>();
            trailLine.sharedMaterial = lineMaterial;
            trailLine.useWorldSpace = true;
            trailLine.widthMultiplier = width;
            trailLine.numCapVertices = 8;    // round, smooth ends
            trailLine.numCornerVertices = 6; // smooth bends
            trailLine.sortingOrder = 5; // above land (0), below heads (10)
            Color c = Color32.Lerp(Color, new Color32(255, 255, 255, 255), 0.35f);
            trailLine.startColor = c;
            trailLine.endColor = c;
            trailLine.positionCount = 0;
            linePointSpacing = Mathf.Max(0.1f, width * 0.4f);
        }

        /// <summary>Removes the trail (after a capture or death).</summary>
        public void ClearTrail()
        {
            Trail.Clear();
            linePoints.Clear();
            if (trailLine != null) trailLine.positionCount = 0;
        }

        private void UpdateTrailLine()
        {
            if (trailLine == null) return;
            if (Trail.Count == 0)
            {
                if (linePoints.Count > 0) ClearTrail();
                return;
            }
            Vector3 head = new Vector3(Position.x, Position.y, 0f);
            if (linePoints.Count == 0 || (head - linePoints[linePoints.Count - 1]).sqrMagnitude >= linePointSpacing * linePointSpacing)
            {
                linePoints.Add(head);
                trailLine.positionCount = linePoints.Count + 1;
                trailLine.SetPosition(linePoints.Count - 1, head);
            }
            trailLine.SetPosition(linePoints.Count, head); // live end follows the head
        }

        private void OnDestroy()
        {
            if (trailLine != null) Destroy(trailLine.gameObject);
        }

        public void Spawn(Vector2Int cell, Vector2 heading)
        {
            Cell = cell;
            Position = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            Heading = heading.normalized;
            ClearTrail();
            Kills = 0;
            BestPercent = 0f;
            SetCrown(0f);
            Alive = true;
            gameObject.SetActive(true);
            Brain.OnSpawn(this);
            UpdateVisual();
        }

        public void Die()
        {
            Alive = false;
            ClearTrail();
            SetCrown(0f);
            gameObject.SetActive(false);
        }

        /// <summary>Starts (seconds &gt; 0) or ends (0) crown mode and shows a small crown above the head.</summary>
        public void SetCrown(float seconds)
        {
            CrownTimeLeft = Mathf.Max(0f, seconds);
            if (HasCrown && crownIcon == null)
            {
                var go = new GameObject("CrownIcon");
                go.transform.SetParent(transform, false); // not under Visual, so it never rotates
                crownIcon = go.AddComponent<SpriteRenderer>();
                crownIcon.sprite = SpriteFactory.Crown;
                crownIcon.sortingOrder = 12;
            }
            if (crownIcon != null) crownIcon.enabled = HasCrown;
        }

        /// <summary>Counts crown mode down. The icon blinks during the last 2 seconds.</summary>
        public void TickCrown(float dt, float headSize)
        {
            if (!HasCrown) return;
            CrownTimeLeft = Mathf.Max(0f, CrownTimeLeft - dt);
            if (!HasCrown) { SetCrown(0f); return; }
            crownIcon.transform.localScale = Vector3.one * headSize;
            crownIcon.transform.localPosition = new Vector3(0f, headSize * 1.05f, 0f);
            crownIcon.enabled = CrownTimeLeft > 2f || Mathf.Repeat(CrownTimeLeft, 0.3f) > 0.12f;
        }

        /// <summary>Turn toward desired direction, limited by TurnRate.</summary>
        public void Steer(Vector2 desired, float dt)
        {
            if (desired.sqrMagnitude < 0.0001f) return;
            float current = Mathf.Atan2(Heading.y, Heading.x) * Mathf.Rad2Deg;
            float target = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
            float next = Mathf.MoveTowardsAngle(current, target, TurnRate * dt) * Mathf.Deg2Rad;
            Heading = new Vector2(Mathf.Cos(next), Mathf.Sin(next));
        }

        /// <summary>True if cell is one of the last n trail cells (the "neck" behind the head).</summary>
        public bool IsRecentTrail(Vector2Int cell, int n)
        {
            for (int i = Trail.Count - 1; i >= 0 && i >= Trail.Count - n; i--)
                if (Trail[i] == cell) return true;
            return false;
        }

        public void UpdateVisual()
        {
            transform.position = new Vector3(Position.x, Position.y, 0f);
            UpdateTrailLine();
            if (rotateVisual)
            {
                float angle = Mathf.Atan2(Heading.y, Heading.x) * Mathf.Rad2Deg + angleOffset;
                visual.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        public static Vector2 CellCenter(Vector2Int c) => new Vector2(c.x + 0.5f, c.y + 0.5f);
    }
}
