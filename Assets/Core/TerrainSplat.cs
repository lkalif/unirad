// 
// Radegast Metaverse Client
// Copyright (c) 2009-2013, Radegast Development Team
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 
//     * Redistributions of source code must retain the above copyright notice,
//       this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of the application "Radegast", nor the names of its
//       contributors may be used to endorse or promote products derived from
//       this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// Based on code from Aurora Sim and OpenSimulator
// Copyright (c) Contributors, http://aurora-sim.org/, http://opensimulator.org/
//
// $Id$
//


using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Imaging;

public static class TerrainSplat
{
    #region Constants

    private static readonly UUID DIRT_DETAIL = new UUID("0bc58228-74a0-7e83-89bc-5c23464bcec5");
    private static readonly UUID GRASS_DETAIL = new UUID("63338ede-0037-c4fd-855b-015d77112fc8");
    private static readonly UUID MOUNTAIN_DETAIL = new UUID("303cd381-8560-7579-23f1-f0a880799740");
    private static readonly UUID ROCK_DETAIL = new UUID("53a2f406-4895-1d13-d541-d2e3b86bc19c");
    private static readonly int RegionSize = 256;
    static AutoResetEvent textureDone = new AutoResetEvent(false);
    private static readonly UUID[] DEFAULT_TERRAIN_DETAIL = new UUID[]
        {
            DIRT_DETAIL,
            GRASS_DETAIL,
            MOUNTAIN_DETAIL,
            ROCK_DETAIL
        };

    private static readonly Color[] DEFAULT_TERRAIN_COLOR = new Color[]
        {
            Color.FromArgb(255, 164, 136, 117),
            Color.FromArgb(255, 65, 87, 47),
            Color.FromArgb(255, 157, 145, 131),
            Color.FromArgb(255, 125, 128, 130)
        };

    private static readonly UUID TERRAIN_CACHE_MAGIC = new UUID("2c0c7ef2-56be-4eb8-aacb-76712c535b4b");

    #endregion Constants

    /// <summary>
    /// Builds a composited terrain texture given the region texture
    /// and heightmap settings
    /// </summary>
    /// <param name="heightmap">Terrain heightmap</param>
    /// <param name="regionInfo">Region information including terrain texture parameters</param>
    /// <returns>A composited 256x256 RGB texture ready for rendering</returns>
    /// <remarks>Based on the algorithm described at http://opensimulator.org/wiki/Terrain_Splatting
    /// </remarks>
    public static Bitmap Splat(GridClient client, float[,] heightmap, UUID[] textureIDs, float[] startHeights, float[] heightRanges)
    {
        Debug.Assert(textureIDs.Length == 4);
        Debug.Assert(startHeights.Length == 4);
        Debug.Assert(heightRanges.Length == 4);
        int outputSize = 2048;

        Bitmap[] detailTexture = new Bitmap[4];

        // Swap empty terrain textureIDs with default IDs
        for (int i = 0; i < textureIDs.Length; i++)
        {
            if (textureIDs[i] == UUID.Zero)
                textureIDs[i] = DEFAULT_TERRAIN_DETAIL[i];
        }

        #region Texture Fetching
        for (int i = 0; i < 4; i++)
        {
            textureDone.Reset();
            UUID textureID = textureIDs[i];

            client.Assets.RequestImage(textureID, TextureDownloadCallback(detailTexture, i));

            if (!textureDone.WaitOne(60 * 1000, false))
            {
                UnityEngine.Debug.LogWarning("Timout in terrain texure download");
            }
        }

        #endregion Texture Fetching

        // Fill in any missing textures with a solid color
        for (int i = 0; i < 4; i++)
        {
            if (detailTexture[i] == null)
            {
                // Create a solid color texture for this layer
                detailTexture[i] = new Bitmap(outputSize, outputSize, PixelFormat.Format24bppRgb);
                using (Graphics gfx = Graphics.FromImage(detailTexture[i]))
                {
                    using (SolidBrush brush = new SolidBrush(DEFAULT_TERRAIN_COLOR[i]))
                        gfx.FillRectangle(brush, 0, 0, outputSize, outputSize);
                }
            }
            else if (detailTexture[i].Width != outputSize || detailTexture[i].Height != outputSize)
            {
                detailTexture[i] = ResizeBitmap(detailTexture[i], 256, 256);
            }
        }

        #region Layer Map

        int diff = heightmap.GetLength(0) / RegionSize;
        float[] layermap = new float[RegionSize * RegionSize];

        for (int y = 0; y < heightmap.GetLength(0); y += diff)
        {
            for (int x = 0; x < heightmap.GetLength(1); x += diff)
            {
                int newX = x / diff;
                int newY = y / diff;
                float height = heightmap[newX, newY];

                float pctX = (float)newX / 255f;
                float pctY = (float)newY / 255f;

                // Use bilinear interpolation between the four corners of start height and
                // height range to select the current values at this position
                float startHeight = ImageUtils.Bilinear(
                    startHeights[0],
                    startHeights[2],
                    startHeights[1],
                    startHeights[3],
                    pctX, pctY);
                startHeight = Utils.Clamp(startHeight, 0f, 255f);

                float heightRange = ImageUtils.Bilinear(
                    heightRanges[0],
                    heightRanges[2],
                    heightRanges[1],
                    heightRanges[3],
                    pctX, pctY);
                heightRange = Utils.Clamp(heightRange, 0f, 255f);

                // Generate two frequencies of perlin noise based on our global position
                // The magic values were taken from http://opensimulator.org/wiki/Terrain_Splatting
                Vector3 vec = new Vector3
                (
                    newX * 0.20319f,
                    newY * 0.20319f,
                    height * 0.25f
                );

                float lowFreq = Perlin.noise2(vec.X * 0.222222f, vec.Y * 0.222222f) * 6.5f;
                float highFreq = Perlin.turbulence2(vec.X, vec.Y, 2f) * 2.25f;
                float noise = (lowFreq + highFreq) * 2f;

                // Combine the current height, generated noise, start height, and height range parameters, then scale all of it
                float layer = ((height + noise - startHeight) / heightRange) * 4f;
                if (Single.IsNaN(layer))
                    layer = 0f;
                layermap[newY * RegionSize + newX] = Utils.Clamp(layer, 0f, 3f);
            }
        }

        #endregion Layer Map

        #region Texture Compositing
        Bitmap output = new Bitmap(outputSize, outputSize, PixelFormat.Format24bppRgb);
        BitmapData outputData = output.LockBits(new Rectangle(0, 0, outputSize, outputSize), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

        unsafe
        {
            // Get handles to all of the texture data arrays
            BitmapData[] datas = new BitmapData[]
                {
                    detailTexture[0].LockBits(new Rectangle(0, 0, 256, 256), ImageLockMode.ReadOnly, detailTexture[0].PixelFormat),
                    detailTexture[1].LockBits(new Rectangle(0, 0, 256, 256), ImageLockMode.ReadOnly, detailTexture[1].PixelFormat),
                    detailTexture[2].LockBits(new Rectangle(0, 0, 256, 256), ImageLockMode.ReadOnly, detailTexture[2].PixelFormat),
                    detailTexture[3].LockBits(new Rectangle(0, 0, 256, 256), ImageLockMode.ReadOnly, detailTexture[3].PixelFormat)
                };

            int[] comps = new int[]
                {
                    (datas[0].PixelFormat == PixelFormat.Format32bppArgb) ? 4 : 3,
                    (datas[1].PixelFormat == PixelFormat.Format32bppArgb) ? 4 : 3,
                    (datas[2].PixelFormat == PixelFormat.Format32bppArgb) ? 4 : 3,
                    (datas[3].PixelFormat == PixelFormat.Format32bppArgb) ? 4 : 3
                };

            int[] strides = new int[]
                {
                    datas[0].Stride,
                    datas[1].Stride,
                    datas[2].Stride,
                    datas[3].Stride
                };

            IntPtr[] scans = new IntPtr[]
                {
                    datas[0].Scan0,
                    datas[1].Scan0,
                    datas[2].Scan0,
                    datas[3].Scan0
                };

            int ratio = outputSize / RegionSize;

            for (int y = 0; y < outputSize; y++)
            {
                for (int x = 0; x < outputSize; x++)
                {
                    float layer = layermap[(y / ratio) * RegionSize + x / ratio];
                    float layerx = layermap[(y / ratio) * RegionSize + Math.Min(outputSize - 1, (x + 1)) / ratio];
                    float layerxx = layermap[(y / ratio) * RegionSize + Math.Max(0, (x - 1)) / ratio];
                    float layery = layermap[Math.Min(outputSize - 1, (y + 1)) / ratio * RegionSize + x / ratio];
                    float layeryy = layermap[(Math.Max(0, (y - 1)) / ratio) * RegionSize + x / ratio];

                    // Select two textures
                    int l0 = (int)Math.Floor(layer);
                    int l1 = Math.Min(l0 + 1, 3);

                    byte* ptrA = (byte*)scans[l0] + (y % 256) * strides[l0] + (x % 256) * comps[l0];
                    byte* ptrB = (byte*)scans[l1] + (y % 256) * strides[l1] + (x % 256) * comps[l1];
                    byte* ptrO = (byte*)outputData.Scan0 + y * outputData.Stride + x * 3;

                    float aB = *(ptrA + 0);
                    float aG = *(ptrA + 1);
                    float aR = *(ptrA + 2);

                    int lX = (int)Math.Floor(layerx);
                    byte* ptrX = (byte*)scans[lX] + (y % 256) * strides[lX] + (x % 256) * comps[lX];
                    int lXX = (int)Math.Floor(layerxx);
                    byte* ptrXX = (byte*)scans[lXX] + (y % 256) * strides[lXX] + (x % 256) * comps[lXX];
                    int lY = (int)Math.Floor(layery);
                    byte* ptrY = (byte*)scans[lY] + (y % 256) * strides[lY] + (x % 256) * comps[lY];
                    int lYY = (int)Math.Floor(layeryy);
                    byte* ptrYY = (byte*)scans[lYY] + (y % 256) * strides[lYY] + (x % 256) * comps[lYY];

                    float bB = *(ptrB + 0);
                    float bG = *(ptrB + 1);
                    float bR = *(ptrB + 2);

                    float layerDiff = layer - l0;
                    float xlayerDiff = layerx - layer;
                    float xxlayerDiff = layerxx - layer;
                    float ylayerDiff = layery - layer;
                    float yylayerDiff = layeryy - layer;
                    // Interpolate between the two selected textures
                    *(ptrO + 0) = (byte)Math.Floor(aB + layerDiff * (bB - aB) +
                        xlayerDiff * (*ptrX - aB) +
                        xxlayerDiff * (*(ptrXX) - aB) +
                        ylayerDiff * (*ptrY - aB) +
                        yylayerDiff * (*(ptrYY) - aB));
                    *(ptrO + 1) = (byte)Math.Floor(aG + layerDiff * (bG - aG) +
                        xlayerDiff * (*(ptrX + 1) - aG) +
                        xxlayerDiff * (*(ptrXX + 1) - aG) +
                        ylayerDiff * (*(ptrY + 1) - aG) +
                        yylayerDiff * (*(ptrYY + 1) - aG));
                    *(ptrO + 2) = (byte)Math.Floor(aR + layerDiff * (bR - aR) +
                        xlayerDiff * (*(ptrX + 2) - aR) +
                        xxlayerDiff * (*(ptrXX + 2) - aR) +
                        ylayerDiff * (*(ptrY + 2) - aR) +
                        yylayerDiff * (*(ptrYY + 2) - aR));
                }
            }

            for (int i = 0; i < 4; i++)
            {
                detailTexture[i].UnlockBits(datas[i]);
                detailTexture[i].Dispose();
            }
        }

        layermap = null;
        output.UnlockBits(outputData);

        output.RotateFlip(RotateFlipType.Rotate270FlipNone);

        #endregion Texture Compositing

        return output;
    }

    private static TextureDownloadCallback TextureDownloadCallback(Bitmap[] detailTexture, int i)
    {
        return (state, assetTexture) =>
        {
            if (state == TextureRequestState.Finished && assetTexture != null && assetTexture.AssetData != null)
            {
                Image img = CSJ2K.J2kImage.FromBytes(assetTexture.AssetData);
                UnityEngine.Debug.Log("Image decoded");
                detailTexture[i] = (Bitmap)img;
            }
            textureDone.Set();
        };
    }

    public static Bitmap ResizeBitmap(Bitmap b, int nWidth, int nHeight)
    {
        Bitmap result = new Bitmap(nWidth, nHeight);
        using (Graphics g = Graphics.FromImage((Image)result))
        {
            g.DrawImage(b, 0, 0, nWidth, nHeight);
        }
        b.Dispose();
        return result;
    }

    public static Bitmap TileBitmap(Bitmap b, int tiles)
    {
        Bitmap result = new Bitmap(b.Width * tiles, b.Width * tiles);
        using (Graphics g = Graphics.FromImage((Image)result))
        {
            for (int x = 0; x < tiles; x++)
            {
                for (int y = 0; y < tiles; y++)
                {
                    g.DrawImage(b, x * 256, y * 256, x * 256 + 256, y * 256 + 256);
                }
            }
        }
        b.Dispose();
        return result;
    }

    public static Bitmap SplatSimple(float[,] heightmap)
    {
        const float BASE_HSV_H = 93f / 360f;
        const float BASE_HSV_S = 44f / 100f;
        const float BASE_HSV_V = 34f / 100f;

        Bitmap img = new Bitmap(256, 256);
        BitmapData bitmapData = img.LockBits(new Rectangle(0, 0, 256, 256), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

        unsafe
        {
            for (int y = 255; y >= 0; y--)
            {
                for (int x = 0; x < 256; x++)
                {
                    float normHeight = heightmap[x, y] / 255f;
                    normHeight = Utils.Clamp(normHeight, BASE_HSV_V, 1.0f);

                    Color4 color = Color4.FromHSV(BASE_HSV_H, BASE_HSV_S, normHeight);

                    byte* ptr = (byte*)bitmapData.Scan0 + y * bitmapData.Stride + x * 3;
                    *(ptr + 0) = (byte)(color.B * 255f);
                    *(ptr + 1) = (byte)(color.G * 255f);
                    *(ptr + 2) = (byte)(color.R * 255f);
                }
            }
        }

        img.UnlockBits(bitmapData);
        return img;
    }
}

public static class ImageUtils
{
    /// <summary>
    /// Performs bilinear interpolation between four values
    /// </summary>
    /// <param name="v00">First, or top left value</param>
    /// <param name="v01">Second, or top right value</param>
    /// <param name="v10">Third, or bottom left value</param>
    /// <param name="v11">Fourth, or bottom right value</param>
    /// <param name="xPercent">Interpolation value on the X axis, between 0.0 and 1.0</param>
    /// <param name="yPercent">Interpolation value on fht Y axis, between 0.0 and 1.0</param>
    /// <returns>The bilinearly interpolated result</returns>
    public static float Bilinear(float v00, float v01, float v10, float v11, float xPercent, float yPercent)
    {
        return Utils.Lerp(Utils.Lerp(v00, v01, xPercent), Utils.Lerp(v10, v11, xPercent), yPercent);
    }

    /// <summary>
    /// Performs a high quality image resize
    /// </summary>
    /// <param name="image">Image to resize</param>
    /// <param name="width">New width</param>
    /// <param name="height">New height</param>
    /// <returns>Resized image</returns>
    public static Bitmap ResizeImage(Image image, int width, int height)
    {
        Bitmap result = new Bitmap(width, height);

        using (Graphics graphics = Graphics.FromImage(result))
        {
            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            graphics.DrawImage(image, 0, 0, result.Width, result.Height);
        }

        return result;
    }
}

public static class Perlin
{
    // We use a hardcoded seed to keep the noise generation consistent between runs
    private const int SEED = 42;

    private const int SAMPLE_SIZE = 1024;
    private const int B = SAMPLE_SIZE;
    private const int BM = SAMPLE_SIZE - 1;
    private const int N = 0x1000;

    private static readonly int[] p = new int[SAMPLE_SIZE + SAMPLE_SIZE + 2];
    private static readonly float[,] g3 = new float[SAMPLE_SIZE + SAMPLE_SIZE + 2, 3];
    private static readonly float[,] g2 = new float[SAMPLE_SIZE + SAMPLE_SIZE + 2, 2];
    private static readonly float[] g1 = new float[SAMPLE_SIZE + SAMPLE_SIZE + 2];

    static Perlin()
    {
        Random rng = new Random(SEED);
        int i, j, k;

        for (i = 0; i < B; i++)
        {
            p[i] = i;
            g1[i] = (float)((rng.Next() % (B + B)) - B) / B;

            for (j = 0; j < 2; j++)
                g2[i, j] = (float)((rng.Next() % (B + B)) - B) / B;
            normalize2(g2, i);

            for (j = 0; j < 3; j++)
                g3[i, j] = (float)((rng.Next() % (B + B)) - B) / B;
            normalize3(g3, i);
        }

        while (--i > 0)
        {
            k = p[i];
            p[i] = p[j = rng.Next() % B];
            p[j] = k;
        }

        for (i = 0; i < B + 2; i++)
        {
            p[B + i] = p[i];
            g1[B + i] = g1[i];
            for (j = 0; j < 2; j++)
                g2[B + i, j] = g2[i, j];
            for (j = 0; j < 3; j++)
                g3[B + i, j] = g3[i, j];
        }
    }

    public static float noise1(float arg)
    {
        int bx0, bx1;
        float rx0, rx1, sx, t, u, v;

        t = arg + N;
        bx0 = ((int)t) & BM;
        bx1 = (bx0 + 1) & BM;
        rx0 = t - (int)t;
        rx1 = rx0 - 1f;

        sx = s_curve(rx0);

        u = rx0 * g1[p[bx0]];
        v = rx1 * g1[p[bx1]];

        return Utils.Lerp(u, v, sx);
    }

    public static float noise2(float x, float y)
    {
        int bx0, bx1, by0, by1, b00, b10, b01, b11;
        float rx0, rx1, ry0, ry1, sx, sy, a, b, t, u, v;
        int i, j;

        t = x + N;
        bx0 = ((int)t) & BM;
        bx1 = (bx0 + 1) & BM;
        rx0 = t - (int)t;
        rx1 = rx0 - 1f;

        t = y + N;
        by0 = ((int)t) & BM;
        by1 = (by0 + 1) & BM;
        ry0 = t - (int)t;
        ry1 = ry0 - 1f;

        i = p[bx0];
        j = p[bx1];

        b00 = p[i + by0];
        b10 = p[j + by0];
        b01 = p[i + by1];
        b11 = p[j + by1];

        sx = s_curve(rx0);
        sy = s_curve(ry0);

        u = rx0 * g2[b00, 0] + ry0 * g2[b00, 1];
        v = rx1 * g2[b10, 0] + ry0 * g2[b10, 1];
        a = Utils.Lerp(u, v, sx);

        u = rx0 * g2[b01, 0] + ry1 * g2[b01, 1];
        v = rx1 * g2[b11, 0] + ry1 * g2[b11, 1];
        b = Utils.Lerp(u, v, sx);

        return Utils.Lerp(a, b, sy);
    }

    public static float noise3(float x, float y, float z)
    {
        int bx0, bx1, by0, by1, bz0, bz1, b00, b10, b01, b11;
        float rx0, rx1, ry0, ry1, rz0, rz1, sy, sz, a, b, c, d, t, u, v;
        int i, j;

        t = x + N;
        bx0 = ((int)t) & BM;
        bx1 = (bx0 + 1) & BM;
        rx0 = t - (int)t;
        rx1 = rx0 - 1f;

        t = y + N;
        by0 = ((int)t) & BM;
        by1 = (by0 + 1) & BM;
        ry0 = t - (int)t;
        ry1 = ry0 - 1f;

        t = z + N;
        bz0 = ((int)t) & BM;
        bz1 = (bz0 + 1) & BM;
        rz0 = t - (int)t;
        rz1 = rz0 - 1f;

        i = p[bx0];
        j = p[bx1];

        b00 = p[i + by0];
        b10 = p[j + by0];
        b01 = p[i + by1];
        b11 = p[j + by1];

        t = s_curve(rx0);
        sy = s_curve(ry0);
        sz = s_curve(rz0);

        u = rx0 * g3[b00 + bz0, 0] + ry0 * g3[b00 + bz0, 1] + rz0 * g3[b00 + bz0, 2];
        v = rx1 * g3[b10 + bz0, 0] + ry0 * g3[b10 + bz0, 1] + rz0 * g3[b10 + bz0, 2];
        a = Utils.Lerp(u, v, t);

        u = rx0 * g3[b01 + bz0, 0] + ry1 * g3[b01 + bz0, 1] + rz0 * g3[b01 + bz0, 2];
        v = rx1 * g3[b11 + bz0, 0] + ry1 * g3[b11 + bz0, 1] + rz0 * g3[b11 + bz0, 2];
        b = Utils.Lerp(u, v, t);

        c = Utils.Lerp(a, b, sy);

        u = rx0 * g3[b00 + bz1, 0] + ry0 * g3[b00 + bz1, 1] + rz1 * g3[b00 + bz1, 2];
        v = rx1 * g3[b10 + bz1, 0] + ry0 * g3[b10 + bz1, 1] + rz1 * g3[b10 + bz1, 2];
        a = Utils.Lerp(u, v, t);

        u = rx0 * g3[b01 + bz1, 0] + ry1 * g3[b01 + bz1, 1] + rz1 * g3[b01 + bz1, 2];
        v = rx1 * g3[b11 + bz1, 0] + ry1 * g3[b11 + bz1, 1] + rz1 * g3[b11 + bz1, 2];
        b = Utils.Lerp(u, v, t);

        d = Utils.Lerp(a, b, sy);
        return Utils.Lerp(c, d, sz);
    }

    public static float turbulence1(float x, float freq)
    {
        float t;
        float v;

        for (t = 0f; freq >= 1f; freq *= 0.5f)
        {
            v = freq * x;
            t += noise1(v) / freq;
        }
        return t;
    }

    public static float turbulence2(float x, float y, float freq)
    {
        float t;
        Vector2 vec;

        for (t = 0f; freq >= 1f; freq *= 0.5f)
        {
            vec.X = freq * x;
            vec.Y = freq * y;
            t += noise2(vec.X, vec.Y) / freq;
        }
        return t;
    }

    public static float turbulence3(float x, float y, float z, float freq)
    {
        float t;
        Vector3 vec;

        for (t = 0f; freq >= 1f; freq *= 0.5f)
        {
            vec.X = freq * x;
            vec.Y = freq * y;
            vec.Z = freq * z;
            t += noise3(vec.X, vec.Y, vec.Z) / freq;
        }
        return t;
    }

    private static void normalize2(float[,] v, int i)
    {
        float s;

        s = (float)Math.Sqrt(v[i, 0] * v[i, 0] + v[i, 1] * v[i, 1]);
        s = 1.0f / s;
        v[i, 0] = v[i, 0] * s;
        v[i, 1] = v[i, 1] * s;
    }

    private static void normalize3(float[,] v, int i)
    {
        float s;

        s = (float)Math.Sqrt(v[i, 0] * v[i, 0] + v[i, 1] * v[i, 1] + v[i, 2] * v[i, 2]);
        s = 1.0f / s;

        v[i, 0] = v[i, 0] * s;
        v[i, 1] = v[i, 1] * s;
        v[i, 2] = v[i, 2] * s;
    }

    private static float s_curve(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
