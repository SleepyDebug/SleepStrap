using System.Windows.Media;

namespace Bloxstrap.Models
{
    public sealed record SkyboxChoice(string Name, string ResourceFolder, ImageSource? Preview, bool IsNone = false);
}
