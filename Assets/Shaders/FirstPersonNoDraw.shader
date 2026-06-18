Shader "Hidden/KamenRider/FirstPersonNoDraw"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            ColorMask 0
            ZWrite Off
            Cull Off
        }
    }
}
