Shader "Custom/ProceduralWater"
{
	Properties
	{
		_Color("Color", Color) = (1,0,0,1)
		_MainTex("Texture", 2D) = "white" {}
	}

		SubShader
	{
		Tags{ "RenderType" = "Opaque" "Queue" = "Overlay+10"}
		LOD 100

		Pass
		{
			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma target 3.5
			#pragma vertex vert
			#pragma fragment frag

			struct Vert
			{
				float4 position;
				float3 normal;
			};

			// uniform float4 _Color;
			uniform StructuredBuffer<Vert> _vertices;
			sampler2D _MainTex;
			float4 _MainTex_ST;

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				// float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
				float4 col : COLOR;
			};

			v2f vert(appdata v, uint vid : SV_VertexID)
			{
				// v2f o;
				// o.vertex = UnityObjectToClipPos(_vertices[vid].position);
				// return o;

				v2f o;
				float4 vertex_position = _vertices[vid].position;
				// float4 vertex_normal = float4(_vertices[vid].normal, 1.0f);
				vertex_position.x += sin(5.0 * _Time.g);
				o.vertex = mul(UNITY_MATRIX_VP, vertex_position);
				o.uv = TRANSFORM_TEX(float2(vertex_position.x, vertex_position.z), _MainTex);
				// float3 normalDirection = normalize(vertex_normal.xyz);
				// float4 AmbientLight = UNITY_LIGHTMODEL_AMBIENT;
				// float4 LightDirection = normalize(_WorldSpaceLightPos0);
				// float4 DiffuseLight = saturate(dot(LightDirection, normalDirection)) * _LightColor0;
				o.col = float4(1, 1, 1, 1); // AmbientLight + DiffuseLight);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{ 
				fixed4 final = tex2D(_MainTex, i.uv);
				final *= i.col;
				return final; // _Color;
			}
			ENDCG
		}
	}
}
