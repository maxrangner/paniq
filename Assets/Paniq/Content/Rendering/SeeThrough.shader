// A pale silhouette drawn only where a wall or a door is nearer the camera, so
// people and objects behind a wall can still be seen. It is added as a second
// material on a renderer, which draws the same mesh a second time. The depth
// test finds the hidden parts; the stencil test (the mark "Paniq/Wall Mark"
// leaves on walls and doors) keeps only the ones hidden by a wall. Without the
// stencil test a chair would show its own back legs through its own seat, and
// anything under a table would ghost across the table top. Presentation only.
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

            // ...and only where that something is a wall or a door.
            Stencil
            {
                Ref 1
                ReadMask 1
                Comp Equal
            }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            // The fire's glow is drawn in batches of many cubes at once.
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GhostColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
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
