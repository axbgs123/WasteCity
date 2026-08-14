Shader "WasteCity/Terrain/FirstPassGeometry"
{
    Properties
    {
        _BaseColorArray("Base Color Array", 2DArray) = "" {}
        _NormalArray("Normal Array", 2DArray) = "" {}
        _MaskArray("Mask Array", 2DArray) = "" {}
        _HeightArray("Height Array", 2DArray) = "" {}
        _LayerIndex("Terrain Array Layer", Float) = 4
        _TriplanarScale("Triplanar Scale", Float) = 2.25
        _RoleTint("Role Tint", Color) = (1,1,1,1)
        _RoleTintStrength("Role Tint Strength", Range(0,1)) = 0.5
        _MetallicScale("Metallic Scale", Range(0,1)) = 0.15
        _SmoothnessScale("Smoothness Scale", Range(0,1)) = 0.45
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1
        _NormalStrength("Normal Strength", Range(0,2)) = 1
        _HeightStrength("Height Relief Strength", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D_ARRAY(_BaseColorArray);
        SAMPLER(sampler_BaseColorArray);
        TEXTURE2D_ARRAY(_NormalArray);
        SAMPLER(sampler_NormalArray);
        TEXTURE2D_ARRAY(_MaskArray);
        SAMPLER(sampler_MaskArray);
        TEXTURE2D_ARRAY(_HeightArray);
        SAMPLER(sampler_HeightArray);

        CBUFFER_START(UnityPerMaterial)
            float4 _RoleTint;
            float _LayerIndex;
            float _TriplanarScale;
            float _RoleTintStrength;
            float _MetallicScale;
            float _SmoothnessScale;
            float _OcclusionStrength;
            float _NormalStrength;
            float _HeightStrength;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half fogFactor : TEXCOORD2;
            half3 vertexLighting : TEXCOORD3;
            float4 shadowCoord : TEXCOORD4;
        };

        float3 TriplanarWeights(float3 normalWS)
        {
            float3 weights = pow(abs(normalWS), 4.0);
            return weights / max(weights.x + weights.y + weights.z, 0.0001);
        }

        float2 ProjectionUVX(float3 positionWS, float3 normalWS)
        {
            return positionWS.zy * float2(normalWS.x < 0.0 ? -1.0 : 1.0, 1.0) * _TriplanarScale;
        }

        float2 ProjectionUVY(float3 positionWS, float3 normalWS)
        {
            return positionWS.xz * float2(normalWS.y < 0.0 ? -1.0 : 1.0, 1.0) * _TriplanarScale;
        }

        float2 ProjectionUVZ(float3 positionWS, float3 normalWS)
        {
            return positionWS.xy * float2(normalWS.z < 0.0 ? 1.0 : -1.0, 1.0) * _TriplanarScale;
        }

        half3 StrengthenNormal(half3 value)
        {
            value.xy *= _NormalStrength;
            return normalize(value);
        }

        half3 SafeNormalize(half3 value, half3 fallback)
        {
            float lengthSquared = dot(value, value);
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z) ||
                !IsFinite(lengthSquared) || lengthSquared <= 0.000001)
                return normalize(fallback);
            return value * rsqrt(lengthSquared);
        }

        half3 RedirectNormalX(half3 tangentNormal, half signX)
        {
            // X projection uses world Z as tangent and world Y as bitangent.
            return normalize(half3(tangentNormal.z * signX, tangentNormal.y, tangentNormal.x * signX));
        }

        half3 RedirectNormalY(half3 tangentNormal, half signY)
        {
            // Y projection uses world X as tangent and world Z as bitangent.
            return normalize(half3(tangentNormal.x * signY, tangentNormal.z * signY, tangentNormal.y));
        }

        half3 RedirectNormalZ(half3 tangentNormal, half signZ)
        {
            // Z projection uses mirrored world X as tangent and world Y as bitangent.
            return normalize(half3(-tangentNormal.x * signZ, tangentNormal.y, tangentNormal.z * signZ));
        }

        void SampleTriplanar(
            float3 positionWS,
            half3 geometricNormalWS,
            out half4 baseColor,
            out half4 mask,
            out half height,
            out half3 detailNormalWS)
        {
            float3 weights = TriplanarWeights(geometricNormalWS);
            float layer = round(clamp(_LayerIndex, 0.0, 6.0));
            float2 uvX = ProjectionUVX(positionWS, geometricNormalWS);
            float2 uvY = ProjectionUVY(positionWS, geometricNormalWS);
            float2 uvZ = ProjectionUVZ(positionWS, geometricNormalWS);

            half4 baseX = SAMPLE_TEXTURE2D_ARRAY(_BaseColorArray, sampler_BaseColorArray, uvX, layer);
            half4 baseY = SAMPLE_TEXTURE2D_ARRAY(_BaseColorArray, sampler_BaseColorArray, uvY, layer);
            half4 baseZ = SAMPLE_TEXTURE2D_ARRAY(_BaseColorArray, sampler_BaseColorArray, uvZ, layer);
            half4 maskX = SAMPLE_TEXTURE2D_ARRAY(_MaskArray, sampler_MaskArray, uvX, layer);
            half4 maskY = SAMPLE_TEXTURE2D_ARRAY(_MaskArray, sampler_MaskArray, uvY, layer);
            half4 maskZ = SAMPLE_TEXTURE2D_ARRAY(_MaskArray, sampler_MaskArray, uvZ, layer);
            half heightX = SAMPLE_TEXTURE2D_ARRAY(_HeightArray, sampler_HeightArray, uvX, layer).r;
            half heightY = SAMPLE_TEXTURE2D_ARRAY(_HeightArray, sampler_HeightArray, uvY, layer).r;
            half heightZ = SAMPLE_TEXTURE2D_ARRAY(_HeightArray, sampler_HeightArray, uvZ, layer).r;
            half3 normalX = StrengthenNormal(UnpackNormal(
                SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_NormalArray, uvX, layer)));
            half3 normalY = StrengthenNormal(UnpackNormal(
                SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_NormalArray, uvY, layer)));
            half3 normalZ = StrengthenNormal(UnpackNormal(
                SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_NormalArray, uvZ, layer)));

            baseColor = baseX * weights.x + baseY * weights.y + baseZ * weights.z;
            mask = maskX * weights.x + maskY * weights.y + maskZ * weights.z;
            height = heightX * weights.x + heightY * weights.y + heightZ * weights.z;
            half signX = geometricNormalWS.x < 0.0h ? -1.0h : 1.0h;
            half signY = geometricNormalWS.y < 0.0h ? -1.0h : 1.0h;
            half signZ = geometricNormalWS.z < 0.0h ? -1.0h : 1.0h;
            detailNormalWS = SafeNormalize(
                RedirectNormalX(normalX, signX) * weights.x +
                RedirectNormalY(normalY, signY) * weights.y +
                RedirectNormalZ(normalZ, signZ) * weights.z,
                geometricNormalWS);
        }
        ENDHLSL

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                output.vertexLighting = VertexLighting(positions.positionWS, normals.normalWS);
                output.shadowCoord = GetShadowCoord(positions);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 geometricNormalWS = SafeNormalize(input.normalWS, half3(0.0h, 1.0h, 0.0h));
                half4 baseColor;
                half4 mask;
                half height;
                half3 detailNormalWS;
                SampleTriplanar(
                    input.positionWS,
                    geometricNormalWS,
                    baseColor,
                    mask,
                    height,
                    detailNormalWS);

                SurfaceData surfaceData = (SurfaceData)0;
                half heightRelief = (height - 0.5h) * _HeightStrength;
                surfaceData.albedo = lerp(
                    baseColor.rgb,
                    baseColor.rgb * _RoleTint.rgb,
                    saturate(_RoleTintStrength));
                surfaceData.albedo *= 1.0h + heightRelief * 0.12h;
                surfaceData.alpha = 1.0h;
                surfaceData.metallic = saturate(mask.r * _MetallicScale);
                surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
                surfaceData.smoothness = saturate(
                    mask.a * _SmoothnessScale - abs(heightRelief) * 0.08h);
                surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
                surfaceData.occlusion = lerp(
                    1.0h,
                    saturate(mask.g),
                    saturate(_OcclusionStrength));
                surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = SafeNormalize(
                    lerp(geometricNormalWS, detailNormalWS, saturate(mask.b)),
                    geometricNormalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = input.vertexLighting;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                float3 lightDirectionWS = _LightDirection;
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    lightDirectionWS = normalize(_LightPosition - positions.positionWS);
                #endif
                float4 clip = TransformWorldToHClip(ApplyShadowBias(
                    positions.positionWS,
                    normals.normalWS,
                    lightDirectionWS));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return clip;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask 0
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            float4 DepthVert(Attributes input) : SV_POSITION
            {
                return TransformObjectToHClip(input.positionOS.xyz);
            }
            half4 DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack Off
}
