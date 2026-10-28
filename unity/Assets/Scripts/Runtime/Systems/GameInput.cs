using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BoLike.Systems
{
    public enum GameAction { Attack, Jump, Skill0, Skill1, Skill2, Skill3, Pot0, Pot1, Menu }

    /// <summary>
    /// Unified input, port of src/systems/Controls.ts: keyboard, gamepad and the on-screen touch controls
    /// all feed the same actions, and gameplay only reads from here. Presses are buffered for 220 ms so a
    /// tap during an attack's recovery still comes out.
    ///
    /// The touch controls are Input System OnScreenStick / OnScreenButton components whose control paths
    /// point at the gamepad bindings below (left stick, buttonWest, ...), so they need no extra code here.
    /// </summary>
    public static class GameInput
    {
        public const float BufferSeconds = 0.22f;

        static InputActionMap map;
        static InputAction move, guard;
        static readonly Dictionary<GameAction, InputAction> actions = new();
        static readonly Dictionary<GameAction, double> presses = new();

        public static Vector2 Move => Enabled ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        public static bool Guard => Enabled && guard.IsPressed();
        public static bool Enabled => map != null && map.enabled;

        public static void Enable()
        {
            if (map == null) Build();
            map.Enable();
        }

        public static void Disable()
        {
            map?.Disable();
            presses.Clear();
        }

        /// <summary>Returns true once per press, if it was pressed within the buffer window.</summary>
        public static bool Consume(GameAction a)
        {
            if (!presses.TryGetValue(a, out var t)) return false;
            presses.Remove(a);
            return Time.realtimeSinceStartupAsDouble - t <= BufferSeconds;
        }

        public static bool Peek(GameAction a) =>
            presses.TryGetValue(a, out var t) && Time.realtimeSinceStartupAsDouble - t <= BufferSeconds;

        /// <summary>For UI buttons that are not on-screen controls (and for tests).</summary>
        public static void Press(GameAction a) => presses[a] = Time.realtimeSinceStartupAsDouble;

        public static void Reset() => presses.Clear();

        static void Build()
        {
            map = new InputActionMap("Gameplay");

            move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            move.AddBinding("<Gamepad>/dpad");

            guard = map.AddAction("Guard", InputActionType.Button);
            guard.AddBinding("<Keyboard>/l");
            guard.AddBinding("<Keyboard>/c");
            guard.AddBinding("<Keyboard>/shift");
            guard.AddBinding("<Gamepad>/rightTrigger");

            // Same keys as the Phaser build; gamepad paths double as the touch buttons' control paths.
            Add(GameAction.Attack, "<Keyboard>/j", "<Keyboard>/z", "<Gamepad>/buttonWest");
            Add(GameAction.Jump, "<Keyboard>/k", "<Keyboard>/space", "<Keyboard>/x", "<Gamepad>/buttonSouth");
            Add(GameAction.Skill0, "<Keyboard>/u", "<Gamepad>/buttonNorth");
            Add(GameAction.Skill1, "<Keyboard>/i", "<Gamepad>/buttonEast");
            Add(GameAction.Skill2, "<Keyboard>/o", "<Gamepad>/leftShoulder");
            Add(GameAction.Skill3, "<Keyboard>/p", "<Gamepad>/rightShoulder");
            Add(GameAction.Pot0, "<Keyboard>/1", "<Gamepad>/leftTrigger");
            Add(GameAction.Pot1, "<Keyboard>/2", "<Gamepad>/select");
            Add(GameAction.Menu, "<Keyboard>/escape", "<Gamepad>/start");
        }

        static void Add(GameAction a, params string[] bindings)
        {
            var action = map.AddAction(a.ToString(), InputActionType.Button);
            foreach (var b in bindings) action.AddBinding(b);
            action.performed += _ => Press(a);
            actions[a] = action;
        }
    }
}
