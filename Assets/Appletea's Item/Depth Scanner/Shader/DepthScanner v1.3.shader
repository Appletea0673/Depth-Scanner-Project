Shader "Appletea's Shader/Depth/RealDepthScanner v1.3"
{
    Properties
    {
        [HDR]_Color("Line Color", Color) = (0.5, 1, 0.5, 1)
        _speed ("Scanning Speed", Range(0.01, 50)) = 10
        _width ("Grid Width", Range(0, 0.5)) = 0.05
        _repeat ("Repeat", Range(1, 1000)) = 5
        _error ("Ray Width", Range(0, 1)) = 0.01
    }
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
                
                //GPU Instancing処理
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 cp : TEXCOORD1;
                float3 rd : TEXCOORD2;
                float3 pos : TEXCOORD3;
                
                //Single Path Stereo処理
                UNITY_VERTEX_OUTPUT_STEREO
            };

            //UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            uniform float4 _Color;
            uniform float _speed;
            uniform float _width;
            uniform float _repeat;
            uniform float _error;
                        
            float4 Grid_drawer(float2 uv, float width, float repeat)
            {
                uv = abs(uv);
                uv = frac(uv * repeat);
                float4 col = step(uv.x, width);
                col += step(1 - width, uv.x);
                col += step(uv.y, width);
                col += step(1 - width, uv.y);
                col = saturate(col);
                return col;
            }
            
            float4 Object_Grid(float3 pos, float3 normal, float width, float repeat)
            {
				float3 blendWeights = pow(abs(normal), 3);
				blendWeights /= blendWeights.x + blendWeights.y + blendWeights.z;
                
                float4 gridcol = blendWeights.x * Grid_drawer(pos.zy, width, repeat) + blendWeights.y * Grid_drawer(pos.xz, width, repeat) + blendWeights.z * Grid_drawer(pos.xy, width, repeat);
                clip(gridcol.r - 0.5);
				return gridcol;
            }

            v2f vert(appdata v)
            {
                v2f o;
                
                //Single Path Stereo and GPU Instancing処理
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);//メッシュのワールド座標を代入
                
                o.screenPos = ComputeScreenPos(o.vertex); //スクリーンスペースの座標
                o.screenPos.z = -mul(UNITY_MATRIX_V, worldPos).z;//screenPosの使用しないzに深度を書き込み
                
                o.cp = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;//Local座標系におけるObjectのセンターポイントを取得
                o.rd = worldPos.xyz - _WorldSpaceCameraPos;//視線ベクトル
                o.pos = worldPos.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                const float bias = 0;
                const float4 depthSpace = mul(_EnvironmentDepthReprojectionMatrices[unity_StereoEyeIndex], float4(i.pos, 1.0));
                const float2 uv = (depthSpace.xy / depthSpace.w + 1.0f) * 0.5f;
                //比較用のScene上のDepth
                //float linearSceneDepth = (1.0f / ((depthSpace.z / depthSpace.w) + _EnvironmentDepthZBufferParams.y)) * _EnvironmentDepthZBufferParams.x;
                //linearSceneDepth -= bias * linearSceneDepth * UNITY_NEAR_CLIP_VALUE;
                
                //const float inputDepthEye = SampleEnvironmentDepth(uv);
                const float inputDepthEye = UNITY_SAMPLE_TEX2DARRAY(_EnvironmentDepthTexture, float3(uv, (float)unity_StereoEyeIndex)).r;

                const float inputDepthNdc = inputDepthEye * 2.0 - 1.0;
                const float linearDepth = (1.0f / (inputDepthNdc + _EnvironmentDepthZBufferParams.y)) * _EnvironmentDepthZBufferParams.x;
                
                float z = inputDepthNdc;
                
                //Normal導出
                float3 SpotPos = linearDepth * i.rd / i.screenPos.z + _WorldSpaceCameraPos;
                float3 normal = normalize(cross(-ddx(SpotPos), ddy(SpotPos)));//Spotしている面のNormalを導出
                
                //深度によってScanner切り取り
                clip(_error - abs(clamp(linearDepth, 0, 5) / 5 - fmod(_Time.y / _speed, 0.99)));
                clip(100 - distance(SpotPos, _WorldSpaceCameraPos));
                return Object_Grid(SpotPos - i.cp, normal, _width, _repeat) * _Color;
            }
            ENDCG
        }
    }
}