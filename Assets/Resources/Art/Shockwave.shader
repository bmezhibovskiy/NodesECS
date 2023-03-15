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

    // List of properties to control your post process effect
    float4 _PositionsTimes[32];
    int _PositionsCount;
    float _AspectRatio;
    TEXTURE2D_X(_MainTex);

    float2 ShockwaveDistortion(float2 centerUV, float radius, float2 currentPixelUV)
    {
        float aspectRatio = 0.5625;
        // Calculate the distance from the perimiter of the shockwave
        float2 closestPoint = centerUV + normalize(currentPixelUV - centerUV) * radius;
        float2 distVec = currentPixelUV - closestPoint;
        float dist = length(distVec);

        //Higher magnitude means faster falloff and a skinnier shockwave
        double strengthExponent = -50;

        //Higher maxStrength increases the amplitude, or distortion amount
        double maxStrength = 0.16;

        //Strength falls off quickly as distance increases
        double strength = maxStrength * pow(dist + 1, strengthExponent);

        // return strength times normalized distVec
        return  (-strength) * distVec / dist;
    }


    float4 CustomPostProcess(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 resolution = float2(_AspectRatio, 1);

        float2 baseUV = input.texcoord.xy * resolution;

        float2 modifiedUV = float2(0, 0);

        float4 testColor = float4(0,0,0,0);

        for (int i = 0; i < _PositionsCount; i++)
        {
            float4 posTime = _PositionsTimes[i];
            float2 pos = posTime.xy * resolution;
            float time = posTime.w;

            float2 distortion = ShockwaveDistortion(pos, time, baseUV);

            modifiedUV += distortion;
            //testColor += float4(distortion.x, distortion.y, 0, 0);
        }

        float2 finalUV = baseUV + modifiedUV;
        float4 color = SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(finalUV / resolution));

        return color + testColor;
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
                #pragma fragment CustomPostProcess
                #pragma vertex Vert
            ENDHLSL
        }
    }
    Fallback Off
}
