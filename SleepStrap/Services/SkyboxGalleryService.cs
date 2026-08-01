using System.Reflection;
using System.Windows.Media.Imaging;

namespace SleepStrap.Services
{
    internal static class SkyboxGalleryService
    {
        private const string ResourcePrefix = "SleepStrap.Skyboxes/";

        /// <summary>
        /// Raised when a user-imported sky is added or when its master availability
        /// switch changes. Pages can rebuild the gallery without reopening settings.
        /// </summary>
        public static event EventHandler? GalleryChanged
        {
            add => UserSkyboxService.UserSkyboxChanged += value;
            remove => UserSkyboxService.UserSkyboxChanged -= value;
        }

        /// <summary>
        /// Signals pages to rebuild their gallery after a caller has changed the
        /// imported-sky master setting directly.
        /// </summary>
        public static void NotifyGalleryChanged() => UserSkyboxService.NotifyChanged();

        // Keep the gallery grouped by the dominant color of each sky.
        private static readonly string[] PresetNames =
        {
            "Red", "Hades",
            "Orange", "Hazy",
            "Aurora", "ChromaKey", "Spooky",
            "Night", "Moonlight", "Beautiful", "Blue", "Space Blue", "Pandora", "NeonSky", "NeonSky2", "Goodnight", "Cyan", "Light Blue",
            "Chill pink", "Light pink", "Universe", "Pink Sunrise",
            "Chill gray", "Overcast",
            "Emo"
        };

        public static bool IsPreset(string name) =>
            PresetNames.Contains(name, StringComparer.OrdinalIgnoreCase) ||
            String.Equals(name, "Zoff", StringComparison.OrdinalIgnoreCase);

        public static bool IsUserImported(string? selectionKey) =>
            UserSkyboxService.IsUserSkyboxKey(selectionKey);

        public static bool AreUserImportedSkyboxesEnabled => UserSkyboxService.IsEnabled;

        public static bool TryGetUserSkyboxDirectory(string? selectionKey, out string directory) =>
            UserSkyboxService.TryGetSkyboxDirectory(selectionKey, out directory);

        public static string GetResourceFolder(string name) =>
            String.Equals(name, "Zoff", StringComparison.OrdinalIgnoreCase) ? "Pandora" : name;

        public static IReadOnlyList<SkyboxChoice> GetChoices()
        {
            List<SkyboxChoice> choices = new()
            {
                new SkyboxChoice("None", "", null, true, selectionKey: "")
            };

            foreach (string name in PresetNames)
            {
                string displayName = String.Equals(name, "Pandora", StringComparison.OrdinalIgnoreCase) ? "Zoff" : name;
                choices.Add(new SkyboxChoice(displayName, name, LoadPreview(name), selectionKey: displayName));
            }

            // Imported entries are deliberately included even while the master
            // switch is off. Their IsAvailable state tells the UI to render them
            // disabled without hiding a user's saved work.
            choices.AddRange(UserSkyboxService.GetChoices());

            return choices;
        }

        private static BitmapImage LoadPreview(string resourceFolder)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string expected = $"{resourceFolder}/preview.jpg";
            string? resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name =>
                    name.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                    String.Equals(name[ResourcePrefix.Length..].Replace('\\', '/'), expected, StringComparison.OrdinalIgnoreCase));

            if (resourceName is null)
                throw new InvalidOperationException($"The '{resourceFolder}' sky preview is missing.");

            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Could not load sky preview '{resourceName}'.");
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
