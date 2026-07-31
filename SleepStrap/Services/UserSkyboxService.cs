using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;

namespace SleepStrap.Services
{
    /// <summary>
    /// Owns imported panorama files. Imported skies live outside the active
    /// modifications directory so merely importing one cannot change Roblox.
    /// VisualModService materializes the selected entry at launch time.
    /// </summary>
    internal static class UserSkyboxService
    {
        public const string SelectionKeyPrefix = "user:";
        private const int PreviewWidth = 384;
        private const int PreviewHeight = 192;
        private const string PreviewFileName = "preview.png";
        private const string SourceFileName = "source.png";

        private static readonly string[] _skyboxFileNames =
        {
            "sky512_bk.tex", "sky512_dn.tex", "sky512_ft.tex",
            "sky512_lf.tex", "sky512_rt.tex", "sky512_up.tex"
        };

        public static IReadOnlyList<string> SkyboxFileNames => _skyboxFileNames;

        public static bool IsEnabled => App.Settings.Prop.CustomImportedSkyboxesEnabled;

        public static event EventHandler? UserSkyboxChanged;

        public static void NotifyChanged() =>
            UserSkyboxChanged?.Invoke(null, EventArgs.Empty);

        private static string Root => Path.Combine(Paths.SleepBloxData, "CustomSkyboxes");

        public static IReadOnlyList<SkyboxChoice> GetChoices()
        {
            bool enabled = App.Settings.Prop.CustomImportedSkyboxesEnabled;
            var choices = new List<SkyboxChoice>();
            foreach (UserSkyboxDefinition definition in Definitions)
            {
                if (String.IsNullOrWhiteSpace(definition.Name) ||
                    !TryNormalizeId(definition.Id, out string id))
                {
                    continue;
                }

                string selectionKey = GetSelectionKey(id);
                bool hasValidTextures = TryGetSkyboxDirectory(selectionKey, out _);

                choices.Add(new SkyboxChoice(
                    definition.Name,
                    selectionKey,
                    LoadPreview(GetSkyboxDirectory(id)),
                    selectionKey: selectionKey,
                    isUserImported: true,
                    isAvailable: enabled && hasValidTextures));
            }

            return choices;
        }

        public static bool IsUserSkyboxKey(string? selectionKey) =>
            TryGetId(selectionKey, out _);

        public static string GetSelectionKey(string id)
        {
            if (!Guid.TryParse(id, out Guid parsed))
                throw new ArgumentException("The imported skybox identifier is invalid.", nameof(id));

            return SelectionKeyPrefix + parsed.ToString("N");
        }

        /// <summary>
        /// Resolves an imported selection key to its validated texture directory.
        /// The returned directory contains the six Roblox sky512 texture files.
        /// </summary>
        public static bool TryGetSkyboxDirectory(string? selectionKey, out string directory)
        {
            directory = "";
            if (!TryGetId(selectionKey, out string id) ||
                !Definitions.Any(definition => String.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string candidate = GetSkyboxDirectory(id);
            if (_skyboxFileNames.Any(file => !IsRobloxSkyTexture(Path.Combine(candidate, file))))
                return false;

            directory = candidate;
            return true;
        }

        public static void SetEnabled(bool enabled)
        {
            if (App.Settings.Prop.CustomImportedSkyboxesEnabled == enabled)
                return;

            App.Settings.Prop.CustomImportedSkyboxesEnabled = enabled;
            App.Settings.Save();
            NotifyChanged();
        }

        /// <summary>
        /// Converts a 2:1 PNG panorama to six Roblox texture files and registers
        /// it in settings. It never writes to the live Roblox modifications folder.
        /// </summary>
        public static async Task<UserSkyboxDefinition> ImportAsync(string sourcePath, string displayName)
        {
            string name = ValidateDisplayName(displayName);
            ValidateSourcePath(sourcePath);

            string id = Guid.NewGuid().ToString("N");
            string stagingRoot = Path.Combine(Root, $".staging-{id}");
            string destinationRoot = GetSkyboxDirectory(id);
            var definition = new UserSkyboxDefinition
            {
                Id = id,
                Name = name,
                CreatedUtc = DateTime.UtcNow
            };

            Directory.CreateDirectory(Root);
            bool movedToDestination = false;
            try
            {
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(stagingRoot);
                    VisualModService.ConvertPanoramaToSkybox(sourcePath, stagingRoot);
                    File.Copy(sourcePath, Path.Combine(stagingRoot, SourceFileName), true);
                    CreatePreview(sourcePath, Path.Combine(stagingRoot, PreviewFileName));
                });

                if (_skyboxFileNames.Any(file => !IsRobloxSkyTexture(Path.Combine(stagingRoot, file))))
                    throw new InvalidDataException("The imported skybox did not produce valid Roblox texture files.");

                Directory.Move(stagingRoot, destinationRoot);
                movedToDestination = true;
                Definitions.Add(definition);
                App.Settings.Save();
                NotifyChanged();
                return definition;
            }
            catch
            {
                if (Definitions.Remove(definition))
                    App.Settings.Save();
                if (movedToDestination && Directory.Exists(destinationRoot))
                {
                    try { Directory.Delete(destinationRoot, true); }
                    catch { }
                }
                throw;
            }
            finally
            {
                if (Directory.Exists(stagingRoot))
                {
                    try { Directory.Delete(stagingRoot, true); }
                    catch { }
                }
            }
        }

        private static List<UserSkyboxDefinition> Definitions =>
            App.Settings.Prop.UserSkyboxes ??= new List<UserSkyboxDefinition>();

        private static bool TryGetId(string? selectionKey, out string id)
        {
            id = "";
            if (String.IsNullOrWhiteSpace(selectionKey) ||
                !selectionKey.StartsWith(SelectionKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string value = selectionKey[SelectionKeyPrefix.Length..];
            return TryNormalizeId(value, out id);
        }

        private static bool TryNormalizeId(string? value, out string id)
        {
            id = "";
            if (!Guid.TryParse(value, out Guid parsed))
                return false;

            id = parsed.ToString("N");
            return true;
        }

        private static string GetSkyboxDirectory(string id) =>
            Path.Combine(Root, id);

        private static string ValidateDisplayName(string? displayName)
        {
            string name = displayName?.Trim() ?? "";
            if (name.Length == 0)
                throw new InvalidDataException("Give the imported skybox a name.");
            if (name.Length > 64)
                throw new InvalidDataException("Skybox names can be at most 64 characters.");
            if (name.Any(char.IsControl))
                throw new InvalidDataException("Skybox names cannot contain control characters.");
            return name;
        }

        private static void ValidateSourcePath(string sourcePath)
        {
            if (String.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("The selected PNG could not be found.", sourcePath);
            if (!String.Equals(Path.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Choose a PNG panorama.");
        }

        private static void CreatePreview(string sourcePath, string destinationPath)
        {
            using var loadedImage = Image.FromFile(sourcePath);
            using var preview = new Bitmap(PreviewWidth, PreviewHeight, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(preview))
            {
                graphics.Clear(Color.Black);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(loadedImage, 0, 0, preview.Width, preview.Height);
            }
            preview.Save(destinationPath, ImageFormat.Png);
        }

        private static BitmapImage? LoadPreview(string directory)
        {
            string path = Path.Combine(directory, PreviewFileName);
            if (!File.Exists(path))
                return null;

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsRobloxSkyTexture(string path)
        {
            if (!File.Exists(path))
                return false;

            return VisualModService.IsRobloxSkyTexture(path);
        }

    }
}
