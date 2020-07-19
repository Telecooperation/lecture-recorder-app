using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.MediaProperties;
using Windows.Storage;

namespace SimpleRecorder
{
    public class AppSettingsContainer
    {
        public static AppSettings GetCachedSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var result = new AppSettings
            {
                Quality = VideoEncodingQuality.HD1080p,
                FrameRate = 60,
                UseSourceSize = true,
                AdaptBitrate = true,
                WebcamExposureAuto = true,
                WebcamWhiteBalanceAuto = true
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
            if (localSettings.Values.TryGetValue(nameof(AppSettings.StorageFolder), out var storageFolder))
            {
                result.StorageFolder = (string)storageFolder;
            }
            if (localSettings.Values.TryGetValue(nameof(AppSettings.WebcamQuality), out var webcamQuality))
            {
                result.WebcamQuality = webcamQuality as string;
            }
            if (localSettings.Values.TryGetValue(nameof(AppSettings.WebcamDeviceId), out var webcamDeviceId))
            {
                result.WebcamDeviceId = webcamDeviceId as string;
            }
            if (localSettings.Values.TryGetValue(nameof(AppSettings.WebcamExposure), out var webcamExposure))
            {
                result.WebcamExposure = (long)webcamExposure;
            }
            if (localSettings.Values.TryGetValue(nameof(AppSettings.WebcamExposureAuto), out var webcamExposureAuto))
            {
                result.WebcamExposureAuto = (bool)webcamExposureAuto;
            }
            if (localSettings.Values.TryGetValue(nameof(AppSettings.WebcamWhiteBalance), out var webcamWhiteBalance))
            {
                result.WebcamWhiteBalance = (uint)webcamWhiteBalance;
            }
            if (localSettings.Values.TryGetValue(nameof(AppSettings.WebcamWhiteBalanceAuto), out var webcamWhiteBalanceAuto))
            {
                result.WebcamWhiteBalanceAuto = (bool)webcamWhiteBalanceAuto;
            }

            return result;
        }

        public static T ParseEnumValue<T>(string input)
        {
            return (T)Enum.Parse(typeof(T), input, false);
        }

        public static void CacheSettings(AppSettings settings)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[nameof(AppSettings.Quality)] = settings.Quality.ToString();
            localSettings.Values[nameof(AppSettings.FrameRate)] = settings.FrameRate;
            localSettings.Values[nameof(AppSettings.UseSourceSize)] = settings.UseSourceSize;
            localSettings.Values[nameof(AppSettings.AdaptBitrate)] = settings.AdaptBitrate;
            localSettings.Values[nameof(AppSettings.StorageFolder)] = settings.StorageFolder;
            localSettings.Values[nameof(AppSettings.WebcamDeviceId)] = settings.WebcamDeviceId;
            localSettings.Values[nameof(AppSettings.WebcamQuality)] = settings.WebcamQuality;
            localSettings.Values[nameof(AppSettings.WebcamExposure)] = settings.WebcamExposure;
            localSettings.Values[nameof(AppSettings.WebcamExposureAuto)] = settings.WebcamExposureAuto;
            localSettings.Values[nameof(AppSettings.WebcamWhiteBalance)] = settings.WebcamWhiteBalance;
            localSettings.Values[nameof(AppSettings.WebcamWhiteBalanceAuto)] = settings.WebcamWhiteBalanceAuto;
        }
    }
}
