// See-through look: faint body with glowing edges, every surface visible (inner parts too).
// Used for X-ray style, ghosted parts while inspecting, and the fading "previous version" ghost.
// Honours the section cut and Meta depth occlusion.
Shader "QuestViewer/XRay"
{
    Properties
    {
        _Color ("Color", Color) = (0.75, 0.9, 1, 1)
        _Alpha ("Body alpha", Range(0, 1)) = 0.05
        _RimPower ("Rim power", Float) = 2.2
        _RimStrength ("Rim strength", Float) = 0.7
        _Fade ("Fade", Range(0, 1)) = 1
        _EnvironmentDepthBias ("Environment depth bias", Float) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }

        Pass
        {
            Name "XRay"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Alpha, _RimPower, _RimStrength, _Fade;
                float _EnvironmentDepthBias;
            CBUFFER_END

            float4 _QV_CutPlane;
            float _QV_CutOn;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (_QV_CutOn > 0.5) clip(-(dot(_QV_CutPlane.xyz, i.positionWS) + _QV_CutPlane.w));
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - saturate(abs(dot(n, v))), _RimPower) * _RimStrength;
                half a = saturate(_Alpha + rim) * _Fade;
                half4 col = half4(_Color.rgb * a, a);
                float occlusion = META_DEPTH_GET_OCCLUSION_VALUE_WORLDPOS(i.positionWS, _EnvironmentDepthBias);
                return col * occlusion;
            }
            ENDHLSL
        }
    }
}
