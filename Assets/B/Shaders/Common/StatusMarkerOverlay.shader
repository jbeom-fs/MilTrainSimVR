// Shared status marker overlay (B): an unlit colour drawn on top of the scene, for thin markers such as rings, dots and check
// marks that must stay readable over the objects they mark. Generic: no feature code; when a marker shows and in which state is
// decided by the feature that owns it (it switches the Renderer or picks a material).
//
// - Fully on top: ZTest Always (never hidden by the marked object, hands or tools) and drawn after the transparent objects
//   (Queue Overlay = 4000). ZWrite Off, so it never hides or depth-tests anything drawn after it.
// - Unlit: _BaseColor times the vertex colour (LineRenderer colours work); alpha blends, so the colour or alpha can be lowered.
// - Both faces (Cull Off), so a flat ring reads from either side.
// - URP forward and XR: single pass instanced / multiview stereo through the URP instancing and stereo macros.
Shader "B/Common/StatusMarkerOverlay"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "DisableBatching" = "False"
        }

        Pass
        {
            Name "StatusMarkerOverlay"
            Tags { "LightMode" = "UniversalForward" }

            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
