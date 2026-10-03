Shader "Kamehameha/EnergyParticle"
{
    Properties { _Ring ("Ring", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float _Ring;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color; o.uv = v.uv; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv * 2 - 1);
                float glow = pow(saturate(1 - r * r), 3);
                float ring = (1 - smoothstep(0.035, 0.12, abs(r - 0.78))) * 0.8;
                return half4(i.color.rgb * 2, i.color.a * lerp(glow, ring, _Ring));
            }
            ENDHLSL
        }
    }
}
