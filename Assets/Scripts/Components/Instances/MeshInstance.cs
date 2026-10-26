using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace MolecularSurfaces.Components.Instances
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class MeshInstance : Instance<Mesh>
    {
        public Material[] Materials = new Material[0];

        public override void Init()
        {
            base.Init();
            Materials = new Material[] { new Material(Shader.Find("Standard")) };
        }

        public void SetDebugMaterials(params Color[] color)
        {
            Materials = new Material[color.Length];
            for (int x = 0; x < color.Length; x++)
            {
                Materials[x] = new Material(Shader.Find("Standard"));
                Materials[x].color = color[x];
                Materials[x].SetFloat("_Metallic", 0);
                Materials[x].SetFloat("_Glossiness", 0.25f);
                ConfigureTransparency(Materials[x], color[x].a < 0.999f);
            }
        }

        public void SetOpacity(float opacity)
        {
            opacity = Mathf.Clamp01(opacity);
            foreach (var material in Materials)
            {
                if (material == null)
                    continue;

                var color = material.color;
                color.a = opacity;
                material.color = color;
                ConfigureTransparency(material, opacity < 0.999f);
            }
        }

        public void SetTransparent(bool transparent)
        {
            foreach (var material in Materials)
            {
                if (material != null)
                    ConfigureTransparency(material, transparent);
            }
        }

        private static void ConfigureTransparency(Material material, bool transparent)
        {
            if (transparent)
            {
                material.SetFloat("_Mode", 2f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.SetFloat("_Mode", 0f);
                material.SetOverrideTag("RenderType", string.Empty);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
            }
        }

        public void Render()
        {
            var _filter = GetComponent<MeshFilter>();
            var _renderer = GetComponent<MeshRenderer>();
            var _collider = GetComponent<MeshCollider>();

            _filter.sharedMesh = Value;
            _renderer.sharedMaterials = Materials;//Value.SubMeshes.Select(s => s.Material).ToArray();
            //_collider.sharedMesh = _filter.sharedMesh;
        }
    }
}
