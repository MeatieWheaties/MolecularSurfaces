using System;
using UnityEngine;

namespace MolecularSurfaces.Demos
{
    /// <summary>
    /// Mouse orbit camera: left-drag rotates, right/middle-drag pans, scroll zooms.
    /// Input moves a goal pose and the camera eases towards it.
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
        [Tooltip("Higher is snappier. 0 disables easing.")]
        public float Smoothing = 10;

        public bool AutoRotate = true;
        public float AutoRotateSpeed = 8;

        /// <summary>Returns true when the pointer is over UI that should swallow mouse input.</summary>
        public Func<Vector2, bool> IsPointerBlocked;

        /// <summary>True while the user is dragging the camera.</summary>
        public bool IsDragging => _Dragging;

        float _Yaw = 30;
        float _Pitch = 20;
        bool _Dragging;

        // Goal pose; the visible pose eases towards it.
        Vector3 _GoalTarget;
        float _GoalDistance;
        float _GoalYaw;
        float _GoalPitch;

        Vector3 _FrameTarget;
        float _FrameDistance = 10;
        float _FrameRadius = 5;

        Camera _Camera;

        void Awake()
        {
            _Camera = GetComponent<Camera>();
            _GoalTarget = Target;
            _GoalDistance = Distance;
            _GoalYaw = _Yaw;
            _GoalPitch = _Pitch;
            Apply();
        }

        /// <summary>Eases the camera to look at a sphere with the whole of it in view.</summary>
        public void Frame(Vector3 center, float radius)
        {
            var _halfFov = Mathf.Deg2Rad * _Camera.fieldOfView * 0.5f;
            var _halfFovX = Mathf.Atan(Mathf.Tan(_halfFov) * _Camera.aspect);
            _FrameTarget = center;
            _FrameRadius = radius;
            _FrameDistance = radius / Mathf.Sin(Mathf.Min(_halfFov, _halfFovX));

            _GoalTarget = _FrameTarget;
            _GoalDistance = _FrameDistance;
            MaxDistance = Mathf.Max(MaxDistance, _FrameDistance * 4);
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
                _GoalYaw += _dx * RotateSpeed;
                _GoalPitch = Mathf.Clamp(_GoalPitch - _dy * RotateSpeed, -89, 89);
                AutoRotate = false;
            }
            else if (_Dragging && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
            {
                var _scale = _GoalDistance * 0.05f * PanSpeed;
                _GoalTarget -= (transform.right * _dx + transform.up * _dy) * _scale;
            }
            else if (AutoRotate)
            {
                _GoalYaw += AutoRotateSpeed * Time.deltaTime;
            }

            var _scroll = Input.mouseScrollDelta.y;
            if (!_blocked && Mathf.Abs(_scroll) > 0.01f)
                _GoalDistance = Mathf.Clamp(_GoalDistance * (1 - _scroll * ZoomSpeed), MinDistance, MaxDistance);

            if (Input.GetKeyDown(KeyCode.F) && GUIUtility.keyboardControl == 0)
            {
                _GoalTarget = _FrameTarget;
                _GoalDistance = _FrameDistance;
            }

            var _t = Smoothing <= 0 ? 1 : 1 - Mathf.Exp(-Smoothing * Time.unscaledDeltaTime);
            Target = Vector3.Lerp(Target, _GoalTarget, _t);
            Distance = Mathf.Lerp(Distance, _GoalDistance, _t);
            _Yaw = Mathf.Lerp(_Yaw, _GoalYaw, _t);
            _Pitch = Mathf.Lerp(_Pitch, _GoalPitch, _t);

            Apply();
        }

        void Apply()
        {
            var _rotation = Quaternion.Euler(_Pitch, _Yaw, 0);
            transform.SetPositionAndRotation(Target - _rotation * Vector3.forward * Distance, _rotation);

            // Keep the depth range and shadow range tight around the model; precision
            // for AO and shadow quality both depend on it.
            var _reach = Distance + _FrameRadius * 2;
            _Camera.farClipPlane = Mathf.Max(10, _reach * 1.5f);
            _Camera.nearClipPlane = Mathf.Clamp(Distance - _FrameRadius * 2, 0.1f, 5f);
            QualitySettings.shadowDistance = _reach;
        }
    }
}
