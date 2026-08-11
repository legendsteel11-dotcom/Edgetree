// PQ (SMPTE ST 2084) + BT.2020 -> SDR BT.709, as a WPF pixel shader.
// Regenerate the .ps beside this with tools\compile-shader.ps1.
//
// ORDER MATTERS, and the first version had it wrong (2026-08-11): it tone
// mapped and THEN changed colour space, which is a gamut conversion applied to
// numbers that are no longer light. Primaries are converted in linear light,
// before the curve.
//
// THE CURVE IS FILMIC, not Reinhard, for the same round's reason: Reinhard
// compresses each channel on its own, so the brightest, most saturated things
// in frame lose their colour first - the greens went grey while the greys
// stayed put. The ACES approximation holds saturation into the highlights and
// keeps its feet in the blacks, which is most of the difference between "less
// wrong" and "right".
//
//   c0 Exposure   : 1.0 = 10000 nits after the PQ curve is undone, so 100 puts
//                   100-nit diffuse white at 1.0.
//   c1 Saturation : applied after the curve, around Rec.709 luma. 1 = leave it.
//   c2 Contrast   : applied LAST, in gamma space around mid grey - which is
//                   where a contrast dial behaves the way a hand expects.
//                   Doing it in linear light would crush the shadows long
//                   before the highlights moved. 1 = leave it.
sampler2D implicitInput : register(s0);
float Exposure : register(c0);
float Saturation : register(c1);
float Contrast : register(c2);

float3 PQToLinear(float3 e)
{
    const float m1 = 0.1593017578125;
    const float m2 = 78.84375;
    const float c1 = 0.8359375;
    const float c2 = 18.8515625;
    const float c3 = 18.6875;
    float3 p = pow(max(e, 0.0), 1.0 / m2);
    float3 num = max(p - c1, 0.0);
    float3 den = c2 - c3 * p;
    return pow(num / max(den, 0.0001), 1.0 / m1);
}

// Narkowicz's fit of the ACES filmic curve - one rational expression, which is
// what makes it affordable in ps_2_0.
float3 Filmic(float3 x)
{
    return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 src = tex2D(implicitInput, uv);
    // WPF hands over premultiplied alpha; undo it, work in straight colour,
    // and put it back on the way out.
    float a = src.a;
    float3 c = a > 0.0 ? src.rgb / a : src.rgb;

    float3 lin = PQToLinear(c) * Exposure;

    // BT.2020 -> BT.709, in linear light.
    float3 rec709;
    rec709.r = dot(lin, float3( 1.6605, -0.5876, -0.0728));
    rec709.g = dot(lin, float3(-0.1246,  1.1329, -0.0083));
    rec709.b = dot(lin, float3(-0.0182, -0.1006,  1.1187));
    rec709 = max(rec709, 0.0);

    float3 o = Filmic(rec709);

    float luma = dot(o, float3(0.2126, 0.7152, 0.0722));
    o = saturate(luma + (o - luma) * Saturation);

    o = pow(o, 1.0 / 2.2);
    o = saturate((o - 0.5) * Contrast + 0.5);
    return float4(o * a, a);
}
