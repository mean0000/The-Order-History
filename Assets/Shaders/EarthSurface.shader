// =============================================================================
// EarthSurface.shader
// Project  : 딴 나라 이야기 / The Other History
// Purpose  : Stylized Earth surface — day/night texture blend driven by a sun
//            direction vector, Fresnel atmosphere rim, ocean specular highlight,
//            and subtle cel-style land shading.
// Target   : Unity URP (Universal Render Pipeline) — ShaderModel 4.5
// Platform : PC (Windows / macOS)
// Author   : [art] shader-vfx-artist agent
// Date     : 2026-05-13
//
// Exposed parameters (set from C# or Material Inspector):
//   _DayTex          — Albedo texture for the lit (daytime) hemisphere
//   _NightTex        — Emission texture for the dark (night) hemisphere (city lights)
//   _SurfaceMaskTex  — Packed mask: R = ocean mask (0=land, 1=ocean)
//                                   G = (reserved / cloud future use)
//                                   B = (reserved)
//   _SunDirection    — World-space direction FROM the surface TOWARD the sun (normalized)
//   _DayNightSoftness— Width of the terminator gradient (0.05 = sharp, 0.3 = soft)
//   _OceanColor      — Base tint for ocean areas (multiplies day texture)
//   _LandColor       — Base tint for land areas (multiplies day texture)
//   _OceanSpecColor  — Specular highlight colour on ocean
//   _OceanSpecPower  — Blinn-Phong exponent for ocean specular (32–512)
//   _NightEmissionIntensity — Brightness scalar for city-light emission on dark side
//   _AtmosphereColor — Rim atmosphere colour (baked into surface edge)
//   _AtmospherePower — Fresnel exponent for built-in rim (1–5)
//   _AtmosphereIntensity — Strength of the built-in rim glow
//   _CelSteps        — Number of cel-shading bands on land (1 = smooth, 3 = 3-band cel)
//   _CelSoftness     — Blend width between cel bands
// =============================================================================

Shader "TheOther/EarthSurface"
{
    Properties
    {
        // --- Surface Textures ---
        [MainTexture] _DayTex       ("Day Texture",        2D) = "white" {}
        _NightTex                   ("Night Texture",      2D) = "black" {}
        _SurfaceMaskTex             ("Surface Mask (R=Ocean)", 2D) = "black" {}

        // --- Sun ---
        _SunDirection               ("Sun Direction (World)", Vector) = (1, 0.5, 0, 0)
        _DayNightSoftness           ("Day/Night Softness",  Range(0.02, 0.5)) = 0.12

        // --- Color Tints ---
        _OceanColor                 ("Ocean Color Tint",   Color) = (0.08, 0.25, 0.55, 1)
        _LandColor                  ("Land Color Tint",    Color) = (0.55, 0.45, 0.28, 1)

        // --- Ocean Specular ---
        _OceanSpecColor             ("Ocean Specular Color", Color) = (0.8, 0.9, 1.0, 1)
        _OceanSpecPower             ("Ocean Specular Power", Range(16, 512)) = 128

        // --- Night Side ---
        _NightEmissionIntensity     ("Night Emission Intensity", Range(0, 3)) = 1.0

        // --- Built-in Atmosphere Rim (baked onto surface edge) ---
        _AtmosphereColor            ("Atmosphere Rim Color", Color) = (0.35, 0.75, 1.0, 1)
        _AtmospherePower            ("Atmosphere Fresnel Power", Range(0.5, 8)) = 3.5
        _AtmosphereIntensity        ("Atmosphere Rim Intensity", Range(0, 2)) = 0.9

        // --- Cel Shading (land only) ---
        _CelSteps                   ("Cel Steps",          Range(1, 5)) = 2
        _CelSoftness                ("Cel Softness",       Range(0.01, 0.2)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Geometry"
        }

        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   4.5

            // URP keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            // URP includes — only URP paths, no Built-in references
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ---------------------------------------------------------------
            // Textures & Samplers
            // ---------------------------------------------------------------
            TEXTURE2D(_DayTex);        SAMPLER(sampler_DayTex);
            TEXTURE2D(_NightTex);      SAMPLER(sampler_NightTex);
            TEXTURE2D(_SurfaceMaskTex);SAMPLER(sampler_SurfaceMaskTex);

            // ---------------------------------------------------------------
            // Constant Buffer (matches URP CBUFFER conventions)
            // ---------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _DayTex_ST;
                float4 _NightTex_ST;
                float4 _SurfaceMaskTex_ST;

                float3 _SunDirection;
                float  _DayNightSoftness;

                float4 _OceanColor;
                float4 _LandColor;
                float4 _OceanSpecColor;
                float  _OceanSpecPower;
                float  _NightEmissionIntensity;

                float4 _AtmosphereColor;
                float  _AtmospherePower;
                float  _AtmosphereIntensity;

                float  _CelSteps;
                float  _CelSoftness;
            CBUFFER_END

            // ---------------------------------------------------------------
            // Vertex input / output
            // ---------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
            };

            // ---------------------------------------------------------------
            // Helpers
            // ---------------------------------------------------------------

            // Cel quantisation: quantise a [0,1] light value into N steps
            // with a soft edge width of 'softness' per step.
            float CelQuantise(float lightValue, float steps, float softness)
            {
                // Clamp steps to a valid integer count (float slider → floor)
                steps = max(floor(steps), 1.0);
                // Scale to step space, floor to nearest band, then smooth back.
                float scaled    = lightValue * steps;
                float floored   = floor(scaled);
                float fraction  = scaled - floored;
                // Soft edge using smoothstep within the last 'softness' of each band.
                float blended   = floored + smoothstep(1.0 - softness, 1.0, fraction);
                return saturate(blended / steps);
            }

            // ---------------------------------------------------------------
            // Vertex shader
            // ---------------------------------------------------------------
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = nrmInputs.normalWS;
                OUT.viewDirWS  = GetWorldSpaceViewDir(posInputs.positionWS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _DayTex);
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            // ---------------------------------------------------------------
            // Fragment shader
            // ---------------------------------------------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                // --- Normalise interpolated vectors ---
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float3 S = normalize(_SunDirection);         // Sun direction (world)

                // --- Texture samples ---
                half4 dayColor   = SAMPLE_TEXTURE2D(_DayTex,        sampler_DayTex,        IN.uv);
                half4 nightColor = SAMPLE_TEXTURE2D(_NightTex,      sampler_NightTex,      IN.uv);
                half4 maskSample = SAMPLE_TEXTURE2D(_SurfaceMaskTex, sampler_SurfaceMaskTex, IN.uv);

                float oceanMask  = maskSample.r;  // 1 = ocean, 0 = land
                float landMask   = 1.0 - oceanMask;

                // --- Day / Night terminator ---
                // NdotS in [-1,1]: positive = lit, negative = dark
                float NdotS      = dot(N, S);
                // Map to [0,1] soft transition centred at NdotS = 0
                float dayFactor  = smoothstep(-_DayNightSoftness, _DayNightSoftness, NdotS);

                // --- Surface tint: blend ocean/land color into day texture ---
                half3 oceanTint  = dayColor.rgb * _OceanColor.rgb * 2.0;  // *2 to keep energy
                half3 landTint   = dayColor.rgb * _LandColor.rgb  * 2.0;
                half3 surfaceDay = lerp(landTint, oceanTint, oceanMask);

                // --- Cel shading on land (NdotS as light proxy) ---
                float celLight   = CelQuantise(saturate(NdotS), _CelSteps, _CelSoftness);
                // Only apply cel to land; ocean stays smooth for the specular look
                float lightValue = lerp(celLight, saturate(NdotS), oceanMask);
                // Lift shadows slightly so dark side of land isn't pitch black
                lightValue = lightValue * 0.75 + 0.25;
                half3 litSurface = surfaceDay * lightValue;

                // --- Ocean specular (Blinn-Phong) ---
                // Guard: avoid NaN from zero-length H on grazing view
                float3 H         = normalize(S + V);
                float  NdotH     = max(0.0, dot(N, H));
                float  spec      = pow(NdotH, max(_OceanSpecPower, 1.0));
                spec             = spec * saturate(NdotS) * oceanMask;
                half3 specColor  = _OceanSpecColor.rgb * spec * 0.8;

                // --- Night emission (city lights on dark side) ---
                // Fade in as dayFactor goes to 0; clamp to avoid bleed on lit side
                float nightFactor = saturate(1.0 - dayFactor * 2.5);
                half3 nightEmit   = nightColor.rgb * nightFactor * _NightEmissionIntensity;

                // --- Fresnel atmosphere rim (baked on surface edge) ---
                float NdotV      = saturate(dot(N, V));
                // Guard: clamp before pow to avoid negative base
                float fresnelRaw = 1.0 - NdotV;
                float fresnel    = pow(max(fresnelRaw, 0.0), _AtmospherePower);
                half3 atmRim     = _AtmosphereColor.rgb * fresnel * _AtmosphereIntensity;
                // Only show atmosphere rim on the day-facing hemisphere edge
                atmRim           *= saturate(NdotS + 0.5);

                // --- Combine ---
                half3 finalColor = litSurface + specColor + nightEmit + atmRim;

                // --- Fog ---
                finalColor = MixFog(finalColor, IN.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // --- Shadow caster pass (required for URP shadow casting) ---
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma target   4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShadowCasterPass.hlsl"

            // SRP Batcher requires ALL passes to declare an IDENTICAL UnityPerMaterial
            // CBUFFER layout. Unused variables are declared but never read.
            CBUFFER_START(UnityPerMaterial)
                float4 _DayTex_ST;
                float4 _NightTex_ST;
                float4 _SurfaceMaskTex_ST;
                float3 _SunDirection;
                float  _DayNightSoftness;
                float4 _OceanColor;
                float4 _LandColor;
                float4 _OceanSpecColor;
                float  _OceanSpecPower;
                float  _NightEmissionIntensity;
                float4 _AtmosphereColor;
                float  _AtmospherePower;
                float  _AtmosphereIntensity;
                float  _CelSteps;
                float  _CelSoftness;
            CBUFFER_END
            ENDHLSL
        }

        // --- Depth-only pass ---
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma target   4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DepthOnlyPass.hlsl"

            // SRP Batcher: CBUFFER must match ForwardLit pass exactly.
            CBUFFER_START(UnityPerMaterial)
                float4 _DayTex_ST;
                float4 _NightTex_ST;
                float4 _SurfaceMaskTex_ST;
                float3 _SunDirection;
                float  _DayNightSoftness;
                float4 _OceanColor;
                float4 _LandColor;
                float4 _OceanSpecColor;
                float  _OceanSpecPower;
                float  _NightEmissionIntensity;
                float4 _AtmosphereColor;
                float  _AtmospherePower;
                float  _AtmosphereIntensity;
                float  _CelSteps;
                float  _CelSoftness;
            CBUFFER_END
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
