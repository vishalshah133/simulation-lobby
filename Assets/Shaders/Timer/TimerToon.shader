Shader "Simulation Lobby/Timer Toon"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor("Shade Color", Color) = (0.5, 0.5, 0.75, 1)
        _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimAmount("Rim Amount", Range(0, 1)) = 0.45
        _RimSoftness("Rim Softness", Range(0.001, 0.5)) = 0.06
        _HighlightColor("Highlight Color", Color) = (1, 1, 1, 1)
        _HighlightSize("Highlight Size (0 = off)", Range(0, 0.2)) = 0
        _Band1("Shade Edge", Range(0, 1)) = 0.34
        _Band2("Light Edge", Range(0, 1)) = 0.62
        _BandSoftness("Band Softness", Range(0.001, 0.3)) = 0.02
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // One CBUFFER for every pass keeps the SRP Batcher on.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadeColor;
            half4 _RimColor;
            half4 _HighlightColor;
            half _RimAmount;
            half _RimSoftness;
            half _HighlightSize;
            half _Band1;
            half _Band2;
            half _BandSoftness;
            half _AmbientStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD3;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = GetShadowCoord(vertexInput);
                #endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normal = normalize(input.normalWS);
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    float4 shadowCoord = float4(0, 0, 0, 0);
                #endif
                Light mainLight = GetMainLight(shadowCoord);

                half ndl = dot(normal, mainLight.direction);
                half lit = saturate(ndl * 0.5h + 0.5h) * mainLight.shadowAttenuation;

                // Three bands: shade, a mid tone, full colour.
                half edge1 = smoothstep(_Band1 - _BandSoftness, _Band1 + _BandSoftness, lit);
                half edge2 = smoothstep(_Band2 - _BandSoftness, _Band2 + _BandSoftness, lit);
                half3 mid = lerp(_ShadeColor.rgb, _BaseColor.rgb, 0.55h);
                half3 colour = lerp(_ShadeColor.rgb, mid, edge1);
                colour = lerp(colour, _BaseColor.rgb, edge2);

                half3 ambient = saturate(SampleSH(normal) * 1.4h);
                colour *= lerp(half3(1, 1, 1), ambient, _AmbientStrength);
                colour *= lerp(half3(1, 1, 1), mainLight.color, 0.5h);

                if (_HighlightSize > 0.0h)
                {
                    half3 halfDir = normalize(mainLight.direction + viewDir);
                    half spec = smoothstep(1.0h - _HighlightSize - 0.01h, 1.0h - _HighlightSize + 0.01h, dot(normal, halfDir));
                    colour = lerp(colour, _HighlightColor.rgb, spec * _HighlightColor.a * saturate(lit * 3.0h));
                }

                half fresnel = 1.0h - saturate(dot(normal, viewDir));
                half rimThreshold = 1.0h - _RimAmount;
                half rim = smoothstep(rimThreshold - _RimSoftness, rimThreshold + _RimSoftness, fresnel) * saturate(0.45h + lit);
                colour = lerp(colour, _RimColor.rgb, rim * _RimColor.a);

                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
