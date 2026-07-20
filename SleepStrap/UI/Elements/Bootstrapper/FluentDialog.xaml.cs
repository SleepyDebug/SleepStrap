using SleepStrap.AppData;
using SleepStrap.RobloxInterfaces;
using SleepStrap.UI.Elements.Bootstrapper.Base;
using SleepStrap.UI.ViewModels.Bootstrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;

namespace SleepStrap.UI.Elements.Bootstrapper
{
    /// <summary>
    /// Interaction logic for FluentDialog.xaml
    /// </summary>
    public partial class FluentDialog : IBootstrapperDialog
    {
        private readonly FluentDialogViewModel _viewModel;

        public SleepStrap.Bootstrapper? Bootstrapper { get; set; }

        private bool _isClosing;
        public string VersionText { get; init; } = "None";
        public string ChannelText { get; init; } = "production";

        #region UI Elements
        public string Message
        {
            get => _viewModel.Message;
            set
            {
                _viewModel.Message = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.Message));
            }
        }

        public ProgressBarStyle ProgressStyle
        {
            get => _viewModel.ProgressIndeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            set
            {
                _viewModel.ProgressIndeterminate = (value == ProgressBarStyle.Marquee);
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressIndeterminate));
            }
        }

        public int ProgressMaximum
        {
            get => _viewModel.ProgressMaximum;
            set
            {
                _viewModel.ProgressMaximum = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressMaximum));
            }
        }

        public int ProgressValue
        {
            get => _viewModel.ProgressValue;
            set
            {
                _viewModel.ProgressValue = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressValue));
            }
        }

        public TaskbarItemProgressState TaskbarProgressState
        {
            get => _viewModel.TaskbarProgressState;
            set
            {
                _viewModel.TaskbarProgressState = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.TaskbarProgressState));
            }
        }

        public double TaskbarProgressValue
        {
            get => _viewModel.TaskbarProgressValue;
            set
            {
                _viewModel.TaskbarProgressValue = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.TaskbarProgressValue));
            }
        }

        public bool CancelEnabled
        {
            get => _viewModel.CancelEnabled;
            set
            {
                _viewModel.CancelEnabled = value;

                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelButtonVisibility));
                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelEnabled));
            }
        }
        #endregion

        public FluentDialog(bool aero)
        {
            InitializeComponent();

            string version = Utilities.GetRobloxVersionStr(Bootstrapper?.IsStudioLaunch ?? false);
            string channel = Deployment.Channel;
            _viewModel = new FluentDialogViewModel(this, aero, version, channel);
            DataContext = _viewModel;
            Title = App.Settings.Prop.BootstrapperTitle;
            Icon = App.Settings.Prop.BootstrapperIcon.GetIcon().GetImageSource();

            // setting this to true for mica results in the window being undraggable
            if (aero)
                AllowsTransparency = true;

            VersionText = $"{Strings.Common_Version}: {version}";
            ChannelText = $"{Strings.Common_Channel}: {channel}";
        }

        private void BrandStage_Loaded(object sender, RoutedEventArgs e)
        {
            var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.48 };
            LogoStage.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));
            if (LogoStage.RenderTransform is ScaleTransform logoScale)
            {
                logoScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(620)) { EasingFunction = pop });
                logoScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(620)) { EasingFunction = pop });
            }

            LogoGlow.BeginAnimation(OpacityProperty, new DoubleAnimation(0.14, 0.38, TimeSpan.FromMilliseconds(1050))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });

            int index = 0;
            foreach (TextBlock letter in BrandLetters.Children.OfType<TextBlock>())
            {
                letter.FontSize = 31;
                letter.FontWeight = FontWeights.SemiBold;
                letter.Foreground = System.Windows.Media.Brushes.White;
                letter.Opacity = 0;
                letter.RenderTransformOrigin = new System.Windows.Point(0.5, 0.72);
                var scale = new ScaleTransform(0.62, 0.62);
                var rise = new TranslateTransform(0, 20);
                var transforms = new TransformGroup();
                transforms.Children.Add(scale);
                transforms.Children.Add(rise);
                letter.RenderTransform = transforms;

                TimeSpan delay = TimeSpan.FromMilliseconds(190 + index * 72);
                letter.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190)) { BeginTime = delay });
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.62, 1, TimeSpan.FromMilliseconds(420)) { BeginTime = delay, EasingFunction = pop });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.62, 1, TimeSpan.FromMilliseconds(420)) { BeginTime = delay, EasingFunction = pop });
                rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(420)) { BeginTime = delay, EasingFunction = pop });
                index++;
            }

            if (BrandShine.RenderTransform is TranslateTransform shine)
            {
                shine.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-70, 285, TimeSpan.FromMilliseconds(720))
                {
                    BeginTime = TimeSpan.FromMilliseconds(1050),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                });
                BrandShine.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames
                {
                    BeginTime = TimeSpan.FromMilliseconds(1050),
                    KeyFrames =
                    {
                        new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)),
                        new LinearDoubleKeyFrame(0.72, KeyTime.FromPercent(0.18)),
                        new LinearDoubleKeyFrame(0.72, KeyTime.FromPercent(0.72)),
                        new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1))
                    },
                    Duration = TimeSpan.FromMilliseconds(720)
                });
            }
        }

        private void UiWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_isClosing)
                Bootstrapper?.Cancel();
        }

        #region IBootstrapperDialog Methods
        public void ShowBootstrapper() => this.ShowDialog();

        public void CloseBootstrapper()
        {
            _isClosing = true;
            Dispatcher.BeginInvoke(this.Close);
        }

        public void ShowSuccess(string message, Action? callback) => BaseFunctions.ShowSuccess(message, callback);
        #endregion
    }
}
