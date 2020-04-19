using System;
using System.Threading;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace PowerpointAppService
{
    class Program
    {
        static AppServiceConnection connection = null;
        static AutoResetEvent appServiceExit;

        static PowerPointInstance powerPoint = null;

        static void Main(string[] args)
        {
            // connect to app service and wait until the connection gets closed
            appServiceExit = new AutoResetEvent(false);

            // publish the app service and load powerpoint connector
            InitializeAppServiceConnection();
            InitializePowerpoint();
            
            // block exit
            appServiceExit.WaitOne();
        }

        static void InitializePowerpoint()
        {
            powerPoint = new PowerPointInstance();
            powerPoint.StatusChanged += PowerPoint_StatusChanged;
            powerPoint.SlideChanged += PowerPoint_SlideChanged;
        }

        private async static void PowerPoint_StatusChanged(object sender, PowerPointInstance.PowerPointStatus e)
        {
            var msg = new ValueSet();
            msg.Add("TYPE", "Status");
            msg.Add("STATUS", e.ToString());

            await connection?.SendMessageAsync(msg);
        }

        private async static void PowerPoint_SlideChanged(object sender, SlideChangedEventArgs e)
        {
            var msg = new ValueSet();
            msg.Add("TYPE", "SlideChanged");
            msg.Add("TITLE", e.Title);

            await connection?.SendMessageAsync(msg);
        }

        static async void InitializeAppServiceConnection()
        {
            connection = new AppServiceConnection();
            connection.AppServiceName = "PowerpointAppService";
            connection.PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;
            connection.RequestReceived += Connection_RequestReceived;
            connection.ServiceClosed += Connection_ServiceClosed;

            AppServiceConnectionStatus status = await connection.OpenAsync();
            if (status != AppServiceConnectionStatus.Success)
            {
                // TODO: error handling
            }
        }

        private static void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            // signal the event so the process can shut down
            appServiceExit.Set();
        }

        private static void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            // we don't need to hear for incomming commands
        }
    }
}
