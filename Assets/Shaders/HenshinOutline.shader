Shader "KamenRider/HenshinOutline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.08)) = 0.018
        _OutlineAlpha ("Outline Alpha", Range(0.0, 1.0)) = 1.0
        _OutlineIntensity ("Outline Intensity", Range(0.0, 8.0)) = 2.5
        _SweepMinY ("Sweep Min Y", Float) = 0.0
        _SweepMaxY ("Sweep Max Y", Float) = 2.0
        _SweepProgress ("Sweep Progress", Range(0.0, 1.0)) = 0.0
        _SweepWidth ("Sweep Width", Range(0.01, 1.0)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "HenshinOutline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineAlpha;
                float _OutlineIntensity;
                float _SweepMinY;
                float _SweepMaxY;
                float _SweepProgress;
                float _SweepWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                positionWS += normalWS * _OutlineWidth;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float yRange = max(0.0001, _SweepMaxY - _SweepMinY);
                float normalizedY = saturate((input.positionWS.y - _SweepMinY) / yRange);
                float distanceToBand = abs(normalizedY - _SweepProgress);
                float sweep = saturate(1.0 - distanceToBand / max(0.0001, _SweepWidth));
                sweep = smoothstep(0.0, 1.0, sweep);

                float alpha = _OutlineAlpha * _OutlineColor.a * sweep;
                return half4(_OutlineColor.rgb * _OutlineIntensity, alpha);
            }
            ENDHLSL
        }
    }
}
