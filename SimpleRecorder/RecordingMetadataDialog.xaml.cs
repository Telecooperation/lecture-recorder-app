using System;
using System.Collections.Generic;
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
    public sealed partial class RecordingMetadataDialog : ContentDialog
    {
        public RecordingMetadataDialog()
        {
            this.InitializeComponent();

            DatePickerRecording.Date = DateTime.Now.Date;
            TimePickerRecording.Time = DateTime.Now.TimeOfDay;
        }

        public string LectureTitle { get => TxtRecordingTitle.Text; }

        public DateTimeOffset? LectureDate { get => DatePickerRecording.SelectedDate.HasValue ? DatePickerRecording.SelectedDate.Value.Date + TimePickerRecording.Time : DateTime.Now; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
