using UnityEngine;
using UnityEngine.InputSystem;

namespace HarborRacer.Inputs
{
    /// <summary>
    /// Keyboard + gamepad driving input via the Input System. Bindings mirror the design target:
    /// Space accelerate, S brake, arrows/A-D steer, V drift (and trick in trick zones), RightShift/E use item,
    /// Down/Q throw item backward, arrows up/down glider pitch.
    /// </summary>
    public class PlayerKartInput : MonoBehaviour, IKartInput
    {
        InputAction throttle, brake, steer, pitch, drift, item, itemBack;
        bool driftPressed, itemPressed, itemBackPressed;

        public float Throttle => throttle.ReadValue<float>();
        public float Brake => brake.ReadValue<float>();
        public float Steer => steer.ReadValue<float>();
        public float Pitch => pitch.ReadValue<float>();
        public bool DriftHeld => drift.IsPressed();
        public bool DriftPressed => driftPressed;
        public bool TrickPressed => driftPressed; // same key as the (V); context decides
        public bool ItemPressed => itemPressed;
        public bool ItemBackPressed => itemBackPressed;

        void Awake()
        {
            throttle = new InputAction("Throttle", InputActionType.Value);
            throttle.AddBinding("<Keyboard>/space"); throttle.AddBinding("<Keyboard>/w");
            throttle.AddBinding("<Gamepad>/buttonSouth"); throttle.AddBinding("<Gamepad>/rightTrigger");

            brake = new InputAction("Brake", InputActionType.Value);
            brake.AddBinding("<Keyboard>/s"); brake.AddBinding("<Gamepad>/buttonWest"); brake.AddBinding("<Gamepad>/leftTrigger");

            steer = new InputAction("Steer", InputActionType.Value);
            steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            steer.AddBinding("<Gamepad>/leftStick/x");

            pitch = new InputAction("Pitch", InputActionType.Value);
            pitch.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/upArrow").With("Positive", "<Keyboard>/downArrow");
            pitch.AddBinding("<Gamepad>/leftStick/y");   // stick up = nose down handled by sign in KartGlider

            drift = new InputAction("Drift", InputActionType.Button);
            drift.AddBinding("<Keyboard>/v"); drift.AddBinding("<Keyboard>/leftShift"); drift.AddBinding("<Gamepad>/rightShoulder");

            item = new InputAction("UseItem", InputActionType.Button);
            item.AddBinding("<Keyboard>/rightShift"); item.AddBinding("<Keyboard>/e"); item.AddBinding("<Gamepad>/leftShoulder");

            itemBack = new InputAction("ItemBack", InputActionType.Button);
            itemBack.AddBinding("<Keyboard>/downArrow"); itemBack.AddBinding("<Keyboard>/q"); itemBack.AddBinding("<Gamepad>/dpad/down");
        }

        void OnEnable() { foreach (var a in All()) a.Enable(); }
        void OnDisable() { foreach (var a in All()) a.Disable(); }
        void OnDestroy() { foreach (var a in All()) a.Dispose(); }

        InputAction[] All() => new[] { throttle, brake, steer, pitch, drift, item, itemBack };

        void Update()
        {
            driftPressed |= drift.WasPressedThisFrame();
            itemPressed |= item.WasPressedThisFrame();
            itemBackPressed |= itemBack.WasPressedThisFrame();
        }

        public void EndFixedStep() { driftPressed = itemPressed = itemBackPressed = false; }
    }
}
