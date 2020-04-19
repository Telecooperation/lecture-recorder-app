using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleRecorder.Model
{
    public class Recording
    {
        public string Description { get; set; }

        public DateTime LectureDate { get; set; }

        public List<TimeSpan> Slides { get; set; }
    }
}
