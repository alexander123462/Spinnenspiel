// Vegetations-Shader für URP: GPU-Instancing, Windbewegung im Vertex-Shader,
// beidseitige Beleuchtung mit Durchscheinen, Alpha-Clip für Blattkarten, Schatten und Nebel.
// Vertex-Farbe: rgb = Farbtönung, a = Biegegewicht (0 = Basis, 1 = Spitze).
Shader "Spinnenspiel/Foliage"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _TipColor ("Tip Color", Color) = (1,1,1,1)
        _VariationColor ("Variation Color", Color) = (0.8,0.75,0.4,1)
        _VariationAmount ("Variation Amount", Range(0,1)) = 0.3
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _WindStrength ("Wind Strength", Float) = 0.08
        _WindFrequency ("Wind Frequency", Float) = 1.6
        _Translucency ("Translucency", Range(0,1)) = 0.35
        _Smoothness ("Smoothness", Range(0,1)) = 0.15
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _TipColor;
            half4 _VariationColor;
            half _VariationAmount;
            half _Cutoff;
            float _WindStrength;
            float _WindFrequency;
            half _Translucency;
            half _Smoothness;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Globaler Wind (WindController): xy = Richtung (xz-Ebene), z = Stärke, w = Böe 0..1
        float4 _SpinnenWind;

        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float3 ApplyWind(float3 positionWS, float3 pivotWS, float bend)
        {
            float2 dir = _SpinnenWind.xy;
            float strength = _SpinnenWind.z;
            float gust = _SpinnenWind.w;
            float phase = dot(pivotWS.xz, float2(0.37, 0.61));
            float t = _Time.y * _WindFrequency;
            float sway = sin(t + phase) * 0.6 + sin(t * 2.3 + phase * 1.7) * 0.25 + 0.45;
            float flutter = sin(t * 7.1 + positionWS.x * 3.1 + positionWS.z * 2.7) * 0.08;
            float amount = bend * bend * _WindStrength * strength * (sway + gust * 1.5 + flutter);
            positionWS.xz += dir * amount;
            positionWS.y -= abs(amount) * 0.35 * bend;
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = ApplyWind(positionWS, pivotWS, input.color.a);

                float variation = Hash21(pivotWS.xz);
                half3 tint = lerp(_BaseColor.rgb, _TipColor.rgb, input.color.a) * input.color.rgb;
                tint = lerp(tint, tint * _VariationColor.rgb * 1.4, variation * _VariationAmount);

                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = half4(tint, input.color.a);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
                #endif

                float3 normalWS = normalize(input.normalWS);
                normalWS = IS_FRONT_VFACE(face, normalWS, -normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 color = albedo.rgb * input.color.rgb;

                half ndotl = dot(normalWS, mainLight.direction);
                half wrap = saturate(ndotl * 0.6 + 0.4);
                half back = saturate(-ndotl) * _Translucency;
                half3 lightColor = mainLight.color * mainLight.shadowAttenuation;
                half3 diffuse = color * lightColor * (wrap + back * 1.2);

                float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDir = normalize(mainLight.direction + viewWS);
                half spec = pow(saturate(dot(normalWS, halfDir)), 32.0) * _Smoothness;

                half3 ambient = SampleSH(normalWS) * color;
                // Leichte Selbstverschattung zur Basis hin
                half occlusion = lerp(0.55, 1.0, input.color.a);
                half3 final = (diffuse + ambient) * occlusion + spec * lightColor;
                final = MixFog(final, input.fogFactor);
                return half4(final, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), pivotWS, input.color.a);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), pivotWS, input.color.a);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
