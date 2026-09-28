// Einfacher, physikalisch plausibler Wasser-Shader für URP (transparent):
// zwei gegenläufig scrollende Normal-Maps, Fresnel zwischen Tiefenfarbe und Himmelsreflexion (SH),
// Glanzlicht der Sonne, leichte Vertex-Wellen, Nebel. Fließrichtung per Material (_FlowDirection).
Shader "Spinnenspiel/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.22, 0.32, 0.28, 0.55)
        _DeepColor ("Deep Color", Color) = (0.05, 0.12, 0.12, 0.85)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.6
        _NormalTiling ("Normal Tiling", Float) = 0.8
        _FlowDirection ("Flow Direction (xy) Speed (z)", Vector) = (1, 0, 0.12, 0)
        _WaveHeight ("Wave Height", Float) = 0.004
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
        _FresnelPower ("Fresnel Power", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float4 _NormalMap_ST;
                half _NormalStrength;
                float _NormalTiling;
                float4 _FlowDirection;
                float _WaveHeight;
                half _Smoothness;
                half _FresnelPower;
            CBUFFER_END

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half depthFactor : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float t = _Time.y;
                positionWS.y += (sin(positionWS.x * 3.1 + t * 1.7) + sin(positionWS.z * 2.3 - t * 1.3)) * _WaveHeight;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                // Vertex-Farbe Rot = relative Wassertiefe (0 = Ufer, 1 = Mitte), vom Mesh-Generator gesetzt
                output.depthFactor = input.color.r;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half3 SampleWaterNormal(float2 uv)
            {
                half3 n = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalStrength);
                return n;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float2 flow = normalize(_FlowDirection.xy + 1e-5) * _FlowDirection.z;
                float2 uv = input.positionWS.xz * _NormalTiling;
                half3 n1 = SampleWaterNormal(uv + flow * t);
                half3 n2 = SampleWaterNormal(uv * 1.7 - flow.yx * t * 0.6 + 0.37);
                half3 nTS = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                float3 normalWS = normalize(float3(nTS.x, nTS.z, nTS.y));

                float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), _FresnelPower);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = lerp(0.6, 1.0, mainLight.shadowAttenuation);

                half4 body = lerp(_ShallowColor, _DeepColor, saturate(input.depthFactor));
                half3 sky = SampleSH(reflect(-viewWS, normalWS));
                half3 color = lerp(body.rgb * (SampleSH(float3(0, 1, 0)) * 0.6 + mainLight.color * 0.25 * shadow), sky, fresnel * 0.8);

                float3 halfDir = normalize(mainLight.direction + viewWS);
                half specPower = exp2(10 * _Smoothness + 1);
                half spec = pow(saturate(dot(normalWS, halfDir)), specPower) * _Smoothness * 2.0;
                color += mainLight.color * spec * shadow;

                half alpha = saturate(lerp(body.a, 1.0, fresnel * 0.6) + spec);
                // Weicher Uferrand
                alpha *= smoothstep(0.0, 0.25, input.depthFactor);
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
