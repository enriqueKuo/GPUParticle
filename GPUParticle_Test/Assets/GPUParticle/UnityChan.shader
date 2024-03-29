Shader "MaidChan/GPU Particle"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
		[HDR]
		_HDRColor ("HDR Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.8, 0.8, 1, 1)
        
        _MainTex ("Diffuse", 2D) = "white" { }
        _FalloffSampler ("Falloff Control", 2D) = "white" { }
        _RimLightSampler ("RimLight Control", 2D) = "white" { }
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "LightMode" = "ForwardBase" }
        
        Pass
        {
            Cull Back
            ZTest LEqual
			Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            
            #pragma multi_compile_fwdbase
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #define ENABLE_CAST_SHADOWS
            
			float4 _HDRColor;
			float _threshold;
            // Material parameters
            float4 _Color;
            float4 _ShadowColor;
            float4 _LightColor0;
            float4 _MainTex_ST;
            
            // Textures
            sampler2D _MainTex;
            sampler2D _FalloffSampler;
            sampler2D _RimLightSampler;
            
            // Constants
            #define FALLOFF_POWER 1.0
            
            #ifdef ENABLE_CAST_SHADOWS
                
                // Structure from vertex shader to fragment shader
                struct v2f
                {
                    float4 pos: SV_POSITION;
                    LIGHTING_COORDS(0, 1)
                    float3 normal: TEXCOORD2;
                    float2 uv: TEXCOORD3;
                    float3 eyeDir: TEXCOORD4;
                    float3 lightDir: TEXCOORD5;
					
					float3 worldPos: TEXCOORD6;
                };
                
            #else
                
                // Structure from vertex shader to fragment shader
                struct v2f
                {
                    float4 pos: SV_POSITION;
                    float3 normal: TEXCOORD0;
                    float2 uv: TEXCOORD1;
                    float3 eyeDir: TEXCOORD2;
                    float3 lightDir: TEXCOORD3;
					float3 worldPos: TEXCOORD4;
                };
                
            #endif
            
            // Float types
            #define float_t  half
            #define float2_t half2
            #define float3_t half3
            #define float4_t half4
            
            // Vertex shader
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                o.normal = normalize(mul(unity_ObjectToWorld, float4_t(v.normal, 0)).xyz);
                
                // Eye direction vector
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.eyeDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                
                o.lightDir = WorldSpaceLightDir(v.vertex);
                
                #ifdef ENABLE_CAST_SHADOWS
                    TRANSFER_VERTEX_TO_FRAGMENT(o);
                #endif
                
                return o;
            }
            
            // Fragment shader
            float4 frag(v2f i): COLOR
            {
                float4_t diffSamplerColor = tex2D(_MainTex, i.uv);
                
                // Falloff. Convert the angle between the normal and the camera direction into a lookup for the gradient
                float_t normalDotEye = dot(i.normal, i.eyeDir);
                float_t falloffU = clamp(1 - abs(normalDotEye), 0.02, 0.98);
                float4_t falloffSamplerColor = FALLOFF_POWER * tex2D(_FalloffSampler, float2(falloffU, 0.25f));
                float3_t combinedColor = lerp(diffSamplerColor.rgb, falloffSamplerColor.rgb * diffSamplerColor.rgb, falloffSamplerColor.a);
                
                // Rimlight
                float_t rimlightDot = saturate(0.5 * (dot(i.normal, i.lightDir) + 1.0));
                falloffU = saturate(rimlightDot * falloffU);
                //falloffU = saturate( ( rimlightDot * falloffU - 0.5 ) * 32.0 );
                falloffU = tex2D(_RimLightSampler, float2(falloffU, 0.25f)).r;
                float3_t lightColor = diffSamplerColor.rgb * 0.5; // * 2.0;
                combinedColor += falloffU * lightColor;
                
                #ifdef ENABLE_CAST_SHADOWS
                    // Cast shadows
                    float3_t shadowColor = _ShadowColor.rgb * combinedColor;
                    float_t attenuation = saturate(2.0 * LIGHT_ATTENUATION(i) - 1.0);
                    combinedColor = lerp(shadowColor, combinedColor, attenuation);
                #endif
				float4 finalColor = float4_t(combinedColor, diffSamplerColor.a) * _Color * _LightColor0;

				float a0 = i.worldPos.y - _threshold;
    			float a1 = max(a0, 0);
    			float4 ccc = lerp(finalColor, float4(finalColor.rgb,0), min(a1, 1));
                
                return ccc;
				//float4(_HDRColor.rgb,0.1);
				//float4_t(combinedColor, diffSamplerColor.a) * _Color * _LightColor0;
            }
            ENDCG
            
        }
    }
    
    FallBack "Transparent/Cutout/Diffuse"
}
