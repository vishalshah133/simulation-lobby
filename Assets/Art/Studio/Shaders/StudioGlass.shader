// Tinted glass / jelly for the studio look (plans/blocks/studio-look-3d.md).
//
// Refraction: samples the camera's opaque texture (Opaque Texture is on in PC_RPAsset) offset by the
// view-space normal, so whatever sits behind the object bends through it. Absorption tints that light
// by an approximate thickness (thicker through the middle, thin at the silhouette). Fresnel blends in
// reflection-probe reflections at grazing angles, and a rim term plus tight specular highlights from
// every light give the silhouette its bright edge, which is what keeps it readable on a dark set.
//
// Opaque-texture refraction only sees opaque geometry, which is the intent: the glass shows the
// studio, plinth and spike through itself.
Shader "Simulation Lobby/Studio Glass"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.93, 0.52, 0.2, 1)
        _Absorption ("Absorption", Range(0, 6)) = 1.8
        _Refraction ("Refraction strength", Range(0, 0.3)) = 0.07
        _Smoothness ("Smoothness", Range(0, 1)) = 0.96
        _FresnelPower ("Fresnel power", Range(0.5, 8)) = 4
        _ReflectionStrength ("Reflection strength", Range(0, 2)) = 1.1
        _SpecularStrength ("Specular strength", Range(0, 12)) = 4
        _RimColor ("Rim color", Color) = (1, 0.82, 0.62, 1)
        _RimStrength ("Rim strength", Range(0, 3)) = 0.7
        _InnerGlow ("Inner glow", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "StudioGlassForward"
            Tags { "LightMode" = "UniversalForward" }

            // The pass composites the scene behind it itself (via the opaque texture), so it writes
            // an opaque result.
            Blend One Zero
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Absorption;
                half _Refraction;
                half _Smoothness;
                half _FresnelPower;
                half _ReflectionStrength;
                half _SpecularStrength;
                half4 _RimColor;
                half _RimStrength;
                half _InnerGlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // Tight Blinn-Phong lobe, energy-scaled so a smoother surface gives a smaller, brighter glint.
            half3 Glint(Light light, half3 normalWS, half3 viewWS, half smoothness)
            {
                half3 halfDir = SafeNormalize(light.direction + viewWS);
                half nDotH = saturate(dot(normalWS, halfDir));
                half power = exp2(10.0 * smoothness + 1.0);
                half lobe = pow(nDotH, power) * (power + 8.0) / 8.0;
                half nDotL = saturate(dot(normalWS, light.direction));
                return light.color * light.distanceAttenuation * light.shadowAttenuation * lobe * saturate(nDotL * 4.0) * 0.04;
            }

            // Back-light wrap: light from behind the object catches its edge — the glass "rim light".
            half3 EdgeLight(Light light, half3 normalWS, half3 viewWS, half rim)
            {
                half behind = saturate(dot(-viewWS, light.direction) * 0.5 + 0.5);
                return light.color * light.distanceAttenuation * rim * behind;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half nDotV = saturate(dot(normalWS, viewWS));
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // --- Transmission: the scene behind, bent and tinted.
                half3 normalVS = TransformWorldToViewDir(normalWS);
                float2 refractedUV = screenUV - normalVS.xy * _Refraction * (1.0 - nDotV * 0.5);
                half3 behind = SampleSceneColor(refractedUV);
                half thickness = lerp(0.25, 1.0, nDotV);
                half3 transmittance = pow(max(_Tint.rgb, 0.001), _Absorption * thickness);
                half3 transmitted = behind * transmittance + _Tint.rgb * _InnerGlow * thickness;

                // --- Reflection: probes (the studio's softbox cards), stronger at grazing angles.
                half perceptualRoughness = 1.0 - _Smoothness;
                half3 reflectWS = reflect(-viewWS, normalWS);
                half3 environment = GlossyEnvironmentReflection(reflectWS, input.positionWS, perceptualRoughness, 1.0, screenUV)
                                    * _ReflectionStrength;
                half fresnel = 0.04 + 0.96 * pow(1.0 - nDotV, _FresnelPower);

                // --- Lights: glints plus edge light.
                half rim = pow(1.0 - nDotV, 3.0) * _RimStrength;
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 glints = Glint(mainLight, normalWS, viewWS, _Smoothness);
                half3 edges = EdgeLight(mainLight, normalWS, viewWS, rim);

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewWS;
                inputData.normalizedScreenSpaceUV = screenUV;

                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint directionalIndex = 0; directionalIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); directionalIndex++)
                {
                    Light directional = GetAdditionalLight(directionalIndex, input.positionWS);
                    glints += Glint(directional, normalWS, viewWS, _Smoothness);
                    edges += EdgeLight(directional, normalWS, viewWS, rim);
                }
                #endif

                uint additionalCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(additionalCount)
                    Light additional = GetAdditionalLight(lightIndex, input.positionWS);
                    glints += Glint(additional, normalWS, viewWS, _Smoothness);
                    edges += EdgeLight(additional, normalWS, viewWS, rim);
                LIGHT_LOOP_END
                #endif

                half3 color = lerp(transmitted, environment, fresnel)
                              + glints * _SpecularStrength
                              + edges * _RimColor.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // Shadow casting borrowed from URP Lit, so the object grounds itself with a contact shadow.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
