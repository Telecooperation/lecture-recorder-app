using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerpointAppService
{
    public class SlideChangedEventArgs : EventArgs
    {
        public string Title { get; set; }

        public string Keywords { get; set; }
    }
}
