// Opaque model parts: simple PBR-ish lighting (main light + ambient), section cut with a coloured cut face,
// clay mode, per-renderer highlight, and Meta depth occlusion on Quest.
// Globals (set by the viewer): _QV_CutPlane (xyz normal, w = -dot(n, p)), _QV_CutOn, _QV_Clay.
Shader "QuestViewer/Part"
{
    Properties
    {
        _Color ("Color", Color) = (0.8, 0.8, 0.8, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.4
        _Highlight ("Highlight", Color) = (0, 0, 0, 0)
        _EnvironmentDepthBias ("Environment depth bias", Float) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }

        Pass
        {
            Name "Part"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Metallic, _Smoothness;
                half4 _Highlight;
                float _EnvironmentDepthBias;
            CBUFFER_END

            float4 _QV_CutPlane;
            float _QV_CutOn;
            float _QV_Clay;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
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

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                bool cutting = _QV_CutOn > 0.5;
                if (cutting) clip(-(dot(_QV_CutPlane.xyz, i.positionWS) + _QV_CutPlane.w));

                half3 albedo = _QV_Clay > 0.5 ? half3(0.78, 0.76, 0.72) : _Color.rgb;
                half metallic = _QV_Clay > 0.5 ? 0 : _Metallic;
                half smooth = _QV_Clay > 0.5 ? 0.25 : _Smoothness;

                half4 col;
                if (!front && cutting)
                {
                    // Inside of a cut part: flat cross-section colour, like a CAD section view.
                    col = half4(lerp(albedo, half3(1.0, 0.45, 0.2), 0.65) * 0.9, 1);
                }
                else
                {
                    float3 n = normalize(front ? i.normalWS : -i.normalWS);
                    float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                    Light light = GetMainLight();
                    half ndl = saturate(dot(n, light.direction));
                    half3 diffuseColor = albedo * (1 - metallic * 0.85);
                    half3 specColor = lerp(half3(0.04, 0.04, 0.04), albedo, metallic);
                    float3 h = normalize(light.direction + v);
                    half specPower = exp2(10 * smooth + 1);
                    half spec = pow(saturate(dot(n, h)), specPower) * (specPower + 8) / 25.1327;
                    half3 ambient = SampleSH(n);
                    half3 rgb = diffuseColor * (ambient + light.color * ndl) + specColor * spec * light.color * ndl;
                    col = half4(rgb, 1);
                }
                col.rgb += _Highlight.rgb * _Highlight.a;

                float occlusion = META_DEPTH_GET_OCCLUSION_VALUE_WORLDPOS(i.positionWS, _EnvironmentDepthBias);
                return col * occlusion;
            }
            ENDHLSL
        }
    }
}
