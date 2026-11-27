Shader "Caos/Dirt"
{
    Properties
    {
        _BaseColor ("Tierra", Color) = (0.34, 0.22, 0.12, 1)
        _LightColor ("Reflejo", Color) = (0.52, 0.38, 0.22, 1)
        _DeepColor ("Franjas", Color) = (0.20, 0.13, 0.08, 1)
        _Scale ("Grano", Float) = 3.0
        _Strata ("Franjas verticales", Float) = 8.0
        _LightDir ("Direccion de luz", Vector) = (0.4, 0.8, 0.25, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "CaosDirt"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _LightColor;
                float4 _DeepColor;
                float _Scale;
                float _Strata;
                float4 _LightDir;
            CBUFFER_END

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Normalizar sin dividir por cero: evita NaN en el buffer HDR.
            float3 CaosSafeNormalize(float3 v)
            {
                float lenSq = max(dot(v, v), 1e-8);
                return v * rsqrt(lenSq);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz * _Scale;
                float grain = Noise(p);
                float pebbles = step(0.88, Hash21(floor(p * 2.3)));

                // Capas de tierra segun la altura: la escalera parece un talud.
                float strata = sin(input.positionWS.y * _Strata
                    + Noise(p * 0.6) * 2.5) * 0.5 + 0.5;

                float3 color = lerp(_BaseColor.rgb, _LightColor.rgb, grain * 0.55);
                color = lerp(color, _DeepColor.rgb, strata * 0.28);
                color += _LightColor.rgb * pebbles * 0.10;

                float ndl = saturate(dot(CaosSafeNormalize(input.normalWS),
                    CaosSafeNormalize(_LightDir.xyz))) * 0.5 + 0.5;
                color *= lerp(0.74, 1.10, ndl);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
