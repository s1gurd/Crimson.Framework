Shader "VFX/Mesh/Panner Masked Alpha Blended" {
    Properties {
	[Header(Textures)]	
        _MainTex ("Noise Texture", 2D) = "white" {}
        [NoScaleOffset] _MaskTex ("Mask Texture", 2D) = "white" {}
	_ScrewPower ("Screw Power", Float) = 0
	[Header(Color)]
        _EmissivePower ("Emissive Power", Range(0, 10)) = 1
	_FresnelExponent ("Fresnel Exponent", Range(0.0001, 10)) = 0.1
	_AlphaHardiness ("Alpha Hardiness", Range(1, 10)) = 1
	_AlphaPremultiplyAmount ("Alpha Premultiply Amount", Range(0, 1)) = 1
	[Header(Motion)]
        _SpeedX ("Speed X", Float ) = 0
        _SpeedY ("Speed Y", Float ) = 0
    }
    SubShader {
        Tags {
            "IgnoreProjector"="True"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
	    #include "vfx.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
	    float4 _MainTex_ST;
            half _EmissivePower;
	    half _AlphaHardiness;
	    half _AlphaPremultiplyAmount;
	    half _FresnelExponent;
	    float _ScrewPower;
            float _SpeedX;
            float _SpeedY;

            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 texcoord0 : TEXCOORD0;
                half4 vertexColor : COLOR;
            };

            struct VertexOutput {
                float4 pos : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                half4 vertexColor : COLOR;
            };

            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0.xy = v.texcoord0;
		o.uv1.xy = transform(o.uv0.xy + (o.uv0.y * _ScrewPower), _MainTex_ST) + _Time.y * float2(_SpeedX, _SpeedY) + v.texcoord0.zw;		
                float4 posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 normalDir = normalize(UnityObjectToWorldNormal(v.normal));
		float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - posWorld.xyz);
		float fresnel = fresnelinv(viewDir, normalDir, _FresnelExponent);
                o.vertexColor = v.vertexColor;
		o.vertexColor.a = o.vertexColor.a * fresnel;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(VertexOutput i) : COLOR {		
                half4 mask = tex2D(_MaskTex, i.uv0.xy);
                half4 tex = tex2D(_MainTex, i.uv1.xy);
		tex.a = mask.r * tex.r * i.vertexColor.a;
		tex.rgb = lerp(1.0, tex.a, _AlphaPremultiplyAmount) * i.vertexColor.rgb * _EmissivePower;
		tex.a = saturate(tex.a * _AlphaHardiness);
                return tex;
            }
            ENDCG
        }
    }
}
