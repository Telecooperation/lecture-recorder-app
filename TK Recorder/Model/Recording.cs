using System;
using System.Collections.Generic;

namespace TK_Recorder.Model
{
    public class Recording
    {
        public string Description { get; set; }

        public DateTime LectureDate { get; set; }

        public List<TimeSpan> Slides { get; set; }
    }
}
