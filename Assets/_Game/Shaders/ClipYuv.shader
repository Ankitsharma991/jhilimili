// Phase 9 step 6 (F14): packs the screen into one I420 frame (Y plane, then U, then V; BT.601 limited range) in a
// single-channel texture of width x (height * 3 / 2), so one GPU readback gives the encoder its input as it is.
// The byte layout matches UtsavNights.Core.Clips.I420.Source, which the tests check on the CPU.
Shader "Hidden/Utsav/ClipYuv"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            // x, y: the clip's width and height in pixels; z: 1 when the screen texture's first row is the top.
            float4 _ClipSize;

            float3 ScreenAt(float2 px)
            {
                float2 uv = px / _ClipSize.xy;
                uv.y = _ClipSize.z > 0.5 ? uv.y : 1.0 - uv.y;
                return saturate(tex2D(_MainTex, uv).rgb) * 255.0;
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float w = _ClipSize.x;
                float h = _ClipSize.y;
                float row = floor(i.uv.y * h * 1.5);
                float col = floor(i.uv.x * w);
                float index = row * w + col;
                float ySize = w * h;
                float cw = w * 0.5;
                float cSize = cw * h * 0.5; // (w / 2) x (h / 2)
                float value;
                if (index < ySize)
                {
                    float3 c = ScreenAt(float2(col + 0.5, row + 0.5));
                    value = 16.0 + 0.257 * c.r + 0.504 * c.g + 0.098 * c.b;
                }
                else
                {
                    bool isV = index >= ySize + cSize;
                    float k = index - ySize - (isV ? cSize : 0.0);
                    float cy = floor(k / cw);
                    float cx = k - cy * cw;
                    // The centre of the 2 x 2 block: bilinear sampling averages its four pixels.
                    float3 c = ScreenAt(float2(cx * 2.0 + 1.0, cy * 2.0 + 1.0));
                    value = isV ? 128.0 + 0.439 * c.r - 0.368 * c.g - 0.071 * c.b
                                : 128.0 - 0.148 * c.r - 0.291 * c.g + 0.439 * c.b;
                }

                return clamp(value, 0.0, 255.0) / 255.0;
            }
            ENDCG
        }
    }
}
