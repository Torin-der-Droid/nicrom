Shader "FireballSystem/Anime Fireball Core URP"
{
    Properties
    {
        [HDR] _InnerColor ("Inner Color", Color) = (1, 1, .72, 1)
        [HDR] _OuterColor ("Outer Color", Color) = (1, .16, .01, 1)
        _EmissionStrength ("Emission Strength", Range(0, 20)) = 8
        _NoiseScale ("Noise Scale", Range(1, 20)) = 7
        _NoiseSpeed ("Noise Speed", Range(0, 10)) = 3
    }
    SubShader
    {
        Tags { "Queue"="Transparent+80" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "AnimeFireballCore"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            CBUFFER_START(UnityPerMaterial)
                float4 _InnerColor;
                float4 _OuterColor;
                float _EmissionStrength;
                float _NoiseScale;
                float _NoiseSpeed;
            CBUFFER_END

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float fresnel = pow(1.0 - saturate(dot(normalize(input.normalWS), normalize(input.viewWS))), 2.1);
                float n = Noise(input.uv * _NoiseScale + float2(0, -_Time.y * _NoiseSpeed));
                float3 color = lerp(_InnerColor.rgb, _OuterColor.rgb, saturate(fresnel + n * .32));
                float intensity = _EmissionStrength * lerp(1.25, .8, fresnel);
                return half4(color * intensity, saturate(.82 + fresnel * .18));
            }
            ENDHLSL
        }
    }
}
