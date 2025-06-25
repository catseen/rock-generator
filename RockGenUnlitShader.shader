Shader "Unlit/RockGenUnlitShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}  
        _BlendStrength ("Blend Strength", Range(0.1, 5)) = 2.0  
        _Scale ("Texture Scale", Range(0.01, 1)) = 0.1  

        _UseGradient ("Use Gradient", Range(0, 1)) = 1  
        _ColorBottom ("Bottom Color", Color) = (0.3, 0.2, 0.1, 1) 
        _ColorTop ("Top Color", Color) = (0.8, 0.7, 0.5, 1)
        _GradientCenter ("Gradient Center", Range(0, 1)) = 0.8
        _GradientHeight ("Gradient Height", Range(0.01, 1)) = 0.3
        _GradientSharpness ("Gradient Sharpness", Range(0.01, 20)) = 20.0

        _ShadowIntensity ("Shadow Intensity", Range(0, 1)) = 1  

        _UseRim ("Use Rim Light", Range(0, 1)) = 1
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.1, 8)) = 2.0
        _RimIntensity ("Rim Intensity", Range(0, 3)) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 localPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float heightFactor : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _ColorTop, _ColorBottom;
            float _BlendStrength, _Scale, _ShadowIntensity, _UseGradient;
            float _UseRim, _RimPower, _RimIntensity;
            float4 _RimColor;
            float _MinHeight, _MaxHeight;
            float _GradientCenter, _GradientSharpness;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.localPos = IN.positionOS.xyz;
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                OUT.heightFactor = saturate((IN.positionOS.y - _MinHeight) / (_MaxHeight - _MinHeight));
                return OUT;
            }

            float3 TriplanarMapping(float3 worldPos, float3 worldNormal)
            {
                float3 blending = abs(worldNormal);
                blending = pow(blending, _BlendStrength);
                blending /= (blending.x + blending.y + blending.z);

                float2 uvX = worldPos.zy * _Scale;
                float2 uvY = worldPos.xz * _Scale;
                float2 uvZ = worldPos.xy * _Scale;

                float4 colX = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvX);
                float4 colY = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvY);
                float4 colZ = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvZ);

                return colX.rgb * blending.x + colY.rgb * blending.y + colZ.rgb * blending.z;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 normal = normalize(IN.worldNormal);
                Light mainLight = GetMainLight();

                float3 texColor = TriplanarMapping(IN.worldPos, normal);

                float topBlend = saturate(dot(normal, float3(0, 1, 0)));
                float gradientRaw = smoothstep(_GradientCenter - 0.5 / _GradientSharpness, _GradientCenter + 0.5 / _GradientSharpness, IN.heightFactor);
                float gradientFactor = _UseGradient * topBlend * gradientRaw;

                float3 gradientColor = lerp(_ColorBottom.rgb, _ColorTop.rgb, gradientFactor);
                float3 finalColor = lerp(texColor, gradientColor, _UseGradient);

                float NdotL = max(0, dot(normal, mainLight.direction));
                NdotL = lerp(1, NdotL, _ShadowIntensity); 
                finalColor *= mainLight.color * NdotL;

                if (_UseRim > 0.5)
                {
                    float3 viewDir = normalize(_WorldSpaceCameraPos - IN.worldPos);
                    float rimFactor = 1.0 - saturate(dot(viewDir, normal));
                    float rimIntensity = pow(rimFactor, _RimPower) * _RimIntensity;
                    finalColor += rimIntensity * _RimColor.rgb;
                }

                return half4(finalColor, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 MyApplyShadowBias(float3 positionWS, float3 normalWS, float bias)
            {
                return positionWS + normalWS * bias;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 worldNormal = TransformObjectToWorldNormal(IN.normalOS);

                float3 biasedPos = MyApplyShadowBias(worldPos, worldNormal, 0.0);
                float4 posCS = TransformWorldToHClip(biasedPos);
                OUT.positionCS = posCS;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}