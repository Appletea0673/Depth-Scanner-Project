Shader "Appletea's Shader/Depth/RealDepthMonitor v1.0"
{
    SubShader
    {
        Cull Off

        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/BiRP/EnvironmentOcclusionBiRP.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                //GPU Instancing処理
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                
                //Single Path Stereo処理
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                
                //Single Path Stereo and GPU Instancing処理
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                //const float inputDepthEye = UNITY_SAMPLE_TEX2DARRAY(_EnvironmentDepthTexture, float3(uv, (float)unity_StereoEyeIndex)).r;
                const float inputDepthEye = UNITY_SAMPLE_TEX2DARRAY(_EnvironmentDepthTexture, float3(i.uv, 0)).r;

                fixed4 col = fixed4(inputDepthEye, inputDepthEye, inputDepthEye, 1);

                return col;
            }
            ENDCG
        }
    }
}