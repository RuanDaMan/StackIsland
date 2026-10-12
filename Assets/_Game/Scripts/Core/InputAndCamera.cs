using UnityEngine;
using UnityEngine.InputSystem;

namespace StackIsland
{
    /// <summary>All input actions, created in code (New Input System).</summary>
    public static class GameInput
    {
        public static InputActionMap Map { get; private set; }
        public static InputAction Move, Zoom, Pause, ClickMove, Point, ToggleManagers;

        public static void Init()
        {
            if (Map != null) { Map.Disable(); Map.Dispose(); }
            Map = new InputActionMap("Gameplay");
            Move = Map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");
            Zoom = Map.AddAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y");
            Pause = Map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            ClickMove = Map.AddAction("ClickToMove", InputActionType.Button, "<Mouse>/rightButton");
            Point = Map.AddAction("Point", InputActionType.Value, "<Mouse>/position");
            ToggleManagers = Map.AddAction("ToggleManagers", InputActionType.Button, "<Keyboard>/m");
            Map.Enable();
        }

        public static Vector2 MousePosition => Point != null ? Point.ReadValue<Vector2>() : Vector2.zero;
    }

    /// <summary>Close, fixed-angle follow camera (the genre's look): diagonal yaw, steep-ish pitch, wheel zoom.</summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform Target;
        public float Pitch = 52f;
        public float Yaw = 32f;
        public float Distance = 21f;
        public float MinDistance = 13f, MaxDistance = 34f;
        public bool Orbit;   // slow orbit behind the main menu

        float _dist;
        Vector3 _focus;

        void Start()
        {
            _dist = Distance;
            Snap();
        }

        public void Snap()
        {
            if (Target != null) _focus = Target.position;
            Apply();
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            var g = Game.I;
            if (g != null && !g.InputBlocked && GameInput.Zoom != null)
            {
                float scroll = GameInput.Zoom.ReadValue<float>();
                if (Mathf.Abs(scroll) > 0.01f) Distance = Mathf.Clamp(Distance * (scroll > 0 ? 0.9f : 1.11f), MinDistance, MaxDistance);
            }
            if (Orbit) Yaw += dt * 5f;
            _dist = Mathf.Lerp(_dist, Distance, 1 - Mathf.Exp(-10 * dt));
            if (Target != null) _focus = Vector3.Lerp(_focus, Target.position, 1 - Mathf.Exp(-7f * dt));
            Apply();
        }

        void Apply()
        {
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            transform.position = _focus + Vector3.up * 1f + rot * new Vector3(0, 0, -_dist);
            transform.rotation = rot;
        }
    }
}
