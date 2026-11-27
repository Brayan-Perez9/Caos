Shader "Caos/Grass"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.12, 0.42, 0.14, 1)
        _BladeColor ("Brillo de hoja", Color) = (0.38, 0.78, 0.26, 1)
        _SoilColor ("Tierra entre hojas", Color) = (0.26, 0.18, 0.10, 1)
        _Scale ("Densidad", Float) = 2.4
        _WindSpeed ("Velocidad del viento", Float) = 0.9
        _WindStrength ("Amplitud del viento", Range(0, 0.4)) = 0.06
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
            Name "CaosGrass"
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
                float4 _BladeColor;
                float4 _SoilColor;
                float _Scale;
                float _WindSpeed;
                float _WindStrength;
                float4 _LightDir;
            CBUFFER_END

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
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
                // El viento desplaza el patron en mundo: se ve moverse sin mallas nuevas.
                float2 p = input.positionWS.xz * _Scale;
                p += float2(
                    sin(_Time.y * _WindSpeed + input.positionWS.x * 0.35),
                    cos(_Time.y * _WindSpeed * 0.7 + input.positionWS.z * 0.35)
                ) * _WindStrength;

                float broad = Noise(p);
                float fine = Noise(p * 3.7 + broad);
                float micro = Noise(p * 8.1 + fine * 1.7);
                float blades = saturate(broad * 0.30 + fine * 0.50 + micro * 0.32);

                float3 color = lerp(_BaseColor.rgb, _BladeColor.rgb, blades);

                // Dos escalas de mancha: evita el aspecto de alfombra uniforme.
                float patches = Noise(p * 0.32 + 11.0) * 0.65
                              + Noise(p * 0.17 + 29.0) * 0.35;
                color = lerp(color, _SoilColor.rgb,
                    smoothstep(0.58, 0.86, patches) * 0.38);

                // Brillos mínimos tipo rocío, sin texturas ni emisión real.
                float dew = saturate(micro - 0.74) * 3.8;
                color += _BladeColor.rgb * dew * dew * 0.16;

                // Sombreado falso con la normal: da volumen sin pasar a Lit.
                float ndl = saturate(dot(normalize(input.normalWS),
                    normalize(_LightDir.xyz))) * 0.5 + 0.5;
                color *= lerp(0.72, 1.16, ndl);

                float radius = length(input.positionWS.xz);
                color *= lerp(0.82, 1.0, 1.0 - smoothstep(28.0, 48.0, radius));

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
