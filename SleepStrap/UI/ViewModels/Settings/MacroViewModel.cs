using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using SleepStrap.Services;

namespace SleepStrap.UI.ViewModels.Settings
{
    public sealed class MacroViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        public sealed class WeaponOption : NotifyPropertyChangedViewModel
        {
            private readonly Action<WeaponOption> _missingChanged;
            private bool _isMissing;
            private int _effectiveSlotNumber;

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

            public int EffectiveSlotNumber
            {
                get => _effectiveSlotNumber;
                set
                {
                    if (_effectiveSlotNumber == value)
                        return;
                    _effectiveSlotNumber = value;
                    OnPropertyChanged(nameof(EffectiveSlotNumber));
                    OnPropertyChanged(nameof(SlotText));
                }
            }

            public string SlotText => IsMissing ? "Not owned" : $"Slot {EffectiveSlotNumber}";

            public WeaponOption(string name, MacroWeaponCategory category, int originalIndex, string imageFile, bool isMissing, Action<WeaponOption> missingChanged)
            {
                Name = name;
                Category = category;
                OriginalIndex = originalIndex;
                ImageSource = $"pack://application:,,,/Resources/SleepStrap/WeaponImages/{category}/{Uri.EscapeDataString(imageFile)}";
                _isMissing = isMissing;
                _missingChanged = missingChanged;
            }

            public override string ToString() => Name;
        }

        private readonly DispatcherTimer _automationTimer;
        private readonly AsyncRelayCommand _runMacroCommand;
        private long _lastRespawn;
        private long _lastUtility;
        private long _lastInspect;
        private bool _isRunning;
        private bool _automationMasterEnabled = true;
        private bool _masterKeyWasDown;
        private CancellationTokenSource? _macroCancellation;
        private DateTime? _nextAutoRejoinUtc;
        private bool _autoRejoinInProgress;
        private WeaponOption? _selectedPrimary;
        private WeaponOption? _selectedSecondary;
        private WeaponOption? _selectedMelee;
        private WeaponOption? _selectedUtility;

        public ObservableCollection<WeaponOption> PrimaryWeapons { get; } = new();
        public ObservableCollection<WeaponOption> SecondaryWeapons { get; } = new();
        public ObservableCollection<WeaponOption> MeleeWeapons { get; } = new();
        public ObservableCollection<WeaponOption> UtilityWeapons { get; } = new();

        public ObservableCollection<WeaponOption> AvailablePrimary { get; } = new();
        public ObservableCollection<WeaponOption> AvailableSecondary { get; } = new();
        public ObservableCollection<WeaponOption> AvailableMelee { get; } = new();
        public ObservableCollection<WeaponOption> AvailableUtility { get; } = new();

        public ICommand RunMacroCommand => _runMacroCommand;

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                _isRunning = value;
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(RunButtonText));
                _runMacroCommand.NotifyCanExecuteChanged();
            }
        }

        public string RunButtonText => IsRunning ? "Repeating — press ] to stop" : "Start repeating loadout";

        public bool AutomationMasterEnabled
        {
            get => _automationMasterEnabled;
            private set
            {
                if (_automationMasterEnabled == value)
                    return;
                _automationMasterEnabled = value;
                OnPropertyChanged(nameof(AutomationMasterEnabled));
                OnPropertyChanged(nameof(AutomationMasterText));
            }
        }

        public string AutomationMasterText => AutomationMasterEnabled
            ? "Automatic actions on — press ] to pause"
            : "Automatic actions paused — press ] to resume";

        public bool UseListLayout
        {
            get => App.Settings.Prop.MacroUseListLayout;
            set
            {
                App.Settings.Prop.MacroUseListLayout = value;
                SaveAndNotify(nameof(UseListLayout));
                OnPropertyChanged(nameof(LayoutName));
            }
        }

        public string LayoutName => UseListLayout ? "List" : "Grid";

        public WeaponOption? SelectedPrimary
        {
            get => _selectedPrimary;
            set => SetSelection(ref _selectedPrimary, value, nameof(SelectedPrimary), selected => App.Settings.Prop.MacroPrimaryWeapon = selected.Name);
        }

        public WeaponOption? SelectedSecondary
        {
            get => _selectedSecondary;
            set => SetSelection(ref _selectedSecondary, value, nameof(SelectedSecondary), selected => App.Settings.Prop.MacroSecondaryWeapon = selected.Name);
        }

        public WeaponOption? SelectedMelee
        {
            get => _selectedMelee;
            set => SetSelection(ref _selectedMelee, value, nameof(SelectedMelee), selected => App.Settings.Prop.MacroMeleeWeapon = selected.Name);
        }

        public WeaponOption? SelectedUtility
        {
            get => _selectedUtility;
            set => SetSelection(ref _selectedUtility, value, nameof(SelectedUtility), selected => App.Settings.Prop.MacroUtilityWeapon = selected.Name);
        }

        public bool QuickRespawn
        {
            get => App.Settings.Prop.MacroQuickRespawn;
            set { App.Settings.Prop.MacroQuickRespawn = value; SaveAndNotify(nameof(QuickRespawn)); }
        }

        public bool AutoUtility
        {
            get => App.Settings.Prop.MacroAutoUtility;
            set { App.Settings.Prop.MacroAutoUtility = value; SaveAndNotify(nameof(AutoUtility)); }
        }

        public bool AutoInspect
        {
            get => App.Settings.Prop.MacroAutoInspect;
            set { App.Settings.Prop.MacroAutoInspect = value; SaveAndNotify(nameof(AutoInspect)); }
        }

        public bool AutoRejoinEnabled
        {
            get => App.Settings.Prop.MacroAutoRejoinHourly;
            set
            {
                if (value == App.Settings.Prop.MacroAutoRejoinHourly)
                    return;
                App.Settings.Prop.MacroAutoRejoinHourly = value;
                _nextAutoRejoinUtc = value ? DateTime.UtcNow.AddHours(1) : null;
                SaveAndNotify(nameof(AutoRejoinEnabled));
            }
        }

        public MacroViewModel()
        {
            HashSet<string> missing = new(App.Settings.Prop.MacroMissingWeapons, StringComparer.OrdinalIgnoreCase);

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
                ("Battle Axe", "BattleAxe Icon.png"), ("Chainsaw", "Chainsaw Icon.png"), ("Fists", "Fists Icon.png"),
                ("Katana", "Katana Icon.png"), ("Knife", "Knife Icon.png"),
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

            RefreshAvailableWeapons();
            RefreshEffectiveSlots();
            _selectedPrimary = FindSaved(AvailablePrimary, App.Settings.Prop.MacroPrimaryWeapon);
            _selectedSecondary = FindSaved(AvailableSecondary, App.Settings.Prop.MacroSecondaryWeapon);
            _selectedMelee = FindSaved(AvailableMelee, App.Settings.Prop.MacroMeleeWeapon);
            _selectedUtility = FindSaved(AvailableUtility, App.Settings.Prop.MacroUtilityWeapon);
            _runMacroCommand = new AsyncRelayCommand(RunMacroAsync, () => !IsRunning && AllSelectionsPresent());
            _automationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _automationTimer.Tick += AutomationTimer_Tick;
            _automationTimer.Start();
            if (AutoRejoinEnabled)
                _nextAutoRejoinUtc = DateTime.UtcNow.AddHours(1);
        }

        private void AddCategory(
            ObservableCollection<WeaponOption> destination,
            MacroWeaponCategory category,
            HashSet<string> missing,
            IEnumerable<(string Name, string Image)> weapons)
        {
            int index = 0;
            foreach ((string name, string image) in weapons)
                destination.Add(new WeaponOption(name, category, index++, image, missing.Contains(name), MissingChanged));
        }

        private void MissingChanged(WeaponOption changed)
        {
            App.Settings.Prop.MacroMissingWeapons = AllWeapons()
                .Where(weapon => weapon.IsMissing)
                .Select(weapon => weapon.Name)
                .ToList();
            RefreshEffectiveSlots();
            SyncAvailableWeapons(AvailableWeapons(changed.Category), CategoryWeapons(changed.Category));

            if (changed.IsMissing)
                RepairSelectionIfRemoved(changed);

            _runMacroCommand?.NotifyCanExecuteChanged();
            App.Settings.Save();
        }

        private void RefreshAvailableWeapons()
        {
            SyncAvailableWeapons(AvailablePrimary, PrimaryWeapons);
            SyncAvailableWeapons(AvailableSecondary, SecondaryWeapons);
            SyncAvailableWeapons(AvailableMelee, MeleeWeapons);
            SyncAvailableWeapons(AvailableUtility, UtilityWeapons);
        }

        private static void SyncAvailableWeapons(ObservableCollection<WeaponOption> destination, IEnumerable<WeaponOption> source)
        {
            WeaponOption[] wanted = source.Where(weapon => !weapon.IsMissing).ToArray();

            for (int index = destination.Count - 1; index >= 0; index--)
            {
                if (!wanted.Contains(destination[index]))
                    destination.RemoveAt(index);
            }

            for (int index = 0; index < wanted.Length; index++)
            {
                WeaponOption weapon = wanted[index];
                int currentIndex = destination.IndexOf(weapon);
                if (currentIndex < 0)
                    destination.Insert(index, weapon);
                else if (currentIndex != index)
                    destination.Move(currentIndex, index);
            }
        }

        private void RepairSelectionIfRemoved(WeaponOption removed)
        {
            WeaponOption? replacement = AvailableWeapons(removed.Category)
                .FirstOrDefault(weapon => weapon.OriginalIndex > removed.OriginalIndex)
                ?? AvailableWeapons(removed.Category).LastOrDefault();

            switch (removed.Category)
            {
                case MacroWeaponCategory.Primary when ReferenceEquals(_selectedPrimary, removed):
                    SetSelectionOrClear(ref _selectedPrimary, replacement, nameof(SelectedPrimary), value => App.Settings.Prop.MacroPrimaryWeapon = value);
                    break;
                case MacroWeaponCategory.Secondary when ReferenceEquals(_selectedSecondary, removed):
                    SetSelectionOrClear(ref _selectedSecondary, replacement, nameof(SelectedSecondary), value => App.Settings.Prop.MacroSecondaryWeapon = value);
                    break;
                case MacroWeaponCategory.Melee when ReferenceEquals(_selectedMelee, removed):
                    SetSelectionOrClear(ref _selectedMelee, replacement, nameof(SelectedMelee), value => App.Settings.Prop.MacroMeleeWeapon = value);
                    break;
                case MacroWeaponCategory.Utility when ReferenceEquals(_selectedUtility, removed):
                    SetSelectionOrClear(ref _selectedUtility, replacement, nameof(SelectedUtility), value => App.Settings.Prop.MacroUtilityWeapon = value);
                    break;
            }

        }

        private void RefreshEffectiveSlots()
        {
            foreach (MacroWeaponCategory category in Enum.GetValues<MacroWeaponCategory>())
            {
                int missingBefore = 0;
                foreach (WeaponOption weapon in CategoryWeapons(category))
                {
                    if (weapon.IsMissing)
                    {
                        weapon.EffectiveSlotNumber = 0;
                        missingBefore++;
                    }
                    else
                    {
                        weapon.EffectiveSlotNumber = weapon.OriginalIndex - missingBefore + 1;
                    }
                }
            }
        }

        private async Task RunMacroAsync()
        {
            if (!AllSelectionsPresent())
                return;

            IsRunning = true;
            AutomationMasterEnabled = true;
            _macroCancellation = new CancellationTokenSource();
            try
            {
                var selections = new[]
                {
                    CreateSelection(SelectedPrimary!), CreateSelection(SelectedSecondary!),
                    CreateSelection(SelectedMelee!), CreateSelection(SelectedUtility!)
                };

                while (AutomationMasterEnabled && !_macroCancellation.IsCancellationRequested)
                {
                    MacroWeaponLayout layout = UseListLayout ? MacroWeaponLayout.List : MacroWeaponLayout.Grid;
                    await MacroAutomationService.RunLoadoutAsync(selections, layout, _macroCancellation.Token);
                    if (AutomationMasterEnabled && !_macroCancellation.IsCancellationRequested)
                        await Task.Delay(250, _macroCancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Pressing ] intentionally stops the repeating macro immediately.
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("MacroViewModel::RunMacro", ex);
                Frontend.ShowMessageBox($"SleepStrap could not apply that loadout.\n\n{ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                _macroCancellation.Dispose();
                _macroCancellation = null;
                IsRunning = false;
            }
        }

        private (MacroWeaponCategory Category, int OriginalIndex, IReadOnlyList<int> MissingIndices) CreateSelection(WeaponOption weapon)
        {
            IReadOnlyList<int> missing = CategoryWeapons(weapon.Category)
                .Where(item => item.IsMissing)
                .Select(item => item.OriginalIndex)
                .ToArray();
            return (weapon.Category, weapon.OriginalIndex, missing);
        }

        private void AutomationTimer_Tick(object? sender, EventArgs e)
        {
            bool masterKeyDown = MacroAutomationService.IsKeyDown(0xDD); // ] / OEM close bracket
            if (masterKeyDown && !_masterKeyWasDown)
            {
                if (IsRunning)
                {
                    AutomationMasterEnabled = false;
                    _macroCancellation?.Cancel();
                }
                else
                {
                    AutomationMasterEnabled = !AutomationMasterEnabled;
                }
            }
            _masterKeyWasDown = masterKeyDown;

            if (AutoRejoinEnabled && !_autoRejoinInProgress &&
                _nextAutoRejoinUtc is DateTime nextRejoin && DateTime.UtcNow >= nextRejoin)
            {
                _nextAutoRejoinUtc = DateTime.UtcNow.AddHours(1);
                _ = RunHourlyAutoRejoinAsync();
            }

            if (!AutomationMasterEnabled || _autoRejoinInProgress || !MacroAutomationService.IsRobloxForeground())
                return;

            long now = Environment.TickCount64;
            if (QuickRespawn && now - _lastRespawn >= 75)
            {
                MacroAutomationService.TapKey(0x20);
                _lastRespawn = now;
            }
            if (AutoUtility && now - _lastUtility >= 120)
            {
                MacroAutomationService.TapKey(0x47); // G
                _lastUtility = now;
            }
            if (AutoInspect && now - _lastInspect >= 180)
            {
                MacroAutomationService.TapKey(0x56); // V
                _lastInspect = now;
            }
        }

        private void SetSelection(ref WeaponOption? field, WeaponOption? value, string propertyName, Action<WeaponOption> save)
        {
            if (value is null || ReferenceEquals(field, value))
                return;
            field = value;
            save(value);
            App.Settings.Save();
            OnPropertyChanged(propertyName);
            _runMacroCommand?.NotifyCanExecuteChanged();
        }

        private void SetSelectionOrClear(ref WeaponOption? field, WeaponOption? value, string propertyName, Action<string> save)
        {
            if (ReferenceEquals(field, value))
                return;

            field = value;
            save(value?.Name ?? String.Empty);
            OnPropertyChanged(propertyName);
        }

        private static WeaponOption? FindSaved(IEnumerable<WeaponOption> options, string saved) =>
            options.FirstOrDefault(option => option.Name.Equals(saved, StringComparison.OrdinalIgnoreCase)) ?? options.FirstOrDefault();

        private IEnumerable<WeaponOption> AllWeapons() => PrimaryWeapons.Concat(SecondaryWeapons).Concat(MeleeWeapons).Concat(UtilityWeapons);

        private IEnumerable<WeaponOption> CategoryWeapons(MacroWeaponCategory category) => category switch
        {
            MacroWeaponCategory.Primary => PrimaryWeapons,
            MacroWeaponCategory.Secondary => SecondaryWeapons,
            MacroWeaponCategory.Melee => MeleeWeapons,
            _ => UtilityWeapons
        };

        private ObservableCollection<WeaponOption> AvailableWeapons(MacroWeaponCategory category) => category switch
        {
            MacroWeaponCategory.Primary => AvailablePrimary,
            MacroWeaponCategory.Secondary => AvailableSecondary,
            MacroWeaponCategory.Melee => AvailableMelee,
            _ => AvailableUtility
        };

        private bool AllSelectionsPresent() => SelectedPrimary is not null && SelectedSecondary is not null && SelectedMelee is not null && SelectedUtility is not null;

        private void SaveAndNotify(string propertyName)
        {
            App.Settings.Save();
            OnPropertyChanged(propertyName);
        }

        private async Task RunHourlyAutoRejoinAsync()
        {
            bool resumeRepeatingLoadout = IsRunning;
            _autoRejoinInProgress = true;

            try
            {
                if (resumeRepeatingLoadout)
                {
                    _macroCancellation?.Cancel();
                    for (int attempt = 0; attempt < 100 && IsRunning; attempt++)
                        await Task.Delay(50);
                }

                await MacroAutomationService.RunAutoRejoinAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("MacroViewModel::RunHourlyAutoRejoin", ex);
                Frontend.ShowMessageBox($"SleepStrap could not complete Auto Rejoin.\n\n{ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                _autoRejoinInProgress = false;
                if (resumeRepeatingLoadout && AutomationMasterEnabled && AllSelectionsPresent())
                    _ = RunMacroAsync();
            }
        }

        public void Dispose()
        {
            _automationTimer.Stop();
            _automationTimer.Tick -= AutomationTimer_Tick;
        }
    }
}
