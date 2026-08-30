Shader "ProjectNO/Gaea Water Mask"
{
    Properties
    {
        _WaterColor ("Water Color", Color) = (0.04, 0.27, 0.42, 0.38)
        _ShallowColor ("Shallow Color", Color) = (0.18, 0.58, 0.72, 0.28)
        _FoamColor ("Foam Color", Color) = (0.72, 0.95, 1, 0.35)
        _MaskTex ("Water Mask", 2D) = "white" {}
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.45
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.12
        _RippleScale ("Ripple Scale", Float) = 80
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.32
        _WaveAmplitude ("Wave Amplitude", Float) = 0.18
        _WaveScale ("Wave Scale", Float) = 22
        _WaveSpeed ("Wave Speed", Float) = 1.2
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            float4 _MaskTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                half _AlphaCutoff;
                half _EdgeSoftness;
                half _RippleScale;
                half _RippleStrength;
                half _WaveAmplitude;
                half _WaveScale;
                half _WaveSpeed;
                half _FresnelPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float waveA = sin((positionOS.x + positionOS.z) / max(0.001h, _WaveScale) + _Time.y * _WaveSpeed);
                float waveB = sin((positionOS.x * 0.63 - positionOS.z * 1.17) / max(0.001h, _WaveScale * 0.57h) - _Time.y * (_WaveSpeed * 1.31h));
                positionOS.y += (waveA + waveB) * 0.5h * _WaveAmplitude;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(float3(0, 1, 0));
                output.uv = input.uv;
                return output;
            }

            half Hash21(float2 p)
            {
                p = frac(p * half2(123.34h, 456.21h));
                p += dot(p, p + 45.32h);
                return frac(p.x * p.y);
            }

            half SmoothNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0h - 2.0h * f);

                half a = Hash21(i);
                half b = Hash21(i + float2(1, 0));
                half c = Hash21(i + float2(0, 1));
                half d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 texel = _MaskTex_TexelSize.xy;
                half mask =
                    SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv).r * 0.36h +
                    SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + texel * float2(1, 0)).r * 0.08h +
                    SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + texel * float2(-1, 0)).r * 0.08h +
                    SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + texel * float2(0, 1)).r * 0.08h +
                    SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + texel * float2(0, -1)).r * 0.08h +
                    SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + texel * float2(1, 1)).r * 0.08h +
                    SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + texel * float2(-1, 1)).r * 0.08h +
                    SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + texel * float2(1, -1)).r * 0.08h +
                    SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + texel * float2(-1, -1)).r * 0.08h;
                half edge = smoothstep(_AlphaCutoff - _EdgeSoftness, _AlphaCutoff + _EdgeSoftness, mask);
                clip(edge - 0.01h);

                half shoreline = saturate(edge);
                float2 rippleUvA = input.uv * _RippleScale + float2(_Time.y * 0.025, _Time.y * 0.017);
                float2 rippleUvB = input.uv * (_RippleScale * 0.43) + float2(-_Time.y * 0.018, _Time.y * 0.029);
                half ripple = (SmoothNoise(rippleUvA) + SmoothNoise(rippleUvB)) * 0.5h;
                half waveLineA = pow(saturate(sin((input.uv.x + input.uv.y) * _RippleScale * 0.9h + _Time.y * 1.7h) * 0.5h + 0.5h), 7.0h);
                half waveLineB = pow(saturate(sin((input.uv.x * 1.8h - input.uv.y * 0.7h) * _RippleScale * 0.55h - _Time.y * 1.2h) * 0.5h + 0.5h), 9.0h);
                half waveLines = (waveLineA + waveLineB) * 0.5h;

                half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(saturate(1.0h - dot(normalize(input.normalWS), viewDir)), _FresnelPower);

                half3 color = lerp(_ShallowColor.rgb, _WaterColor.rgb, shoreline);
                color += ripple * _RippleStrength;
                color += waveLines * half3(0.22h, 0.36h, 0.42h) * _RippleStrength;
                color += fresnel * half3(0.35h, 0.58h, 0.72h);
                color = lerp(color, _FoamColor.rgb, (1.0h - shoreline) * 0.35h);

                half alpha = lerp(_ShallowColor.a, _WaterColor.a, shoreline);
                alpha += fresnel * 0.2h;
                alpha *= edge;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
