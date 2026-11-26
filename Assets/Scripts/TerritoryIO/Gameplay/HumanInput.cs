using TerritoryIO.Controls;
using UnityEngine;

namespace TerritoryIO.Gameplay
{
    /// <summary>
    /// Human control with free steering:
    ///  - Keyboard: WASD / arrows point the way (two keys = diagonal).
    ///  - FloatingJoystick (default, best on phones): touch anywhere and drag; the head turns toward
    ///    the drag direction. Also works with mouse click-and-drag.
    ///  - PointTowardFinger: the head turns toward your finger / cursor while pressed.
    /// The player turns smoothly at its turn rate, so holding a sideways drag drives a circle.
    /// </summary>
    public class HumanInput : MonoBehaviour, IPlayerBrain
    {
        public ControlScheme Scheme = ControlScheme.FloatingJoystick;

        private bool tracking;
        private Vector2 origin;

        private float DeadZone => Screen.dpi > 0 ? Mathf.Max(8f, Screen.dpi * 0.05f) : 10f;
        private float MaxRadius => Screen.dpi > 0 ? Mathf.Max(40f, Screen.dpi * 0.5f) : 80f;

        public void OnSpawn(Player self)
        {
            tracking = false;
        }

        public Vector2 DesiredHeading(Player self, GameManager game)
        {
            Vector2 keys = InputUtil.KeyboardVector();
            if (keys != Vector2.zero) return keys;

            if (!InputUtil.PointerHeld(out Vector2 pos))
            {
                tracking = false;
                return Vector2.zero; // keep going straight
            }

            if (Scheme == ControlScheme.PointTowardFinger)
            {
                Camera cam = Camera.main;
                if (cam == null) return Vector2.zero;
                Vector2 head = cam.WorldToScreenPoint(new Vector3(self.Position.x, self.Position.y, 0f));
                return pos - head;
            }

            // Floating joystick: the origin follows the finger if it drags too far,
            // so changing direction is always a short move.
            if (!tracking)
            {
                tracking = true;
                origin = pos;
                return Vector2.zero;
            }
            Vector2 delta = pos - origin;
            if (delta.magnitude > MaxRadius) origin = pos - delta.normalized * MaxRadius;
            return delta.magnitude >= DeadZone ? delta : Vector2.zero;
        }

        public void OnCellEntered(Player self, GameManager game) { }
    }
}
