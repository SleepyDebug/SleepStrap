using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SleepStrap.Models
{
    public sealed class SkyboxChoice : INotifyPropertyChanged
    {
        public SkyboxChoice(
            string name,
            string resourceFolder,
            ImageSource? preview,
            bool isNone = false,
            string? selectionKey = null,
            bool isUserImported = false,
            bool isAvailable = true)
        {
            Name = name;
            ResourceFolder = resourceFolder;
            Preview = preview;
            IsNone = isNone;
            SelectionKey = selectionKey ?? resourceFolder;
            IsUserImported = isUserImported;
            _isAvailable = isAvailable;
        }

        public string Name { get; }
        public string ResourceFolder { get; }
        public ImageSource? Preview { get; }
        public bool IsNone { get; }
        /// <summary>
        /// Stable persisted value used to identify this sky. It is distinct from
        /// <see cref="Name"/> so user-created skies may use any display name.
        /// </summary>
        public string SelectionKey { get; }
        public bool IsUserImported { get; }
        public int OriginalIndex { get; set; }

        private bool _isAvailable;
        /// <summary>
        /// Whether this choice can be selected right now. Imported skies mirror
        /// the CustomImportedSkyboxesEnabled master switch.
        /// </summary>
        public bool IsAvailable
        {
            get => _isAvailable;
            set
            {
                if (_isAvailable == value) return;
                _isAvailable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAvailable)));
            }
        }

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
