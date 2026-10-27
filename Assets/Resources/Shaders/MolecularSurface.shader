// Standard-lit surface coloured by vertex colour, with a soft rim term.
Shader "MolecularSurfaces/Surface"
{
    Properties
    {
        _Glossiness ("Smoothness", Range(0, 1)) = 0.35
        _Metallic ("Metallic", Range(0, 1)) = 0
        _RimColor ("Rim Color", Color) = (0.65, 0.78, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.2
        // Read by Hidden/MolecularSurfaces/ChainId when drawing the outline ID buffer.
        _ChainId ("Chain Id", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
            float3 worldNormal;
            float3 viewDir;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _RimColor;
        half _RimPower;
        half _RimStrength;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = IN.color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            half rim = 1 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal)));
            o.Emission = _RimColor.rgb * pow(rim, _RimPower) * _RimStrength;
            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
