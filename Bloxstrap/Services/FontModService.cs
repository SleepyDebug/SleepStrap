using System.Drawing.Text;

using Bloxstrap.AppData;

namespace Bloxstrap.Services
{
    internal static class FontModService
    {
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
            return choice;
        }

        public static void RestoreDefault()
        {
            if (File.Exists(Paths.CustomFont))
            {
                Filesystem.AssertReadOnly(Paths.CustomFont);
                File.Delete(Paths.CustomFont);
            }

            string familyMods = Path.Combine(Paths.Modifications, "content", "fonts", "families");
            if (Directory.Exists(familyMods))
                Directory.Delete(familyMods, true);

            App.Settings.Prop.SelectedFontName = "Roblox Default";
            App.Settings.Prop.SelectedFontSource = "";
            App.Settings.Save();
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
