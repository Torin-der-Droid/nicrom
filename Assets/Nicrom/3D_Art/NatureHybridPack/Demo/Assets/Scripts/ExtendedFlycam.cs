using UnityEngine;
using UnityEngine.InputSystem;

namespace Nicrom.NHP
{
    public class ExtendedFlycam : MonoBehaviour
    {
        /*
            EXTENDED FLYCAM
                Desi Quintans (CowfaceGames.com), 17 August 2012.
                Based on FlyThrough.js by Slin (http://wiki.unity3d.com/index.php/FlyThrough), 17 May 2011.

            LICENSE
                Free as in speech, and free as in beer.

            FEATURES
                WASD/Arrows:    Movement
                          Q:    Climb
                          E:    Drop
                              Shift:    Move faster
                            Control:    Move slower
                                End:    Toggle cursor locking to screen (you can also press Ctrl+P to toggle play mode on and off).
            */

        public float cameraSensitivity = 90;
        public float climbSpeed = 4;
        public float normalMoveSpeed = 10;
        public float slowMoveFactor = 0.25f;
        public float fastMoveFactor = 3;

        private float rotationX = 0.0f;
        private float rotationY = 0.0f;

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            //Screen.lockCursor = true;
        }

        void Update()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            var mouseDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;

            // Match the old 0.1 mouse-axis sensitivity at 60 FPS. Mouse delta
            // already represents this frame's movement, so do not scale by deltaTime.
            const float mouseSensitivityScale = 0.1f / 60f;
            rotationX += mouseDelta.x * cameraSensitivity * mouseSensitivityScale;
            rotationY += mouseDelta.y * cameraSensitivity * mouseSensitivityScale;
            rotationY = Mathf.Clamp(rotationY, -90, 90);

            transform.localRotation = Quaternion.AngleAxis(rotationX, Vector3.up);
            transform.localRotation *= Quaternion.AngleAxis(rotationY, Vector3.left);

            if (keyboard == null)
                return;

            float horizontal = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float vertical = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            float moveSpeed = normalMoveSpeed;

            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                moveSpeed *= fastMoveFactor;
            else if (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
                moveSpeed *= slowMoveFactor;

            transform.position += (transform.forward * vertical + transform.right * horizontal)
                * moveSpeed * Time.deltaTime;

            if (keyboard.qKey.isPressed) { transform.position += transform.up * climbSpeed * Time.deltaTime; }
            if (keyboard.eKey.isPressed) { transform.position -= transform.up * climbSpeed * Time.deltaTime; }

            if (keyboard.endKey.wasPressedThisFrame)
            {
                if (Cursor.lockState == CursorLockMode.None)
                    Cursor.lockState = CursorLockMode.Locked;
                else
                    Cursor.lockState = CursorLockMode.None;
            }
        }
    }
}