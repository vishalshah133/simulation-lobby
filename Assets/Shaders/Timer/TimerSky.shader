Shader "Simulation Lobby/Timer Sky"
{
    Properties
    {
        _TopColor("Top Color", Color) = (0.25, 0.55, 0.95, 1)
        _HorizonColor("Horizon Color", Color) = (0.75, 0.9, 1, 1)
        _Power("Gradient Power", Range(0.2, 3)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" }

        // An inverted sphere around the scene: vertical gradient, unlit, unfogged, never writes depth.
        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half _Power;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float height : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.height = normalize(input.positionOS.xyz).y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half t = pow(saturate(input.height), _Power);
                return half4(lerp(_HorizonColor.rgb, _TopColor.rgb, t), 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
