// Marks the pixels where a wall or a door is the nearest thing to the camera,
// so the see-through silhouette knows where it is allowed to draw. It is added
// as a second material on each wall and door renderer and paints no colour at
// all: it only sets one bit of the stencil (a per-pixel scratch mark the
// graphics card keeps beside the picture). It draws after every solid object,
// with the depth test passing only where the wall itself is frontmost, so a
// person standing in front of a wall hides that part of the mark.
//
// Only the lowest stencil bit is used. URP keeps the upper four bits for its
// own lighting passes and leaves the lower four to projects. Presentation only.
Shader "Paniq/Wall Mark"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+450"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        LOD 100

        Pass
        {
            Name "WallMark"
            Tags { "LightMode" = "UniversalForward" }

            // Equal depth: only where this wall is what the camera actually sees.
            ZTest LEqual
            ZWrite Off
            ColorMask 0
            Cull Back

            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
