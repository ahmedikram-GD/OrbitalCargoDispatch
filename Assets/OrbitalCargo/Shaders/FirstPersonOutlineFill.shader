// URP port of QuickOutline's fill pass: inflates the mesh along SMOOTHED normals (read from UV3;
// falls back to the mesh normal) in VIEW space scaled by depth, so the outline is a clean, even,
// constant-screen-width band with no corner gaps on hard-edged meshes. Draws only where the mask
// pass did NOT write stencil ref 1. _ZTest picks the mode (Always = always-visible / "OutlineAll").
Shader "FlipFurry/FirstPersonOutlineFill"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 8 // Always
        [HDR] _OutlineColor ("Outline Color", Color) = (0.2, 0.9, 1, 1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+110"
            "RenderType" = "Transparent"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "Fill"
            Cull Off
            ZTest [_ZTest]
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            Stencil { Ref 1 Comp NotEqual }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                float3 smoothNormal: TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4  color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings o;
                float3 nrm = any(IN.smoothNormal) ? IN.smoothNormal : IN.normalOS;

                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                float3 viewPos = TransformWorldToView(posWS);
                float3 nrmWS  = TransformObjectToWorldNormal(nrm);
                float3 viewNormal = normalize(TransformWorldToViewDir(nrmWS, true));

                // scale by -viewPos.z so the band stays a constant width on screen at any distance
                float3 expanded = viewPos + viewNormal * (-viewPos.z) * (_OutlineWidth / 1000.0);
                o.positionHCS = TransformWViewToHClip(expanded);
                o.color = _OutlineColor;
                return o;
            }

            half4 frag (Varyings IN) : SV_Target { return IN.color; }
            ENDHLSL
        }
    }
}
