Shader "Unlit/ShowDepthMap"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            
            Texture2DArray_half _PreprocessedEnvironmentDepthTexture;
            SamplerState sampler_PreprocessedEnvironmentDepthTexture;
            Texture2DArray_half _EnvironmentDepthTexture;
            SamplerState sampler_EnvironmentDepthTexture;
            struct Attribures
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceId : SV_InstanceID;
            };

            struct Interpolators
            {
                float2 uv : TEXCOORD0;                
                float4 vertex : SV_POSITION;
                uint depthSlice : SV_RenderTargetArrayIndex;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;            

            Interpolators vert (Attribures v)
            {
                Interpolators o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);                
                o.depthSlice = v.instanceId;
                return o;
            }

            fixed4 frag (Interpolators i) : SV_Target
            {
                float3 uv = float3(i.uv, i.depthSlice);
                fixed4 col = _PreprocessedEnvironmentDepthTexture.Sample(sampler_PreprocessedEnvironmentDepthTexture, uv);
                //fixed4 col = _EnvironmentDepthTexture.Sample(sampler_EnvironmentDepthTexture, uv);
                col.a = 1;
                return col;
            }
            ENDCG
        }
    }
}