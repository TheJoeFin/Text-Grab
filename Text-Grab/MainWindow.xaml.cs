using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Capture;
using Text_Grab.Util;
using Text_Grab.Windows.Other;
using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.System.UserProfile;
using Windows.UI.Xaml.Media;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// This is the helper class which brings the screen area selection.
        /// </summary>
        private readonly RegionSelection _regionSelection = new RegionSelection();

        /// <summary>
        /// Deals with all screen capture methods.
        /// </summary>
        private ICapture _capture;

        public MainWindow()
        {
            InitializeComponent();
        }

        public double WindowResizeZone { get; set; } = 32f;

        public List<string> InstalledLanguages => GlobalizationPreferences.Languages.ToList();

        private async void ScreenshotBTN_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Point windowPoint = screenshotGrid.PointToScreen(new System.Windows.Point(0, 0));
            Rectangle rect = new Rectangle((int)windowPoint.X + 2, (int)windowPoint.Y + 2, (int)screenshotGrid.ActualWidth - 4, (int)screenshotGrid.ActualHeight - 4);
            Bitmap bmp = new Bitmap(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            // ScreenshotImage.Source = BitmapToImageSource(bmp);

            string ocrText = await ExtractText(bmp, InstalledLanguages.FirstOrDefault());
            ocrText.Trim();

            System.Windows.Clipboard.SetText(ocrText);

            System.Windows.MessageBox.Show(ocrText, "OCR Text", MessageBoxButton.OK);            
        }

        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        public async Task<string> ExtractText(Bitmap bmp, string languageCode)
        {

            if (!GlobalizationPreferences.Languages.Contains(languageCode))
                throw new ArgumentOutOfRangeException($"{languageCode} is not installed.");

            StringBuilder text = new StringBuilder();

            await using (MemoryStream memory = new MemoryStream())
            {
                bmp.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0; 
                var bmpDecoder = await BitmapDecoder.CreateAsync(memory.AsRandomAccessStream());
                var softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

                var ocrEngine = OcrEngine.TryCreateFromLanguage(new Language(languageCode));
                var ocrResult = await ocrEngine.RecognizeAsync(softwareBmp);

                foreach (var line in ocrResult.Lines) text.AppendLine(line.Text);
            }

            return text.ToString();
        }

        public async Task<string> ExtractText(string imagePath, string languageCode)
        {

          if (!GlobalizationPreferences.Languages.Contains(languageCode))
                throw new ArgumentOutOfRangeException($"{languageCode} is not installed.");

            StringBuilder text = new StringBuilder();

            await using (var fileStream = File.OpenRead(imagePath))
            {
                var bmpDecoder = await BitmapDecoder.CreateAsync(fileStream.AsRandomAccessStream());
                var softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

                var ocrEngine = OcrEngine.TryCreateFromLanguage(new Language(languageCode));
                var ocrResult = await ocrEngine.RecognizeAsync(softwareBmp);

                foreach (var line in ocrResult.Lines) text.AppendLine(line.Text);
            }

            return text.ToString();
        }

        private System.Windows.Point clickedPoint;
        private bool dragging = false;

        private void mainGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            clickedPoint = e.GetPosition(this);
            dragging = true;
        }

        private void mainGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (dragging == false)
                return;

            // var pos = e.GetPosition(this);
            // var pos2 = new System.Windows.Point(pos.X - clickedPoint.X, pos.Y - clickedPoint.Y);
            // 
            // this.Left += pos2.X;
            // this.Top += pos2.Y;
        }

        private void mainGrid_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            dragging = false;
        }

        private void Rectangle_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            dragging = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            await PickRegion(ModeType.Region);
        }

        private void ForceUpdate()
        {
            InvalidateMeasure();
            InvalidateArrange();
            Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            Arrange(new Rect(DesiredSize));
        }

        private async Task PickRegion(ModeType mode, bool quickSelection = false)
        {
            _regionSelection.Hide();
            Hide();

            var previousMode = UserSettings.All.RecorderModeIndex;

            var selection = await RegionSelectHelper.Select(mode, _viewModel.Region, _regionSelection.Monitor, quickSelection);

            ForceUpdate();

            if (selection.Region != Rect.Empty)
            {
                // WidthIntegerBox.IgnoreValueChanged = true;
                // HeightIntegerBox.IgnoreValueChanged = true;
                // 
                // UserSettings.All.SelectedRegionScale = selection.Monitor.Scale;
                // UserSettings.All.SelectedRegion = _viewModel.Region = selection.Region;
                // 
                // WidthIntegerBox.IgnoreValueChanged = false;
                // HeightIntegerBox.IgnoreValueChanged = false;
                // 
                // DisplaySelection(mode, selection.Monitor);
                // MoveCommandPanel();
            }
            else
            {
                UserSettings.All.RecorderModeIndex = previousMode;
                if (_viewModel.Region.IsEmpty)
                {
                    if (_regionSelection.IsVisible)
                        _regionSelection.Hide();
                }
                else
                {
                    _regionSelection.Select(mode, _viewModel.Region, null ?? _viewModel.CurrentMonitor);
                }

                DisplaySize();
                DetectMonitorChanges();
            }

            Show();
        }

        private async void DetectMonitorChanges(bool detectCurrent = false)
        {
            if (detectCurrent)
            {
                var interop = new System.Windows.Interop.WindowInteropHelper(_regionSelection);
                var current = Screen.FromHandle(interop.Handle);

                _viewModel.CurrentMonitor = _viewModel.Monitors.FirstOrDefault(f => f.Name == current.DeviceName);
                //_viewModel.CurrentMonitor = Monitor.MostIntersected(_viewModel.Monitors, _viewModel.Region.Scale(_viewModel.CurrentMonitor.Scale)) ?? _viewModel.CurrentMonitor;
            }

            if (_viewModel.CurrentMonitor != null && _viewModel.CurrentMonitor.Handle != _viewModel.PreviousMonitor?.Handle)
            {
                if (_viewModel.PreviousMonitor != null && Stage == Stage.Recording && Project?.Any == true)
                {
                    Pause();

                    _capture.DeviceName = _viewModel.CurrentMonitor.Name;
                    _capture?.ResetConfiguration();

                    await Record();
                }

                _viewModel.PreviousMonitor = _viewModel.CurrentMonitor;
            }
        }

        private void DisplaySize()
        {
            switch (UserSettings.All.RecorderModeIndex)
            {
                case (int)ModeType.Window:
                    {
                        SizeTextBlock.ToolTip = null;

                        if (_viewModel.Region.IsEmpty)
                        {
                            SizeTextBlock.SetResourceReference(TextBlock.TextProperty, "S.Recorder.Window.Select");

                            SizeGrid.Visibility = Visibility.Collapsed;
                            SizeTextBlock.Visibility = Visibility.Visible;
                            return;
                        }

                        SizeGrid.Visibility = Visibility.Visible;
                        SizeTextBlock.Visibility = Visibility.Collapsed;
                        return;
                    }
                case (int)ModeType.Fullscreen:
                    {
                        SizeGrid.Visibility = Visibility.Collapsed;
                        SizeTextBlock.Visibility = Visibility.Visible;

                        if (_viewModel.CurrentMonitor == null)
                        {
                            SizeTextBlock.ToolTip = null;
                            SizeTextBlock.SetResourceReference(TextBlock.TextProperty, "S.Recorder.Screen.Select");
                            return;
                        }

                        SizeTextBlock.Text = _viewModel.CurrentMonitor.FriendlyName;
                        SizeTextBlock.ToolTip =
                            LocalizationHelper.GetWithFormat("S.Recorder.Screen.Name.Info1", "Graphics adapter: {0}", _viewModel.CurrentMonitor.AdapterName) +
                            Environment.NewLine +
                            LocalizationHelper.GetWithFormat("S.Recorder.Screen.Name.Info2", "Resolution: {0} x {1}", _viewModel.CurrentMonitor.Bounds.Width, _viewModel.CurrentMonitor.Bounds.Height) +
                            (Math.Abs(_viewModel.CurrentMonitor.Scale - 1) > 0.001 ? Environment.NewLine +
                            LocalizationHelper.GetWithFormat("S.Recorder.Screen.Name.Info3", "Native resolution: {0} x {1}", _viewModel.CurrentMonitor.NativeBounds.Width, _viewModel.CurrentMonitor.NativeBounds.Height) : "") +
                            Environment.NewLine +
                            LocalizationHelper.GetWithFormat("S.Recorder.Screen.Name.Info4", "DPI: {0} ({1:0.##}%)", _viewModel.CurrentMonitor.Dpi, _viewModel.CurrentMonitor.Scale * 100d);

                        return;
                    }
                default:
                    {
                        SizeTextBlock.ToolTip = null;

                        if (_viewModel.Region.IsEmpty)
                        {
                            SizeTextBlock.SetResourceReference(TextBlock.TextProperty, "S.Recorder.Area.Select");

                            SizeGrid.Visibility = Visibility.Collapsed;
                            SizeTextBlock.Visibility = Visibility.Visible;
                            return;
                        }

                        SizeGrid.Visibility = Visibility.Visible;
                        SizeTextBlock.Visibility = Visibility.Collapsed;
                        return;
                    }
            }
        }

        internal async Task Record()
        {
            try
            {
                switch (Stage)
                {
                    case Stage.Stopped:

                        #region If region not yet selected

                        if (_viewModel.Region.IsEmpty)
                        {
                            await PickRegion((ModeType)ReselectSplitButton.SelectedIndex, true);

                            if (_viewModel.Region.IsEmpty)
                                return;
                        }

                        #endregion

                        #region If interaction mode

                        if (UserSettings.All.CaptureFrequency == CaptureFrequency.Interaction)
                        {
                            Stage = Stage.Recording;
                            SetTaskbarButtonOverlay();
                            return;
                        }

                        #endregion

                        #region To record

                        _captureTimer = new Timer { Interval = GetCaptureInterval() };

                        Project = new ProjectInfo().CreateProjectFolder(ProjectByType.ScreenRecorder);

                        _keyList.Clear();
                        FrameCount = 0;

                        await PrepareNewCapture();

                        FrequencyIntegerUpDown.IsEnabled = false;

                        _regionSelection.HideGuidelines();
                        IsRecording = true;
                        Topmost = true;

                        UnregisterEvents();

                        //Detects a possible intersection of capture region and capture controls.
                        var isIntersecting = IsRegionIntersected();

                        if (isIntersecting)
                        {
                            Topmost = false;
                            Splash.Display(LocalizationHelper.GetWithFormat("S.Recorder.Splash.Title", "Press {0} to stop the recording", Util.Native.GetSelectKeyText(UserSettings.All.StopShortcut, UserSettings.All.StopModifiers)),
                                LocalizationHelper.GetWithFormat("S.Recorder.Splash.Subtitle", "The recorder window will be minimized,&#10;restore it or press {0} to pause the capture", Util.Native.GetSelectKeyText(UserSettings.All.StartPauseShortcut, UserSettings.All.StartPauseModifiers)));
                            Splash.SetTime(-UserSettings.All.PreStartValue);
                        }

                        #region Start

                        if (isIntersecting || UserSettings.All.UsePreStart)
                        {
                            Stage = Stage.PreStarting;

                            Title = "ScreenToGif - " + LocalizationHelper.Get("S.Recorder.PreStarting");
                            DisplayTimer.SetElapsed(-UserSettings.All.PreStartValue);

                            _preStartCount = UserSettings.All.PreStartValue - 1;
                            _preStartTimer.Start();
                            return;
                        }

                        DisplayTimer.Start();
                        FrameRate.Start(HasFixedDelay(), GetFixedDelay());

                        if (UserSettings.All.ShowCursor)
                        {
                            #region Show the cursor

                            if (UserSettings.All.AsyncRecording)
                                _captureTimer.Tick += CursorAsync_Elapsed;
                            else
                                _captureTimer.Tick += Cursor_Elapsed;

                            _captureTimer.Start();

                            //Manually capture the first frame on timelapse mode.
                            if (UserSettings.All.CaptureFrequency == CaptureFrequency.PerMinute || UserSettings.All.CaptureFrequency == CaptureFrequency.PerHour)
                            {
                                if (UserSettings.All.AsyncRecording)
                                    CursorAsync_Elapsed(null, null);
                                else
                                    Cursor_Elapsed(null, null);
                            }

                            Stage = Stage.Recording;
                            SetTaskbarButtonOverlay();

                            #endregion

                            return;
                        }

                        #region Don't show the cursor

                        if (UserSettings.All.AsyncRecording)
                            _captureTimer.Tick += NormalAsync_Elapsed;
                        else
                            _captureTimer.Tick += Normal_Elapsed;

                        _captureTimer.Start();

                        //Manually capture the first frame on timelapse mode.
                        if (UserSettings.All.CaptureFrequency == CaptureFrequency.PerMinute || UserSettings.All.CaptureFrequency == CaptureFrequency.PerHour)
                        {
                            if (UserSettings.All.AsyncRecording)
                                NormalAsync_Elapsed(null, null);
                            else
                                Normal_Elapsed(null, null);
                        }

                        Stage = Stage.Recording;
                        SetTaskbarButtonOverlay();

                        #endregion

                        break;

                    #endregion

                    #endregion

                    case Stage.Paused:

                        #region To record again

                        Stage = Stage.Recording;
                        Title = "ScreenToGif";
                        _regionSelection.HideGuidelines();
                        SetTaskbarButtonOverlay();

                        //If it's interaction mode, the capture is done via Snap().
                        if (UserSettings.All.CaptureFrequency == CaptureFrequency.Interaction)
                            return;

                        FrequencyIntegerUpDown.IsEnabled = false;

                        //Detects a possible intersection of capture region and capture controls.
                        if (IsRegionIntersected())
                            WindowState = WindowState.Minimized;

                        DisplayTimer.Start();
                        FrameRate.Start(HasFixedDelay(), GetFixedDelay());

                        _captureTimer.Interval = GetCaptureInterval();
                        _captureTimer.Start();
                        break;
                    default:
                        break;

                        #endregion
                }
            }
            catch (Exception e)
            {
                LogWriter.Log(e, "Impossible to start the recording.");
                ErrorDialog.Ok(Title, LocalizationHelper.Get("S.Recorder.Warning.StartPauseNotPossible"), e.Message, e);
            }
            finally
            {
                //Wait a bit, then refresh the commands. Some of the commands are dependant of the FrameCount property.
                await Task.Delay(TimeSpan.FromMilliseconds(GetCaptureInterval() + 200));

                CommandManager.InvalidateRequerySuggested();
            }
        }

    }

    public enum ModeType
    {
        Region,
        Window,
        Fullscreen
    }

    public class DpiDecorator : Decorator
    {
        public DpiDecorator()
        {
            this.Loaded += (s, e) =>
            {
                Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
                ScaleTransform dpiTransform = new ScaleTransform(1 / m.M11, 1 / m.M22);
                if (dpiTransform.CanFreeze)
                    dpiTransform.Freeze();
                this.LayoutTransform = dpiTransform;
            };
        }
    }
}
