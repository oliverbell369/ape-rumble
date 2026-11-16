// Canyon rocks, pines and glowing crystals: world-space triplanar pixel textures, emissive crystals, fog.
Shader "Wrecked/Scenery"
{
    Properties
    {
        _TexArr ("Textures", 2DArray) = "" {}
        _FogColor ("Fog Color", Vector) = (0.66, 0.76, 0.77, 1)
        _FogNear ("Fog Near", Float) = 26
        _FogFar ("Fog Far", Float) = 150
        _LightDir ("Light Direction", Vector) = (0.45, 0.8, -0.35, 0)
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float4 data : TEXCOORD0; // x = layer, y = 1 if glowing, z = texture scale
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 data : TEXCOORD0;
                float3 color : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 posWS : TEXCOORD3;
                float depth : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.posWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.depth = -TransformWorldToView(ws).z;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color.rgb;
                o.data = v.data;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 N = normalize(i.normalWS);
                float3 an = abs(N);
                float2 uv = an.y > max(an.x, an.z) ? i.posWS.xz : (an.x > an.z ? i.posWS.zy : i.posWS.xy);
                float layer = floor(i.data.x + 0.5);
                float3 tex = SAMPLE_TEXTURE2D_ARRAY(_TexArr, sampler_TexArr, uv * i.data.z, layer).rgb;
                float f = smoothstep(_FogNear, _FogFar, i.depth);
                float3 col;
                if (i.data.y > 0.5)
                {
                    col = i.color * (0.55 + 0.7 * tex.r) + 0.08;
                    f *= 0.45;
                }
                else
                {
                    float ndl = max(dot(N, _LightDir.xyz), 0.0);
                    col = tex * i.color * (0.48 + 0.44 * ndl + 0.16 * (0.5 + 0.5 * N.y));
                    col *= lerp(0.45, 1.0, saturate((i.posWS.y + 40.0) / 50.0));
                }
                return half4(WreckedOut(lerp(col, _FogColor.rgb, f)), 1.0);
            }
            ENDHLSL
        }
    }
}
