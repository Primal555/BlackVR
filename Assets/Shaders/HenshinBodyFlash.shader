Shader "KamenRider/HenshinBodyFlash"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BeltCenterWS ("Belt Center WS", Vector) = (0, 0, 0, 0)
        _MaxDistance ("Max Distance", Float) = 1.0
        _CollapseProgress ("Collapse Progress", Range(0.0, 1.0)) = 0.0
        _CollapseBand ("Collapse Band", Range(0.02, 0.45)) = 0.16
        [HDR] _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashIntensity ("Flash Intensity", Range(0.0, 10.0)) = 0.0
        [HDR] _EyeFlashColor ("Eye Flash Color", Color) = (1, 0, 0, 1)
        _EyeFlashIntensity ("Eye Flash Intensity", Range(0.0, 10.0)) = 0.0
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
            Name "HenshinBodyFlash"
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
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _BeltCenterWS;
                float _MaxDistance;
                float _CollapseProgress;
                float _CollapseBand;
                float4 _FlashColor;
                float _FlashIntensity;
                float4 _EyeFlashColor;
                float _EyeFlashIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float distanceFromBelt = distance(input.positionWS, _BeltCenterWS.xyz);
                float normalizedDistance = saturate(distanceFromBelt / max(0.0001, _MaxDistance));
                float activeRadius = 1.0 - saturate(_CollapseProgress);
                float collapseMask = 1.0 - smoothstep(activeRadius, activeRadius + max(0.0001, _CollapseBand), normalizedDistance);

                float3 flash = _FlashColor.rgb * (_FlashIntensity * collapseMask);
                flash += _EyeFlashColor.rgb * _EyeFlashIntensity;
                return half4(baseColor.rgb + flash, baseColor.a);
            }
            ENDHLSL
        }
    }
}
