// Copyright (c) 2024 momoma
// Released under the MIT license
// https://opensource.org/licenses/mit-license.php

Shader "Appletea's Shader/Effects/Vertex Particle v1.0"
{
	Properties
	{
		[HDR]_Color ("Color", Color) = (1, 1, 1, 1)
		[ToggleUI] _XRotaion ("X Rotation", Float) = 0
	}
	Subshader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" "DisableBatching"="True" "PreviewType"="Plane" }
		Pass
		{
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;

				//GPU Instancing処理
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;

				//Single Path Stereo処理
                UNITY_VERTEX_OUTPUT_STEREO
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			uniform float4 _Color;
			uniform float _Size;
			uniform float _XRotaion;

			v2f vert(appdata v)
			{
				v2f o;
				//GPU Instancing, Single Path Stereo処理
                //InstanceID展開
                UNITY_SETUP_INSTANCE_ID(v);
                //構造体の初期化
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                //出力構造体にターゲットのインデックス割り当て
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				#if defined(USING_STEREO_MATRICES)
					float3 cameraPos = (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5;
				#else
					float3 cameraPos = _WorldSpaceCameraPos;
				#endif

				float3 direction = mul(unity_WorldToObject, float4(cameraPos, 1)).xyz;
				direction.y = _XRotaion ? direction.y : 0;
				direction = normalize(-direction);

				float3x3 billboardMatrix;
				billboardMatrix[2] = direction;
				billboardMatrix[0] = normalize(float3(direction.z, 0, -direction.x));
				billboardMatrix[1] = normalize(cross(direction, billboardMatrix[0]));
				billboardMatrix = transpose(billboardMatrix);

				v.vertex.xyz = mul(billboardMatrix, v.vertex.xyz);
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			float4 frag(v2f i) : SV_TARGET
			{
				
				//生成したUVを利用
				float2 uv = i.uv - float2(0.5, 0.5);
				float l = length(uv * 2);
				clip(1 - l);
				float3 color = _Color.rgb;

				//Gaussianを描画
				float delta = 0.5;
				color *= pow(2.71, -(l * l / (2 * delta)));
				color = min(1, color);
				//color = pow(color, 2.2);
				return float4(color,smoothstep(1,0.8,l));
			}
			ENDCG
		}
	}
}