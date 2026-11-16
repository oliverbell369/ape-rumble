// Sky dome that follows the camera: gradient from the horizon haze to blue, darker below (the ravine).
Shader "Wrecked/Sky"
{
    Properties
    {
        _Top ("Top", Vector) = (0.36, 0.58, 0.77, 1)
        _Horizon ("Horizon", Vector) = (0.66, 0.76, 0.77, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" "PreviewType" = "Skybox" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WreckedCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Top;
                float4 _Horizon;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float h = normalize(i.dir).y;
                float3 c = lerp(_Horizon.rgb, _Top.rgb, smoothstep(0.02, 0.55, h));
                c = lerp(c, _Horizon.rgb * float3(0.55, 0.68, 0.7), smoothstep(-0.02, -0.5, h));
                return half4(WreckedOut(c), 1.0);
            }
            ENDHLSL
        }
    }
}
