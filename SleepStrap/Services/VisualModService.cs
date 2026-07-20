using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

using BCnEncoder.Encoder;
using BCnEncoder.Shared;

namespace SleepStrap.Services
{
    internal static class VisualModService
    {
        private const string TextureResourcePrefix = "SleepStrap.DarkTextures/";
        private const string RtxShineTexturePath = "brdfLUT.dds";
        private const string RivalsSkyboxFixResource = "SleepStrap.RivalsSkyboxFix/CacheHeader.bin";
        private const int SkyboxFaceSize = 512;
        private const int EmbeddedSkyboxFaceSize = 1024;

        private static readonly byte[] RobloxSkyDdsHeader = Convert.FromBase64String(
            "RERTIHwAAAAHEAoAAAQAAAAEAAAAAAgAAAAAAAsAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAVVZFUgAAAABOVlRUAAECACAAAAAEAAAARFhUMQAAAAAAAAAAAAAAAAAAAAAAAAAACBBAAAAAAAAAAAAAAAAAAAAAAAA=");

        private static readonly string[] SkyboxFiles =
        {
            "sky512_bk.tex", "sky512_dn.tex", "sky512_ft.tex",
            "sky512_lf.tex", "sky512_rt.tex", "sky512_up.tex"
        };

        private static readonly string[] RobloxProcessNames =
        {
            "RobloxPlayerBeta", "RobloxPlayerLauncher",
            "RobloxCrashHandler", "RobloxCrashHandler64"
        };

        private static readonly string[] RivalsSkyboxCacheFiles =
        {
            @"a5\a564ec8aeef3614e788d02f0090089d8",
            @"73\7328622d2d509b95dd4dd2c721d1ca8b",
            @"a5\a50f6563c50ca4d5dcb255ee5cfab097",
            @"6c\6c94b9385e52d221f0538aadaceead2d",
            @"92\9244e00ff9fd6cee0bb40a262bb35d31",
            @"78\78cb2e93aee0cdbd79b15a866bc93a54"
        };

        private static string TextureModRoot => Path.Combine(Paths.Modifications, @"PlatformContent\pc\textures");
        private static string SkyboxModRoot => Path.Combine(TextureModRoot, "sky");
        private static string DarkTextureBackupRoot => Path.Combine(Paths.SleepStrapData, "Backups", "DarkTextures");
        private static string RtxShineBackupRoot => Path.Combine(Paths.SleepStrapData, "Backups", "RtxShine");
        private static string SkyboxBackupRoot => Path.Combine(Paths.SleepStrapData, "Backups", "Skybox");
        private static string RivalsSkyboxCacheBackupRoot => Path.Combine(Paths.SleepStrapData, "Backups", "RivalsSkyboxCache");
        private static string SkyboxCacheRoot => Path.Combine(Paths.SleepStrapData, "Skybox");

        public static bool HasCachedSkybox => SkyboxFiles.All(file => File.Exists(Path.Combine(SkyboxCacheRoot, file)));

        public static int CloseRobloxProcesses()
        {
            List<System.Diagnostics.Process> processes = RobloxProcessNames
                .SelectMany(System.Diagnostics.Process.GetProcessesByName)
                .GroupBy(process => process.Id)
                .Select(group => group.First())
                .ToList();

            foreach (System.Diagnostics.Process process in processes)
            {
                using (process)
                {
                    try
                    {
                        if (process.HasExited)
                            continue;

                        process.CloseMainWindow();
                        if (!process.WaitForExit(1500))
                        {
                            process.Kill(true);
                            if (!process.WaitForExit(5000))
                                throw new InvalidOperationException($"Roblox process {process.Id} did not close.");
                        }
                    }
                    catch (InvalidOperationException) when (process.HasExited)
                    {
                        // The process finished between checks.
                    }
                }
            }

            string[] stillRunning = RobloxProcessNames
                .Where(name => System.Diagnostics.Process.GetProcessesByName(name).Length > 0)
                .ToArray();
            if (stillRunning.Length > 0)
                throw new InvalidOperationException($"Close Roblox before changing the skybox. Still running: {String.Join(", ", stillRunning)}.");

            return processes.Count;
        }

        public static async Task ImportSkyboxAsync(string sourcePath)
        {
            string stagingRoot = Path.Combine(Paths.SleepStrapData, "Staging", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingRoot);
            try
            {
                await Task.Run(() => ConvertPanoramaToSkybox(sourcePath, stagingRoot));
                if (!App.Settings.Prop.DarkTexturesEnabled)
                    CreateBackupIfNeeded(SkyboxBackupRoot, GetSkyboxRelativePaths());

                Directory.CreateDirectory(SkyboxCacheRoot);
                foreach (string file in SkyboxFiles)
                    File.Copy(Path.Combine(stagingRoot, file), Path.Combine(SkyboxCacheRoot, file), true);
                ApplyCachedSkybox();
            }
            finally
            {
                if (Directory.Exists(stagingRoot))
                    Directory.Delete(stagingRoot, true);
            }
        }

        public static void ApplyEmbeddedSkybox(string resourceFolder)
        {
            ApplyEmbeddedSkybox(resourceFolder, true);
        }

        public static void EnsureSelectedSkyboxReady(bool applyRivalsFix = true)
        {
            string selected = App.Settings.Prop.CustomSkyboxSourceName;
            if (!App.Settings.Prop.CustomSkyboxEnabled || !SkyboxGalleryService.IsPreset(selected))
                return;

            bool cacheIsReady = HasCachedSkybox && SkyboxFiles.All(file => IsDdsTexture(Path.Combine(SkyboxCacheRoot, file)));
            if (cacheIsReady)
                ApplyCachedSkybox();
            else
                ApplyEmbeddedSkybox(selected, false);

            if (applyRivalsFix)
                ApplyRivalsSkyboxCompatibilityFix();
        }

        private static void ApplyEmbeddedSkybox(string resourceFolder, bool createBackup)
        {
            if (String.IsNullOrWhiteSpace(resourceFolder) || resourceFolder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("The selected skybox name is invalid.");

            if (createBackup && !App.Settings.Prop.DarkTexturesEnabled)
                CreateBackupIfNeeded(SkyboxBackupRoot, GetSkyboxRelativePaths());

            const string resourcePrefix = "SleepStrap.Skyboxes/";
            Assembly assembly = Assembly.GetExecutingAssembly();
            Dictionary<string, string> resources = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal))
                .ToDictionary(
                    name => name[resourcePrefix.Length..].Replace('\\', '/'),
                    name => name,
                    StringComparer.OrdinalIgnoreCase);

            Directory.CreateDirectory(SkyboxCacheRoot);
            foreach (string file in SkyboxFiles)
            {
                string relativeResource = $"{resourceFolder}/{file}";
                if (!resources.TryGetValue(relativeResource, out string? resourceName))
                    throw new InvalidOperationException($"The '{resourceFolder}' sky is missing '{file}'.");

                using Stream input = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Could not load embedded sky file '{resourceName}'.");
                WriteRobloxSkyTexture(input, Path.Combine(SkyboxCacheRoot, file));
            }

            ApplyCachedSkybox();
        }

        public static void RemoveCustomSkybox()
        {
            if (App.Settings.Prop.DarkTexturesEnabled)
                ApplyDarkSkybox();
            else
                RestoreBackup(SkyboxBackupRoot, GetSkyboxRelativePaths());

            if (Directory.Exists(SkyboxCacheRoot))
                Directory.Delete(SkyboxCacheRoot, true);

            RestoreRivalsSkyboxCompatibilityFix();
        }

        public static int ApplyRivalsSkyboxCompatibilityFix(string? cacheRoot = null)
        {
            cacheRoot ??= Path.Combine(Paths.Roblox, "rbx-storage");
            CreateRivalsSkyboxCacheBackupIfNeeded(cacheRoot);

            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream input = assembly.GetManifestResourceStream(RivalsSkyboxFixResource)
                ?? throw new InvalidOperationException("The embedded RIVALS skybox compatibility record is missing.");
            using MemoryStream payloadStream = new();
            input.CopyTo(payloadStream);
            byte[] payload = payloadStream.ToArray();

            foreach (string relativePath in RivalsSkyboxCacheFiles)
            {
                string destination = Path.Combine(cacheRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                Filesystem.AssertReadOnly(destination);
                File.WriteAllBytes(destination, payload);
                File.SetAttributes(destination, File.GetAttributes(destination) | FileAttributes.ReadOnly);
            }

            return RivalsSkyboxCacheFiles.Length;
        }

        public static void RestoreRivalsSkyboxCompatibilityFix(string? cacheRoot = null)
        {
            string manifestPath = Path.Combine(RivalsSkyboxCacheBackupRoot, "manifest.json");
            if (!File.Exists(manifestPath))
                return;

            cacheRoot ??= Path.Combine(Paths.Roblox, "rbx-storage");
            Dictionary<string, int> previousFiles = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(manifestPath))
                ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string relativePath in RivalsSkyboxCacheFiles)
            {
                string destination = Path.Combine(cacheRoot, relativePath);
                Filesystem.AssertReadOnly(destination);
                if (previousFiles.TryGetValue(relativePath, out int attributes))
                {
                    string backup = Path.Combine(RivalsSkyboxCacheBackupRoot, "Files", relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(backup, destination, true);
                    File.SetAttributes(destination, (FileAttributes)attributes);
                }
                else if (File.Exists(destination))
                {
                    File.Delete(destination);
                }
            }

            Directory.Delete(RivalsSkyboxCacheBackupRoot, true);
        }

        public static int DeployCachedSkyboxToInstalledVersions()
        {
            if (!HasCachedSkybox || SkyboxFiles.Any(file => !IsDdsTexture(Path.Combine(SkyboxCacheRoot, file))))
                throw new InvalidOperationException("The selected skybox has not been converted into Roblox texture files.");

            if (!Directory.Exists(Paths.Versions))
                return 0;

            int updatedVersions = 0;
            foreach (string versionDirectory in Directory.EnumerateDirectories(Paths.Versions, "version-*"))
            {
                if (!File.Exists(Path.Combine(versionDirectory, "RobloxPlayerBeta.exe")))
                    continue;

                string destinationRoot = Path.Combine(versionDirectory, @"PlatformContent\pc\textures\sky");
                Directory.CreateDirectory(destinationRoot);
                foreach (string file in SkyboxFiles)
                {
                    string destination = Path.Combine(destinationRoot, file);
                    Filesystem.AssertReadOnly(destination);
                    File.Copy(Path.Combine(SkyboxCacheRoot, file), destination, true);
                    Filesystem.AssertReadOnly(destination);
                }

                updatedVersions++;
            }

            return updatedVersions;
        }

        public static void SetDarkTextures(bool enabled)
        {
            bool reapplyCustomSkybox = App.Settings.Prop.CustomSkyboxEnabled && HasCachedSkybox;
            IReadOnlyList<string> texturePaths = GetDarkTextureResources().Select(item => item.RelativePath).ToArray();

            if (enabled)
            {
                if (reapplyCustomSkybox)
                    RestoreBackup(SkyboxBackupRoot, GetSkyboxRelativePaths());
                CreateBackupIfNeeded(DarkTextureBackupRoot, texturePaths);
                ApplyDarkTextures();
                if (reapplyCustomSkybox)
                    ApplyCachedSkybox();
            }
            else
            {
                RestoreBackup(DarkTextureBackupRoot, texturePaths);
                if (reapplyCustomSkybox)
                {
                    CreateBackupIfNeeded(SkyboxBackupRoot, GetSkyboxRelativePaths());
                    ApplyCachedSkybox();
                }
            }
        }

        public static void SetRtxShine(bool enabled)
        {
            IReadOnlyList<string> texturePaths = new[] { RtxShineTexturePath };

            if (enabled)
            {
                CreateBackupIfNeeded(RtxShineBackupRoot, texturePaths);
                ApplyRtxShine();
            }
            else
            {
                RestoreBackup(RtxShineBackupRoot, texturePaths);
            }
        }

        public static void RefreshRtxShineState()
        {
            if (App.Settings.Prop.RtxShineEnabled)
                SetRtxShine(true);
            else if (Directory.Exists(RtxShineBackupRoot))
                SetRtxShine(false);
        }

        private static IReadOnlyList<string> GetSkyboxRelativePaths() => SkyboxFiles.Select(file => $"sky/{file}").ToArray();

        private static IReadOnlyList<(string ResourceName, string RelativePath)> GetDarkTextureResources() =>
            Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Where(name => name.StartsWith(TextureResourcePrefix, StringComparison.Ordinal))
                .Select(name => (name, name[TextureResourcePrefix.Length..].Replace('\\', '/')))
                // The stock BRDF lookup is byte-identical in the dark pack. Leave it to
                // the independent RTX shine layer so texture toggles cannot overwrite it.
                .Where(item => !String.Equals(item.Item2, RtxShineTexturePath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Item2, StringComparer.OrdinalIgnoreCase)
                .Select(item => (ResourceName: item.name, RelativePath: item.Item2))
                .ToArray();

        private static void ApplyDarkTextures()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (var item in GetDarkTextureResources())
            {
                string destination = GetTextureModPath(item.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using Stream input = assembly.GetManifestResourceStream(item.ResourceName)
                    ?? throw new InvalidOperationException($"Missing embedded texture '{item.ResourceName}'.");
                using FileStream output = File.Create(destination);
                input.CopyTo(output);
            }
        }

        private static void ApplyRtxShine()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = $"{TextureResourcePrefix}{RtxShineTexturePath}";
            using Stream input = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException("The embedded RTX shine lookup texture is missing.");
            using MemoryStream buffer = new();
            input.CopyTo(buffer);
            byte[] texture = buffer.ToArray();

            const int ddsHeaderSize = 148;
            if (texture.Length < ddsHeaderSize ||
                texture[0] != (byte)'D' || texture[1] != (byte)'D' ||
                texture[2] != (byte)'S' || texture[3] != (byte)' ' ||
                BitConverter.ToInt32(texture, 128) != 34)
            {
                throw new InvalidDataException("The RTX shine lookup texture is not a supported RG16F DDS file.");
            }

            int width = BitConverter.ToInt32(texture, 16);
            int height = BitConverter.ToInt32(texture, 12);
            int pixelBytes = checked(width * height * 4);
            if (width <= 0 || height <= 0 || texture.Length < ddsHeaderSize + pixelBytes)
                throw new InvalidDataException("The RTX shine lookup texture has invalid dimensions.");

            for (int offset = ddsHeaderSize; offset < ddsHeaderSize + pixelBytes; offset += 4)
            {
                float scale = (float)BitConverter.Int16BitsToHalf(BitConverter.ToInt16(texture, offset));
                float bias = (float)BitConverter.Int16BitsToHalf(BitConverter.ToInt16(texture, offset + 2));

                // Push the split-sum response close to a polished metal while retaining
                // the original view-angle gradient. Albedo stays intact, but Roblox's
                // world materials receive a much stronger environment reflection.
                System.Half shinyScale = (System.Half)Math.Clamp(0.92f + (scale * 0.08f), 0.92f, 1f);
                System.Half shinyBias = (System.Half)Math.Clamp(0.52f + (bias * 0.43f), 0.52f, 0.95f);
                short scaleBits = BitConverter.HalfToInt16Bits(shinyScale);
                short biasBits = BitConverter.HalfToInt16Bits(shinyBias);
                texture[offset] = (byte)scaleBits;
                texture[offset + 1] = (byte)(scaleBits >> 8);
                texture[offset + 2] = (byte)biasBits;
                texture[offset + 3] = (byte)(biasBits >> 8);
            }

            string destination = GetTextureModPath(RtxShineTexturePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllBytes(destination, texture);
        }

        private static void ApplyDarkSkybox()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            var resources = GetDarkTextureResources().ToDictionary(item => item.RelativePath, item => item.ResourceName, StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in GetSkyboxRelativePaths())
            {
                if (!resources.TryGetValue(relativePath, out string? resourceName))
                    throw new InvalidOperationException($"The dark texture pack is missing '{relativePath}'.");
                string destination = GetTextureModPath(relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using Stream input = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Missing embedded texture '{resourceName}'.");
                using FileStream output = File.Create(destination);
                input.CopyTo(output);
            }
        }

        private static void ApplyCachedSkybox()
        {
            if (!HasCachedSkybox)
                throw new InvalidOperationException("The converted skybox cache is incomplete. Import the panorama again.");
            Directory.CreateDirectory(SkyboxModRoot);
            foreach (string file in SkyboxFiles)
                File.Copy(Path.Combine(SkyboxCacheRoot, file), Path.Combine(SkyboxModRoot, file), true);
        }

        private static bool IsDdsTexture(string path)
        {
            if (!File.Exists(path))
                return false;

            using FileStream input = File.OpenRead(path);
            byte[] header = new byte[RobloxSkyDdsHeader.Length];
            return input.Read(header) == header.Length && header.SequenceEqual(RobloxSkyDdsHeader);
        }

        private static void WriteRobloxSkyTexture(Stream input, string destination)
        {
            using MemoryStream source = new();
            input.CopyTo(source);
            source.Position = 0;

            Span<byte> signature = stackalloc byte[4];
            bool alreadyDds = source.Read(signature) == signature.Length &&
                signature[0] == (byte)'D' && signature[1] == (byte)'D' &&
                signature[2] == (byte)'S' && signature[3] == (byte)' ';
            source.Position = 0;

            if (alreadyDds)
            {
                using FileStream copiedOutput = File.Create(destination);
                source.CopyTo(copiedOutput);
                if (copiedOutput.Length >= RobloxSkyDdsHeader.Length)
                {
                    copiedOutput.Position = 0;
                    copiedOutput.Write(RobloxSkyDdsHeader);
                }
                return;
            }

            using Image loadedImage = Image.FromStream(source);
            using var face = new Bitmap(EmbeddedSkyboxFaceSize, EmbeddedSkyboxFaceSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(face))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(loadedImage, 0, 0, face.Width, face.Height);
            }

            byte[] rgbaPixels = ReadBgraPixels(face);
            for (int offset = 0; offset < rgbaPixels.Length; offset += 4)
                (rgbaPixels[offset], rgbaPixels[offset + 2]) = (rgbaPixels[offset + 2], rgbaPixels[offset]);

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

            using FileStream output = File.Create(destination);
            encoder.EncodeToStream(rgbaPixels, face.Width, face.Height, BCnEncoder.Encoder.PixelFormat.Rgba32, output);
            output.Position = 0;
            output.Write(RobloxSkyDdsHeader);
        }

        private static void CreateRivalsSkyboxCacheBackupIfNeeded(string cacheRoot)
        {
            string manifestPath = Path.Combine(RivalsSkyboxCacheBackupRoot, "manifest.json");
            if (File.Exists(manifestPath))
                return;

            Directory.CreateDirectory(RivalsSkyboxCacheBackupRoot);
            var existingFiles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in RivalsSkyboxCacheFiles)
            {
                string source = Path.Combine(cacheRoot, relativePath);
                if (!File.Exists(source))
                    continue;

                existingFiles[relativePath] = (int)File.GetAttributes(source);
                string backup = Path.Combine(RivalsSkyboxCacheBackupRoot, "Files", relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Copy(source, backup, true);
            }

            File.WriteAllText(manifestPath, JsonSerializer.Serialize(existingFiles, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static string GetTextureModPath(string relativePath) =>
            Path.Combine(TextureModRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        private static void CreateBackupIfNeeded(string backupRoot, IReadOnlyList<string> relativePaths)
        {
            string manifestPath = Path.Combine(backupRoot, "manifest.json");
            if (File.Exists(manifestPath))
                return;
            Directory.CreateDirectory(backupRoot);
            var existingFiles = new List<string>();
            foreach (string relativePath in relativePaths)
            {
                string source = GetTextureModPath(relativePath);
                if (!File.Exists(source))
                    continue;
                existingFiles.Add(relativePath);
                string backup = Path.Combine(backupRoot, "Files", relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Copy(source, backup, true);
            }
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(existingFiles, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static void RestoreBackup(string backupRoot, IReadOnlyList<string> relativePaths)
        {
            string manifestPath = Path.Combine(backupRoot, "manifest.json");
            if (!File.Exists(manifestPath))
                return;
            var existingFiles = JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(manifestPath))
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in relativePaths)
            {
                string destination = GetTextureModPath(relativePath);
                if (existingFiles.Contains(relativePath))
                {
                    string backup = Path.Combine(backupRoot, "Files", relativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(backup, destination, true);
                }
                else if (File.Exists(destination))
                {
                    Filesystem.AssertReadOnly(destination);
                    File.Delete(destination);
                }
            }
            Directory.Delete(backupRoot, true);
        }

        private static void ConvertPanoramaToSkybox(string sourcePath, string outputRoot)
        {
            using var loadedImage = Image.FromFile(sourcePath);
            if (loadedImage.Width < 512 || loadedImage.Height < 256)
                throw new InvalidDataException("Choose a panorama that is at least 512 × 256 pixels.");
            double aspectRatio = loadedImage.Width / (double)loadedImage.Height;
            if (aspectRatio < 1.8 || aspectRatio > 2.2)
                throw new InvalidDataException("Sky images must be equirectangular panoramas with a 2:1 width-to-height ratio.");

            using var panorama = new Bitmap(loadedImage.Width, loadedImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
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
                using FileStream output = File.Create(Path.Combine(outputRoot, $"sky512_{suffixes[face]}.tex"));
                encoder.EncodeToStream(facePixels, SkyboxFaceSize, SkyboxFaceSize, BCnEncoder.Encoder.PixelFormat.Rgba32, output);
            }
        }

        private static byte[] ReadBgraPixels(Bitmap bitmap)
        {
            var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
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
            finally { bitmap.UnlockBits(data); }
        }

        private static byte[] RenderFace(byte[] panorama, int panoramaWidth, int panoramaHeight, int face)
        {
            byte[] output = new byte[SkyboxFaceSize * SkyboxFaceSize * 4];
            Parallel.For(0, SkyboxFaceSize, y =>
            {
                for (int x = 0; x < SkyboxFaceSize; x++)
                {
                    double u = (2.0 * (x + 0.5) / SkyboxFaceSize) - 1.0;
                    double v = (2.0 * (y + 0.5) / SkyboxFaceSize) - 1.0;
                    (double dx, double dy, double dz) = GetDirection(face, u, v);
                    double length = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
                    dx /= length; dy /= length; dz /= length;
                    double longitude = Math.Atan2(dx, dz);
                    double latitude = Math.Asin(Math.Clamp(dy, -1.0, 1.0));
                    double sourceX = ((longitude / (2.0 * Math.PI)) + 0.5) * panoramaWidth;
                    double sourceY = (0.5 - (latitude / Math.PI)) * panoramaHeight;
                    SampleBilinear(panorama, panoramaWidth, panoramaHeight, sourceX, sourceY, output, ((y * SkyboxFaceSize) + x) * 4);
                }
            });
            return output;
        }

        private static (double X, double Y, double Z) GetDirection(int face, double u, double v) => face switch
        {
            0 => (1, -v, -u), 1 => (-1, -v, u), 2 => (u, 1, v),
            3 => (u, -1, -v), 4 => (u, -v, 1), 5 => (-u, -v, -1),
            _ => throw new ArgumentOutOfRangeException(nameof(face))
        };

        private static void SampleBilinear(byte[] source, int width, int height, double x, double y, byte[] destination, int offset)
        {
            x %= width;
            if (x < 0) x += width;
            y = Math.Clamp(y, 0, height - 1.001);
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            int x1 = (x0 + 1) % width, y1 = Math.Min(y0 + 1, height - 1);
            double tx = x - x0, ty = y - y0;
            int p00 = ((y0 * width) + x0) * 4, p10 = ((y0 * width) + x1) * 4;
            int p01 = ((y1 * width) + x0) * 4, p11 = ((y1 * width) + x1) * 4;
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
}
