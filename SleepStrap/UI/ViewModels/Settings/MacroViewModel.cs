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
        private bool _usingAutoHotkeyActions;
        private bool _quickLoadoutVisible;
        private bool _quickHotkeyWasDown;
        private bool _quickLoadoutRunning;
        private bool _quickHotkeyCaptureActive;
        private CancellationTokenSource? _macroCancellation;
        private DateTime? _nextAutoRejoinUtc;
        private bool _autoRejoinInProgress;
        private WeaponOption? _selectedPrimary;
        private WeaponOption? _selectedSecondary;
        private WeaponOption? _selectedMelee;
        private WeaponOption? _selectedUtility;
        private WeaponOption? _quickSelectedPrimary;
        private WeaponOption? _quickSelectedSecondary;
        private WeaponOption? _quickSelectedMelee;
        private WeaponOption? _quickSelectedUtility;

        public ObservableCollection<WeaponOption> PrimaryWeapons { get; } = new();
        public ObservableCollection<WeaponOption> SecondaryWeapons { get; } = new();
        public ObservableCollection<WeaponOption> MeleeWeapons { get; } = new();
        public ObservableCollection<WeaponOption> UtilityWeapons { get; } = new();

        public ObservableCollection<WeaponOption> AvailablePrimary { get; } = new();
        public ObservableCollection<WeaponOption> AvailableSecondary { get; } = new();
        public ObservableCollection<WeaponOption> AvailableMelee { get; } = new();
        public ObservableCollection<WeaponOption> AvailableUtility { get; } = new();

        public ICommand RunMacroCommand => _runMacroCommand;
        public Visibility QuickLoadoutVisibility => _quickLoadoutVisible ? Visibility.Visible : Visibility.Collapsed;
        public string QuickLoadoutHotkeyDisplay => FormatHotkey(
            App.Settings.Prop.MacroQuickLoadoutHotkeyModifiers,
            App.Settings.Prop.MacroQuickLoadoutHotkeyVirtualKey);

        public bool QuickLoadoutEnabled
        {
            get => App.Settings.Prop.MacroQuickLoadoutEnabled;
            set
            {
                App.Settings.Prop.MacroQuickLoadoutEnabled = value;
                SaveAndNotify(nameof(QuickLoadoutEnabled));
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                _isRunning = value;
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(RunButtonText));
                _runMacroCommand.NotifyCanExecuteChanged();
                ReconfigureAutomaticActions();
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
                ReconfigureAutomaticActions();
            }
        }

        public string AutomationMasterText => AutomationMasterEnabled
            ? "Automatic actions on — press ] to pause"
            : "Automatic actions paused — press ] to resume";

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

        public WeaponOption? QuickSelectedPrimary
        {
            get => _quickSelectedPrimary;
            set => SetSelection(ref _quickSelectedPrimary, value, nameof(QuickSelectedPrimary), selected => App.Settings.Prop.MacroQuickLoadoutPrimaryWeapon = selected.Name);
        }

        public WeaponOption? QuickSelectedSecondary
        {
            get => _quickSelectedSecondary;
            set => SetSelection(ref _quickSelectedSecondary, value, nameof(QuickSelectedSecondary), selected => App.Settings.Prop.MacroQuickLoadoutSecondaryWeapon = selected.Name);
        }

        public WeaponOption? QuickSelectedMelee
        {
            get => _quickSelectedMelee;
            set => SetSelection(ref _quickSelectedMelee, value, nameof(QuickSelectedMelee), selected => App.Settings.Prop.MacroQuickLoadoutMeleeWeapon = selected.Name);
        }

        public WeaponOption? QuickSelectedUtility
        {
            get => _quickSelectedUtility;
            set => SetSelection(ref _quickSelectedUtility, value, nameof(QuickSelectedUtility), selected => App.Settings.Prop.MacroQuickLoadoutUtilityWeapon = selected.Name);
        }

        public bool QuickRespawn
        {
            get => App.Settings.Prop.MacroQuickRespawn;
            set { App.Settings.Prop.MacroQuickRespawn = value; SaveAndNotify(nameof(QuickRespawn)); ReconfigureAutomaticActions(); }
        }

        public bool AutoUtility
        {
            get => App.Settings.Prop.MacroAutoUtility;
            set { App.Settings.Prop.MacroAutoUtility = value; SaveAndNotify(nameof(AutoUtility)); ReconfigureAutomaticActions(); }
        }

        public bool AutoInspect
        {
            get => App.Settings.Prop.MacroAutoInspect;
            set { App.Settings.Prop.MacroAutoInspect = value; SaveAndNotify(nameof(AutoInspect)); ReconfigureAutomaticActions(); }
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
            _quickSelectedPrimary = FindSaved(AvailablePrimary, App.Settings.Prop.MacroQuickLoadoutPrimaryWeapon);
            _quickSelectedSecondary = FindSaved(AvailableSecondary, App.Settings.Prop.MacroQuickLoadoutSecondaryWeapon);
            _quickSelectedMelee = FindSaved(AvailableMelee, App.Settings.Prop.MacroQuickLoadoutMeleeWeapon);
            _quickSelectedUtility = FindSaved(AvailableUtility, App.Settings.Prop.MacroQuickLoadoutUtilityWeapon);

            _runMacroCommand = new AsyncRelayCommand(RunMacroAsync, () => !IsRunning && AllSelectionsPresent());
            _automationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _automationTimer.Tick += AutomationTimer_Tick;
            _automationTimer.Start();
            if (AutoRejoinEnabled)
                _nextAutoRejoinUtc = DateTime.UtcNow.AddHours(1);
            ReconfigureAutomaticActions();
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

        public void RevealQuickLoadout()
        {
            if (_quickLoadoutVisible)
                return;

            _quickLoadoutVisible = true;
            OnPropertyChanged(nameof(QuickLoadoutVisibility));
        }

        public void SetQuickLoadoutHotkey(int modifiers, int virtualKey)
        {
            App.Settings.Prop.MacroQuickLoadoutHotkeyModifiers = modifiers;
            App.Settings.Prop.MacroQuickLoadoutHotkeyVirtualKey = virtualKey;
            App.Settings.Save();
            _quickHotkeyWasDown = false;
            OnPropertyChanged(nameof(QuickLoadoutHotkeyDisplay));
        }

        public void SetQuickHotkeyCaptureActive(bool active)
        {
            _quickHotkeyCaptureActive = active;
            if (active)
                _quickHotkeyWasDown = false;
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

            switch (removed.Category)
            {
                case MacroWeaponCategory.Primary when ReferenceEquals(_quickSelectedPrimary, removed):
                    SetSelectionOrClear(ref _quickSelectedPrimary, replacement, nameof(QuickSelectedPrimary), value => App.Settings.Prop.MacroQuickLoadoutPrimaryWeapon = value);
                    break;
                case MacroWeaponCategory.Secondary when ReferenceEquals(_quickSelectedSecondary, removed):
                    SetSelectionOrClear(ref _quickSelectedSecondary, replacement, nameof(QuickSelectedSecondary), value => App.Settings.Prop.MacroQuickLoadoutSecondaryWeapon = value);
                    break;
                case MacroWeaponCategory.Melee when ReferenceEquals(_quickSelectedMelee, removed):
                    SetSelectionOrClear(ref _quickSelectedMelee, replacement, nameof(QuickSelectedMelee), value => App.Settings.Prop.MacroQuickLoadoutMeleeWeapon = value);
                    break;
                case MacroWeaponCategory.Utility when ReferenceEquals(_quickSelectedUtility, removed):
                    SetSelectionOrClear(ref _quickSelectedUtility, replacement, nameof(QuickSelectedUtility), value => App.Settings.Prop.MacroQuickLoadoutUtilityWeapon = value);
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
                    await MacroAutomationService.RunLoadoutAsync(selections, _macroCancellation.Token);
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
            bool quickHotkeyDown = IsQuickLoadoutHotkeyDown();
            if (quickHotkeyDown && !_quickHotkeyWasDown && !_quickLoadoutRunning && !IsRunning && AllQuickSelectionsPresent())
                _ = RunQuickLoadoutAsync();
            _quickHotkeyWasDown = quickHotkeyDown;

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

            if (_usingAutoHotkeyActions || !AutomationMasterEnabled || !MacroAutomationService.IsRobloxForeground())
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

        private bool IsQuickLoadoutHotkeyDown()
        {
            if (!QuickLoadoutEnabled || _quickHotkeyCaptureActive)
                return false;

            int key = App.Settings.Prop.MacroQuickLoadoutHotkeyVirtualKey;
            if (key == 0 || !MacroAutomationService.IsKeyDown(key))
                return false;

            int modifiers = App.Settings.Prop.MacroQuickLoadoutHotkeyModifiers;
            if ((modifiers & 1) != 0 && !MacroAutomationService.IsKeyDown(0x12)) return false;
            if ((modifiers & 2) != 0 && !MacroAutomationService.IsKeyDown(0x11)) return false;
            if ((modifiers & 4) != 0 && !MacroAutomationService.IsKeyDown(0x10)) return false;
            if ((modifiers & 8) != 0 && !MacroAutomationService.IsKeyDown(0x5B) && !MacroAutomationService.IsKeyDown(0x5C)) return false;
            return true;
        }

        private async Task RunQuickLoadoutAsync()
        {
            _quickLoadoutRunning = true;
            try
            {
                var selections = new[]
                {
                    CreateSelection(QuickSelectedPrimary!), CreateSelection(QuickSelectedSecondary!),
                    CreateSelection(QuickSelectedMelee!), CreateSelection(QuickSelectedUtility!)
                };
                await MacroAutomationService.RunLoadoutAsync(selections, CancellationToken.None);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("MacroViewModel::RunQuickLoadout", ex);
                Frontend.ShowMessageBox($"SleepStrap could not apply the quick loadout.\n\n{ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                _quickLoadoutRunning = false;
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

        private bool AllQuickSelectionsPresent() => QuickSelectedPrimary is not null && QuickSelectedSecondary is not null && QuickSelectedMelee is not null && QuickSelectedUtility is not null;

        private static string FormatHotkey(int modifiers, int virtualKey)
        {
            var parts = new List<string>();
            if ((modifiers & 2) != 0) parts.Add("Ctrl");
            if ((modifiers & 1) != 0) parts.Add("Alt");
            if ((modifiers & 4) != 0) parts.Add("Shift");
            if ((modifiers & 8) != 0) parts.Add("Win");
            Key key = KeyInterop.KeyFromVirtualKey(virtualKey);
            parts.Add(key == Key.None ? $"0x{virtualKey:X2}" : key.ToString());
            return String.Join(" + ", parts);
        }

        private void SaveAndNotify(string propertyName)
        {
            App.Settings.Save();
            OnPropertyChanged(propertyName);
        }

        private void ReconfigureAutomaticActions()
        {
            _usingAutoHotkeyActions = MacroAutomationService.ConfigureAutomaticActions(
                QuickRespawn,
                AutoUtility,
                AutoInspect,
                AutomationMasterEnabled && !_autoRejoinInProgress);
        }

        private async Task RunHourlyAutoRejoinAsync()
        {
            bool resumeRepeatingLoadout = IsRunning;
            _autoRejoinInProgress = true;
            ReconfigureAutomaticActions();

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
                ReconfigureAutomaticActions();
                if (resumeRepeatingLoadout && AutomationMasterEnabled && AllSelectionsPresent())
                    _ = RunMacroAsync();
            }
        }

        public void Dispose()
        {
            _automationTimer.Stop();
            _automationTimer.Tick -= AutomationTimer_Tick;
            MacroAutomationService.StopAutomaticActions();
        }
    }
}
