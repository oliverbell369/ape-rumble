using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Wrecked
{
    public enum DeskKey { W, A, S, D, Q, E, Space, Up, Down, Left, Right }

    /// <summary>Keyboard and mouse for testing without a headset. Works with either input system.</summary>
    public static class DeskInput
    {
        public static bool RightHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButton(1);
#else
                return false;
#endif
            }
        }

        public static bool LeftPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        public static bool AnyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) || LeftPressed();
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.anyKeyDown;
#else
            return false;
#endif
        }

        public static Vector2 MouseDelta()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return Vector2.zero;
            Vector2 d = Mouse.current.delta.ReadValue();
            return new Vector2(d.x, -d.y); // screen y is up; match the web version (down = positive)
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X") * 10f, -Input.GetAxisRaw("Mouse Y") * 10f);
#else
            return Vector2.zero;
#endif
        }

        public static void UpdateCursor()
        {
            bool click = false, esc = false;
#if ENABLE_INPUT_SYSTEM
            click = Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame);
            esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            click = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            esc = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (esc) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            else if (click) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        public static bool Held(DeskKey k)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (k)
            {
                case DeskKey.W: return kb.wKey.isPressed;
                case DeskKey.A: return kb.aKey.isPressed;
                case DeskKey.S: return kb.sKey.isPressed;
                case DeskKey.D: return kb.dKey.isPressed;
                case DeskKey.Q: return kb.qKey.isPressed;
                case DeskKey.E: return kb.eKey.isPressed;
                case DeskKey.Space: return kb.spaceKey.isPressed;
                case DeskKey.Up: return kb.upArrowKey.isPressed;
                case DeskKey.Down: return kb.downArrowKey.isPressed;
                case DeskKey.Left: return kb.leftArrowKey.isPressed;
                case DeskKey.Right: return kb.rightArrowKey.isPressed;
            }
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            switch (k)
            {
                case DeskKey.W: return Input.GetKey(KeyCode.W);
                case DeskKey.A: return Input.GetKey(KeyCode.A);
                case DeskKey.S: return Input.GetKey(KeyCode.S);
                case DeskKey.D: return Input.GetKey(KeyCode.D);
                case DeskKey.Q: return Input.GetKey(KeyCode.Q);
                case DeskKey.E: return Input.GetKey(KeyCode.E);
                case DeskKey.Space: return Input.GetKey(KeyCode.Space);
                case DeskKey.Up: return Input.GetKey(KeyCode.UpArrow);
                case DeskKey.Down: return Input.GetKey(KeyCode.DownArrow);
                case DeskKey.Left: return Input.GetKey(KeyCode.LeftArrow);
                case DeskKey.Right: return Input.GetKey(KeyCode.RightArrow);
            }
            return false;
#else
            return false;
#endif
        }
    }
}
