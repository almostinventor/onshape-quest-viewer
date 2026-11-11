// Translucent blue hologram: glowing rim, analog scan lines, a sweeping bright band and a little flicker.
// A depth prepass keeps only the front surface, so overlapping parts don't stack up into a white blob.
// Respects Meta depth occlusion on Quest (real objects in front hide it).
Shader "QuestViewer/Hologram"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.85, 1, 1)
        _Alpha ("Body alpha", Range(0, 1)) = 0.16
        _RimPower ("Rim power", Float) = 2.5
        _RimStrength ("Rim strength", Float) = 1.3
        _LineDensity ("Scan lines per metre", Float) = 320
        _LineStrength ("Scan line strength", Range(0, 1)) = 0.35
        _LineSpeed ("Scan line drift (m/s)", Float) = 0.02
        _BandY ("Band world height", Float) = -1000
        _BandWidth ("Band width (m)", Float) = 0.025
        _BandStrength ("Band strength", Float) = 1.6
        _Flicker ("Flicker", Range(0, 1)) = 0.1
        _EnvironmentDepthBias ("Environment depth bias", Float) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Alpha, _RimPower, _RimStrength, _LineDensity, _LineStrength, _LineSpeed;
            float _BandY, _BandWidth;
            half _BandStrength, _Flicker;
            float _EnvironmentDepthBias;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "HologramDepth"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend One OneMinusSrcAlpha // premultiplied, blends correctly over passthrough

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

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
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - saturate(abs(dot(n, viewDir))), _RimPower) * _RimStrength;

                // Analog scan lines drifting upwards in world space.
                half lines = 0.5h + 0.5h * sin((i.positionWS.y + _Time.y * _LineSpeed) * _LineDensity * 6.2832);
                lines = lerp(1.0h, lines, _LineStrength);

                // Bright sweeping band.
                half band = saturate(1.0h - abs(i.positionWS.y - _BandY) / max(_BandWidth, 1e-4)) * _BandStrength;

                // Subtle flicker.
                half flicker = 1.0h - _Flicker * (0.5h + 0.5h * sin(_Time.y * 37.0 + sin(_Time.y * 13.0) * 3.0));

                half intensity = (_Alpha + rim) * lines * flicker + band;
                half alpha = saturate(intensity);
                half4 col = half4(_Color.rgb * intensity, alpha);

                float occlusion = META_DEPTH_GET_OCCLUSION_VALUE_WORLDPOS(i.positionWS, _EnvironmentDepthBias);
                return col * occlusion;
            }
            ENDHLSL
        }
    }
}
