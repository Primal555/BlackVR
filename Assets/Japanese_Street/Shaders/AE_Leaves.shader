// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "AE/Leaves"
{
	Properties
	{
		_Cutoff( "Mask Clip Value", Float ) = 0.5
		_Wind("Wind", Range( 0 , 10)) = 0.49
		_Wind_Speed("Wind_Speed", Range( 0 , 10)) = 0.49
		_AlbedoColor("Albedo Color", Color) = (1,1,1,0)
		_Albedo("Albedo", 2D) = "white" {}
		[Normal]_Normal("Normal", 2D) = "bump" {}
		_AO("AO", 2D) = "white" {}
		_Smoothness("Smoothness", 2D) = "white" {}
		_Amplitude("Amplitude", Range( 0 , 0.15)) = 0.15
		_Smoothness_Power("Smoothness_Power", Range( 0 , 1)) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "TransparentCutout"  "Queue" = "AlphaTest+0" }
		Cull Off
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows vertex:vertexDataFunc 
		struct Input
		{
			float3 worldPos;
			float2 uv_texcoord;
		};

		uniform float _Wind_Speed;
		uniform float _Wind;
		uniform float _Amplitude;
		uniform sampler2D _Normal;
		uniform sampler2D _Albedo;
		uniform float4 _AlbedoColor;
		uniform float _Smoothness_Power;
		uniform sampler2D _Smoothness;
		uniform float4 _Smoothness_ST;
		uniform sampler2D _AO;
		uniform float _Cutoff = 0.5;


		float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }

		float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }

		float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }

		float snoise( float2 v )
		{
			const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
			float2 i = floor( v + dot( v, C.yy ) );
			float2 x0 = v - i + dot( i, C.xx );
			float2 i1;
			i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
			float4 x12 = x0.xyxy + C.xxzz;
			x12.xy -= i1;
			i = mod2D289( i );
			float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
			float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
			m = m * m;
			m = m * m;
			float3 x = 2.0 * frac( p * C.www ) - 1.0;
			float3 h = abs( x ) - 0.5;
			float3 ox = floor( x + 0.5 );
			float3 a0 = x - ox;
			m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
			float3 g;
			g.x = a0.x * x0.x + h.x * x0.y;
			g.yz = a0.yz * x12.xz + h.yz * x12.yw;
			return 130.0 * dot( m, g );
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float lerpResult35 = lerp( 0.0 , v.color.r , 0.4229066);
			float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex );
			float3 temp_output_14_0 = ( ( _Time.x * _Wind_Speed ) + ( ase_worldPos * _Wind ) );
			float simplePerlin2D16 = snoise( temp_output_14_0.xy );
			simplePerlin2D16 = simplePerlin2D16*0.5 + 0.5;
			float clampResult18 = clamp( (0.0 + (simplePerlin2D16 - -1.0) * (2.5E-05 - 0.0) / (1.0 - -1.0)) , 0.0 , 1.0 );
			float temp_output_20_0 = ( _Time.x * _Wind_Speed * clampResult18 );
			float simplePerlin2D29 = snoise( ( temp_output_14_0 + temp_output_20_0 ).xy );
			simplePerlin2D29 = simplePerlin2D29*0.5 + 0.5;
			float3 temp_output_22_0 = ( temp_output_14_0 + temp_output_20_0 + 164.0 );
			float simplePerlin2D28 = snoise( temp_output_22_0.xy );
			simplePerlin2D28 = simplePerlin2D28*0.5 + 0.5;
			float simplePerlin2D26 = snoise( ( temp_output_22_0 + 164.0 ).xy );
			simplePerlin2D26 = simplePerlin2D26*0.5 + 0.5;
			float3 appendResult30 = (float3(simplePerlin2D29 , simplePerlin2D28 , simplePerlin2D26));
			v.vertex.xyz += ( lerpResult35 * appendResult30 * _Amplitude );
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			o.Normal = UnpackNormal( tex2D( _Normal, i.uv_texcoord ) );
			float4 tex2DNode3 = tex2D( _Albedo, i.uv_texcoord );
			float4 clampResult2 = clamp( ( tex2DNode3 * _AlbedoColor ) , float4( 0,0,0,0 ) , float4( 1,1,1,0 ) );
			o.Albedo = clampResult2.rgb;
			float2 uv_Smoothness = i.uv_texcoord * _Smoothness_ST.xy + _Smoothness_ST.zw;
			float lerpResult44 = lerp( 0.0 , _Smoothness_Power , tex2D( _Smoothness, uv_Smoothness ).a);
			o.Smoothness = lerpResult44;
			o.Occlusion = tex2D( _AO, i.uv_texcoord ).r;
			o.Alpha = 1;
			clip( tex2DNode3.a - _Cutoff );
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=17500
160;146;1664;806;2926.633;-520.1583;1.967781;True;False
Node;AmplifyShaderEditor.RangedFloatNode;9;-4372.055,1884.191;Float;False;Property;_Wind;Wind;1;0;Create;True;0;0;False;0;0.49;7.76;0;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldPosInputsNode;8;-4400.841,1599.621;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.TimeNode;11;-3705.834,1348.003;Inherit;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;13;-3719.624,1529.567;Float;False;Property;_Wind_Speed;Wind_Speed;2;0;Create;True;0;0;False;0;0.49;0.94;0;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;10;-3853.222,1734.113;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;12;-3319.725,1375.582;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;14;-3411.732,1759.401;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.NoiseGeneratorNode;16;-3232.467,1888.104;Inherit;False;Simplex2D;True;False;2;0;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;17;-2950.043,1936.538;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;-1;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;2.5E-05;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;18;-2702.82,1527.684;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TimeNode;19;-2886.187,1245.426;Inherit;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;24;-2986.119,1623.101;Float;False;Constant;_Float0;Float 0;7;0;Create;True;0;0;False;0;164;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;20;-2465.247,1363.51;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;22;-2337.711,1757.864;Inherit;False;3;3;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;21;-2182.625,1546.063;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;23;-2317.931,2071.766;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1;-1787.875,-438.74;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;3;-1213.93,-747.8289;Inherit;True;Property;_Albedo;Albedo;4;0;Create;True;0;0;False;0;-1;None;f75b4185c1374c04ea6a66f7fac08243;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;36;-1571.054,940.8697;Inherit;False;Constant;_Float1;Float 1;8;0;Create;True;0;0;False;0;0.4229066;0;0;0.5;0;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;32;-1814.491,727.8225;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.NoiseGeneratorNode;28;-1844.466,1816.561;Inherit;False;Simplex2D;True;False;2;0;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.NoiseGeneratorNode;26;-1899.4,2062.564;Inherit;False;Simplex2D;True;False;2;0;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.NoiseGeneratorNode;29;-1882.678,1630.266;Inherit;False;Simplex2D;True;False;2;0;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;4;-1393.333,-429.5446;Float;False;Property;_AlbedoColor;Albedo Color;3;0;Create;True;0;0;False;0;1,1,1,0;1,1,1,0;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;34;-1425.488,1132.576;Float;False;Property;_Amplitude;Amplitude;8;0;Create;True;0;0;False;0;0.15;0.0425;0;0.15;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;35;-1082.654,692.2698;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;30;-1299.911,1783.123;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;5;-1102.23,-511.1284;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;39;-1023.503,-135.5204;Inherit;True;Property;_Smoothness;Smoothness;7;0;Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;45;-751.785,113.8125;Inherit;False;Property;_Smoothness_Power;Smoothness_Power;9;0;Create;True;0;0;False;0;0;0.485;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;33;-752.3741,984.4411;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode;38;-1440.271,216.9512;Inherit;True;Property;_AO;AO;6;0;Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;44;-469.785,54.8125;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;6;-1450.747,-147.3763;Inherit;True;Property;_Normal;Normal;5;1;[Normal];Create;True;0;0;False;0;-1;None;None;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;2;-816.4036,-457.9558;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;1,1,1,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;0,0;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;AE/Leaves;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;Off;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Masked;0.5;True;True;0;False;TransparentCutout;;AlphaTest;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;0;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;1;False;-1;0;False;-1;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;10;0;8;0
WireConnection;10;1;9;0
WireConnection;12;0;11;1
WireConnection;12;1;13;0
WireConnection;14;0;12;0
WireConnection;14;1;10;0
WireConnection;16;0;14;0
WireConnection;17;0;16;0
WireConnection;18;0;17;0
WireConnection;20;0;19;1
WireConnection;20;1;13;0
WireConnection;20;2;18;0
WireConnection;22;0;14;0
WireConnection;22;1;20;0
WireConnection;22;2;24;0
WireConnection;21;0;14;0
WireConnection;21;1;20;0
WireConnection;23;0;22;0
WireConnection;23;1;24;0
WireConnection;3;1;1;0
WireConnection;28;0;22;0
WireConnection;26;0;23;0
WireConnection;29;0;21;0
WireConnection;35;1;32;1
WireConnection;35;2;36;0
WireConnection;30;0;29;0
WireConnection;30;1;28;0
WireConnection;30;2;26;0
WireConnection;5;0;3;0
WireConnection;5;1;4;0
WireConnection;33;0;35;0
WireConnection;33;1;30;0
WireConnection;33;2;34;0
WireConnection;38;1;1;0
WireConnection;44;1;45;0
WireConnection;44;2;39;4
WireConnection;6;1;1;0
WireConnection;2;0;5;0
WireConnection;0;0;2;0
WireConnection;0;1;6;0
WireConnection;0;4;44;0
WireConnection;0;5;38;1
WireConnection;0;10;3;4
WireConnection;0;11;33;0
ASEEND*/
//CHKSM=392F96D8D979842775FC60A3B017F7B87071B38E