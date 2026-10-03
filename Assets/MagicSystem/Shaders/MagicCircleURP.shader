Shader "MagicSystem/Golden Magic Circle URP"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint Color", Color) = (1, 0.55, 0.08, 1)
        _EmissionStrength ("Emission Strength", Range(0, 12)) = 3
        _Alpha ("Alpha", Range(0, 1)) = 1
        [Toggle] _UseRuneMask ("Use Rune Mask", Float) = 0
        _RuneIndex ("Rune Index", Range(0, 11)) = 0
        _RuneAngleOffset ("Rune Angle Offset", Range(-180, 180)) = 0
        _RuneHalfWidth ("Rune Half Width", Range(5, 15)) = 13
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }
        Pass
        {
            Name "MagicCircleUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _TintColor;
                float _EmissionStrength;
                float _Alpha;
                float _UseRuneMask;
                float _RuneIndex;
                float _RuneAngleOffset;
                float _RuneHalfWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float RuneSector(float2 uv)
            {
                float2 centered = uv - 0.5;
                float angle = degrees(atan2(centered.x, centered.y));
                if (angle < 0) angle += 360;
                float center = fmod(_RuneIndex * 30 + _RuneAngleOffset + 360, 360);
                float delta = abs(fmod(angle - center + 540, 360) - 180);
                float angularMask = 1 - smoothstep(_RuneHalfWidth - 1, _RuneHalfWidth, delta);
                float radius = length(centered) * 2;
                float ringMask = smoothstep(0.48, 0.58, radius) * (1 - smoothstep(0.98, 1.05, radius));
                return angularMask * ringMask;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 textureColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float mask = lerp(1, RuneSector(input.uv), saturate(_UseRuneMask));
                float alpha = textureColor.a * _Alpha * _TintColor.a * mask;
                clip(alpha - 0.001);
                float3 color = textureColor.rgb * _TintColor.rgb * _EmissionStrength;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
