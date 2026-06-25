Shader "KamenRider/HenshinLightRay"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0.0, 12.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+60"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "HenshinLightRay"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float _Intensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 color = input.color * _TintColor;
                return half4(color.rgb * _Intensity, color.a);
            }
            ENDHLSL
        }
    }
}
