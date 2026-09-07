Shader "ProjectNO/Province Terrain Overlay"
{
    Properties
    {
        _ProvinceMap ("Province Map", 2D) = "black" {}
        _TintMap ("Province Tint Map", 2D) = "white" {}
        _FillOpacity ("Fill Opacity", Range(0, 1)) = 0.20
        _BorderColor ("Border Color", Color) = (0.08, 0.10, 0.12, 0.82)
        _HoverBorderColor ("Hover Border Color", Color) = (1, 0.32, 0, 1)
        _BorderOpacity ("Border Opacity", Range(0, 1)) = 0.82
        _HoverColor ("Hover Color", Color) = (0, 0, 0, 0)
        _HasHover ("Has Hover", Float) = 0
        _FlipY ("Flip Y", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ProvinceMap);
            SAMPLER(sampler_ProvinceMap);
            float4 _ProvinceMap_TexelSize;
            TEXTURE2D(_TintMap);
            SAMPLER(sampler_TintMap);

            CBUFFER_START(UnityPerMaterial)
                half _FillOpacity;
                half4 _BorderColor;
                half4 _HoverBorderColor;
                half _BorderOpacity;
                half4 _HoverColor;
                half _HasHover;
                half _FlipY;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float2 GetMapUv(float2 uv)
            {
                return float2(uv.x, lerp(uv.y, 1.0h - uv.y, _FlipY));
            }

            half ColorDifference(half4 left, half4 right)
            {
                return step(0.004h, dot(abs(left.rgb - right.rgb), half3(1, 1, 1)) + abs(left.a - right.a));
            }

            half MatchesHover(half4 color)
            {
                half difference = dot(abs(color.rgb - _HoverColor.rgb), half3(1, 1, 1)) + abs(color.a - _HoverColor.a);
                return _HasHover * (1.0h - step(0.004h, difference));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 mapUv = GetMapUv(input.uv);
                float2 texel = _ProvinceMap_TexelSize.xy;
                half4 center = SAMPLE_TEXTURE2D(_ProvinceMap, sampler_ProvinceMap, mapUv);
                half4 right = SAMPLE_TEXTURE2D(_ProvinceMap, sampler_ProvinceMap, mapUv + float2(texel.x, 0));
                half4 left = SAMPLE_TEXTURE2D(_ProvinceMap, sampler_ProvinceMap, mapUv - float2(texel.x, 0));
                half4 top = SAMPLE_TEXTURE2D(_ProvinceMap, sampler_ProvinceMap, mapUv + float2(0, texel.y));
                half4 bottom = SAMPLE_TEXTURE2D(_ProvinceMap, sampler_ProvinceMap, mapUv - float2(0, texel.y));

                half border = max(max(ColorDifference(center, right), ColorDifference(center, left)),
                    max(ColorDifference(center, top), ColorDifference(center, bottom)));
                half nearHoveredProvince = max(max(MatchesHover(center), MatchesHover(right)),
                    max(MatchesHover(left), max(MatchesHover(top), MatchesHover(bottom))));
                half hoverBorder = border * nearHoveredProvince;

                half4 tint = SAMPLE_TEXTURE2D(_TintMap, sampler_TintMap, mapUv);
                half fillAlpha = tint.a * _FillOpacity;
                half4 result = half4(tint.rgb, fillAlpha);
                half4 borderColor = lerp(_BorderColor, _HoverBorderColor, hoverBorder);
                half borderAlpha = border * lerp(_BorderOpacity, 1.0h, hoverBorder);
                result.rgb = lerp(result.rgb, borderColor.rgb, borderAlpha);
                result.a = max(result.a, borderAlpha);
                return result;
            }
            ENDHLSL
        }
    }
}
