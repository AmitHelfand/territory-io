// Territory.io land shader.
// The territory texture holds ONE texel per grid cell: R = who owns the cell (a "key"),
// G = whose trail is on the cell (only used when trails are drawn as cells).
// Instead of showing those texels as squares, every screen pixel blends the 3x3 nearest cells
// with smooth (quadratic B-spline) weights and picks the owner with the biggest share.
// The border between two owners is then a smooth curve, and it is anti-aliased to exactly one
// screen pixel, so it stays sharp at any zoom, screen size or resolution. No pixels, no blur.
//
// Keys: 0 = empty floor (checkerboard), 1..253 = player id, 254 = wall edge, 255 = outside.
// This file must stay inside a folder named "Resources" (the game loads it by name).
Shader "Hidden/TerritoryIO/Land"
{
    Properties
    {
        [PerRendererData] _MainTex ("Cell keys (set by code)", 2D) = "black" {}
        _Palette ("Palette (set by code)", 2D) = "black" {}
        _GridSize ("Grid size (w, h, 1/w, 1/h)", Vector) = (1, 1, 1, 1)
        _FloorA ("Floor A", Color) = (0.086, 0.11, 0.188, 1)
        _FloorB ("Floor B", Color) = (0.102, 0.129, 0.22, 1)
        _Checker ("Checker size in cells", Float) = 16
        _TrailCells ("Draw trail cells (0/1)", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);
    TEXTURE2D(_Palette);
    SAMPLER(sampler_Palette);

    CBUFFER_START(UnityPerMaterial)
        float4 _GridSize;
        float4 _FloorA;
        float4 _FloorB;
        float _Checker;
        float _TrailCells;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 cell : TEXCOORD0; // position in grid cells: cell (x, y) covers [x, x+1] x [y, y+1]
    };

    Varyings vert(Attributes v)
    {
        Varyings o;
        o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
        o.cell = v.uv * _GridSize.xy;
        return o;
    }

    // Owner key (x) and trail key (y) of one cell. Cells outside the texture repeat the edge.
    int2 KeysAt(float2 cellIndex)
    {
        float2 uv = (cellIndex + 0.5) * _GridSize.zw;
        float2 v = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).rg;
        return int2(v * 255.0 + 0.5);
    }

    float3 PaletteColor(int key, float row)
    {
        return SAMPLE_TEXTURE2D_LOD(_Palette, sampler_Palette, float2((key + 0.5) / 256.0, row), 0).rgb;
    }

    float3 LandColor(int key, float2 cell)
    {
        if (key == 0)
        {
            float2 q = floor(cell / _Checker);
            return frac((q.x + q.y) * 0.5) < 0.25 ? _FloorA.rgb : _FloorB.rgb;
        }
        return PaletteColor(key, 0.25);
    }

    // Picks the key with the largest total weight ("best") and the runner-up ("second"),
    // and returns t = how much of the pixel belongs to best (0.5 exactly on the border).
    void Resolve(int k[9], float w[9], float gx[9], float gy[9], float2 pixelSize,
                 out int best, out int second, out float t)
    {
        float s[9];
        float bestS = -1.0;
        best = k[4];
        [unroll] for (int a = 0; a < 9; a++)
        {
            float sum = 0.0;
            [unroll] for (int b = 0; b < 9; b++) sum += (k[b] == k[a]) ? w[b] : 0.0;
            s[a] = sum;
            if (sum > bestS) { bestS = sum; best = k[a]; }
        }

        float secondS = -1.0;
        second = best;
        [unroll] for (int c = 0; c < 9; c++)
        {
            if (k[c] != best && s[c] > secondS) { secondS = s[c]; second = k[c]; }
        }

        // d = best share - second share. Its slope (in cells) comes from the weight derivatives,
        // turned into "per screen pixel" with the size of one pixel in cells.
        float d = bestS - secondS;
        float2 g = 0.0;
        [unroll] for (int e = 0; e < 9; e++)
        {
            float sgn = (k[e] == best) ? 1.0 : ((k[e] == second) ? -1.0 : 0.0);
            g += sgn * float2(gx[e], gy[e]);
        }
        float perPixel = abs(g.x) * pixelSize.x + abs(g.y) * pixelSize.y;
        t = saturate(0.5 + d / max(perPixel, 1e-5));
    }

    half4 frag(Varyings i) : SV_Target
    {
        float2 cell = i.cell;
        float2 pixelSize = fwidth(cell); // size of one screen pixel, in cells
        float2 n = floor(cell);
        float2 u = cell - n - 0.5;       // -0.5..0.5 inside the cell

        // Quadratic B-spline weights for the cells at offset -1, 0, +1 (and their derivatives).
        float3 wx = float3(0.5 * (0.5 - u.x) * (0.5 - u.x), 0.75 - u.x * u.x, 0.5 * (0.5 + u.x) * (0.5 + u.x));
        float3 wy = float3(0.5 * (0.5 - u.y) * (0.5 - u.y), 0.75 - u.y * u.y, 0.5 * (0.5 + u.y) * (0.5 + u.y));
        float3 dx = float3(u.x - 0.5, -2.0 * u.x, 0.5 + u.x);
        float3 dy = float3(u.y - 0.5, -2.0 * u.y, 0.5 + u.y);

        int own[9];
        int trl[9];
        float w[9];
        float gx[9];
        float gy[9];
        bool sameOwner = true;
        bool anyTrail = false;
        [unroll] for (int j = 0; j < 3; j++)
        {
            [unroll] for (int x = 0; x < 3; x++)
            {
                int idx = j * 3 + x;
                int2 kk = KeysAt(n + float2(x - 1, j - 1));
                own[idx] = kk.x;
                trl[idx] = kk.y;
                w[idx] = wx[x] * wy[j];
                gx[idx] = dx[x] * wy[j];
                gy[idx] = wx[x] * dy[j];
            }
        }
        [unroll] for (int m = 0; m < 9; m++)
        {
            sameOwner = sameOwner && own[m] == own[4];
            anyTrail = anyTrail || trl[m] != 0;
        }

        float3 col;
        if (sameOwner)
        {
            col = LandColor(own[4], cell); // inside one owner's area: nothing to blend
        }
        else
        {
            int best, second;
            float t;
            Resolve(own, w, gx, gy, pixelSize, best, second, t);
            col = lerp(LandColor(second, cell), LandColor(best, cell), t);
        }

        if (_TrailCells > 0.5 && anyTrail)
        {
            int best, second;
            float t;
            Resolve(trl, w, gx, gy, pixelSize, best, second, t);
            // Premultiplied: key 0 (no trail) is fully transparent.
            float4 cb = best != 0 ? float4(PaletteColor(best, 0.75), 1.0) : 0.0;
            float4 cs = second != 0 ? float4(PaletteColor(second, 0.75), 1.0) : 0.0;
            float4 tc = lerp(cs, cb, t);
            col = col * (1.0 - 0.75 * tc.a) + 0.75 * tc.rgb;
        }

        return half4(col, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        // 2D Renderer (the one this project uses).
        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        // Normal URP renderer (in case the project is ever switched to it).
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
    Fallback Off
}
