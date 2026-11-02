using System;
using UnityEngine;

namespace BetterCursors
{
    internal static class Resampler
    {
        private struct Taps
        {
            public int Start;
            public float[] Weights;
        }

        public static Color32[] Resize(Color32[] src, int sw, int sh, int dw, int dh)
        {
            // premultiplied alpha
            var a = new float[sw * sh * 4];
            for (int i = 0; i < src.Length; i++)
            {
                float alpha = src[i].a / 255f;
                a[i * 4] = src[i].r * alpha;
                a[i * 4 + 1] = src[i].g * alpha;
                a[i * 4 + 2] = src[i].b * alpha;
                a[i * 4 + 3] = src[i].a;
            }

            var across = TapsFor(sw, dw);
            var b = new float[dw * sh * 4];
            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < dw; x++)
                {
                    var t = across[x];
                    int o = (y * dw + x) * 4;
                    for (int k = 0; k < t.Weights.Length; k++)
                    {
                        int sx = Mathf.Clamp(t.Start + k, 0, sw - 1);
                        int s = (y * sw + sx) * 4;
                        float w = t.Weights[k];
                        b[o] += a[s] * w;
                        b[o + 1] += a[s + 1] * w;
                        b[o + 2] += a[s + 2] * w;
                        b[o + 3] += a[s + 3] * w;
                    }
                }
            }

            var down = TapsFor(sh, dh);
            var result = new Color32[dw * dh];
            for (int y = 0; y < dh; y++)
            {
                var t = down[y];
                for (int x = 0; x < dw; x++)
                {
                    float r = 0, g = 0, bl = 0, al = 0;
                    for (int k = 0; k < t.Weights.Length; k++)
                    {
                        int sy = Mathf.Clamp(t.Start + k, 0, sh - 1);
                        int s = (sy * dw + x) * 4;
                        float w = t.Weights[k];
                        r += b[s] * w;
                        g += b[s + 1] * w;
                        bl += b[s + 2] * w;
                        al += b[s + 3] * w;
                    }

                    float inv = al > 0.5f ? 255f / al : 0f;
                    result[y * dw + x] = new Color32(Byte(r * inv), Byte(g * inv), Byte(bl * inv), Byte(al));
                }
            }

            return result;
        }

        private static Taps[] TapsFor(int srcLen, int dstLen)
        {
            float scale = srcLen / (float)dstLen;
            float radius = Math.Max(1f, scale);
            var taps = new Taps[dstLen];
            for (int i = 0; i < dstLen; i++)
            {
                float centre = (i + 0.5f) * scale - 0.5f;
                int start = (int)Math.Floor(centre - radius) + 1;
                int end = (int)Math.Floor(centre + radius);
                var weights = new float[Math.Max(1, end - start + 1)];
                float total = 0;
                for (int j = 0; j < weights.Length; j++)
                {
                    float w = Math.Max(0f, 1f - Math.Abs(start + j - centre) / radius);
                    weights[j] = w;
                    total += w;
                }

                for (int j = 0; j < weights.Length; j++)
                    weights[j] = total > 0 ? weights[j] / total : 1f / weights.Length;
                taps[i] = new Taps { Start = start, Weights = weights };
            }

            return taps;
        }

        private static byte Byte(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);
        }
    }
}
