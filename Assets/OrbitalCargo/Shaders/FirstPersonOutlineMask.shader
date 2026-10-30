// URP port of QuickOutline's mask pass: stamps the object's silhouette into stencil ref 1 (no
// colour), so the fill pass can draw only OUTSIDE it. Used as a separate material (URP renders one
// pass per material) rendered just before the fill.
Shader "FlipFurry/FirstPersonOutlineMask"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 8 // Always
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+100" "RenderType" = "Transparent" }

        Pass
        {
            Name "Mask"
            Cull Off
            ZTest [_ZTest]
            ZWrite Off
            ColorMask 0
            Stencil { Ref 1 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; };

            Varyings vert (Attributes IN) { Varyings o; o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz); return o; }
            half4 frag (Varyings IN) : SV_Target { return half4(0,0,0,0); }
            ENDHLSL
        }
    }
}
