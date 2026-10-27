// Replacement shader: writes each surface's _ChainId into the red channel so the
// effects pass can draw outlines wherever the chain changes. 0 = background.
Shader "Hidden/MolecularSurfaces/ChainId"
{
    Properties
    {
        _ChainId ("Chain Id", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _ChainId;

            float4 vert (float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag () : SV_Target
            {
                // Keep IDs in 1..255 so they survive an 8-bit target.
                float id = fmod(max(_ChainId, 1) - 1, 255) + 1;
                return fixed4(id / 255.0, 0, 0, 1);
            }
            ENDCG
        }
    }
}
