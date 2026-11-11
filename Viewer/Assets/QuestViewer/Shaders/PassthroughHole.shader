// Punches a transparent hole in the eye buffer so passthrough shows through, while writing depth.
// Used on the hand mesh: your real hands appear in front of virtual objects and are hidden behind them.
Shader "QuestViewer/PassthroughHole"
{
    Properties
    {
        _Inflate ("Inflate (m)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry-10" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Inflate;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes v)
        {
            Varyings o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 posWS = TransformObjectToWorld(v.positionOS.xyz) + TransformObjectToWorldNormal(v.normalOS) * _Inflate;
            o.positionCS = TransformWorldToHClip(posWS);
            return o;
        }

        half4 frag(Varyings i) : SV_Target { return half4(0, 0, 0, 0); }
        ENDHLSL

        Pass
        {
            Name "PassthroughHole"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Back
            Blend Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
