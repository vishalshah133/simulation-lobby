// Procedural psychedelic surface for the trip format. Unlit, double-sided, driven entirely by the global _TripTime
// (set from simulation time by TripShaderGlobals), so every frame is a pure function of the run: a re-render is identical.
Shader "SimulationLobby/TripPlasma"
{
    Properties
    {
        [Enum(Plasma,0,Kaleido,1,Stripes,2,Grid,3,Glow,4)] _Mode ("Pattern", Float) = 0
        _PalA ("Palette A (base)", Color) = (0.5, 0.5, 0.5, 1)
        _PalB ("Palette B (amplitude)", Color) = (0.5, 0.5, 0.5, 1)
        _PalC ("Palette C (frequency)", Color) = (1, 1, 1, 1)
        _PalD ("Palette D (phase)", Color) = (0, 0.33, 0.67, 1)
        _Glow ("Glow (HDR multiplier)", Float) = 2
        _Scale ("Pattern scale (world-space props)", Float) = 1
        _Period ("Metres per pattern cycle along the track", Float) = 40
        _Speed ("Pattern speed", Float) = 0.5
        _HueSpeed ("Palette scroll (cycles/s)", Float) = 0.07
        _Fold ("Kaleido folds", Float) = 8
        _Pulse ("Vertex breathing (fraction)", Float) = 0
        _UseWorldUV ("Use world-space UV (props)", Float) = 0
        _Fog ("Fog amount (0 = ignore fog, for the backdrop)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off
        ZWrite On

        Pass
        {
            Name "TripForward"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _TripTime;

            CBUFFER_START(UnityPerMaterial)
                float _Mode;
                float4 _PalA;
                float4 _PalB;
                float4 _PalC;
                float4 _PalD;
                float _Glow;
                float _Scale;
                float _Period;
                float _Speed;
                float _HueSpeed;
                float _Fold;
                float _Pulse;
                float _UseWorldUV;
                float _Fog;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 wp0 = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(IN.normalOS);
                float breathe = sin(_TripTime * 1.3 + dot(wp0, float3(0.11, 0.07, 0.13))) * _Pulse;
                float3 wp = wp0 + nWS * breathe * 1.5;
                OUT.positionWS = wp;
                OUT.normalWS = nWS;
                OUT.positionCS = TransformWorldToHClip(wp);
                OUT.uv = IN.uv;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            float3 Palette(float t)
            {
                return _PalA.rgb + _PalB.rgb * cos(6.28318 * (_PalC.rgb * t + _PalD.rgb));
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float t = _TripTime;
                float2 uv = IN.uv;
                float3 wp = IN.positionWS;
                if (_UseWorldUV > 0.5)
                {
                    uv = float2(wp.x + wp.z * 0.7, wp.y + wp.z * 0.4) * 0.12;
                }
                float2 p = uv * _Scale;
                float hue = t * _HueSpeed;
                float ts = t * _Speed;
                float3 col;

                // `along` counts pattern cycles down the track (one per _Period metres). Keeping the period long
                // (tens of metres) is what keeps the ride flash-safe at 100+ m/s: the pattern passes at about 2 Hz, not as a strobe.
                float along = uv.y / max(1.0, _Period);
                if (_UseWorldUV > 0.5) along = p.y * 0.1;

                if (_Mode < 0.5)
                {
                    // plasma: four interfering waves
                    float2 q = float2(uv.x * 12.56637, along * 6.28318);
                    if (_UseWorldUV > 0.5) q = p * 6.0;
                    float v = sin(q.x + ts) + sin(q.y * 1.3 - ts * 1.3) + sin((q.x + q.y) * 0.7 + ts * 0.7)
                            + sin(length(q + float2(sin(ts * 0.5), cos(ts * 0.4)) * 3.0) * 1.4 - ts);
                    col = Palette(v * 0.25 + hue) * (0.55 + 0.45 * sin(v * 2.0));
                }
                else if (_Mode < 1.5)
                {
                    // kaleidoscope: fold the angle around the tunnel, then ripple along it
                    float a = abs(frac(uv.x * _Fold) - 0.5) * 2.0;
                    float v = sin(a * 9.0 + along * 6.28318 - ts * 2.0) + sin(a * 4.0 - along * 3.14159 + ts) * 0.7;
                    float ring = smoothstep(0.1, 0.5, abs(frac(along - ts * 0.15) - 0.5));
                    col = Palette(v * 0.2 + a * 0.5 + hue) * (0.5 + 0.5 * ring);
                }
                else if (_Mode < 2.5)
                {
                    // soft neon stripes that spiral down the tunnel
                    float s = along - ts * 0.15 + uv.x * 2.0;
                    float band = frac(s);
                    float edge = smoothstep(0.0, 0.3, band) * (1.0 - smoothstep(0.55, 0.9, band));
                    col = Palette(floor(s) * 0.17 + hue) * (0.25 + 0.75 * edge);
                }
                else if (_Mode < 3.5)
                {
                    // neon grid on a dark ground
                    float2 g = abs(frac(float2(uv.x * 8.0, along) + float2(0.0, -ts * 0.1)) - 0.5);
                    float ln = smoothstep(0.38, 0.5, max(g.x, g.y));
                    col = Palette(along * 0.2 + hue) * (0.12 + 0.88 * ln);
                }
                else
                {
                    // glow: a gradient that rolls across the surface, brightest at grazing angles
                    float3 V = normalize(GetCameraPositionWS() - wp);
                    float fres = pow(1.0 - saturate(abs(dot(normalize(IN.normalWS), V))), 2.0);
                    float k = dot(normalize(IN.normalWS), float3(0.3, 0.8, 0.5)) * 0.4 + wp.z * 0.004 + wp.y * 0.003 + hue + ts * 0.05;
                    col = Palette(k) * (0.5 + 1.2 * fres);
                }

                col *= _Glow;
                col = lerp(col, MixFog(col, IN.fogFactor), _Fog);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
