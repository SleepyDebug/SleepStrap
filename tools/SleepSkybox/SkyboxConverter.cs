using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

using BCnEncoder.Encoder;
using BCnEncoder.Shared;

namespace SleepSkybox;

internal static class SkyboxConverter
{
    // Roblox's sky512 filenames contain 1024×1024 BC1/DXT1 face textures.
    // The Roblox DDS header below declares this size, so the payload must match.
    private const int FaceSize = 1024;

    private static readonly byte[] RobloxSkyDdsHeader = Convert.FromBase64String(
        "RERTIHwAAAAHEAoAAAQAAAAEAAAAAAgAAAAAAAsAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAVVZFUgAAAABOVlRUAAECACAAAAAEAAAARFhUMQAAAAAAAAAAAAAAAAAAAAAAAAAACBBAAAAAAAAAAAAAAAAAAAAAAAA=");

    public static IReadOnlyList<string> OutputFileNames { get; } = new[]
    {
        "sky512_bk.tex", "sky512_dn.tex", "sky512_ft.tex",
        "sky512_lf.tex", "sky512_rt.tex", "sky512_up.tex"
    };

    public static string GetSkyDirectory(string outputRoot) =>
        Path.Combine(outputRoot, "PlatformContent", "pc", "textures", "sky");

    public static void ConvertPngPanorama(string sourcePath, string outputRoot)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The selected PNG could not be found.", sourcePath);
        if (!String.Equals(Path.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a PNG panorama.");

        using var loadedImage = Image.FromFile(sourcePath);
        if (loadedImage.Width < 512 || loadedImage.Height < 256)
            throw new InvalidDataException("Choose a panorama that is at least 512 × 256 pixels.");

        double aspectRatio = loadedImage.Width / (double)loadedImage.Height;
        if (aspectRatio < 1.8 || aspectRatio > 2.2)
        {
            throw new InvalidDataException(
                "This needs an equirectangular panorama: a wide 2:1 image, such as 2048 × 1024.");
        }

        string skyDirectory = GetSkyDirectory(outputRoot);
        Directory.CreateDirectory(skyDirectory);

        using var panorama = new Bitmap(
            loadedImage.Width,
            loadedImage.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(panorama))
            graphics.DrawImage(loadedImage, 0, 0, panorama.Width, panorama.Height);

        byte[] sourcePixels = ReadBgraPixels(panorama);
        string[] suffixes = { "rt", "lf", "up", "dn", "ft", "bk" };
        var encoder = new BcEncoder
        {
            OutputOptions =
            {
                GenerateMipMaps = true,
                Format = CompressionFormat.Bc1,
                FileFormat = OutputFileFormat.Dds,
                Quality = CompressionQuality.Balanced
            }
        };

        for (int face = 0; face < suffixes.Length; face++)
        {
            byte[] facePixels = RenderFace(sourcePixels, panorama.Width, panorama.Height, face);
            string destination = Path.Combine(skyDirectory, $"sky512_{suffixes[face]}.tex");
            using FileStream output = File.Create(destination);
            encoder.EncodeToStream(facePixels, FaceSize, FaceSize, BCnEncoder.Encoder.PixelFormat.Rgba32, output);
            output.Position = 0;
            output.Write(RobloxSkyDdsHeader);
        }

        var manifest = new
        {
            createdUtc = DateTime.UtcNow,
            sourceFile = Path.GetFileName(sourcePath),
            format = "Roblox sky512 DDS/BC1 texture files",
            files = OutputFileNames
        };
        File.WriteAllText(
            Path.Combine(outputRoot, "sleepSkybox.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static byte[] ReadBgraPixels(Bitmap bitmap)
    {
        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(
            rectangle,
            ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = bitmap.Width * 4;
            byte[] source = new byte[Math.Abs(data.Stride) * bitmap.Height];
            Marshal.Copy(data.Scan0, source, 0, source.Length);
            if (data.Stride == rowBytes)
                return source;

            byte[] packed = new byte[rowBytes * bitmap.Height];
            for (int y = 0; y < bitmap.Height; y++)
            {
                int sourceRow = data.Stride > 0 ? y : bitmap.Height - 1 - y;
                Buffer.BlockCopy(source, sourceRow * Math.Abs(data.Stride), packed, y * rowBytes, rowBytes);
            }
            return packed;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static byte[] RenderFace(byte[] panorama, int panoramaWidth, int panoramaHeight, int face)
    {
        byte[] output = new byte[FaceSize * FaceSize * 4];
        Parallel.For(0, FaceSize, y =>
        {
            for (int x = 0; x < FaceSize; x++)
            {
                double u = (2.0 * (x + 0.5) / FaceSize) - 1.0;
                double v = (2.0 * (y + 0.5) / FaceSize) - 1.0;
                (double dx, double dy, double dz) = GetDirection(face, u, v);
                double length = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
                dx /= length;
                dy /= length;
                dz /= length;
                double longitude = Math.Atan2(dx, dz);
                double latitude = Math.Asin(Math.Clamp(dy, -1.0, 1.0));
                double sourceX = ((longitude / (2.0 * Math.PI)) + 0.5) * panoramaWidth;
                double sourceY = (0.5 - (latitude / Math.PI)) * panoramaHeight;
                SampleBilinear(
                    panorama,
                    panoramaWidth,
                    panoramaHeight,
                    sourceX,
                    sourceY,
                    output,
                    ((y * FaceSize) + x) * 4);
            }
        });
        return output;
    }

    private static (double X, double Y, double Z) GetDirection(int face, double u, double v) => face switch
    {
        0 => (1, -v, -u),
        1 => (-1, -v, u),
        2 => (u, 1, v),
        3 => (u, -1, -v),
        4 => (u, -v, 1),
        5 => (-u, -v, -1),
        _ => throw new ArgumentOutOfRangeException(nameof(face))
    };

    private static void SampleBilinear(
        byte[] source,
        int width,
        int height,
        double x,
        double y,
        byte[] destination,
        int offset)
    {
        x %= width;
        if (x < 0)
            x += width;
        y = Math.Clamp(y, 0, height - 1.001);
        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        int x1 = (x0 + 1) % width;
        int y1 = Math.Min(y0 + 1, height - 1);
        double tx = x - x0;
        double ty = y - y0;
        int p00 = ((y0 * width) + x0) * 4;
        int p10 = ((y0 * width) + x1) * 4;
        int p01 = ((y1 * width) + x0) * 4;
        int p11 = ((y1 * width) + x1) * 4;

        destination[offset] = Interpolate(source[p00 + 2], source[p10 + 2], source[p01 + 2], source[p11 + 2], tx, ty);
        destination[offset + 1] = Interpolate(source[p00 + 1], source[p10 + 1], source[p01 + 1], source[p11 + 1], tx, ty);
        destination[offset + 2] = Interpolate(source[p00], source[p10], source[p01], source[p11], tx, ty);
        destination[offset + 3] = 255;
    }

    private static byte Interpolate(byte p00, byte p10, byte p01, byte p11, double tx, double ty)
    {
        double top = p00 + ((p10 - p00) * tx);
        double bottom = p01 + ((p11 - p01) * tx);
        return (byte)Math.Clamp(Math.Round(top + ((bottom - top) * ty)), 0, 255);
    }
}
