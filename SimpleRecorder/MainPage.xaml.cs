using CaptureUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SimpleRecorder.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.ApplicationModel.ExtendedExecution.Foreground;
using Windows.Devices.Enumeration;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace SimpleRecorder
{
    public sealed partial class MainPage : Page
    {
        private IDirect3DDevice _screenDevice;
        private Encoder _screenEncoder;

        private MediaCapture _webcamMediaCapture;
        private LowLagMediaRecording _webcamMediaRecording;

        private StorageFolder _storageFolder = null;
        private GraphicsCaptureItem _item = null;

        public MainPage()
        {
            InitializeComponent();

            if (!GraphicsCaptureSession.IsSupported())
            {
                IsEnabled = false;

                var dialog = new MessageDialog(
                    "Screen capture is not supported on this device for this release of Windows!",
                    "Screen capture unsupported");

                var ignored = dialog.ShowAsync();
                return;
            }

            // initialize screen recording
            _screenDevice = Direct3D11Helpers.CreateDevice();

            // connect to the powerpoint app service
            App.AppServiceConnected += MainPage_AppServiceConnected;
        }

        private void LoadSettings()
        {
            var settings = GetCachedSettings();

            var names = new List<string>();
            names.Add(nameof(VideoEncodingQuality.HD1080p));
            names.Add(nameof(VideoEncodingQuality.HD720p));
            names.Add(nameof(VideoEncodingQuality.Uhd2160p));
            names.Add(nameof(VideoEncodingQuality.Uhd4320p));
            QualityComboBox.ItemsSource = names;
            QualityComboBox.SelectedIndex = names.IndexOf(settings.Quality.ToString());

            var frameRates = new List<string> { "15fps", "30fps", "60fps" };
            FrameRateComboBox.ItemsSource = frameRates;
            FrameRateComboBox.SelectedIndex = frameRates.IndexOf($"{settings.FrameRate}fps");

            UseCaptureItemSizeCheckBox.IsChecked = settings.UseSourceSize;
            AdaptBitrateCheckBox.IsChecked = settings.AdaptBitrate;

            WebcamDeviceComboBox.SelectedItem = WebcamDeviceComboBox.Items.Where(x => (x as ComboBoxItem).Tag.ToString() == settings.WebcamDeviceId).FirstOrDefault();
        }

        private void PopulateStreamPropertiesUI(MediaStreamType streamType, ComboBox comboBox, bool showFrameRate = true)
        {
            // query all properties of the specified stream type 
            IEnumerable<StreamPropertiesHelper> allStreamProperties =
                _webcamMediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(streamType).Select(x => new StreamPropertiesHelper(x));

            // order them by resolution then frame rate
            allStreamProperties = allStreamProperties.OrderByDescending(x => x.Height * x.Width).ThenByDescending(x => x.FrameRate);

            // populate the combo box with the entries
            foreach (var property in allStreamProperties)
            {
                ComboBoxItem comboBoxItem = new ComboBoxItem();
                comboBoxItem.Content = property.GetFriendlyName(showFrameRate);
                comboBoxItem.Tag = property;
                comboBox.Items.Add(comboBoxItem);
            }

            var settings = GetCachedSettings();
            comboBox.SelectedItem = WebcamComboBox.Items.Where(x => (x as ComboBoxItem).Content.ToString() == settings.WebcamQuality).FirstOrDefault();
        }

        private async Task InitWebcamAsync(string deviceId)
        {
            if (_webcamMediaCapture != null)
            {
                await _webcamMediaCapture.StopPreviewAsync();
            }

            _webcamMediaCapture = new MediaCapture();
            _webcamMediaCapture.RecordLimitationExceeded += CaptureManager_RecordLimitationExceeded;

            await _webcamMediaCapture.InitializeAsync(new MediaCaptureInitializationSettings()
            {
                VideoDeviceId = deviceId
            });

            WebcamPreview.Source = _webcamMediaCapture;
            await _webcamMediaCapture.StartPreviewAsync();
        }

        private async void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            var button = (ToggleButton)sender;

            // get storage folder
            if (_storageFolder == null)
            {
                var msg = new MessageDialog("Please choose a folder first...");
                await msg.ShowAsync();

                button.IsChecked = false;
                return;
            }

            var requestSuspensionExtension = new ExtendedExecutionForegroundSession();
            requestSuspensionExtension.Reason = ExtendedExecutionForegroundReason.Unspecified;
            var requestExtensionResult = await requestSuspensionExtension.RequestExtensionAsync();

            // get storage files
            var time = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss_");

            var screenFile = await _storageFolder.CreateFileAsync(time + "slides.mp4");
            var webcamFile = await _storageFolder.CreateFileAsync(time + "talkinghead.mp4");
            var jsonFile = await _storageFolder.CreateFileAsync(time + "meta.json");

            // get encoder properties
            var frameRate = uint.Parse(((string)FrameRateComboBox.SelectedItem).Replace("fps", ""));
            var quality = (VideoEncodingQuality)Enum.Parse(typeof(VideoEncodingQuality), (string)QualityComboBox.SelectedItem, false);
            var useSourceSize = UseCaptureItemSizeCheckBox.IsChecked.Value;

            var temp = MediaEncodingProfile.CreateMp4(quality);
            uint bitrate = 2500000; // temp.Video.Bitrate; // 18 000 000
            var width = temp.Video.Width;
            var height = temp.Video.Height;

            // get capture item
            var picker = new GraphicsCapturePicker();
            _item = await picker.PickSingleItemAsync();
            if (_item == null)
            {
                button.IsChecked = false;
                return;
            }

            // use the capture item's size for the encoding if desired
            if (useSourceSize)
            {
                width = (uint)_item.Size.Width;
                height = (uint)_item.Size.Height;

                // even if we're using the capture item's real size,
                // we still want to make sure the numbers are even.
                width = EnsureEven(width);
                height = EnsureEven(height);
            }

            // tell the user we've started recording
            var originalBrush = MainTextBlock.Foreground;
            MainTextBlock.Foreground = new SolidColorBrush(Colors.Red);
            MainProgressBar.IsIndeterminate = true;

            button.IsEnabled = false;

            MainTextBlock.Text = "3 ...";
            await Task.Delay(1000);

            MainTextBlock.Text = "2 ...";
            await Task.Delay(1000);

            MainTextBlock.Text = "1 ...";
            await Task.Delay(1000);

            button.IsEnabled = true;

            MainTextBlock.Text = "● rec";

            try
            {
                // start webcam recording
                MediaEncodingProfile webcamEncodingProfile = null;

                if (AdaptBitrateCheckBox.IsChecked.Value)
                {
                    var selectedItem = WebcamComboBox.SelectedItem as ComboBoxItem;
                    var encodingProperties = (selectedItem.Tag as StreamPropertiesHelper);

                    if (encodingProperties.Height > 720)
                    {
                        webcamEncodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
                        webcamEncodingProfile.Video.Bitrate = 8000000;
                    }
                    else if (encodingProperties.Height > 480)
                    {
                        webcamEncodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
                        webcamEncodingProfile.Video.Bitrate = 5000000;
                    }
                    else
                    {
                        webcamEncodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
                    }
                }
                else
                {
                    webcamEncodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
                }

                _webcamMediaRecording = await _webcamMediaCapture.PrepareLowLagRecordToStorageFileAsync(webcamEncodingProfile, webcamFile);

                // kick off the screen encoding parallel
                using (var stream = await screenFile.OpenAsync(FileAccessMode.ReadWrite))
                using (_screenEncoder = new Encoder(_screenDevice, _item))
                {
                    // webcam recording
                    if (_webcamMediaCapture != null)
                    {
                        await _webcamMediaRecording.StartAsync();
                    }

                    // screen recording
                    await _screenEncoder.EncodeAsync(
                        stream,
                        width, height, bitrate,
                        frameRate);
                }

                MainTextBlock.Foreground = originalBrush;

                // user has finished recording, so stop webcam recording
                await _webcamMediaRecording.StopAsync();
                await _webcamMediaRecording.FinishAsync();
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog(
                    $"Uh-oh! Something went wrong!\n0x{ex.HResult:X8} - {ex.Message}",
                    "Recording failed");

                await dialog.ShowAsync();

                button.IsChecked = false;
                MainTextBlock.Text = "failure";
                MainTextBlock.Foreground = originalBrush;
                MainProgressBar.IsIndeterminate = false;

                _item = null;
                if (_webcamMediaRecording != null)
                {
                    await _webcamMediaRecording.StopAsync();
                    await _webcamMediaRecording.FinishAsync();
                }

                return;
            }

            // at this point the encoding has finished
            MainTextBlock.Text = "saving...";

            // save slide markers
            var recording = new Recording()
            {
                Slides = _screenEncoder.GetTimestamps()
            };

            var settings = new JsonSerializerSettings();
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();

            var json = JsonConvert.SerializeObject(recording, Formatting.Indented, settings);
            await FileIO.WriteTextAsync(jsonFile, json);

            // add metadata
            var recordingMetadataDialog = new RecordingMetadataDialog();
            var recordingMetadataDialogResult = await recordingMetadataDialog.ShowAsync();

            if (recordingMetadataDialogResult == ContentDialogResult.Primary)
            {
                recording.Description = recordingMetadataDialog.LectureTitle;

                if (recordingMetadataDialog.LectureDate.HasValue)
                {
                    recording.LectureDate = recordingMetadataDialog.LectureDate.Value.DateTime;
                }
            }
            else
            {
                recording.Description = null;
                recording.LectureDate = DateTime.Now;
            }

            json = JsonConvert.SerializeObject(recording, Formatting.Indented, settings);
            await FileIO.WriteTextAsync(jsonFile, json);

            // tell the user we're done
            button.IsChecked = false;
            MainTextBlock.Text = "done";
            MainProgressBar.IsIndeterminate = false;

            requestSuspensionExtension.Dispose();
        }

        private void CaptureManager_RecordLimitationExceeded(MediaCapture sender)
        {
            // stop the recording
            _screenEncoder?.Dispose();

            MainTextBlock.Text = "Limit reached (3h)";
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            // If the encoder is doing stuff, tell it to stop
            _screenEncoder?.Dispose();
        }

        private uint EnsureEven(uint number)
        {
            return (number % 2 == 0) ? number : number + 1;
        }

        private AppSettings GetCurrentSettings()
        {
            var quality = ParseEnumValue<VideoEncodingQuality>((string)QualityComboBox.SelectedItem);
            var frameRate = uint.Parse(((string)FrameRateComboBox.SelectedItem).Replace("fps", ""));
            var useSourceSize = UseCaptureItemSizeCheckBox.IsChecked.Value;
            var adaptBitrate = AdaptBitrateCheckBox.IsChecked.Value;
            var webcamQuality = (WebcamComboBox.SelectedItem as ComboBoxItem).Content.ToString();

            return new AppSettings
            {
                Quality = quality,
                FrameRate = frameRate,
                UseSourceSize = useSourceSize,
                WebcamDeviceId = (WebcamDeviceComboBox.SelectedItem as ComboBoxItem).Tag.ToString(),
                WebcamQuality = webcamQuality,
                AdaptBitrate = adaptBitrate
            };
        }

        private AppSettings GetCachedSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var result = new AppSettings
            {
                Quality = VideoEncodingQuality.HD1080p,
                FrameRate = 60,
                UseSourceSize = true,
                AdaptBitrate = true
            };
            if (localSettings.Values.TryGetValue(nameof(AppSettings.Quality), out var quality))
            {
                result.Quality = ParseEnumValue<VideoEncodingQuality>((string)quality);
            }
            if (localSettings.Values.TryGetValue(nameof(AppSettings.FrameRate), out var frameRate))
            {
                result.FrameRate = (uint)frameRate;
            }
            if (localSettings.Values.TryGetValue(nameof(AppSettings.UseSourceSize), out var useSourceSize))
            {
                result.UseSourceSize = (bool)useSourceSize;
            }
            if (localSettings.Values.TryGetValue(nameof(AppSettings.AdaptBitrate), out var adaptBitrate))
            {
                result.AdaptBitrate = (bool)adaptBitrate;
            }
            if (localSettings.Values.TryGetValue(nameof(AppSettings.WebcamQuality), out var webcamQuality))
            {
                result.WebcamQuality = webcamQuality as string;
            }
            if (localSettings.Values.TryGetValue(nameof(AppSettings.WebcamDeviceId), out var webcamDeviceId))
            {
                result.WebcamDeviceId = webcamDeviceId as string;
            }
            return result;
        }

        public void CacheCurrentSettings()
        {
            var settings = GetCurrentSettings();
            CacheSettings(settings);
        }

        private static void CacheSettings(AppSettings settings)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[nameof(AppSettings.Quality)] = settings.Quality.ToString();
            localSettings.Values[nameof(AppSettings.FrameRate)] = settings.FrameRate;
            localSettings.Values[nameof(AppSettings.UseSourceSize)] = settings.UseSourceSize;
            localSettings.Values[nameof(AppSettings.AdaptBitrate)] = settings.AdaptBitrate;
            localSettings.Values[nameof(AppSettings.WebcamDeviceId)] = settings.WebcamDeviceId;
            localSettings.Values[nameof(AppSettings.WebcamQuality)] = settings.WebcamQuality;
        }

        private static T ParseEnumValue<T>(string input)
        {
            return (T)Enum.Parse(typeof(T), input, false);
        }

        private async Task InitWebcamDevicesAsync()
        {
            // Finds all video capture devices
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            foreach (var device in devices)
            {
                var comboBoxItem = new ComboBoxItem();
                comboBoxItem.Content = device.Name;
                comboBoxItem.Tag = device.Id;
                WebcamDeviceComboBox.Items.Add(comboBoxItem);
            }
        }

        private async void WebcamComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = (sender as ComboBox).SelectedItem as ComboBoxItem;
            var encodingProperties = (selectedItem.Tag as StreamPropertiesHelper).EncodingProperties;
            await _webcamMediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoRecord, encodingProperties);


            // exposure control
            var exposureControl = _webcamMediaCapture.VideoDeviceController.ExposureControl;

            if (exposureControl.Supported)
            {
                ExposureAutoCheckBox.Visibility = Visibility.Visible;
                ExposureSlider.Visibility = Visibility.Visible;

                ExposureAutoCheckBox.IsChecked = exposureControl.Auto;

                ExposureSlider.Minimum = exposureControl.Min.Ticks;
                ExposureSlider.Maximum = exposureControl.Max.Ticks;
                ExposureSlider.StepFrequency = exposureControl.Step.Ticks;

                ExposureSlider.ValueChanged -= ExposureSlider_ValueChanged;
                var value = exposureControl.Value;
                ExposureSlider.Value = value.Ticks;
                ExposureSlider.ValueChanged += ExposureSlider_ValueChanged;
            }
            else
            {
                ExposureAutoCheckBox.Visibility = Visibility.Collapsed;
                ExposureSlider.Visibility = Visibility.Collapsed;
            }

            // exposure compensation control
            var exposureCompensationControl = _webcamMediaCapture.VideoDeviceController.ExposureCompensationControl;

            if (exposureCompensationControl.Supported)
            {
                EvSlider.Visibility = Visibility.Visible;
                EvSlider.Minimum = exposureCompensationControl.Min;
                EvSlider.Maximum = exposureCompensationControl.Max;
                EvSlider.StepFrequency = exposureCompensationControl.Step;

                EvSlider.ValueChanged -= EvSlider_ValueChanged;
                EvSlider.Value = exposureCompensationControl.Value;
                EvSlider.ValueChanged += EvSlider_ValueChanged;
            }
            else
            {
                EvSlider.Visibility = Visibility.Collapsed;
            }

            // white balance control
            var whiteBalanceControl = _webcamMediaCapture.VideoDeviceController.WhiteBalanceControl;

            if (whiteBalanceControl.Supported)
            {
                WbSlider.Visibility = Visibility.Visible;
                WbComboBox.Visibility = Visibility.Visible;

                if (WbComboBox.ItemsSource == null)
                {
                    WbComboBox.ItemsSource = Enum.GetValues(typeof(ColorTemperaturePreset)).Cast<ColorTemperaturePreset>();
                }

                WbComboBox.SelectedItem = whiteBalanceControl.Preset;

                if (whiteBalanceControl.Max - whiteBalanceControl.Min > whiteBalanceControl.Step)
                {

                    WbSlider.Minimum = whiteBalanceControl.Min;
                    WbSlider.Maximum = whiteBalanceControl.Max;
                    WbSlider.StepFrequency = whiteBalanceControl.Step;

                    WbSlider.ValueChanged -= WbSlider_ValueChanged;
                    WbSlider.Value = whiteBalanceControl.Value;
                    WbSlider.ValueChanged += WbSlider_ValueChanged;
                }
                else
                {
                    WbSlider.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                WbSlider.Visibility = Visibility.Collapsed;
                WbComboBox.Visibility = Visibility.Collapsed;
            }
        }

        private async void WebcamDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = WebcamDeviceComboBox.SelectedItem as ComboBoxItem;

            await InitWebcamAsync(selectedItem.Tag.ToString());
            PopulateStreamPropertiesUI(MediaStreamType.VideoRecord, WebcamComboBox, true);
        }

        private void MainPage_AppServiceConnected(object sender, EventArgs e)
        {
            App.Connection.RequestReceived += AppService_RequestReceived;
        }

        private async void AppService_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var msg = args.Request.Message;
            var result = msg["TYPE"].ToString();

            if (result == "SlideChanged")
            {
                _screenEncoder?.AddCurrentTimestamp();
            }
            else if (result == "Status")
            {
                if (msg["STATUS"].ToString() == "CONNECTED")
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        PowerPointGreen.Visibility = Visibility.Visible;
                        PowerPointRed.Visibility = Visibility.Collapsed;
                    });
                }
                else
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        PowerPointGreen.Visibility = Visibility.Collapsed;
                        PowerPointRed.Visibility = Visibility.Visible;
                    });
                }
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            await InitWebcamDevicesAsync();

            LoadSettings();
        }

        private async void BtnFolderPicker_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            folderPicker.FileTypeFilter.Add("*");

            _storageFolder = await folderPicker.PickSingleFolderAsync();
            FolderName.Text = _storageFolder.Path;
        }

        private async void ExposureSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var value = TimeSpan.FromTicks((long)(sender as Slider).Value);
            await _webcamMediaCapture.VideoDeviceController.ExposureControl.SetValueAsync(value);
        }

        private async void ExposureCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            var autoExposure = ((sender as CheckBox).IsChecked == true);
            await _webcamMediaCapture.VideoDeviceController.ExposureControl.SetAutoAsync(autoExposure);
        }

        private async void EvSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var value = (sender as Slider).Value;
            await _webcamMediaCapture.VideoDeviceController.ExposureCompensationControl.SetValueAsync((float)value);
        }

        private async void WbComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = (ColorTemperaturePreset)WbComboBox.SelectedItem;
            WbSlider.IsEnabled = (selected == ColorTemperaturePreset.Manual);
            await _webcamMediaCapture.VideoDeviceController.WhiteBalanceControl.SetPresetAsync(selected);

        }

        private async void WbSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            var value = (sender as Slider).Value;
            await _webcamMediaCapture.VideoDeviceController.WhiteBalanceControl.SetValueAsync((uint)value);
        }
    }
}
