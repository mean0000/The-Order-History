// =============================================================================
// Atmosphere.shader
// Project  : 딴 나라 이야기 / The Other History
// Purpose  : Atmospheric scattering illusion on a slightly oversized transparent
//            sphere surrounding the Earth.  Uses a Fresnel (NdotV) gradient so
//            the colour is densest at the silhouette rim and invisible at the
//            centre — mimicking the thin glowing halo visible from orbit.
//
// Target   : Unity URP — Transparent, Cull Off (visible from both sides)
// Platform : PC (Windows / macOS)
// Author   : [art] shader-vfx-artist agent
// Date     : 2026-05-13
//
// Setup note:
//   • Apply to a Sphere mesh scaled to ~1.025× the Earth sphere.
//   • The sphere does NOT need a Collider — it is purely visual.
//   • Keep "Cast Shadows = Off" and "Receive Shadows = Off" in the
//     MeshRenderer to avoid self-shadowing artefacts.
//
// Exposed parameters:
//   _AtmosphereColor      — Core rim colour (recommend sky blue / cyan)
//   _AtmosphereColorOuter — Secondary colour blended toward the very edge
//   _FresnelPower         — Tightness of the rim band (2=wide, 8=tight sliver)
//   _Intensity            — Overall opacity/brightness scalar
//   _SunDirection         — World-space sun direction; used to tint the day side
//   _DaySideBoost         — Extra brightness on the day-facing rim
//   _NightSideAttenuation — How much to dim the night-side atmosphere (0=same, 1=fully dark)
// =============================================================================

Shader "TheOther/Atmosphere"
{
    Properties
    {
        [HDR] _AtmosphereColor      ("Atmosphere Color (rim)",  Color) = (0.35, 0.72, 1.0, 1.0)
        [HDR] _AtmosphereColorOuter ("Atmosphere Color (edge)", Color) = (0.55, 0.88, 1.0, 1.0)
        _FresnelPower               ("Fresnel Power",      Range(1.0, 10.0)) = 4.0
        _Intensity                  ("Intensity",          Range(0.0, 2.0))  = 0.85
        _SunDirection               ("Sun Direction (World)", Vector) = (1, 0.5, 0, 0)
        _DaySideBoost               ("Day Side Boost",     Range(0.0, 1.0))  = 0.35
        _NightSideAttenuation       ("Night Side Attenuation", Range(0.0, 1.0)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"       // Renders before CountryHighlight
        }

        LOD 100

        Pass
        {
            Name "AtmosphereForward"
            Tags { "LightMode" = "UniversalForward" }

            // Standard alpha blend for soft translucent layering.
            Blend SrcAlpha One          // Additive blend: atmosphere glows on dark bg
            ZWrite Off
            ZTest LEqual
            Cull Off                    // Visible from inside AND outside the sphere

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ---------------------------------------------------------------
            // Constant Buffer
            // ---------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _AtmosphereColor;
                float4 _AtmosphereColorOuter;
                float  _FresnelPower;
                float  _Intensity;
                float3 _SunDirection;
                float  _DaySideBoost;
                float  _NightSideAttenuation;
            CBUFFER_END

            // ---------------------------------------------------------------
            // Vertex I/O
            // ---------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = nrmInputs.normalWS;
                OUT.viewDirWS   = GetWorldSpaceViewDir(posInputs.positionWS);
                return OUT;
            }

            // ---------------------------------------------------------------
            // Fragment shader
            // ---------------------------------------------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float3 S = normalize(_SunDirection);

                // --- Fresnel rim ---
                // NdotV: 1 = facing camera (centre), 0 = edge/silhouette.
                // We invert so rimFactor peaks at the silhouette.
                float NdotV      = saturate(dot(N, V));
                float rimFactor  = pow(1.0 - NdotV, max(_FresnelPower, 0.001));

                // --- Day / night hemisphere factor ---
                float NdotS      = dot(N, S);
                // dayFactor: 1 on lit hemisphere, 0 on dark hemisphere
                float dayFactor  = saturate(NdotS * 0.5 + 0.5);

                // --- Attenuate atmosphere on night side ---
                // The atmosphere is physically present on both sides, but dimmer
                // on the night side (no sunlight to scatter).
                float nightAttenuation = lerp(1.0, 1.0 - _NightSideAttenuation, (1.0 - dayFactor));
                float dayBoost         = 1.0 + _DaySideBoost * dayFactor;

                // --- Colour ---
                // Blend inner and outer atmosphere colour based on rim intensity.
                half3 atmColor   = lerp(_AtmosphereColor.rgb, _AtmosphereColorOuter.rgb, rimFactor);

                // Guard: clamp colour to prevent NaN from HDR overflow.
                // No pre-multiply needed — additive blend (SrcAlpha One) does:
                //   dst_new = src.rgb * src.a + dst.rgb
                // so we supply the raw colour in RGB and the rim opacity in A.
                half3 finalAtm = min(atmColor, 4.0);
                float finalAlpha = saturate(rimFactor * _Intensity * nightAttenuation * dayBoost);

                return half4(finalAtm, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
