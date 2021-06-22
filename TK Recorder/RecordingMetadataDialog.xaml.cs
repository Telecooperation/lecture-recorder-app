using System;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TK_Recorder
{
    public sealed partial class RecordingMetadataDialog : ContentDialog
    {
        public RecordingMetadataDialog()
        {
            this.InitializeComponent();

            DatePickerRecording.Date = DateTime.Now.Date;
            TimePickerRecording.Time = new TimeSpan(0, 1, 0);
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
