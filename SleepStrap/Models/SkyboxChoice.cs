using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SleepStrap.Models
{
    public sealed class SkyboxChoice : INotifyPropertyChanged
    {
        public SkyboxChoice(string name, string resourceFolder, ImageSource? preview, bool isNone = false)
        {
            Name = name;
            ResourceFolder = resourceFolder;
            Preview = preview;
            IsNone = isNone;
        }

        public string Name { get; }
        public string ResourceFolder { get; }
        public ImageSource? Preview { get; }
        public bool IsNone { get; }
        public int OriginalIndex { get; set; }

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite == value) return;
                _isFavorite = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
