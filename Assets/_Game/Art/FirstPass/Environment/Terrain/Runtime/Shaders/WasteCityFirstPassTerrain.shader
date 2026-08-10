Shader "WasteCity/Terrain/FirstPassBlend"
{
    Properties
    {
        _BaseColorArray("Base Color Array", 2DArray) = "" {}
        _NormalArray("Normal Array", 2DArray) = "" {}
        _MaskArray("Mask Array", 2DArray) = "" {}
        _HeightArray("Height Array", 2DArray) = "" {}
        _ControlA("Control A", 2D) = "white" {}
        _ControlB("Control B", 2D) = "black" {}
        _WorldOriginXZ("World Origin XZ", Vector) = (0,0,0,0)
        _WorldSizeXZ("World Size XZ", Vector) = (1,1,0,0)
        _CellsPerTexture("Cells Per Texture", Float) = 4
        _HeightBlendStrength("Height Blend Strength", Float) = 1
        _MacroVariation("Macro Variation", Float) = 0.05
        _WaterVelocityA("Water Velocity A", Vector) = (0.006,0.002,0,0)
        _WaterVelocityB("Water Velocity B", Vector) = (-0.003,0.005,0,0)
        _WaterNormalScaleB("Water Normal Scale B", Float) = 1.17
        _WaterHighlightStrength("Water Highlight Strength", Float) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

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
            TEXTURE2D(_ControlA);
            SAMPLER(sampler_ControlA);
            TEXTURE2D(_ControlB);
            SAMPLER(sampler_ControlB);

            CBUFFER_START(UnityPerMaterial)
                float4 _WorldOriginXZ;
                float4 _WorldSizeXZ;
                float _CellsPerTexture;
                float _HeightBlendStrength;
                float _MacroVariation;
                float4 _WaterVelocityA;
                float4 _WaterVelocityB;
                float _WaterNormalScaleB;
                float _WaterHighlightStrength;
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
                half4 tangentWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            struct LayerSample
            {
                half4 baseColor;
                half3 normalTS;
                half4 mask;
                half height;
                half waterHighlight;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(
                    normals.tangentWS,
                    input.tangentOS.w * GetOddNegativeScale());
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            void InsertLayer(
                uint candidateIndex,
                float candidateWeight,
                inout uint index0,
                inout float weight0,
                inout uint index1,
                inout float weight1,
                inout uint index2,
                inout float weight2)
            {
                if (candidateWeight > weight0)
                {
                    index2 = index1;
                    weight2 = weight1;
                    index1 = index0;
                    weight1 = weight0;
                    index0 = candidateIndex;
                    weight0 = candidateWeight;
                }
                else if (candidateWeight > weight1)
                {
                    index2 = index1;
                    weight2 = weight1;
                    index1 = candidateIndex;
                    weight1 = candidateWeight;
                }
                else if (candidateWeight > weight2)
                {
                    index2 = candidateIndex;
                    weight2 = candidateWeight;
                }
            }

            LayerSample SampleLayer(uint layerIndex, float2 worldUV)
            {
                LayerSample sample;
                sample.baseColor = SAMPLE_TEXTURE2D_ARRAY(
                    _BaseColorArray,
                    sampler_BaseColorArray,
                    worldUV,
                    layerIndex);
                sample.mask = SAMPLE_TEXTURE2D_ARRAY(
                    _MaskArray,
                    sampler_MaskArray,
                    worldUV,
                    layerIndex);
                sample.height = SAMPLE_TEXTURE2D_ARRAY(
                    _HeightArray,
                    sampler_HeightArray,
                    worldUV,
                    layerIndex).r;
                sample.waterHighlight = 0;

                float2 normalUV = worldUV;
                if (layerIndex == 5u)
                    normalUV += _Time.y * _WaterVelocityA.xy;
                half3 normalA = UnpackNormal(SAMPLE_TEXTURE2D_ARRAY(
                    _NormalArray,
                    sampler_NormalArray,
                    normalUV,
                    layerIndex));
                sample.normalTS = normalA;

                if (layerIndex == 5u)
                {
                    float2 normalUVB =
                        worldUV * _WaterNormalScaleB + _Time.y * _WaterVelocityB.xy;
                    half3 normalB = UnpackNormal(SAMPLE_TEXTURE2D_ARRAY(
                        _NormalArray,
                        sampler_NormalArray,
                        normalUVB,
                        layerIndex));
                    sample.normalTS = normalize(half3(
                        normalA.xy + normalB.xy,
                        max(0.001h, normalA.z * normalB.z)));
                    sample.waterHighlight = saturate(dot(normalA, normalB) * 0.5h + 0.5h);
                }

                return sample;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 mapUV = saturate(
                    (input.positionWS.xz - _WorldOriginXZ.xy) /
                    max(_WorldSizeXZ.xy, float2(0.0001, 0.0001)));
                float4 baseWeights = SAMPLE_TEXTURE2D(_ControlA, sampler_ControlA, mapUV);
                float4 specialWeights = SAMPLE_TEXTURE2D(_ControlB, sampler_ControlB, mapUV);
                float weights[7] =
                {
                    baseWeights.r,
                    baseWeights.g,
                    baseWeights.b,
                    baseWeights.a,
                    specialWeights.r,
                    specialWeights.g,
                    specialWeights.b,
                };

                float totalWeight = 0;
                [unroll]
                for (uint layer = 0u; layer < 7u; layer++)
                    totalWeight += max(0, weights[layer]);
                if (totalWeight <= 0.0001)
                {
                    weights[0] = 1;
                    [unroll]
                    for (uint emptyLayer = 1u; emptyLayer < 7u; emptyLayer++)
                        weights[emptyLayer] = 0;
                    totalWeight = 1;
                }
                [unroll]
                for (uint normalizedLayer = 0u; normalizedLayer < 7u; normalizedLayer++)
                    weights[normalizedLayer] = max(0, weights[normalizedLayer]) / totalWeight;

                uint index0 = 0u;
                uint index1 = 0u;
                uint index2 = 0u;
                float weight0 = -1;
                float weight1 = -1;
                float weight2 = -1;
                [unroll]
                for (uint candidate = 0u; candidate < 7u; candidate++)
                {
                    InsertLayer(
                        candidate,
                        weights[candidate],
                        index0,
                        weight0,
                        index1,
                        weight1,
                        index2,
                        weight2);
                }

                float2 worldUV = input.positionWS.xz / max(_CellsPerTexture, 0.0001);
                LayerSample sample0 = SampleLayer(index0, worldUV);
                LayerSample sample1 = SampleLayer(index1, worldUV);
                LayerSample sample2 = SampleLayer(index2, worldUV);

                float heightStrength = saturate(_HeightBlendStrength);
                float heightWeight0 = weight0 * lerp(1.0, 0.5 + sample0.height, heightStrength);
                float heightWeight1 = weight1 * lerp(1.0, 0.5 + sample1.height, heightStrength);
                float heightWeight2 = weight2 * lerp(1.0, 0.5 + sample2.height, heightStrength);
                float selectedWeightTotal = max(
                    heightWeight0 + heightWeight1 + heightWeight2,
                    0.0001);
                float3 blendWeights =
                    float3(heightWeight0, heightWeight1, heightWeight2) / selectedWeightTotal;

                half4 blendedBaseColor =
                    sample0.baseColor * blendWeights.x +
                    sample1.baseColor * blendWeights.y +
                    sample2.baseColor * blendWeights.z;
                half4 blendedMask =
                    sample0.mask * blendWeights.x +
                    sample1.mask * blendWeights.y +
                    sample2.mask * blendWeights.z;
                half3 blendedNormalTS = normalize(
                    sample0.normalTS * blendWeights.x +
                    sample1.normalTS * blendWeights.y +
                    sample2.normalTS * blendWeights.z);
                half waterHighlight =
                    sample0.waterHighlight * blendWeights.x +
                    sample1.waterHighlight * blendWeights.y +
                    sample2.waterHighlight * blendWeights.z;

                float macroWave =
                    sin(input.positionWS.x * 0.071) * 0.5 +
                    sin(input.positionWS.z * 0.053) * 0.35 +
                    sin((input.positionWS.x + input.positionWS.z) * 0.031) * 0.15;
                float macroTint =
                    1.0 + macroWave * min(saturate(_MacroVariation), 0.05);
                blendedBaseColor.rgb *= macroTint;

                half3 bitangentWS =
                    input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = TransformTangentToWorld(
                    blendedNormalTS,
                    half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS));
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.fogCoord = input.fogFactor;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = blendedBaseColor.rgb;
                surfaceData.alpha = blendedBaseColor.a;
                surfaceData.normalTS = blendedNormalTS;
                surfaceData.metallic = blendedMask.r;
                surfaceData.occlusion = blendedMask.g;
                surfaceData.smoothness = saturate(
                    blendedMask.a +
                    waterHighlight * min(saturate(_WaterHighlightStrength), 0.12));
                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
}
