﻿using CaptureUtils;
using Newtonsoft.Json;
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

            var requestSuspensionExtension = new ExtendedExecutionForegroundSession();
            requestSuspensionExtension.Reason = ExtendedExecutionForegroundReason.Unspecified;
            var requestExtensionResult = await requestSuspensionExtension.RequestExtensionAsync();

            // get storage folder
            var folderPicker = new FolderPicker();
            folderPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();

            // get storage files
            var screenFile = await folder.CreateFileAsync("slides.mp4");
            var webcamFile = await folder.CreateFileAsync("talkinghead.mp4");
            var jsonFile = await folder.CreateFileAsync("meta.json");

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
            var item = await picker.PickSingleItemAsync();
            if (item == null)
            {
                button.IsChecked = false;
                return;
            }

            // use the capture item's size for the encoding if desired
            if (useSourceSize)
            {
                width = (uint)item.Size.Width;
                height = (uint)item.Size.Height;

                // even if we're using the capture item's real size,
                // we still want to make sure the numbers are even.
                width = EnsureEven(width);
                height = EnsureEven(height);
            }

            // tell the user we've started recording
            MainTextBlock.Text = "● rec";

            var originalBrush = MainTextBlock.Foreground;
            MainTextBlock.Foreground = new SolidColorBrush(Colors.Red);

            MainProgressBar.IsIndeterminate = true;

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
                        webcamEncodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Pal);
                        webcamEncodingProfile.Video.Bitrate = 2500000;
                    }
                }
                else
                {
                    webcamEncodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
                }

                _webcamMediaRecording = await _webcamMediaCapture.PrepareLowLagRecordToStorageFileAsync(webcamEncodingProfile, webcamFile);

                // kick off the screen encoding parallel
                using (var stream = await screenFile.OpenAsync(FileAccessMode.ReadWrite))
                using (_screenEncoder = new Encoder(_screenDevice, item))
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
                return;
            }

            // at this point the encoding has finished
            MainTextBlock.Text = "saving...";

            // save slide markers
            var recording = new Recording()
            {
                Slides = _screenEncoder.GetTimestamps()
            };

            var json = JsonConvert.SerializeObject(recording, Formatting.Indented);
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
    }
}
