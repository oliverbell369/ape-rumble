// The Ravine Sawmill boxes: pixel textures from a Texture2DArray, grass/dirt patches, simple light and fog.
Shader "Wrecked/Level"
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
                float4 data : TEXCOORD0; // xy = tiled uv, z = texture layer
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
                float layer = floor(i.data.z + 0.5);
                float3 tex = SAMPLE_TEXTURE2D_ARRAY(_TexArr, sampler_TexArr, i.data.xy, layer).rgb;
                if (layer < 0.5)
                {
                    // patches of dirt through the grass, with pixel-stepped edges
                    float2 q = floor(i.posWS.xz * 4.0) / 4.0;
                    float n = WreckedNoise(q * 0.3) * 0.7 + WreckedNoise(q * 0.9) * 0.3;
                    float3 dirt = SAMPLE_TEXTURE2D_ARRAY(_TexArr, sampler_TexArr, i.data.xy, 1).rgb;
                    tex = lerp(tex, dirt, step(0.62, n));
                }
                float3 N = normalize(i.normalWS);
                float ndl = max(dot(N, _LightDir.xyz), 0.0);
                float light = 0.5 + 0.42 * ndl + 0.16 * (0.5 + 0.5 * N.y);
                light *= lerp(0.55, 1.0, saturate((i.posWS.y + 20.0) / 22.0));
                float f = smoothstep(_FogNear, _FogFar, i.depth);
                float3 col = lerp(tex * i.color * light, _FogColor.rgb, f);
                return half4(WreckedOut(col), 1.0);
            }
            ENDHLSL
        }
    }
}
