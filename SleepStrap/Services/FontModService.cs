using System.Drawing.Text;

using SleepStrap.AppData;

namespace SleepStrap.Services
{
    internal static class FontModService
    {
        private const string CustomFontAsset = "rbxasset://fonts/CustomFont.ttf";
        private static string FontBackupRoot => Path.Combine(Paths.SleepStrapData, "Backups", "InGameFonts");

        private static readonly byte[][] ValidHeaders =
        {
            new byte[] { 0x00, 0x01, 0x00, 0x00 },
            new byte[] { 0x4F, 0x54, 0x54, 0x4F },
            new byte[] { 0x74, 0x74, 0x63, 0x66 }
        };

        public static IReadOnlyList<FontChoice> GetAvailableFonts()
        {
            List<FontChoice> choices = new();
            HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

            // Montserrat is deliberately pinned first.
            string montserratPath = EnsureMontserratExtracted();
            AddChoice(choices, seenPaths, new FontChoice("Montserrat ★", montserratPath));
            choices.Add(new FontChoice("Roblox Default", "", true));

            foreach (string path in EnumerateRobloxFonts())
                AddChoice(choices, seenPaths, new FontChoice($"Roblox · {GetFontName(path)}", path));

            if (File.Exists(Paths.CustomFont) &&
                !String.IsNullOrWhiteSpace(App.Settings.Prop.SelectedFontName) &&
                !choices.Any(x => String.Equals(x.DisplayName, App.Settings.Prop.SelectedFontName, StringComparison.OrdinalIgnoreCase)))
            {
                choices.Insert(2, new FontChoice(App.Settings.Prop.SelectedFontName, Paths.CustomFont));
            }

            return choices;
        }

        public static FontChoice ApplyFont(FontChoice choice)
        {
            if (choice.IsDefault)
            {
                RestoreDefault();
                return choice;
            }

            ValidateFont(choice.FilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(Paths.CustomFont)!);

            if (!String.Equals(choice.FilePath, Paths.CustomFont, StringComparison.OrdinalIgnoreCase))
            {
                Filesystem.AssertReadOnly(Paths.CustomFont);
                File.Copy(choice.FilePath, Paths.CustomFont, true);
            }

            App.Settings.Prop.SelectedFontName = choice.DisplayName;
            App.Settings.Prop.SelectedFontSource = choice.FilePath;
            App.Settings.Save();

            string currentVersion = new RobloxPlayerData().Directory;
            if (Directory.Exists(currentVersion))
                PrepareInGameFontOverrides(currentVersion);

            return choice;
        }

        public static void RestoreDefault()
        {
            RestoreGeneratedOverrides();

            if (File.Exists(Paths.CustomFont))
            {
                Filesystem.AssertReadOnly(Paths.CustomFont);
                File.Delete(Paths.CustomFont);
            }

            App.Settings.Prop.SelectedFontName = "Roblox Default";
            App.Settings.Prop.SelectedFontSource = "";
            App.Settings.Save();
        }

        /// <summary>
        /// Replaces both family-manifest faces and direct Latin text-font assets. Some
        /// Roblox experiences reference a font file directly instead of a family JSON,
        /// so doing both is required for a consistent in-game font.
        /// </summary>
        public static void PrepareInGameFontOverrides(string robloxVersionDirectory)
        {
            if (!File.Exists(Paths.CustomFont))
            {
                RestoreGeneratedOverrides();
                return;
            }

            string fontsRoot = Path.Combine(robloxVersionDirectory, "content", "fonts");
            string familiesRoot = Path.Combine(fontsRoot, "families");
            if (!Directory.Exists(fontsRoot))
                return;

            foreach (string sourcePath in Directory.EnumerateFiles(fontsRoot, "*.*", SearchOption.AllDirectories)
                .Where(IsFontFile)
                .Where(ShouldReplaceTextFont))
            {
                string relativeFromFonts = Path.GetRelativePath(fontsRoot, sourcePath);
                string relativeModPath = Path.Combine("content", "fonts", relativeFromFonts);
                string destination = Path.Combine(Paths.Modifications, relativeModPath);
                TrackOverride(relativeModPath, destination);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                Filesystem.AssertReadOnly(destination);
                File.Copy(Paths.CustomFont, destination, true);
            }

            if (!Directory.Exists(familiesRoot))
                return;

            foreach (string jsonFilePath in Directory.EnumerateFiles(familiesRoot, "*.json", SearchOption.TopDirectoryOnly))
            {
                FontFamily? family = JsonSerializer.Deserialize<FontFamily>(File.ReadAllText(jsonFilePath));
                if (family is null)
                    continue;

                foreach (FontFace face in family.Faces)
                    face.AssetId = CustomFontAsset;

                string relativeModPath = Path.Combine("content", "fonts", "families", Path.GetFileName(jsonFilePath));
                string destination = Path.Combine(Paths.Modifications, relativeModPath);
                TrackOverride(relativeModPath, destination, IsGeneratedFamilyOverride);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                Filesystem.AssertReadOnly(destination);
                File.WriteAllText(destination, JsonSerializer.Serialize(family, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        private static bool ShouldReplaceTextFont(string path)
        {
            string fileName = Path.GetFileName(path);
            string[] preservedTokens = { "emoji", "twemoji", "symbol", "icon", "noto" };
            return !preservedTokens.Any(token => fileName.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsGeneratedFamilyOverride(string path)
        {
            try
            {
                return File.ReadAllText(path).Contains(CustomFontAsset, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void TrackOverride(string relativePath, string destination, Func<string, bool>? isAlreadyOwned = null)
        {
            string trackedPath = Path.Combine(FontBackupRoot, "tracked.json");
            string existingPath = Path.Combine(FontBackupRoot, "existing.json");
            HashSet<string> tracked = File.Exists(trackedPath)
                ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(trackedPath)) ?? new(StringComparer.OrdinalIgnoreCase)
                : new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> existing = File.Exists(existingPath)
                ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(existingPath)) ?? new(StringComparer.OrdinalIgnoreCase)
                : new(StringComparer.OrdinalIgnoreCase);

            if (!tracked.Add(relativePath))
                return;

            Directory.CreateDirectory(FontBackupRoot);
            if (File.Exists(destination) && !(isAlreadyOwned?.Invoke(destination) ?? false))
            {
                existing.Add(relativePath);
                string backup = Path.Combine(FontBackupRoot, "Files", relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Copy(destination, backup, true);
            }

            File.WriteAllText(trackedPath, JsonSerializer.Serialize(tracked, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(existingPath, JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static void RestoreGeneratedOverrides()
        {
            string trackedPath = Path.Combine(FontBackupRoot, "tracked.json");
            if (!File.Exists(trackedPath))
            {
                // Migrate the family-only implementation used by older SleepStrap builds.
                string legacyFamilies = Path.Combine(Paths.Modifications, "content", "fonts", "families");
                if (Directory.Exists(legacyFamilies))
                {
                    foreach (string path in Directory.EnumerateFiles(legacyFamilies, "*.json"))
                    {
                        if (IsGeneratedFamilyOverride(path))
                            File.Delete(path);
                    }
                }
                return;
            }

            HashSet<string> tracked = JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(trackedPath))
                ?? new(StringComparer.OrdinalIgnoreCase);
            string existingPath = Path.Combine(FontBackupRoot, "existing.json");
            HashSet<string> existing = File.Exists(existingPath)
                ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(existingPath)) ?? new(StringComparer.OrdinalIgnoreCase)
                : new(StringComparer.OrdinalIgnoreCase);

            foreach (string relativePath in tracked)
            {
                string destination = Path.Combine(Paths.Modifications, relativePath);
                Filesystem.AssertReadOnly(destination);
                if (existing.Contains(relativePath))
                {
                    string backup = Path.Combine(FontBackupRoot, "Files", relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(backup, destination, true);
                }
                else if (File.Exists(destination))
                {
                    File.Delete(destination);
                }
            }

            Directory.Delete(FontBackupRoot, true);
        }

        private static string EnsureMontserratExtracted()
        {
            string directory = Path.Combine(Paths.SleepStrapData, "Fonts");
            string path = Path.Combine(directory, "Montserrat.ttf");
            Directory.CreateDirectory(directory);

            using Stream stream = typeof(FontModService).Assembly.GetManifestResourceStream("SleepStrap.Fonts/Montserrat.ttf")
                ?? throw new InvalidOperationException("The bundled Montserrat font could not be loaded.");
            using FileStream output = File.Create(path);
            stream.CopyTo(output);
            return path;
        }

        private static IEnumerable<string> EnumerateRobloxFonts()
        {
            string directory = Path.Combine(new RobloxPlayerData().Directory, "content", "fonts");
            if (!Directory.Exists(directory))
                yield break;

            foreach (string path in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(IsFontFile)
                .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }

        private static string GetFontName(string path)
        {
            try
            {
                using PrivateFontCollection fonts = new();
                fonts.AddFontFile(path);
                string? name = fonts.Families.FirstOrDefault()?.Name;
                if (!String.IsNullOrWhiteSpace(name))
                    return name;
            }
            catch
            {
                // The filename is still useful for font collections or uncommon OpenType variants.
            }

            return Path.GetFileNameWithoutExtension(path);
        }

        private static void ValidateFont(string path)
        {
            if (!File.Exists(path) || !IsFontFile(path))
                throw new InvalidOperationException("Choose a valid TTF, OTF, or TTC font file.");

            byte[] header = new byte[4];
            using FileStream stream = File.OpenRead(path);
            if (stream.Read(header, 0, header.Length) != header.Length || !ValidHeaders.Any(x => header.SequenceEqual(x)))
                throw new InvalidOperationException("The selected file does not contain a supported font.");
        }

        private static bool IsFontFile(string path) =>
            Path.GetExtension(path).Equals(".ttf", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(path).Equals(".otf", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(path).Equals(".ttc", StringComparison.OrdinalIgnoreCase);

        private static void AddChoice(List<FontChoice> choices, HashSet<string> seenPaths, FontChoice choice)
        {
            if (choice.IsDefault || seenPaths.Add(choice.FilePath))
                choices.Add(choice);
        }
    }
}
