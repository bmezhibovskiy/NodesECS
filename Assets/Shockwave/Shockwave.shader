Shader "Hidden/Shader/Shockwave"
{
    Properties
    {
        // This property is necessary to make the CommandBuffer.Blit bind the source texture to _MainTex
        _MainTex("Main Texture", 2DArray) = "grey" {}
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/FXAA.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"

    #define _PI 3.1415926535897932384626433832795

    //Higher magnitude means faster falloff and a skinnier shockwave. Must be negative.
    static const float distortionExponent = -50;

    //Higher number means the shockwave distorts more intensely overall
    static const float maxAmplitude = 0.16;

    //Higher number means the shockwave dissipates faster.
    static const float decayFactor = 5000;

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    //xy is normalized position, w is radius
    float4 _ShockwavesGeometry[32];
    //x is gauge (thinness), y is intensity, z is decay speed
    float4 _ShockwavesParams[32];
    int _ShockwavesCount;
    float _AspectRatio;
    TEXTURE2D_X(_MainTex);

    float2 ShockwaveDistortion(float2 centerUV, float radius, float2 currentPixelUV, float gauge, float intensity, float decaySpeed)
    {
        float2 closestPoint = centerUV + normalize(currentPixelUV - centerUV) * radius;
        float2 distVec = currentPixelUV - closestPoint;
        float dist = length(distVec);

        float amplitude = intensity * maxAmplitude / (1 + radius * radius * radius * decayFactor * decaySpeed);

        float distortionAmount = amplitude * pow(dist + 1, distortionExponent * gauge);

        return -distortionAmount * distVec / dist;
    }

    float4 ShockwavePostProcess(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 resolution = float2(_AspectRatio, 1);

        float2 baseUV = input.texcoord.xy * resolution;

        float2 modifiedUV = float2(0, 0);

        for (int i = 0; i < _ShockwavesCount; ++i)
        {
            float4 geometry = _ShockwavesGeometry[i];
            float2 shockwaveCenter = geometry.xy * resolution;
            float radius = geometry.w;

            float4 params = _ShockwavesParams[i];
            float gauge = params.x;
            float intensity = params.y;
            float decaySpeed = params.z;

            modifiedUV += ShockwaveDistortion(shockwaveCenter, radius, baseUV, gauge, intensity, decaySpeed);
        }

        float2 finalUV = (baseUV + modifiedUV) / resolution;

        return SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(finalUV));
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Shockwave"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment ShockwavePostProcess
                #pragma vertex Vert
            ENDHLSL
        }
    }
    Fallback Off
}
