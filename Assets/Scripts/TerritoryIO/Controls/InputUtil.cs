using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TerritoryIO.Controls
{
    /// <summary>
    /// Thin wrapper so the game works with the new Input System, the old Input Manager, or both
    /// (Project Settings > Player > Active Input Handling).
    /// </summary>
    public static class InputUtil
    {
        /// <summary>
        /// Arrow keys / WASD currently held, combined into a vector (diagonals allowed), or zero.
        /// </summary>
        public static Vector2 KeyboardVector()
        {
            float x = 0f, y = 0f;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) x += 1f;
#endif
            return new Vector2(x, y);
        }

        /// <summary>True while the mouse button or a finger is down; gives the screen position.</summary>
        public static bool PointerHeld(out Vector2 screenPos)
        {
#if ENABLE_INPUT_SYSTEM
            var p = Pointer.current;
            if (p != null && p.press.isPressed)
            {
                screenPos = p.position.ReadValue();
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetMouseButton(0))
            {
                screenPos = UnityEngine.Input.mousePosition;
                return true;
            }
#endif
            screenPos = Vector2.zero;
            return false;
        }

        /// <summary>Space / Enter / tap / click this frame. Used to restart a round.</summary>
        public static bool RestartPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) return true;
            var p = Pointer.current;
            return p != null && p.press.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.Space)
                || UnityEngine.Input.GetKeyDown(KeyCode.Return)
                || UnityEngine.Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        /// <summary>Space or Enter this frame (keyboard restart; tapping uses the on-screen buttons).</summary>
        public static bool KeyRestartPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return);
#else
            return false;
#endif
        }

        /// <summary>Escape this frame. On Android the Back button also counts as Escape.</summary>
        public static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
