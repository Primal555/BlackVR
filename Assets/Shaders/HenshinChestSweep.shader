Shader "KamenRider/HenshinChestSweep"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _CenterX ("Center X", Float) = 0.0
        _HalfWidth ("Half Width", Float) = 1.0
        _SweepProgress ("Sweep Progress", Range(0.0, 1.0)) = 0.0
        _SweepWidth ("Sweep Width", Range(0.01, 0.75)) = 0.22
        [HDR] _SweepColor ("Sweep Color", Color) = (1, 1, 1, 1)
        _SweepIntensity ("Sweep Intensity", Range(0.0, 10.0)) = 0.0
        [HDR] _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashIntensity ("Flash Intensity", Range(0.0, 10.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "HenshinChestSweep"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

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
                float _CenterX;
                float _HalfWidth;
                float _SweepProgress;
                float _SweepWidth;
                float4 _SweepColor;
                float _SweepIntensity;
                float4 _FlashColor;
                float _FlashIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionOS = input.positionOS.xyz;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float normalizedX = saturate((input.positionOS.x - (_CenterX - _HalfWidth)) / max(0.0001, _HalfWidth * 2.0));
                float distanceToSweep = abs(normalizedX - _SweepProgress);
                float sweep = saturate(1.0 - distanceToSweep / max(0.0001, _SweepWidth));
                sweep = smoothstep(0.0, 1.0, sweep);

                float3 glow = _SweepColor.rgb * (_SweepIntensity * sweep);
                glow += _FlashColor.rgb * _FlashIntensity;
                return half4(baseColor.rgb + glow, baseColor.a);
            }
            ENDHLSL
        }
    }
}
