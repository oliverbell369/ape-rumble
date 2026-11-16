// Apes, hands, the swinging log and ropes: object-space triplanar fur/bark, per-object tint (instancing-ready), fog.
Shader "Wrecked/Ape"
{
    Properties
    {
        _TexArr ("Textures", 2DArray) = "" {}
        _FogColor ("Fog Color", Vector) = (0.66, 0.76, 0.77, 1)
        _FogNear ("Fog Near", Float) = 26
        _FogFar ("Fog Far", Float) = 150
        _LightDir ("Light Direction", Vector) = (0.45, 0.8, -0.35, 0)
        _Tint ("Tint", Vector) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma require 2darray
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WreckedCommon.hlsl"

            TEXTURE2D_ARRAY(_TexArr);
            SAMPLER(sampler_TexArr);

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _FogNear;
                float _FogFar;
                float4 _LightDir;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float4 data : TEXCOORD0; // x = layer (-1 = flat colour), y = 1 if tinted by the ape colour
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 color : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 posOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
                float layer : TEXCOORD4;
                float depth : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.depth = -TransformWorldToView(ws).z;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.posOS = v.positionOS.xyz;
                o.normalOS = v.normalOS;
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Tint);
                o.color = v.data.y > 0.5 ? v.color.rgb * tint.rgb : v.color.rgb;
                o.layer = v.data.x;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 base = i.color;
                if (i.layer > -0.5)
                {
                    float3 an = abs(i.normalOS);
                    float2 uv = an.y > max(an.x, an.z) ? i.posOS.xz : (an.x > an.z ? i.posOS.zy : i.posOS.xy);
                    base *= SAMPLE_TEXTURE2D_ARRAY(_TexArr, sampler_TexArr, uv * 2.2, floor(i.layer + 0.5)).rgb;
                }
                float3 N = normalize(i.normalWS);
                float ndl = max(dot(N, _LightDir.xyz), 0.0);
                float3 col = base * (0.55 + 0.42 * ndl + 0.15 * (0.5 + 0.5 * N.y));
                col = lerp(col, _FogColor.rgb, smoothstep(_FogNear, _FogFar, i.depth));
                return half4(WreckedOut(col), 1.0);
            }
            ENDHLSL
        }
    }
}
