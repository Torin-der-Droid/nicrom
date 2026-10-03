Shader "MagicSystem/Golden Center Beam URP"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1, 0.62, 0.08, 1)
        _EmissionStrength ("Emission Strength", Range(0, 24)) = 12
        _Alpha ("Alpha", Range(0, 1)) = 1
        _TearProgress ("Tear Progress", Range(0, 1.2)) = 0
        _Flicker ("Flicker", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent+120"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }
        Pass
        {
            Name "CenterBeamUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
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
                float3 positionOS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float _EmissionStrength;
                float _Alpha;
                float _TearProgress;
                float _Flicker;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 tearCell = floor(float2(frac(input.uv.x) * 18.0, input.uv.y * 11.0));
                float cellNoise = Hash21(tearCell);
                float tearThreshold = lerp(0.12, 0.94, cellNoise);
                float chunkVisible = 1.0 - smoothstep(tearThreshold - 0.035, tearThreshold + 0.035, _TearProgress);
                float tornEdge = 1.0 - saturate(abs(_TearProgress - tearThreshold) / 0.085);

                float movingBands = 0.84 + 0.16 * sin((input.uv.y * 9.0 - _Time.y * 7.5) * 6.28318);
                float whiteCore = saturate(1.25 - length(input.positionOS.xz) * 1.6);
                float flickerAmount = 1.0 - _Flicker * Hash21(tearCell + floor(_Time.y * 42.0));
                float alpha = _Alpha * chunkVisible * movingBands * flickerAmount;
                clip(alpha - 0.015);

                float3 gold = _TintColor.rgb * _EmissionStrength;
                float3 core = lerp(gold, float3(1.0, .96, .7) * _EmissionStrength * 1.3, whiteCore);
                core += float3(1.0, .72, .12) * tornEdge * _EmissionStrength * 1.8;
                return half4(core, alpha * _TintColor.a);
            }
            ENDHLSL
        }
    }
}
