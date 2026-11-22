// The creature rig's drawing on the GPU (RigDrawer.cs plays RigDisplay's list with it). Each fill is drawn twice:
// its triangle fan into the stencil's low four bits (counting windings: the nonzero rule; strokes just mark), then a
// quad over its box that paints where the count isn't zero and clears it. Clip paths raise a level in the stencil's
// upper four bits; everything drawn checks it's at the current level. Layers (group opacity, the fabric filter) are
// drawn into their own texture and laid back with the Composite pass. The check's software copy is
// tools/unity-port/rig/check/RigRaster.cs: keep the two the same.
//
// Passes: 0 FanNonzero, 1 FanUnion, 2 Cover, 3 ClipSet, 4 ClipPopDecr, 5 ClearLow, 6 Composite, 7 Straight, 8 Blur
// The stencil reference comes from the material (render state can't come from a property block): RigDrawer keeps
// one material per reference value.
Shader "Hidden/PlushCrush/RigFill"
{
    Properties
    {
        _Ref ("Stencil reference", Int) = 0
        _Grain ("Fabric grain", 2D) = "black" {}
    }

    HLSLINCLUDE
    #pragma target 3.0

    float4x4 _RigToClip;      // rig units -> clip space (with the platform's render-texture flip)
    float4 _TargetSize;       // w, h, 1/w, 1/h in pixels
    float _UseBox;            // 1: the mesh is a unit quad stretched over _Box (its own units)
    float4 _Box;
    float4 _Xf0, _Xf1;        // its units -> rig units: x' = dot(_Xf0.xyz, (x, y, 1)), y' = dot(_Xf1.xyz, (x, y, 1))

    float _PaintType;         // 0 a colour, 1 radial gradient, 2 linear gradient
    float4 _Color;            // straight alpha
    float _Alpha;
    float4 _GBox;             // the shape's bounding box (objectBoundingBox gradients)
    float _GUser;             // 1: userSpaceOnUse
    float4 _GRad;             // cx, cy, r
    float4 _GLin;             // x1, y1, x2, y2
    float4 _GInv0, _GInv1;    // the inverse of the gradient's own transform (gradientTransform), same form as _Xf
    float4 _GOff;             // up to four stop offsets
    float4 _GCol0, _GCol1, _GCol2, _GCol3;   // stop colours, straight alpha
    float _GN;

    sampler2D _Layer;
    sampler2D _Mask; float _UseMask;   // a mask layer: its luminance multiplies the layer
    float4 _BlurDir;          // blur pass: (1/w, 0) or (0, 1/h), sigma in pixels in z
    float _Opacity;
    float _Fabric;
    sampler2D _Grain;
    float4 _GrainRect;        // the grain image's x, y, width, height in rig units

    struct appdata { float3 vertex : POSITION; };
    struct v2f { float4 pos : SV_POSITION; float2 rig : TEXCOORD0; };

    v2f vert(appdata v)
    {
        v2f o;
        float2 p = _UseBox > 0.5 ? lerp(_Box.xy, _Box.zw, v.vertex.xy) : v.vertex.xy;
        float2 q = float2(dot(_Xf0.xyz, float3(p, 1)), dot(_Xf1.xyz, float3(p, 1)));
        o.pos = mul(_RigToClip, float4(q, 0, 1));
        o.rig = p;   // the paint is worked out in the shape's own units
        return o;
    }

    float4 pm(float4 c) { return float4(c.rgb * c.a, c.a); }

    // a gradient's colour at t (pad), interpolated premultiplied (as Chrome does)
    float4 gradAt(float t)
    {
        float4 c0 = pm(_GCol0), c1 = pm(_GCol1), c2 = pm(_GCol2), c3 = pm(_GCol3);
        int n = (int)(_GN + 0.5);
        if (t <= _GOff.x || n < 2) return c0;
        if (t <= _GOff.y) return lerp(c0, c1, _GOff.y > _GOff.x ? (t - _GOff.x) / (_GOff.y - _GOff.x) : 1);
        if (n < 3) return c1;
        if (t <= _GOff.z) return lerp(c1, c2, _GOff.z > _GOff.y ? (t - _GOff.y) / (_GOff.z - _GOff.y) : 1);
        if (n < 4) return c2;
        if (t <= _GOff.w) return lerp(c2, c3, _GOff.w > _GOff.z ? (t - _GOff.z) / (_GOff.w - _GOff.z) : 1);
        return c3;
    }

    float4 paint(float2 p)
    {
        if (_PaintType < 0.5) return pm(_Color) * _Alpha;
        float2 uv = _GUser > 0.5 ? p : (p - _GBox.xy) / (_GBox.zw - _GBox.xy);
        uv = float2(dot(_GInv0.xyz, float3(uv, 1)), dot(_GInv1.xyz, float3(uv, 1)));
        float t;
        if (_PaintType < 1.5) t = length(uv - _GRad.xy) / _GRad.z;
        else { float2 e = _GLin.zw - _GLin.xy; float L = dot(e, e); t = L > 0 ? dot(uv - _GLin.xy, e) / L : 0; }
        return gradAt(t) * _Alpha;
    }

    float toLin(float c) { return c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4); }
    float toSrgb(float c) { return c <= 0.0031308 ? c * 12.92 : 1.055 * pow(max(c, 0), 1.0 / 2.4) - 0.055; }

    // the fabric filter (feComposite in + feBlend multiply, in linear RGB as Chrome runs filters), premultiplied in and out
    float4 fabric(float4 s, float g)
    {
        if (s.a <= 0) return s;
        float ab = g * s.a, ar = s.a + ab - s.a * ab, k = 1 - ab;
        float3 c = s.rgb / s.a;
        float3 lin = float3(toLin(c.r), toLin(c.g), toLin(c.b)) * s.a * k / ar;
        return float4(float3(toSrgb(lin.r), toSrgb(lin.g), toSrgb(lin.b)) * ar, ar);
    }

    float grainAt(float2 rig)
    {
        float2 uv = (rig - _GrainRect.xy) / _GrainRect.zw;
        if (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1) return 0;
        return tex2D(_Grain, float2(uv.x, 1 - uv.y)).a;   // the image's first row is its top
    }

    float4 fragNone(v2f i) : SV_Target { return 0; }
    float4 fragPaint(v2f i) : SV_Target { return paint(i.rig); }

    float4 fragComposite(v2f i) : SV_Target
    {
        float2 uv = i.pos.xy * _TargetSize.zw;
        float4 c = tex2D(_Layer, uv);
        if (_UseMask > 0.5) { float4 m = tex2D(_Mask, uv); c *= 0.2125 * m.r + 0.7154 * m.g + 0.0721 * m.b; }   // premultiplied: luminance x alpha
        if (_Fabric > 0.5) c = fabric(c, grainAt(i.rig));
        return c * _Opacity;
    }

    // feGaussianBlur, one direction (premultiplied; 3 sigma each side, as the check's software copy)
    float4 fragBlur(v2f i) : SV_Target
    {
        float2 uv = i.pos.xy * _TargetSize.zw;
        float sigma = _BlurDir.z; int r = (int)ceil(sigma * 3);
        float4 acc = 0; float sum = 0;
        for (int k = -48; k <= 48; k++)
        {
            if (k < -r || k > r) continue;
            float w = exp(-k * k / (2 * sigma * sigma));
            acc += tex2D(_Layer, uv + _BlurDir.xy * k) * w; sum += w;
        }
        return acc / sum;
    }

    // a CSS filter chain on the whole creature (brightness, saturate, sepia: menu.js light() at night) as one colour
    // matrix on the straight colour, in sRGB as Chrome runs filter functions; identity when off
    float _UseCM;
    float4 _CM0, _CM1, _CM2;

    float4 fragStraight(v2f i) : SV_Target
    {
        float4 c = tex2D(_Layer, i.pos.xy * _TargetSize.zw);
        if (c.a <= 0) return float4(0, 0, 0, 0);
        float3 s = c.rgb / c.a;
        if (_UseCM > 0.5) s = saturate(float3(dot(_CM0.xyz, s), dot(_CM1.xyz, s), dot(_CM2.xyz, s)));
        return float4(s, c.a);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        ZTest Always ZWrite Off Cull Off

        Pass // 0 FanNonzero: count windings (front +1, back -1) in the low bits, at this clip level
        {
            ColorMask 0
            Stencil { Ref [_Ref] ReadMask 240 WriteMask 15 CompFront Equal CompBack Equal PassFront IncrWrap PassBack DecrWrap FailFront Keep FailBack Keep ZFailFront Keep ZFailBack Keep }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragNone
            ENDHLSL
        }
        Pass // 1 FanUnion: mark (ref's low bit) where any triangle covers, at this clip level
        {
            ColorMask 0
            Stencil { Ref [_Ref] ReadMask 240 WriteMask 15 Comp Equal Pass Replace Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragNone
            ENDHLSL
        }
        Pass // 2 Cover: paint where the low bits aren't zero, and clear them
        {
            Blend One OneMinusSrcAlpha
            Stencil { Ref [_Ref] ReadMask 15 WriteMask 15 Comp NotEqual Pass Replace Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragPaint
            ENDHLSL
        }
        Pass // 3 ClipSet: where the clip shape's low bits aren't zero, the next level (ref = level + 1 << 4)
        {
            ColorMask 0
            Stencil { Ref [_Ref] ReadMask 15 WriteMask 255 Comp NotEqual Pass Replace Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragNone
            ENDHLSL
        }
        Pass // 4 ClipPopDecr: the top level steps down (leaving 15 in the low bits; ClearLow follows)
        {
            ColorMask 0
            Stencil { Ref [_Ref] ReadMask 240 WriteMask 255 Comp Equal Pass DecrWrap Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragNone
            ENDHLSL
        }
        Pass // 5 ClearLow: clear the low bits where they aren't zero
        {
            ColorMask 0
            Stencil { Ref [_Ref] ReadMask 15 WriteMask 15 Comp NotEqual Pass Replace Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragNone
            ENDHLSL
        }
        Pass // 6 Composite: a layer laid back with its opacity (and the fabric grain), inside the current clip level
        {
            Blend One OneMinusSrcAlpha
            Stencil { Ref [_Ref] ReadMask 240 Comp Equal Pass Keep Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            ENDHLSL
        }
        Pass // 7 Straight: the finished drawing to straight alpha, for the UI
        {
            Blend Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragStraight
            ENDHLSL
        }
        Pass // 8 Blur: one direction of a Gaussian blur, into a plain texture
        {
            Blend Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDHLSL
        }
    }
}
