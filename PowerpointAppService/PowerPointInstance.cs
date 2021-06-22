using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Office.Interop.PowerPoint;
using pp = Microsoft.Office.Interop.PowerPoint;

namespace PowerpointAppService
{
    public class PowerPointInstance
    {
        private Timer isActiveTimer;

        private pp.Application powerpointInstance;

        public event EventHandler<PowerPointStatus> StatusChanged;

        public event EventHandler<SlideChangedEventArgs> SlideChanged;

        public enum PowerPointStatus
        {
            CONNECTED, DISCONNECTED
        }

        public PowerPointInstance()
        {
            isActiveTimer = new Timer();
            isActiveTimer.Interval = 5000;
            isActiveTimer.Elapsed += IsActiveTimer_Elapsed;
            isActiveTimer.Enabled = true;
        }

        private void IsActiveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (powerpointInstance == null)
            {
                if (!InitializePowerpoint())
                {
                    powerpointInstance = null;
                    OnStatusChanged(PowerPointStatus.DISCONNECTED);
                    return;
                }
            }

            try
            {
                var currentApp = powerpointInstance.Active;

            }
            catch (Exception ex)
            {
                // powerpoint is busy, so ignore exception message
                if (ex.HResult == -2147417846)
                    return;

                powerpointInstance = null;
                OnStatusChanged(PowerPointStatus.DISCONNECTED);

                Console.WriteLine("Failed to check for PowerPoint active.", ex);
            }
        }

        protected virtual void OnStatusChanged(PowerPointStatus e)
        {
            StatusChanged(this, e);

            Console.WriteLine("CONNECTED");
        }

        public bool InitializePowerpoint()
        {
            try
            {
                // remove old instance
                CleanCom();

                // connect to powerpoint
                powerpointInstance = Marshal.GetActiveObject("PowerPoint.Application") as pp.Application;

                if (powerpointInstance != null)
                {
                    powerpointInstance.SlideShowBegin += Powerpoint_SlideShowBegin;
                    powerpointInstance.SlideShowEnd += Powerpoint_SlideShowEnd;
                    powerpointInstance.SlideShowNextSlide += Powerpoint_SlideShowNextSlide;

                    OnStatusChanged(PowerPointStatus.CONNECTED);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // do nothing :(
                Console.WriteLine("Error during PowerPoint initialization.", ex);
            }

            OnStatusChanged(PowerPointStatus.DISCONNECTED);
            powerpointInstance = null;

            return false;
        }

        private void Powerpoint_SlideShowBegin(SlideShowWindow Wn)
        {

        }

        private void Powerpoint_SlideShowEnd(Presentation Pres)
        {

        }

        private void Powerpoint_SlideShowNextSlide(SlideShowWindow Wn)
        {
            // extract title
            string title = "";

            //if (Wn.View.Slide.Shapes.HasTitle == Microsoft.Office.Core.MsoTriState.msoTrue)
            //{
            //    if (Wn.View.Slide.Shapes.Title.HasTextFrame == Microsoft.Office.Core.MsoTriState.msoTrue)
            //    {
            //        if (Wn.View.Slide.Shapes.Title.TextFrame.HasText == Microsoft.Office.Core.MsoTriState.msoTrue)
            //        {
            //            title = Wn.View.Slide.Shapes.Title.TextFrame.TextRange.Text;
            //        }
            //    }
            //}

            //string keywords = "";

            //foreach (Shape shape in Wn.View.Slide.Shapes)
            //{
            //    if (shape.HasTextFrame == Microsoft.Office.Core.MsoTriState.msoTrue)
            //    {
            //        if (shape.TextFrame.HasText == Microsoft.Office.Core.MsoTriState.msoTrue)
            //        {
            //            keywords += " " + shape.TextFrame.TextRange.Text;
            //        }
            //    }
            //}

            //keywords = keywords.Replace("\r\n", "");
            //keywords = keywords.Trim();

            SlideChanged(this, new SlideChangedEventArgs() { Title = title });
        }

        public void Dispose()
        {
            CleanCom();
        }

        private void CleanCom()
        {
            if (powerpointInstance != null)
            {
                try
                {
                    int refvalue = 0;
                    do
                    {
                        refvalue = Marshal.ReleaseComObject(powerpointInstance);
                    } while (refvalue > 0);
                }
                catch (Exception ex)
                {

                }
            }
        }
    }
}
