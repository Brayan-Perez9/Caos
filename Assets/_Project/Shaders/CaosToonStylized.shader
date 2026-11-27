Shader "Caos/ToonStylized"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Color de sombra", Color) = (0.24, 0.30, 0.42, 1)
        _RimColor ("Color del borde", Color) = (0.35, 0.82, 1, 1)
        _RimPower ("Suavidad del borde", Range(0.5, 8)) = 3.2
        _RimStrength ("Intensidad del borde", Range(0, 1)) = 0.28
        _Emission ("Emision", Range(0, 2)) = 0
        _Smoothness ("Brillo", Range(0, 1)) = 0.28
        _NoiseScale ("Variacion superficial", Float) = 1.6
        _LightDir ("Direccion de luz", Vector) = (0.45, 0.85, 0.30, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        // La luz y las sombras usan el mismo pase que URP/Lit para que los
        // objetos redondeados proyecten sombras sin duplicar lógica de shader.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"

        Pass
        {
            Name "CaosToonStylized"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
                float _Emission;
                float _Smoothness;
                float _NoiseScale;
                float4 _LightDir;
            CBUFFER_END

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 lightDir = normalize(_LightDir.xyz);

                // Vertex color permite variacion dentro de una sola malla (flores, hierba, copas).
                float3 base = _BaseColor.rgb * input.color.rgb;
                float ndl = saturate(dot(normal, lightDir)) * 0.5 + 0.5;
                float3 shade = lerp(_ShadowColor.rgb, base, 0.42);
                float3 color = lerp(shade, base, smoothstep(0.10, 0.88, ndl));

                // Fresnel suave: separa siluetas sin contorno negro.
                float rim = pow(1.0 - saturate(dot(normal, viewDir)), _RimPower);
                color += _RimColor.rgb * rim * _RimStrength;

                // Reflejo stylized, dependiente de la suavidad del material.
                float3 halfDir = normalize(viewDir + lightDir);
                float gloss = lerp(8.0, 48.0, _Smoothness);
                float spec = pow(saturate(dot(normal, halfDir)), gloss) * _Smoothness * 0.32;
                color += spec;

                // Ruido barato por celda de mundo rompe la repeticion sin texturas.
                float variation = Hash21(floor(input.positionWS.xz * _NoiseScale));
                color *= lerp(0.94, 1.06, variation);
                color += base * _Emission;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
