using System.Windows.Media;

namespace SleepStrap.Services
{
    internal static class SkyboxThemeService
    {
        private static readonly IReadOnlyDictionary<string, Color> Colors =
            new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
            {
                ["Red"] = Color.FromRgb(170, 42, 42), ["Hades"] = Color.FromRgb(126, 40, 36),
                ["Orange"] = Color.FromRgb(190, 91, 35), ["Hazy"] = Color.FromRgb(157, 91, 49),
                ["Aurora"] = Color.FromRgb(46, 129, 76), ["ChromaKey"] = Color.FromRgb(34, 145, 88), ["Spooky"] = Color.FromRgb(67, 105, 64),
                ["Night"] = Color.FromRgb(28, 48, 95), ["Moonlight"] = Color.FromRgb(67, 87, 143), ["Beautiful"] = Color.FromRgb(67, 106, 165),
                ["Blue"] = Color.FromRgb(40, 89, 164), ["Space Blue"] = Color.FromRgb(28, 78, 136), ["Pandora"] = Color.FromRgb(26, 101, 154),
                ["NeonSky"] = Color.FromRgb(20, 142, 207), ["NeonSky2"] = Color.FromRgb(47, 120, 190), ["Goodnight"] = Color.FromRgb(44, 75, 128),
                ["Cyan"] = Color.FromRgb(28, 157, 171), ["Light Blue"] = Color.FromRgb(106, 166, 205),
                ["Chill pink"] = Color.FromRgb(177, 102, 142), ["Light pink"] = Color.FromRgb(216, 145, 174), ["Universe"] = Color.FromRgb(106, 76, 147), ["Pink Sunrise"] = Color.FromRgb(193, 109, 125),
                ["Chill gray"] = Color.FromRgb(132, 140, 151), ["Overcast"] = Color.FromRgb(101, 108, 119),
                ["Emo"] = Color.FromRgb(29, 25, 35)
            };

        public static Color GetColor(string? skyName)
        {
            if (String.Equals(skyName, "Zoff", StringComparison.OrdinalIgnoreCase))
                skyName = "Pandora";
            return skyName is not null && Colors.TryGetValue(skyName, out Color color)
                ? color
                : Color.FromRgb(44, 48, 56);
        }
    }
}
