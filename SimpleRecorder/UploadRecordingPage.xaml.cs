using SimpleRecorder.Model;
using SimpleRecorder.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Die Elementvorlage "Inhaltsdialogfeld" wird unter https://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

namespace SimpleRecorder
{
    public sealed partial class UploadRecordingPage : ContentDialog
    {
        private BackendService backendService = new BackendService();

        private ObservableCollection<Lecture> lecturesList = new ObservableCollection<Lecture>();

        public UploadRecordingPage()
        {
            this.InitializeComponent();
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            await backendService.UploadRecordingAsync(new Lecture() { Id =15 });
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            var lectures = await this.backendService.GetCurrentLecturesAsync();
            lectures.ForEach(x => lecturesList.Add(x));
        }
    }
}
