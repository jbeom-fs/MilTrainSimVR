// 가짜 빛 기둥 (11/02, Docs/A/1102_빛줄기_표현방법.md 1번) — 스포트 조명 모양의 원뿔 메시에 입혀 공중의 빛줄기를 보이게 한다.
// 빛을 더하기만 한다 (아무것도 밝히지 않음 — 표면 밝히기는 진짜 Light 가 한다). 양면, 깊이 쓰기 없음.
//   · 길이: UV v 0(조명) → 1(끝) 으로 옅어진다
//   · 테두리: 시선과 면이 비스듬할수록 옅다 (Fresnel) → 딱딱한 원뿔 테두리 대신 부드러운 기둥
//   · 다른 물체와 만나는 곳: 장면 깊이와의 차이가 작을수록 옅다 (Depth Fade) — URP Depth Texture 필요 (PC_RPAsset 에 켜져 있음)
//   · 카메라 가까이: 옅어진다 — VR 에서 머리가 빛 기둥 안에 들어가도 화면이 뿌옇지 않게
//   · 옆면 줄 없애기 (11/02): 법선을 픽셀마다 원뿔 축에서 다시 구하고(조각 경계 없음), 화면 노이즈로 디더링(8비트 계단 없음)
// VR Single Pass Instanced 용 매크로를 넣었다.
Shader "MilTrainSimVR/A/LightShaft"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.9, 0.75, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.1
        _EdgePower ("Edge Softness (Fresnel power)", Range(0.5, 8)) = 2
        _LengthPower ("Length Fade Power", Range(0.1, 4)) = 1.2
        _DepthFade ("Depth Fade (m)", Range(0.01, 2)) = 0.3
        _CameraFadeNear ("Camera Fade Near (m)", Float) = 0.3
        _CameraFadeFar ("Camera Fade Far (m)", Float) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "LightShaft"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgePower;
                half _LengthPower;
                float _DepthFade;
                float _CameraFadeNear;
                float _CameraFadeFar;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                float slope : TEXCOORD4;   // 옆면 법선의 뒤로 기운 정도 (원뿔이 퍼지는 정도)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.positionOS = v.positionOS.xyz;
                o.slope = v.normalOS.z / max(length(v.normalOS.xy), 1e-4);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(p.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // 원뿔 축(+Z)에서 바깥으로 — 조각마다 끊기지 않는 매끈한 법선
                float2 radial = normalize(i.positionOS.xy + 1e-6);
                float3 normalWS = normalize(TransformObjectToWorldNormal(float3(radial, i.slope)));
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half edge = pow(saturate(abs(dot(normalWS, viewDir))), _EdgePower);
                half lengthFade = pow(saturate(1.0 - i.uv.y), _LengthPower);

                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                half depthFade = saturate((sceneDepth - i.screenPos.w) / _DepthFade);

                float cameraDistance = distance(GetCameraPositionWS(), i.positionWS);
                half cameraFade = saturate((cameraDistance - _CameraFadeNear) / max(_CameraFadeFar - _CameraFadeNear, 0.001));

                half a = edge * lengthFade * depthFade * cameraFade * _Intensity;
                // 디더링 — 화면 위치마다 ±0.5/255 노이즈로 어두운 그라데이션의 계단을 흩뜨린다
                float noise = InterleavedGradientNoise(screenUV * _ScreenParams.xy, 0) - 0.5;
                return half4(max(0, _Color.rgb * a + noise / 255.0), 0);
            }
            ENDHLSL
        }
    }
}
