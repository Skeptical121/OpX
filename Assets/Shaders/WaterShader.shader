Shader "Custom/WaterShader" {
	Properties{
		_Color("Color", Color) = (1,1,1,1)
		[NoScaleOffset] _MainTex("Deriv (AG) Height (B)", 2D) = "black" {}
		// [NoScaleOffset] _FlowMap("Flow (RG)", 2D) = "black" {}
		// [Toggle(_DUAL_GRID)] _DualGrid("Dual Grid", Int) = 0
		_Tiling("Tiling, Constant", Float) = 1
		_Speed("Speed", Float) = 1
		_HeightScale("Height Scale, Constant", Float) = 0.25
		_HeightScaleModulated("Height Scale, Modulated", Float) = 0.75

		// _WaterFogColor("Water Fog Color", Color) = (0, 0, 0, 0)
		// _WaterFogDensity("Water Fog Density", Range(0, 2)) = 0.1
		_PhaseTime("Phase Time", Float) = 10
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
		_InvFade("Soft Factor", Range(0.01,3.0)) = 0.1
	}
		SubShader{
			Tags {"Queue" = "Transparent" "RenderType" = "Transparent" }
			LOD 200

			// GrabPass { "_WaterBackground" }


			// finalcolor:ResetAlpha
			CGPROGRAM
			#pragma surface surf Standard alpha:fade vertex:vert
			#pragma target 3.5



			sampler2D _MainTex;
			float _Tiling, _Speed;
			float _HeightScale, _HeightScaleModulated;

			sampler2D_float _CameraDepthTexture;
			// sampler2D _WaterBackground;
			// float4 _CameraDepthTexture_TexelSize;
			// float3 _WaterFogColor;
			// float _WaterFogDensity;

			float _Length;
			uint _Size;
			float _InvFade;
			float _PhaseTime;

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				uint vid : SV_VertexID;
			};

			struct Input {
				float2 uv_MainTex;
				// float4 color : COLOR;
				float4 screenPos;
				float eyeDepth;
			};

			struct Vert
			{
				float4 position;
				float3 normal;
			};

#ifdef SHADER_API_D3D11
			StructuredBuffer<Vert> _vertices;
			StructuredBuffer<float2> _FlowMap;
#endif

			half _Glossiness;
			half _Metallic;
			fixed4 _Color;

			uint Index(float x, float y)
			{
				return max(0, x) * _Size + max(0, y);
			}

			/*float3 ColorBelowWater(float4 screenPos) {
				float2 uv = screenPos.xy / screenPos.w;
#if UNITY_UV_STARTS_AT_TOP
				if (_CameraDepthTexture_TexelSize.y < 0) {
					uv.y = 1 - uv.y;
				}
#endif
				float backgroundDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
				float surfaceDepth = UNITY_Z_0_FAR_FROM_CLIPSPACE(screenPos.z);
				float depthDifference = backgroundDepth - surfaceDepth;

				// return depthDifference / 20;
				float3 backgroundColor = tex2D(_WaterBackground, uv).rgb;
				float fogFactor = exp2(-_WaterFogDensity * depthDifference);
				return lerp(_WaterFogColor, backgroundColor, fogFactor);
			}

			void ResetAlpha(Input IN, SurfaceOutputStandard o, inout fixed4 color) {
				color.a = 1;
			}*/



			void vert(inout appdata v, out Input o)
			{
#ifdef SHADER_API_D3D11
				v.vertex = _vertices[v.vid].position;
				v.normal = _vertices[v.vid].normal;
				v.tangent = float4(1, 0, 0, 1);
				// v.tangent = float4(normalize(cross(float3(v.normal.x, 0, v.normal.z), _vertices[v.vid].normal)), 1); // 0, 0, 1 so we get 1, 0, 0
				// v.color = float4(_vertices[v.vid].normal, 1);
				v.texcoord = float2(_vertices[v.vid].position.x, _vertices[v.vid].position.z) / _Length;

				UNITY_INITIALIZE_OUTPUT(Input, o);
				COMPUTE_EYEDEPTH(o.eyeDepth);
#endif
			}

			float3 NewFlow(float2 uv, float2 flow, float time)
			{
				float flowSpeedSqrt = sqrt(sqrt(flow.x * flow.x + flow.y * flow.y) * _Speed);
				float2 uvFlow = (uv - flow.xy * _Speed / _Length) / _Tiling; // (uv - time * flow.xy * _Speed / _Length) / tiling; // 16.0f;
				float3 dh = tex2D(_MainTex, uvFlow);
				dh *= flowSpeedSqrt * _HeightScaleModulated + _HeightScale;
				return dh;
			}

			float3 NewFlowCalc(float2 uv, float2 offset, float time)
			{
				offset = offset * 0.5f - 0.25f;
				float2 uvTiled = floor(uv + offset);
				float2 flow = 0;
#ifdef SHADER_API_D3D11
				flow = _FlowMap[Index(uvTiled.x, uvTiled.y)]; // tex2D(_FlowMap, uvTiled).rgb;
#endif
				return NewFlow(uv, flow, time);

				// float3 dh1 = NewFlow(float2(uv.x + floor((time + _PhaseTime * 0.5f) / _PhaseTime), uv.y + floor((time + _PhaseTime * 0.5f) / _PhaseTime)), flow, time);
				// float3 dh2 = NewFlow(float2(uv.x + 5 + floor(time / _PhaseTime), uv.y + 2 + floor(time / _PhaseTime)), flow, time); // (time + _PhaseTime * 0.5f) % _PhaseTime);
				// float t = abs(2 * frac(time / _PhaseTime) - 1);
				// return lerp(dh2, dh1, t);


				//if (time < _PhaseTime * 0.5f)
				//	return lerp(dh2, dh1, time / (_PhaseTime * 0.5f));
				//else
				//	return lerp(dh1, dh2, (time - _PhaseTime * 0.5f) / (_PhaseTime * 0.5f));
			}

			float3 CalcDH(float2 uv, float time)
			{
				float3 dhA = NewFlowCalc(uv, float2(0, 0), time);
				float3 dhB = NewFlowCalc(uv, float2(1, 0), time);
				float3 dhC = NewFlowCalc(uv, float2(0, 1), time);
				float3 dhD = NewFlowCalc(uv, float2(1, 1), time);

				float2 t = abs(2 * frac(uv - 0.25f) - 1);
				float wA = (1 - t.x) * (1 - t.y);
				float wB = t.x * (1 - t.y);
				float wC = (1 - t.x) * t.y;
				float wD = t.x * t.y;

				return dhA * wA + dhB * wB + dhC * wC + dhD * wD;
			}

			void surf(Input IN, inout SurfaceOutputStandard o) {
				float time = _Time.y;
				float2 uv = IN.uv_MainTex; // uv is the grid in our model
				float3 dh = CalcDH(uv, time);

				o.Albedo = _Color; // c.rgb; // IN.color; // c.rgb; // hmm
				o.Normal = normalize(float3(-dh.xy, 1));
				o.Metallic = _Metallic;
				o.Smoothness = _Glossiness;

				float rawZ = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(IN.screenPos));
				float sceneZ = LinearEyeDepth(rawZ);
				float partZ = IN.eyeDepth;

				float fade = 1.0;
				if (rawZ > 0.0) // Make sure the depth texture exists
					fade = saturate(_InvFade * (sceneZ - partZ));

				o.Alpha = fade; // 0.85f; // IN.color.a;
			
				// o.Alpha = 0.8f;
				// o.Emission = ColorBelowWater(IN.screenPos) * (1 - 0.8f);
			}

			ENDCG
		}
}