Shader "Caos/NeonGrid"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.035, 0.055, 0.09, 1)
        _GridColor ("Lineas", Color) = (0.10, 0.85, 1.0, 1)
        _AccentColor ("Acento", Color) = (1.0, 0.25, 0.55, 1)
        _GridScale ("Escala de rejilla", Float) = 1.0
        _LineWidth ("Grosor de linea", Range(0.001, 0.2)) = 0.035
        _PulseSpeed ("Velocidad de pulso", Float) = 1.4
        _FadeStart ("Inicio de penumbra", Float) = 18.0
        _FadeEnd ("Fin de penumbra", Float) = 34.0
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
            Name "CaosNeonGrid"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            // Un bloque por material permite al SRP Batcher agrupar los suelos.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _GridColor;
                float4 _AccentColor;
                float _GridScale;
                float _LineWidth;
                float _PulseSpeed;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            // Devuelve 1 cerca de cada entero: max() evita ramas por pixel.
            float GridLine(float coordinate, float width)
            {
                float distanceToLine = abs(frac(coordinate) - 0.5);
                return smoothstep(0.5 - width, 0.5, distanceToLine);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 grid = input.positionWS.xz * _GridScale;
                float lines = max(GridLine(grid.x, _LineWidth),
                                  GridLine(grid.y, _LineWidth));
                float major = max(GridLine(grid.x * 0.2, _LineWidth * 0.35),
                                  GridLine(grid.y * 0.2, _LineWidth * 0.35));

                // Pulso en mundo, con tiempo absoluto: no acumula error por frame.
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed
                    + input.positionWS.z * 0.35);

                float3 color = _BaseColor.rgb;
                color = lerp(color, _GridColor.rgb, lines * 0.85);
                color = lerp(color, _AccentColor.rgb, major * 0.45);
                color += _GridColor.rgb * lines * pulse * 0.16;

                // Vignette de mundo: el centro queda legible y el borde oscurece.
                float radius = length(input.positionWS.xz);
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, radius);
                color *= lerp(0.55, 1.0, fade);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
