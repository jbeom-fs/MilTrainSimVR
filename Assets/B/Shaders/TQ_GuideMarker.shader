// Tourniquet step marker ring (TourniquetGuideMarker LineRenderer). Unlit, two-sided, alpha blended, no depth write.
// Depth is tested against the scene as usual, but each vertex's depth is pulled toward the camera by _DepthPull metres
// (view space; the screen position is unchanged). A device surface within that distance in front of the ring no longer cuts
// it, while anything farther in front (another object, the far side of the scene) still hides it. Not ZTest Always.
// URP forward, single pass instanced XR (instance id and stereo output set up in the vertex stage).
Shader "B/Tourniquet/GuideMarker"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (0.3, 0.78, 1, 1)
        _DepthPull("Depth pull toward the camera (m)", Range(0, 0.15)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "GuideMarker"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _DepthPull;
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

                float3 positionVS = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz));
                float4 positionCS = TransformWViewToHClip(positionVS);
                // View space looks down -Z: pulling toward the camera raises z. Never past the near plane, never farther away.
                float nearZ = -(_ProjectionParams.y + 0.001);
                float pulledZ = max(positionVS.z, min(positionVS.z + _DepthPull, nearZ));
                float4 pulledCS = TransformWViewToHClip(float3(positionVS.xy, pulledZ));
                positionCS.z = pulledCS.z / pulledCS.w * positionCS.w;

                output.positionCS = positionCS;
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
}
