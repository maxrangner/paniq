// A pale silhouette drawn only where something else is nearer the camera, so
// people and objects behind a wall can still be seen. It is added as a second
// material on a renderer, which draws the same mesh a second time; the depth
// test does the rest. Presentation only.
Shader "Paniq/See-Through"
{
    Properties
    {
        _GhostColor ("Colour", Color) = (0.65, 0.78, 0.95, 0.3)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        LOD 100

        Pass
        {
            Name "SeeThrough"
            Tags { "LightMode" = "UniversalForward" }

            // Greater: only the parts hidden behind something already drawn.
            ZTest Greater
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GhostColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return half4(_GhostColor.rgb, _GhostColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
