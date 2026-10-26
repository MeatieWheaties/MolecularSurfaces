using System;
using UnityEngine;

namespace MolecularSurfaces.Demos
{
    /// <summary>
    /// Mouse orbit camera: left-drag rotates, right/middle-drag pans, scroll zooms.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OrbitCamera : MonoBehaviour
    {
        public Vector3 Target;
        public float Distance = 10;

        public float RotateSpeed = 4;
        public float PanSpeed = 1;
        public float ZoomSpeed = 0.15f;
        public float MinDistance = 1;
        public float MaxDistance = 2000;

        public bool AutoRotate = true;
        public float AutoRotateSpeed = 8;

        /// <summary>Returns true when the pointer is over UI that should swallow mouse input.</summary>
        public Func<Vector2, bool> IsPointerBlocked;

        float _Yaw = 30;
        float _Pitch = 20;
        bool _Dragging;

        Vector3 _FrameTarget;
        float _FrameDistance = 10;

        Camera _Camera;

        void Awake()
        {
            _Camera = GetComponent<Camera>();
            Apply();
        }

        /// <summary>Points the camera at a sphere so the whole of it is in view.</summary>
        public void Frame(Vector3 center, float radius)
        {
            var _halfFov = Mathf.Deg2Rad * _Camera.fieldOfView * 0.5f;
            var _halfFovX = Mathf.Atan(Mathf.Tan(_halfFov) * _Camera.aspect);
            _FrameTarget = center;
            _FrameDistance = radius / Mathf.Sin(Mathf.Min(_halfFov, _halfFovX));

            Target = _FrameTarget;
            Distance = _FrameDistance;
            MaxDistance = Mathf.Max(MaxDistance, _FrameDistance * 4);
            _Camera.farClipPlane = Mathf.Max(_Camera.farClipPlane, _FrameDistance * 8);
            Apply();
        }

        void LateUpdate()
        {
            var _mouse = (Vector2)Input.mousePosition;
            var _blocked = IsPointerBlocked != null && IsPointerBlocked(_mouse);

            // Only start a drag outside the UI, but keep it going if the pointer passes over it.
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                _Dragging = !_blocked;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2))
                _Dragging = false;

            var _dx = Input.GetAxis("Mouse X");
            var _dy = Input.GetAxis("Mouse Y");

            if (_Dragging && Input.GetMouseButton(0))
            {
                _Yaw += _dx * RotateSpeed;
                _Pitch = Mathf.Clamp(_Pitch - _dy * RotateSpeed, -89, 89);
                AutoRotate = false;
            }
            else if (_Dragging && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
            {
                var _scale = Distance * 0.002f * PanSpeed * 25;
                Target -= (transform.right * _dx + transform.up * _dy) * _scale;
            }
            else if (AutoRotate)
            {
                _Yaw += AutoRotateSpeed * Time.deltaTime;
            }

            var _scroll = Input.mouseScrollDelta.y;
            if (!_blocked && Mathf.Abs(_scroll) > 0.01f)
                Distance = Mathf.Clamp(Distance * (1 - _scroll * ZoomSpeed), MinDistance, MaxDistance);

            if (Input.GetKeyDown(KeyCode.F) && GUIUtility.keyboardControl == 0)
            {
                Target = _FrameTarget;
                Distance = _FrameDistance;
            }

            Apply();
        }

        void Apply()
        {
            var _rotation = Quaternion.Euler(_Pitch, _Yaw, 0);
            transform.SetPositionAndRotation(Target - _rotation * Vector3.forward * Distance, _rotation);
        }
    }
}
