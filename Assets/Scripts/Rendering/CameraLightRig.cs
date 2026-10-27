using UnityEngine;
using UnityEngine.Rendering;

namespace MolecularSurfaces.Rendering
{
    /// <summary>
    /// Key + fill lighting that turns with the camera, so whichever side of the
    /// molecule you're looking at is well lit, plus a soft trilight ambient.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(90)]
    public class CameraLightRig : MonoBehaviour
    {
        [Tooltip("Uses the scene's first directional light if left empty.")]
        public Light Key;
        public Vector3 KeyAngles = new Vector3(35, -40, 0);
        public float KeyIntensity = 1.05f;
        public Color KeyColor = new Color(1f, 0.96f, 0.9f);

        public Vector3 FillAngles = new Vector3(10, 55, 0);
        public float FillIntensity = 0.35f;
        public Color FillColor = new Color(0.7f, 0.8f, 1f);

        public Color AmbientSky = new Color(0.38f, 0.42f, 0.5f);
        public Color AmbientEquator = new Color(0.22f, 0.23f, 0.26f);
        public Color AmbientGround = new Color(0.08f, 0.08f, 0.09f);

        Light _Fill;

        void Start()
        {
            if (Key == null)
            {
                foreach (var _light in FindObjectsOfType<Light>())
                    if (_light.type == LightType.Directional) { Key = _light; break; }
            }

            if (Key != null)
            {
                Key.color = KeyColor;
                Key.intensity = KeyIntensity;
                Key.shadows = LightShadows.Soft;
                Key.shadowStrength = 0.55f;
            }

            _Fill = new GameObject("[Fill Light]").AddComponent<Light>();
            _Fill.type = LightType.Directional;
            _Fill.color = FillColor;
            _Fill.intensity = FillIntensity;
            _Fill.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
        }

        void LateUpdate()
        {
            if (Key != null) Key.transform.rotation = transform.rotation * Quaternion.Euler(KeyAngles);
            if (_Fill != null) _Fill.transform.rotation = transform.rotation * Quaternion.Euler(FillAngles);
        }
    }
}
