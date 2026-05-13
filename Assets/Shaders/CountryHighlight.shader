// =============================================================================
// CountryHighlight.shader
// Project  : 딴 나라 이야기 / The Other History
// Purpose  : Transparent overlay drawn on top of the Earth sphere to highlight
//            a selected country.  Two visual layers:
//              1. Soft fill   — semi-transparent color flood over the country area
//              2. Border glow — bright edge glow tracing the country boundary
//            A pulse effect is driven externally by DOTween via _PulseIntensity.
//
// Target   : Unity URP — Transparent queue, no depth write
// Platform : PC (Windows / macOS)
// Author   : [art] shader-vfx-artist agent
// Date     : 2026-05-13
//
// Exposed parameters:
//   _HighlightMask        — Country mask texture
//                           R channel = country fill area (0=outside, 1=inside)
//                           G channel = border/edge proximity (0=center, 1=edge)
//   _HighlightColor       — Fill color (HDR supported; A = base fill opacity)
//   _BorderColor          — Edge glow color (HDR recommended for bloom)
//   _FillOpacity          — Master fill transparency (0=invisible, 1=solid)
//   _BorderGlowWidth      — Controls how wide the border glow feathers inward
//   _BorderGlowIntensity  — Peak brightness of the border glow
//   _PulseIntensity        — [0,1] driven by DOTween; modulates glow brightness
//   _PulseBase            — Minimum brightness at PulseIntensity=0 (default 0.3)
//   _HoverGlow            — Weak ambient glow for hover state (0=off, 1=hover)
//   _HoverGlowColor       — Color of the hover state glow
// =============================================================================

Shader "TheOther/CountryHighlight"
{
    Properties
    {
        // --- Mask ---
        _HighlightMask          ("Highlight Mask (R=Fill, G=Border)", 2D) = "black" {}

        // --- Color ---
        [HDR] _HighlightColor   ("Highlight Fill Color",    Color) = (0.2, 0.7, 1.0, 0.35)
        [HDR] _BorderColor      ("Border Glow Color",       Color) = (0.4, 0.9, 1.0, 1.0)

        // --- Fill ---
        _FillOpacity            ("Fill Opacity",            Range(0, 1)) = 0.3

        // --- Border Glow ---
        _BorderGlowWidth        ("Border Glow Width",       Range(0.01, 1.0)) = 0.4
        _BorderGlowIntensity    ("Border Glow Intensity",   Range(0, 4)) = 2.0

        // --- Pulse (controlled from C# / DOTween) ---
        _PulseIntensity         ("Pulse Intensity [0-1]",   Range(0, 1)) = 0.0
        _PulseBase              ("Pulse Base",              Range(0, 1)) = 0.3

        // --- Hover ---
        _HoverGlow              ("Hover Glow",              Range(0, 1)) = 0.0
        [HDR] _HoverGlowColor   ("Hover Glow Color",        Color) = (0.6, 0.85, 1.0, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent+1"    // +1: renders above Atmosphere
        }

        LOD 100

        Pass
        {
            Name "HighlightForward"
            Tags { "LightMode" = "UniversalForward" }

            // Transparent additive blend for glow; fill uses standard alpha.
            // We use SrcAlpha / OneMinusSrcAlpha for fill, then add the glow
            // via emission in the output alpha. A single pass achieves both by
            // outputting finalColor with composited alpha.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ---------------------------------------------------------------
            // Textures & Samplers
            // ---------------------------------------------------------------
            TEXTURE2D(_HighlightMask);  SAMPLER(sampler_HighlightMask);

            // ---------------------------------------------------------------
            // Constant Buffer
            // ---------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _HighlightMask_ST;
                float4 _HighlightColor;
                float4 _BorderColor;
                float  _FillOpacity;
                float  _BorderGlowWidth;
                float  _BorderGlowIntensity;
                float  _PulseIntensity;
                float  _PulseBase;
                float  _HoverGlow;
                float4 _HoverGlowColor;
            CBUFFER_END

            // ---------------------------------------------------------------
            // Vertex I/O
            // ---------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _HighlightMask);
                return OUT;
            }

            // ---------------------------------------------------------------
            // Fragment shader
            // ---------------------------------------------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                // --- Sample mask ---
                half2 maskSample = SAMPLE_TEXTURE2D(_HighlightMask, sampler_HighlightMask, IN.uv).rg;
                float fillMask   = maskSample.r;   // 0 = outside country, 1 = inside
                float borderMask = maskSample.g;   // 0 = center, 1 = at the border edge

                // Early-out: if outside the country entirely, discard to save fillrate.
                // Use a small epsilon to keep soft edges.
                clip(fillMask - 0.005);

                // --- Pulse multiplier ---
                // PulseIntensity goes 0→1→0 (driven by DOTween).
                // At 0: base brightness. At 1: full brightness.
                float pulse = lerp(_PulseBase, 1.0, _PulseIntensity);

                // --- Border glow ---
                // borderMask: 1 = edge, 0 = center interior.
                // We feather the glow inward using a power curve to create a
                // soft halo effect that hugs the boundary.
                float glowCurve   = pow(saturate(borderMask), max(1.0 / max(_BorderGlowWidth, 0.001), 0.001));
                float glowStrength = glowCurve * _BorderGlowIntensity * pulse;
                half3 glowColor    = _BorderColor.rgb * glowStrength;

                // --- Fill color ---
                half3 fillColor  = _HighlightColor.rgb;
                float fillAlpha  = fillMask * _FillOpacity * _HighlightColor.a * pulse;

                // --- Hover glow (soft radial brightening when _HoverGlow > 0) ---
                half3 hoverColor = _HoverGlowColor.rgb * _HoverGlow * fillMask * 0.5;

                // --- Combine ---
                // Output color: fill + glow (additive) + hover glow
                // Alpha: max of fill alpha and a glow-driven alpha so the border
                //        edge is always visible even if fill is off.
                half3 finalColor  = fillColor + glowColor + hoverColor;
                float glowAlpha   = saturate(glowStrength * 0.5);   // glow visibility
                float finalAlpha  = max(fillAlpha, glowAlpha);

                // Guard: clamp to prevent over-bright NaN from HDR inputs
                finalColor = min(finalColor, 8.0);
                finalAlpha = saturate(finalAlpha);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
