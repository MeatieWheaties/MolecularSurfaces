using UnityEngine;

namespace MolecularSurfaces.Rendering
{
    /// <summary>
    /// Camera image effect: screen-space ambient occlusion, outlines around each chain
    /// and a gradient background. Chain IDs come from a hidden camera that re-renders
    /// the scene with a replacement shader into an ID buffer.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)] // After camera controllers have moved the camera this frame.
    public class SurfaceEffects : MonoBehaviour
    {
        const string EFFECTS_SHADER = "Hidden/MolecularSurfaces/SurfaceEffects";
        const string ID_SHADER = "Hidden/MolecularSurfaces/ChainId";

        [Header("Ambient Occlusion")]
        public bool AmbientOcclusion = true;
        [Tooltip("World-space sampling radius (Å at render scale 1).")]
        public float AORadius = 4f;
        [Range(0, 1.5f)] public float AOIntensity = 1f;
        public float AOBias = 0.05f;

        [Header("Outlines")]
        public bool Outlines = true;
        [Range(0.5f, 4)] public float OutlineWidth = 1.5f;
        public Color OutlineColor = new Color(0.03f, 0.035f, 0.05f, 0.9f);

        [Header("Background")]
        public Color BackgroundTop = new Color(0.16f, 0.18f, 0.23f);
        public Color BackgroundBottom = new Color(0.05f, 0.055f, 0.07f);

        public enum View { Final, AmbientOcclusion, ChainIds }
        [Header("Debug")]
        public View DebugView = View.Final;

        Camera _Camera;
        Camera _IdCamera;
        RenderTexture _IdTexture;
        Material _Material;
        Shader _IdShader;

        void OnEnable()
        {
            _Camera = GetComponent<Camera>();
            _Camera.depthTextureMode |= DepthTextureMode.Depth;

            var _effects = Shader.Find(EFFECTS_SHADER);
            _IdShader = Shader.Find(ID_SHADER);
            if (_effects == null || _IdShader == null)
            {
                Debug.LogWarning("SurfaceEffects: shaders not found; effects disabled.");
                enabled = false;
                return;
            }

            _Material = new Material(_effects) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDisable()
        {
            if (_Material != null) DestroyImmediate(_Material);
            if (_IdCamera != null) DestroyImmediate(_IdCamera.gameObject);
            if (_IdTexture != null) { _IdTexture.Release(); DestroyImmediate(_IdTexture); }
            _Material = null;
            _IdCamera = null;
            _IdTexture = null;
        }

        void LateUpdate()
        {
            if (_Material == null) return;

            var _width = _Camera.pixelWidth;
            var _height = _Camera.pixelHeight;
            if (_IdTexture == null || _IdTexture.width != _width || _IdTexture.height != _height)
            {
                if (_IdTexture != null) { _IdTexture.Release(); DestroyImmediate(_IdTexture); }
                _IdTexture = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Chain IDs",
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            if (_IdCamera == null)
            {
                var _go = new GameObject("[Chain ID Camera]") { hideFlags = HideFlags.HideAndDontSave };
                _IdCamera = _go.AddComponent<Camera>();
                _IdCamera.enabled = false;
            }

            _IdCamera.CopyFrom(_Camera);
            _IdCamera.targetTexture = _IdTexture;
            _IdCamera.clearFlags = CameraClearFlags.SolidColor;
            _IdCamera.backgroundColor = Color.clear;
            _IdCamera.allowMSAA = false;
            _IdCamera.allowHDR = false;
            _IdCamera.depthTextureMode = DepthTextureMode.None;
            _IdCamera.RenderWithShader(_IdShader, "RenderType");
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_Material == null || _IdTexture == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            var _tanHalf = Mathf.Tan(_Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            _Material.SetVector("_TanHalfFov", new Vector4(_tanHalf * _Camera.aspect, _tanHalf));
            _Material.SetFloat("_AORadius", AORadius);
            _Material.SetFloat("_AOBias", AOBias);
            _Material.SetFloat("_AOIntensity", AmbientOcclusion ? AOIntensity : 0);
            _Material.SetFloat("_OutlineWidth", Outlines ? OutlineWidth : 0);
            // These aren't declared as Color properties in the shader, so Unity won't
            // convert them to linear for us.
            _Material.SetColor("_OutlineColor", ToActiveSpace(OutlineColor));
            _Material.SetColor("_BackgroundTop", ToActiveSpace(BackgroundTop));
            _Material.SetColor("_BackgroundBottom", ToActiveSpace(BackgroundBottom));
            _Material.SetFloat("_DebugView", (float)DebugView);
            _Material.SetTexture("_IdTex", _IdTexture);

            RenderTexture _ao = null;
            if (AmbientOcclusion && AOIntensity > 0)
            {
                var _format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;
                _ao = RenderTexture.GetTemporary(source.width, source.height, 0, _format);
                var _blur = RenderTexture.GetTemporary(source.width, source.height, 0, _format);

                // The ID texture shares the depth texture's orientation, so using it as
                // the blit source keeps the AO buffer aligned with depth.
                Graphics.Blit(_IdTexture, _ao, _Material, 0);
                _Material.SetVector("_BlurDirection", new Vector2(1, 0));
                Graphics.Blit(_ao, _blur, _Material, 1);
                _Material.SetVector("_BlurDirection", new Vector2(0, 1));
                Graphics.Blit(_blur, _ao, _Material, 1);

                RenderTexture.ReleaseTemporary(_blur);
                _Material.SetTexture("_AOTex", _ao);
            }
            else
            {
                _Material.SetTexture("_AOTex", Texture2D.whiteTexture);
            }

            Graphics.Blit(source, destination, _Material, 2);

            if (_ao != null) RenderTexture.ReleaseTemporary(_ao);
        }

        static Color ToActiveSpace(Color color) => QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
    }
}
