// EyeKeeper's procedural fluid material. CPU reference: FluidTexture.cs.
// Shader Model 3: hardware path only. The CPU renderer is used for offscreen/remote fallback.
float Time : register(c0);
float Aspect : register(c1);
#ifndef STYLE
#define STYLE 0
#endif
#define Mode STYLE
float3 A : register(c3);
float3 B : register(c4);
float3 C : register(c5);
float3 D : register(c6);

float ridge(float v, float center, float width) {
    float r = saturate(1 - abs(v - center) * width); return r * r * r;
}
float3 ramp(float field) {
    float s = min(saturate(field) * 6, 5.99999);
    float f = frac(s); f = f * f * (3 - 2 * f);
    if (s < 1) return lerp(A, B, f);
    if (s < 2) return lerp(B, C, f);
    if (s < 3) return lerp(C, D, f);
    if (s < 4) return lerp(D, C, f);
    if (s < 5) return lerp(C, B, f);
    return lerp(B, A, f);
}
float4 main(float2 uv : TEXCOORD) : COLOR {
    float t = Time * .22;
    float2 p = (uv - .5) * float2(3.4 * Aspect, 3.4);
    float2 q = float2(sin(p.x * 1.1 + sin(p.y * 1.3 + t)), sin(p.y + sin(p.x * 1.25 - t * .8) + 1.6));
    float2 w = p + .95 * q + .4 * float2(sin(p.y * 1.9 - t * .75), sin(p.x * 1.7 + t * .6));
    float2 r = float2(sin(w.x * 1.65 + w.y * .65 + t * .6), sin(w.y * 1.5 - w.x * .55 - t * .7));
    float phase = (w.x + .68 * r.x) * 1.55 + (w.y + .68 * r.y) * 1.15 + t * .35;
    float field = .5 + .5 * sin(phase);
    if (Mode > .5 && Mode < 1.5) field = .5 + .5 * sin(phase * 2.1 + .8 * sin(w.x * 2.3 - w.y * 1.4 + t));
    if (Mode > 1.5 && Mode < 2.5) {
        float wave = w.y + .8 * sin(w.x * 1.1 + t * .7) + .38 * sin(w.x * 2.3 - t * .5);
        field = exp(-wave * wave * 1.4);
    }
    if (Mode > 2.5 && Mode < 3.5) field = .5 + .5 * sin(atan2(w.y, w.x) * 3 + length(w) * 3.7 - t);
    if (Mode > 3.5 && Mode < 4.5) field = .5 + .5 * sin(w.x * .6 + w.y * .7 + sin(w.y * .8 + t));
    if (Mode > 4.5 && Mode < 5.5) field = .5 + .5 * sin(phase * 1.3 + sin(w.x * 1.1 - w.y * 1.4));
    if (Mode > 5.5 && Mode < 6.5) field = .5 + .5 * sin(p.y * 1.3 + sin(w.x * .9 + t) * 1.6 + w.y * .4);
    if (Mode > 6.5) field = .5 + .5 * sin(phase * 3.5 + .4 * sin(w.y * 3 + t));
    float3 color = ramp(field);
    if (Mode > 3.5 && Mode < 4.5) {
        float blend = .5 + .5 * sin(w.x * .6 - w.y * .5 - t * .4);
        color = lerp(lerp(B, C, field), lerp(D, A, field), blend * .75);
    }
    if (Mode > 4.5 && Mode < 5.5) color = lerp(color, ramp(.5 + .5 * sin(phase + 1.5 + w.y * .5)), .28);
    float light = .83 + .17 * sin(w.x * .7 - w.y * .8 + t * .35);
    float shine = ridge(field, .62, 19) * .20 + ridge(field, .245, 35) * .085;
    if (Mode > 6.5) shine += ridge(field, .82, 30) * .28;
    return float4(saturate(lerp(color * light, 1, shine)), 1);
}
