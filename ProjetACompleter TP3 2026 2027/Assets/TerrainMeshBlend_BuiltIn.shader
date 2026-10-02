Shader "Custom/TerrainMeshBlend_BuiltIn"
{
    Properties
    {
        _SandTex  ("Sable (bas)", 2D)    = "white" {}
        _GrassTex ("Herbe (milieu)", 2D) = "white" {}
        _RockTex  ("Roche (pentes)", 2D) = "white" {}
        _SnowTex  ("Neige (haut)", 2D)   = "white" {}
        _Tiling   ("Taille d'une texture (metres)", Float) = 8

        _SandMax   ("Sable jusqu'a (Y monde)", Float) = 5
        _SnowMin   ("Neige a partir de (Y monde)", Float) = 40
        _BlendH    ("Douceur transition hauteur", Float) = 4

        _SlopeStart ("Debut roche (0 plat, 1 vertical)", Range(0,1)) = 0.25
        _SlopeEnd   ("Roche complete", Range(0,1)) = 0.5
        _SnowSlopeKill ("Neige absente sur pentes", Range(0,1)) = 0.8

        _NoiseScale    ("Echelle du bruit", Float) = 0.05
        _NoiseStrength ("Force du bruit (metres)", Float) = 6
        _TriSharp      ("Nettete triplanar", Range(1,16)) = 6
        _Glossiness    ("Smoothness", Range(0,1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _SandTex, _GrassTex, _RockTex, _SnowTex;
        float _Tiling;
        float _SandMax, _SnowMin, _BlendH;
        float _SlopeStart, _SlopeEnd, _SnowSlopeKill;
        float _NoiseScale, _NoiseStrength, _TriSharp;
        half _Glossiness;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float Noise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                        lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
        }

        float3 Planar(sampler2D tex, float3 wp)
        {
            return tex2D(tex, wp.xz / _Tiling).rgb;
        }

        float3 Triplanar(sampler2D tex, float3 wp, float3 n)
        {
            float3 w = pow(abs(n), _TriSharp);
            w /= (w.x + w.y + w.z);
            return tex2D(tex, wp.zy / _Tiling).rgb * w.x
                 + tex2D(tex, wp.xz / _Tiling).rgb * w.y
                 + tex2D(tex, wp.xy / _Tiling).rgb * w.z;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 n  = normalize(IN.worldNormal);
            float3 wp = IN.worldPos;

            float h = wp.y + (Noise(wp.xz * _NoiseScale) - 0.5) * 2 * _NoiseStrength;

            float wSand = 1 - smoothstep(_SandMax - _BlendH, _SandMax + _BlendH, h);
            float wSnow =     smoothstep(_SnowMin - _BlendH, _SnowMin + _BlendH, h);

            float slope = 1 - saturate(n.y);
            float wRock = smoothstep(_SlopeStart, _SlopeEnd, slope);

            wSnow *= 1 - wRock * _SnowSlopeKill;
            float wGrass = saturate(1 - wSand - wSnow);
            float sum = wSand + wGrass + wSnow + 1e-5;

            float3 flatColor = (Planar(_SandTex, wp) * wSand
                              + Planar(_GrassTex, wp) * wGrass
                              + Planar(_SnowTex, wp) * wSnow) / sum;

            float3 rock = Triplanar(_RockTex, wp, n);

            o.Albedo = lerp(flatColor, rock, wRock);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
