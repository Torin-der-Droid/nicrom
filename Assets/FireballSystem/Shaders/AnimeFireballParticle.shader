Shader "FireballSystem/Anime Fireball Particle URP"
{
    Properties
    {
        [MainTexture] _BaseMap ("Particle", 2D) = "white" {}
        [HDR] _TintColor ("Tint Color", Color) = (1, .25, .02, 1)
        _EmissionStrength ("Emission Strength", Range(0, 20)) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+90" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "AnimeFireballParticle"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _TintColor;
                float _EmissionStrength;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float alpha = tex.a * input.color.a * _TintColor.a;
                clip(alpha - .008);
                float3 color = tex.rgb * input.color.rgb * _TintColor.rgb * _EmissionStrength;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
