using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using SleepStrap.Services;

namespace SleepStrap.UI.ViewModels.Settings
{
    public sealed class MacroViewModel : NotifyPropertyChangedViewModel
    {
        public sealed class WeaponOption : NotifyPropertyChangedViewModel
        {
            private readonly Action<WeaponOption> _missingChanged;
            private bool _isMissing;
            private int _effectiveSlot;

            public string Name { get; }
            public MacroWeaponCategory Category { get; }
            public int OriginalIndex { get; }
            public string ImageSource { get; }

            public bool IsMissing
            {
                get => _isMissing;
                set
                {
                    if (_isMissing == value)
                        return;

                    _isMissing = value;
                    OnPropertyChanged(nameof(IsMissing));
                    OnPropertyChanged(nameof(SlotText));
                    _missingChanged(this);
                }
            }

            public int EffectiveSlot
            {
                get => _effectiveSlot;
                set
                {
                    if (_effectiveSlot == value)
                        return;

                    _effectiveSlot = value;
                    OnPropertyChanged(nameof(EffectiveSlot));
                    OnPropertyChanged(nameof(SlotText));
                }
            }

            public string SlotText => IsMissing ? "Not owned" : $"Slot {EffectiveSlot}";

            public WeaponOption(
                string name,
                MacroWeaponCategory category,
                int originalIndex,
                string imageFile,
                bool isMissing,
                Action<WeaponOption> missingChanged)
            {
                Name = name;
                Category = category;
                OriginalIndex = originalIndex;
                ImageSource =
                    $"pack://application:,,,/Resources/SleepStrap/WeaponImages/{category}/{Uri.EscapeDataString(imageFile)}";
                _isMissing = isMissing;
                _missingChanged = missingChanged;
            }

            public override string ToString() => Name;
        }

        private readonly AsyncRelayCommand _runGridLoadoutCommand;
        private WeaponOption? _selectedPrimary;
        private WeaponOption? _selectedSecondary;
        private WeaponOption? _selectedMelee;
        private WeaponOption? _selectedUtility;
        private bool _isRunning;

        public ObservableCollection<WeaponOption> PrimaryWeapons { get; } = new();
        public ObservableCollection<WeaponOption> SecondaryWeapons { get; } = new();
        public ObservableCollection<WeaponOption> MeleeWeapons { get; } = new();
        public ObservableCollection<WeaponOption> UtilityWeapons { get; } = new();
        public ObservableCollection<WeaponOption> AvailablePrimary { get; } = new();
        public ObservableCollection<WeaponOption> AvailableSecondary { get; } = new();
        public ObservableCollection<WeaponOption> AvailableMelee { get; } = new();
        public ObservableCollection<WeaponOption> AvailableUtility { get; } = new();

        public ICommand RunGridLoadoutCommand => _runGridLoadoutCommand;

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (_isRunning == value)
                    return;

                _isRunning = value;
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(RunButtonText));
                _runGridLoadoutCommand.NotifyCanExecuteChanged();
            }
        }

        public string RunButtonText => IsRunning ? "Applying..." : "Apply grid loadout";

        public WeaponOption? SelectedPrimary
        {
            get => _selectedPrimary;
            set => SetSelection(
                ref _selectedPrimary,
                value,
                nameof(SelectedPrimary),
                name => App.Settings.Prop.MacroPrimaryWeapon = name);
        }

        public WeaponOption? SelectedSecondary
        {
            get => _selectedSecondary;
            set => SetSelection(
                ref _selectedSecondary,
                value,
                nameof(SelectedSecondary),
                name => App.Settings.Prop.MacroSecondaryWeapon = name);
        }

        public WeaponOption? SelectedMelee
        {
            get => _selectedMelee;
            set => SetSelection(
                ref _selectedMelee,
                value,
                nameof(SelectedMelee),
                name => App.Settings.Prop.MacroMeleeWeapon = name);
        }

        public WeaponOption? SelectedUtility
        {
            get => _selectedUtility;
            set => SetSelection(
                ref _selectedUtility,
                value,
                nameof(SelectedUtility),
                name => App.Settings.Prop.MacroUtilityWeapon = name);
        }

        public bool AutoRejoinEnabled
        {
            get => App.Settings.Prop.MacroAutoRejoinHourly;
            set
            {
                if (value == App.Settings.Prop.MacroAutoRejoinHourly)
                    return;

                AutoRejoinSchedulerService.SetEnabled(value);
                OnPropertyChanged(nameof(AutoRejoinEnabled));
            }
        }

        public MacroViewModel()
        {
            HashSet<string> missing = new(
                App.Settings.Prop.MacroMissingWeapons,
                StringComparer.OrdinalIgnoreCase);

            AddCategory(PrimaryWeapons, MacroWeaponCategory.Primary, missing, new[]
            {
                ("Distortion", "Distortion Icon.png"), ("Permafrost", "Permafrost Icon.png"),
                ("Energy Rifle", "EnergyRifle Icon.png"), ("Flamethrower", "Flamethrower Icon.png"),
                ("Grenade Launcher", "GrenadeLauncher Icon.png"), ("Minigun", "Minigun Icon.png"),
                ("Paintball Gun", "PaintballGun Icon.png"), ("Assault Rifle", "AssaultRifle Icon.png"),
                ("Bow", "Bow Icon.png"), ("Burst Rifle", "BurstRifle Icon.png"),
                ("Crossbow", "Crossbow Icon.png"), ("Gunblade", "Gunblade Icon.png"),
                ("RPG", "RPG Icon.png"), ("Shotgun", "Shotgun Icon.png"), ("Sniper", "Sniper Icon.png")
            });
            AddCategory(SecondaryWeapons, MacroWeaponCategory.Secondary, missing, new[]
            {
                ("Warper", "Warper Icon.png"), ("Energy Pistols", "EnergyPistols Icon.png"),
                ("Exogun", "Exogun Icon.png"), ("Slingshot", "Slingshot Icon.png"),
                ("Daggers", "Daggers Icon.png"), ("Flare Gun", "FlareGun Icon.png"),
                ("Handgun", "Handgun Icon.png"), ("Revolver", "Revolver Icon.png"),
                ("Shorty", "Shorty Icon.png"), ("Spray", "Spray Icon.png"), ("Uzi", "Uzi Icon.png")
            });
            AddCategory(MeleeWeapons, MacroWeaponCategory.Melee, missing, new[]
            {
                ("Maul", "Maul Icon.png"), ("Spear", "Spear Icon.png"), ("Trowel", "Trowel Icon.png"),
                ("Battle Axe", "BattleAxe Icon.png"), ("Chainsaw", "Chainsaw Icon.png"),
                ("Fists", "Fists Icon.png"), ("Katana", "Katana Icon.png"), ("Knife", "Knife Icon.png"),
                ("Riot Shield", "RiotShield Icon.png"), ("Scythe", "Scythe Icon.png")
            });
            AddCategory(UtilityWeapons, MacroWeaponCategory.Utility, missing, new[]
            {
                ("Grappler", "Grappler Icon.png"), ("Medkit", "Medkit Icon.png"),
                ("Subspace Tripmine", "SubspaceTripmine Icon.png"), ("Warpstone", "Warpstone Icon.png"),
                ("Flashbang", "Flashbang Icon.png"), ("Freeze Ray", "FreezeRay Icon.png"),
                ("Grenade", "Grenade Icon.png"), ("Jump Pad", "JumpPad Icon.png"),
                ("Molotov", "Molotov Icon.png"), ("Satchel", "Satchel Icon.png"),
                ("Smoke Grenade", "SmokeGrenade Icon.png"), ("War Horn", "WarHorn Icon.png")
            });

            RefreshAll();
            _selectedPrimary = FindSaved(AvailablePrimary, App.Settings.Prop.MacroPrimaryWeapon);
            _selectedSecondary = FindSaved(AvailableSecondary, App.Settings.Prop.MacroSecondaryWeapon);
            _selectedMelee = FindSaved(AvailableMelee, App.Settings.Prop.MacroMeleeWeapon);
            _selectedUtility = FindSaved(AvailableUtility, App.Settings.Prop.MacroUtilityWeapon);
            _runGridLoadoutCommand = new AsyncRelayCommand(
                RunGridLoadoutAsync,
                () => !IsRunning && AllSelectionsPresent());
        }

        private void AddCategory(
            ObservableCollection<WeaponOption> destination,
            MacroWeaponCategory category,
            HashSet<string> missing,
            IEnumerable<(string Name, string Image)> weapons)
        {
            int index = 0;
            foreach ((string name, string image) in weapons)
            {
                destination.Add(new WeaponOption(
                    name,
                    category,
                    index++,
                    image,
                    missing.Contains(name),
                    MissingChanged));
            }
        }

        private void MissingChanged(WeaponOption changed)
        {
            App.Settings.Prop.MacroMissingWeapons = AllWeapons()
                .Where(weapon => weapon.IsMissing)
                .Select(weapon => weapon.Name)
                .ToList();

            RefreshCategory(changed.Category);
            RepairSelection(changed.Category);
            App.Settings.Save();
            _runGridLoadoutCommand.NotifyCanExecuteChanged();
        }

        private void RefreshAll()
        {
            foreach (MacroWeaponCategory category in Enum.GetValues<MacroWeaponCategory>())
                RefreshCategory(category);
        }

        private void RefreshCategory(MacroWeaponCategory category)
        {
            ObservableCollection<WeaponOption> source = CategoryWeapons(category);
            ObservableCollection<WeaponOption> available = AvailableWeapons(category);
            WeaponOption[] wanted = source.Where(weapon => !weapon.IsMissing).ToArray();

            available.Clear();
            foreach (WeaponOption weapon in wanted)
                available.Add(weapon);

            int missingBefore = 0;
            foreach (WeaponOption weapon in source)
            {
                if (weapon.IsMissing)
                {
                    weapon.EffectiveSlot = 0;
                    missingBefore++;
                }
                else
                {
                    weapon.EffectiveSlot = weapon.OriginalIndex - missingBefore + 1;
                }
            }
        }

        private void RepairSelection(MacroWeaponCategory category)
        {
            switch (category)
            {
                case MacroWeaponCategory.Primary when _selectedPrimary?.IsMissing == true:
                    SelectedPrimary = AvailablePrimary.FirstOrDefault();
                    break;
                case MacroWeaponCategory.Secondary when _selectedSecondary?.IsMissing == true:
                    SelectedSecondary = AvailableSecondary.FirstOrDefault();
                    break;
                case MacroWeaponCategory.Melee when _selectedMelee?.IsMissing == true:
                    SelectedMelee = AvailableMelee.FirstOrDefault();
                    break;
                case MacroWeaponCategory.Utility when _selectedUtility?.IsMissing == true:
                    SelectedUtility = AvailableUtility.FirstOrDefault();
                    break;
            }
        }

        private async Task RunGridLoadoutAsync()
        {
            if (!AllSelectionsPresent())
                return;

            IsRunning = true;
            try
            {
                var selections = new[]
                {
                    CreateSelection(SelectedPrimary!),
                    CreateSelection(SelectedSecondary!),
                    CreateSelection(SelectedMelee!),
                    CreateSelection(SelectedUtility!)
                };
                await MacroAutomationService.RunGridLoadoutAsync(selections, CancellationToken.None);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("MacroViewModel::RunGridLoadout", ex);
                Frontend.ShowMessageBox(
                    $"{App.ProjectName} could not apply that grid loadout.\n\n{ex.Message}",
                    MessageBoxImage.Error);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private (MacroWeaponCategory Category, int OriginalIndex, IReadOnlyList<int> MissingIndices)
            CreateSelection(WeaponOption weapon)
        {
            int[] missing = CategoryWeapons(weapon.Category)
                .Where(item => item.IsMissing)
                .Select(item => item.OriginalIndex)
                .ToArray();
            return (weapon.Category, weapon.OriginalIndex, missing);
        }

        private void SetSelection(
            ref WeaponOption? field,
            WeaponOption? value,
            string propertyName,
            Action<string> save)
        {
            if (value is null || ReferenceEquals(field, value))
                return;

            field = value;
            save(value.Name);
            App.Settings.Save();
            OnPropertyChanged(propertyName);
            _runGridLoadoutCommand?.NotifyCanExecuteChanged();
        }

        private IEnumerable<WeaponOption> AllWeapons() =>
            PrimaryWeapons.Concat(SecondaryWeapons).Concat(MeleeWeapons).Concat(UtilityWeapons);

        private ObservableCollection<WeaponOption> CategoryWeapons(MacroWeaponCategory category) =>
            category switch
            {
                MacroWeaponCategory.Primary => PrimaryWeapons,
                MacroWeaponCategory.Secondary => SecondaryWeapons,
                MacroWeaponCategory.Melee => MeleeWeapons,
                _ => UtilityWeapons
            };

        private ObservableCollection<WeaponOption> AvailableWeapons(MacroWeaponCategory category) =>
            category switch
            {
                MacroWeaponCategory.Primary => AvailablePrimary,
                MacroWeaponCategory.Secondary => AvailableSecondary,
                MacroWeaponCategory.Melee => AvailableMelee,
                _ => AvailableUtility
            };

        private static WeaponOption? FindSaved(IEnumerable<WeaponOption> options, string saved) =>
            options.FirstOrDefault(option => option.Name.Equals(saved, StringComparison.OrdinalIgnoreCase))
            ?? options.FirstOrDefault();

        private bool AllSelectionsPresent() =>
            SelectedPrimary is not null &&
            SelectedSecondary is not null &&
            SelectedMelee is not null &&
            SelectedUtility is not null;
    }
}
