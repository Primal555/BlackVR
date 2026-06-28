Shader "KamenRider/HenshinBeltReveal"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.0
        _RevealProgress ("Reveal Progress", Range(0.0, 1.0)) = 0.0
        _CenterX ("Center X", Float) = 0.0
        _HalfWidth ("Half Width", Float) = 1.0
        _VertexOffsetOS ("Vertex Offset OS", Vector) = (0, 0, 0, 0)
        _EdgeWidth ("Edge Width", Range(0.01, 0.5)) = 0.08
        [HDR] _GlowColor ("Glow Color", Color) = (1, 0.05, 0, 1)
        _GlowIntensity ("Glow Intensity", Range(0.0, 8.0)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "HenshinBeltReveal"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Alpha;
                float _RevealProgress;
                float _CenterX;
                float _HalfWidth;
                float4 _VertexOffsetOS;
                float _EdgeWidth;
                float4 _GlowColor;
                float _GlowIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz + _VertexOffsetOS.xyz;
                output.positionOS = input.positionOS.xyz;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionCS = TransformObjectToHClip(positionOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float normalizedDistance = abs(input.positionOS.x - _CenterX) / max(0.0001, _HalfWidth);
                float reveal = saturate(_RevealProgress);
                float edgeWidth = max(0.0001, _EdgeWidth);
                float visibleMask = 1.0 - smoothstep(reveal, reveal + edgeWidth, normalizedDistance);
                float edgeMask = 1.0 - abs(normalizedDistance - reveal) / edgeWidth;
                edgeMask = smoothstep(0.0, 1.0, saturate(edgeMask));

                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float alpha = baseColor.a * _Alpha * visibleMask;
                float3 glow = _GlowColor.rgb * (_GlowIntensity * edgeMask * _Alpha);

                return half4(baseColor.rgb + glow, alpha);
            }
            ENDHLSL
        }
    }
}
