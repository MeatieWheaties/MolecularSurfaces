// Screen-space effects for the molecular viewer (built-in pipeline):
//   Pass 0  ambient occlusion from the camera depth texture
//   Pass 1  depth-aware blur of the AO (run once per axis)
//   Pass 2  composite: AO, chain outlines from the ID buffer, gradient background
Shader "Hidden/MolecularSurfaces/SurfaceEffects"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    float4 _CameraDepthTexture_TexelSize;

    sampler2D _IdTex;
    float4 _IdTex_TexelSize;
    sampler2D _AOTex;

    float2 _TanHalfFov;     // tan(fov/2) * (aspect, 1)
    float _AORadius;
    float _AOIntensity;
    float _AOBias;
    float2 _BlurDirection;
    float _OutlineWidth;
    float4 _OutlineColor;
    float4 _BackgroundTop;
    float4 _BackgroundBottom;
    float _DebugView;       // 0 final, 1 AO buffer, 2 chain IDs

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert (appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    float RawDepth(float2 uv) { return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv); }
    bool IsBackground(float raw) { return Linear01Depth(raw) > 0.9999; }

    // Eye-space position with +z pointing away from the camera.
    float3 ViewPosition(float2 uv, float raw)
    {
        float depth = LinearEyeDepth(raw);
        return float3((uv * 2 - 1) * _TanHalfFov * depth, depth);
    }

    float3 ViewPosition(float2 uv) { return ViewPosition(uv, RawDepth(uv)); }

    float2 ProjectToUV(float3 p) { return (p.xy / (p.z * _TanHalfFov)) * 0.5 + 0.5; }

    float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0: ambient occlusion. Rendered from a source that shares the depth texture's orientation.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            static const int SAMPLES = 16;

            fixed4 frag (v2f i) : SV_Target
            {
                float raw = RawDepth(i.uv);
                if (IsBackground(raw)) return 1;

                float3 p = ViewPosition(i.uv, raw);

                // Normal from neighbouring depths, taking the smaller step on each axis
                // so silhouettes don't smear into the background.
                float2 t = _CameraDepthTexture_TexelSize.xy;
                float3 pr = ViewPosition(i.uv + float2(t.x, 0));
                float3 pl = ViewPosition(i.uv - float2(t.x, 0));
                float3 pu = ViewPosition(i.uv + float2(0, t.y));
                float3 pd = ViewPosition(i.uv - float2(0, t.y));
                float3 dx = abs(pr.z - p.z) < abs(p.z - pl.z) ? pr - p : p - pl;
                float3 dy = abs(pu.z - p.z) < abs(p.z - pd.z) ? pu - p : p - pd;
                float3 n = normalize(cross(dy, dx));
                if (dot(n, p) > 0) n = -n;

                float3 random = normalize(float3(
                    Hash(i.uv * _ScreenParams.xy),
                    Hash(i.uv * _ScreenParams.xy + 17.17),
                    Hash(i.uv * _ScreenParams.xy + 43.43)) * 2 - 1);

                float occlusion = 0;
                [unroll]
                for (int s = 0; s < SAMPLES; s++)
                {
                    // Spiral of directions with increasing length, rotated per pixel.
                    float k = (s + 0.5) / SAMPLES;
                    float phi = s * 2.39996;
                    float z = 1 - 2 * k;
                    float r = sqrt(1 - z * z);
                    float3 dir = float3(cos(phi) * r, sin(phi) * r, z);
                    dir = reflect(dir, random);
                    dir *= sign(dot(dir, n));
                    dir *= lerp(0.15, 1.0, k * k);

                    float3 sp = p + (dir + n * 0.1) * _AORadius;
                    float2 suv = ProjectToUV(sp);
                    float sceneDepth = LinearEyeDepth(RawDepth(suv));

                    float range = saturate(_AORadius / abs(p.z - sceneDepth));
                    occlusion += (sceneDepth < sp.z - _AOBias ? 1.0 : 0.0) * range;
                }

                // Squared so crevices read clearly once the result is shown in sRGB.
                float open = 1 - occlusion / SAMPLES;
                return open * open;
            }
            ENDCG
        }

        // 1: separable depth-aware blur.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            fixed4 frag (v2f i) : SV_Target
            {
                float center = LinearEyeDepth(RawDepth(i.uv));
                float sum = 0;
                float weights = 0;

                [unroll]
                for (int s = -4; s <= 4; s++)
                {
                    float2 uv = i.uv + _BlurDirection * _MainTex_TexelSize.xy * s;
                    float depth = LinearEyeDepth(RawDepth(uv));
                    float w = exp(-s * s / 8.0) * saturate(1 - abs(depth - center) / (_AORadius * 0.5));
                    sum += tex2D(_MainTex, uv).r * w;
                    weights += w;
                }

                return sum / max(weights, 1e-4);
            }
            ENDCG
        }

        // 2: composite.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float Id(float2 uv) { return tex2D(_IdTex, uv).r; }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // With anti-aliasing on D3D-like platforms the source is flipped
                // relative to the depth/ID/AO textures.
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) uv.y = 1 - uv.y;
                #endif

                fixed4 color = tex2D(_MainTex, i.uv);
                float id = Id(uv);

                if (_DebugView > 1.5) return fixed4(frac(id * 255 * 0.37), frac(id * 255 * 0.61), frac(id * 255 * 0.83), 1) * (id > 0);
                if (_DebugView > 0.5) return tex2D(_AOTex, uv).rrrr;

                if (id <= 0)
                {
                    float2 c = uv - 0.5;
                    float vignette = 1 - dot(c, c) * 0.6;
                    color.rgb = lerp(_BackgroundBottom.rgb, _BackgroundTop.rgb, uv.y) * vignette;
                }
                else
                {
                    color.rgb *= lerp(1, tex2D(_AOTex, uv).r, _AOIntensity);
                }

                // Outline wherever a neighbouring pixel belongs to a different chain
                // (or to the background), averaged over 8 taps for soft edges.
                if (_OutlineWidth > 0)
                {
                    float2 o = _IdTex_TexelSize.xy * _OutlineWidth;
                    float edge = 0;
                    edge += abs(Id(uv + float2( o.x, 0)) - id) > 0.001;
                    edge += abs(Id(uv + float2(-o.x, 0)) - id) > 0.001;
                    edge += abs(Id(uv + float2(0,  o.y)) - id) > 0.001;
                    edge += abs(Id(uv + float2(0, -o.y)) - id) > 0.001;
                    edge += abs(Id(uv + o * 0.7071) - id) > 0.001;
                    edge += abs(Id(uv - o * 0.7071) - id) > 0.001;
                    edge += abs(Id(uv + float2(o.x, -o.y) * 0.7071) - id) > 0.001;
                    edge += abs(Id(uv + float2(-o.x, o.y) * 0.7071) - id) > 0.001;
                    color.rgb = lerp(color.rgb, _OutlineColor.rgb, saturate(edge / 3) * _OutlineColor.a);
                }

                return color;
            }
            ENDCG
        }
    }
}
