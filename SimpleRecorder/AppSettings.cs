using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.MediaProperties;

namespace SimpleRecorder
{ 
    public struct AppSettings
    {
        public VideoEncodingQuality Quality;

        public uint FrameRate;

        public bool UseSourceSize;

        public bool AdaptBitrate;

        public string WebcamDeviceId;

        public string WebcamQuality;

        public long WebcamExposure;

        public bool WebcamExposureAuto;

        public float WebcamExposureCompensation;

        public uint WebcamWhiteBalance;

        public bool WebcamWhiteBalanceAuto;
    }
}
