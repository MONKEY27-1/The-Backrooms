// The camcorder's tape: what a cheap 90s camcorder does to the picture on its way onto VHS.
// Pass 0 shrinks HDRP's frame (texture array) into one of our own small 2D targets,
// pass 1 shrinks one of our targets further (the exposure meter's chain),
// pass 2 rebuilds the full-size frame from the small one as a VHS signal.
Shader "Hidden/Backrooms/VhsSignal"
{
    Properties
    {
        _MainTex("Main Texture", 2DArray) = "grey" {}
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"

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

    TEXTURE2D_X(_MainTex);
    TEXTURE2D(_SrcTex);
    TEXTURE2D(_LowTex);
    float4 _SrcSize;    // source: width, height, 1/width, 1/height
    float4 _LowSize;    // tape frame: width, height, 1/width, 1/height
    float  _ClampMax;   // the meter caps very bright pixels so a lamp in view doesn't swamp the average
    float4 _Vhs;        // x intensity, y damage, z field number (59.94 Hz), w visible width (0.75 = 4:3 on 16:9)
    float4 _Vhs2;       // x tracking band position, y seconds, z reduce flashing

    float3 Capped(float3 c) { float l = max(Luminance(c), 1e-5); return c * min(1.0, _ClampMax / l); }

    // Pass 0: HDRP's frame -> our target, a 4-tap box over one source texel each side.
    float4 DownFromCamera(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        float2 o = _SrcSize.zw;
        float3 c = 0;
        c += Capped(SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(uv + float2(-o.x, -o.y))).rgb);
        c += Capped(SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(uv + float2( o.x, -o.y))).rgb);
        c += Capped(SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(uv + float2(-o.x,  o.y))).rgb);
        c += Capped(SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(uv + float2( o.x,  o.y))).rgb);
        return float4(c * 0.25, 1);
    }

    // Pass 1: one of our 2D targets -> a smaller one (4x), same box.
    float4 Down2D(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;
        float2 o = _SrcSize.zw;
        float3 c = SAMPLE_TEXTURE2D_LOD(_SrcTex, s_linear_clamp_sampler, uv + float2(-o.x, -o.y), 0).rgb
                 + SAMPLE_TEXTURE2D_LOD(_SrcTex, s_linear_clamp_sampler, uv + float2( o.x, -o.y), 0).rgb
                 + SAMPLE_TEXTURE2D_LOD(_SrcTex, s_linear_clamp_sampler, uv + float2(-o.x,  o.y), 0).rgb
                 + SAMPLE_TEXTURE2D_LOD(_SrcTex, s_linear_clamp_sampler, uv + float2( o.x,  o.y), 0).rgb;
        return float4(c * 0.25, 1);
    }

    // PCG-style integer hash: no visible pattern even on neighbouring pixels (the float hash showed a weave).
    float Hash(float2 p)
    {
        uint2 v = (uint2)(int2)floor(p * 7.0 + 65536.0);
        uint h = v.x * 747796405u + v.y * 2891336453u;
        h = ((h >> ((h >> 28u) + 4u)) ^ h) * 277803737u;
        h = (h >> 22u) ^ h;
        return h * (1.0 / 4294967295.0);
    }

    float3 RgbToYiq(float3 c) { return float3(dot(c, float3(0.299, 0.587, 0.114)), dot(c, float3(0.596, -0.274, -0.322)), dot(c, float3(0.211, -0.523, 0.312))); }
    float3 YiqToRgb(float3 y) { return float3(y.x + 0.956 * y.y + 0.621 * y.z, y.x - 0.272 * y.y - 0.647 * y.z, y.x - 1.106 * y.y + 1.703 * y.z); }
    // The tape works on the gamma-encoded signal, so the smearing happens there too.
    float3 Tape(float2 uv) { return RgbToYiq(LinearToSRGB(saturate(SAMPLE_TEXTURE2D_LOD(_LowTex, s_linear_clamp_sampler, uv, 0).rgb))); }

    // Pass 2: the VHS signal.
    float4 Composite(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        float I = _Vhs.x, D = _Vhs.y, field = _Vhs.z;
        float safe = 1.0 - _Vhs2.z * 0.85;

        if (abs(uv.x - 0.5) > _Vhs.w * 0.5) return float4(0, 0, 0, 1);   // 4:3 pillarbox

        // HDRP's post-process uvs run bottom-up on screen here (checked in a D3D11 capture: the tear must be at the bottom).
    #if UNITY_UV_STARTS_AT_TOP
        float top = 1.0 - uv.y;
    #else
        float top = uv.y;
    #endif
        float line_ = floor(top * 240.0);   // one field's lines
        float px = _LowSize.z;               // one tape pixel, in uv

        // Time-base error: every line sits a little off, new every field; a slow skew over the picture.
        float shift = (Hash(float2(line_, field)) - 0.5) * px * (0.35 + 3.0 * D) * I;
        shift += sin(top * 9.0 + _Vhs2.y * 1.7) * px * 0.25 * I;
        // A tracking band rolls up the picture when the signal is hurt.
        float bandDist = abs(frac(top - _Vhs2.x + 0.5) - 0.5);
        float band = saturate(1.0 - bandDist / (0.015 + 0.05 * D)) * saturate(D * 1.5 - 0.1) * safe;
        shift += (Hash(float2(line_, field + 7.0)) - 0.5) * px * 30.0 * band;
        // Head switching: the bottom few lines tear sideways.
        float hs = saturate((top - 0.975) / 0.025);
        shift += hs * hs * px * (6.0 + 10.0 * Hash(float2(field, 3.0))) * I;
        float2 suv = float2(uv.x + shift, uv.y);

        // Luma: a narrow gaussian along the line (wider read as out of focus rather than as tape).
        float lw = px * (0.3 + 0.3 * I);
        float Y = 0, wy = 0;
        [unroll] for (int i = -2; i <= 2; i++) { float w = exp(-i * i * 0.5); Y += w * Tape(suv + float2(i * lw, 0)).x; wy += w; }
        Y /= wy;
        // Chroma: only ~40 lines, smeared wide and lagging to the right.
        float cw = px * (1.2 + 2.2 * I), delay = px * 1.2 * I;
        float2 IQ = 0; float Yw = 0, wc = 0;
        [unroll] for (int k = -3; k <= 3; k++) { float w = exp(-k * k / 4.5); float3 s = Tape(suv + float2(k * cw - delay, 0)); IQ += w * s.yz; Yw += w * s.x; wc += w; }
        IQ /= wc; Yw /= wc;

        Y += (Y - Yw) * 0.45 * I;              // the sharpness circuit: a halo at every edge
        Y = lerp(Y, Y * 0.93 + 0.03, I);       // muddy blacks
        IQ *= lerp(1.0, 0.85, I);

        // Tape noise: grainier in the dark; coloured streaks along the lines.
        float2 pix = floor(float2(uv.x * _LowSize.x * 1.5, top * _LowSize.y));
        float grain = Hash(pix + field * 13.1) - 0.5;
        Y += grain * (0.035 + 0.05 * D) * I * (1.2 - Y);
        float2 streak = float2(Hash(float2(floor(uv.x * _LowSize.x / 6.0), line_ + field * 3.7)),
                               Hash(float2(line_, field * 1.3 + floor(uv.x * _LowSize.x / 9.0)))) - 0.5;
        IQ += streak * (0.012 + 0.05 * D) * I;

        // Dropouts: thin bright streaks where the tape lost its oxide.
        if (Hash(float2(line_ * 1.37, field)) < (0.0015 + 0.03 * D * D) * I * safe)
        {
            float x = uv.x - Hash(float2(line_, field + 1.0));
            float len = 0.05 + 0.3 * Hash(float2(line_, field + 2.0));
            if (x > 0 && x < len) Y = lerp(Y, 0.9 + grain * 0.3, 0.75 * (1.0 - x / len));
        }
        Y = lerp(Y, Hash(pix + field) * 0.8, band * 0.6);        // the tracking band's snow
        Y = lerp(Y, Hash(pix + field * 3.0), hs * 0.35 * I);     // head-switch noise
        float snow = smoothstep(0.8, 1.0, D) * 0.55 * safe;      // a dying signal: only right at the edge
        Y = lerp(Y, Hash(pix + field * 5.3), snow);
        IQ *= 1.0 - snow;

        return float4(SRGBToLinear(saturate(YiqToRgb(float3(Y, IQ)))), 1);
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "DownFromCamera"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DownFromCamera
            ENDHLSL
        }
        Pass
        {
            Name "Down2D"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Down2D
            ENDHLSL
        }
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
    }
    Fallback Off
}
