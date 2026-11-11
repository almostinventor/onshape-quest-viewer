// HUD lines and shapes: unlit vertex colour x _Color, always on top, premultiplied alpha over passthrough.
Shader "QuestViewer/HudLine"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.9, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay" "RenderType" = "Transparent" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "HudLine"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color * _Color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return half4(i.color.rgb * i.color.a, i.color.a); }
            ENDHLSL
        }
    }
}
