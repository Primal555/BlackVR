Shader "KamenRider/HenshinSoftSteam"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 0.25)
        _Softness ("Softness", Range(0.1, 4.0)) = 1.7
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
            Name "HenshinSoftSteam"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Softness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float radius = length(centeredUv);
                float softCircle = saturate(1.0 - smoothstep(0.18, 1.0, radius));
                softCircle = pow(softCircle, _Softness);

                float wispyNoise = sin((input.uv.x * 17.0) + (input.uv.y * 11.0)) * 0.08;
                float alpha = saturate(softCircle + wispyNoise) * _BaseColor.a * input.color.a;
                return half4(_BaseColor.rgb * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
