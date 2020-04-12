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

        public string WebcamDeviceId;

        public string WebcamQuality;
    }
}
