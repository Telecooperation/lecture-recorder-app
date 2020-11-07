using Newtonsoft.Json;
using SimpleRecorder.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;

namespace SimpleRecorder.Services
{
    public class BackendService
    {
        private readonly string apiEndpoint = "https://localhost:5001";

        public async Task<Semester> GetCurrentSemesterAsync()
        {
            return await Get<Semester>(new Uri(apiEndpoint + "/api/semester/current"));
        }

        public async Task<List<Lecture>> GetCurrentLecturesAsync()
        {
            var semester = await GetCurrentSemesterAsync();

            if (semester == null)
            {
                return new List<Lecture>();
            }

            return await Get<List<Lecture>>(new Uri(apiEndpoint + "/api/lecture/semester/" + semester.Id));
        }

        public async Task UploadRecordingAsync(Lecture lecture)
        {
            using (var httpClientHandler = new HttpClientHandler())
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

                using (var client = new HttpClient(httpClientHandler))
                {
                    var slidesFile = await StorageFile.GetFileFromPathAsync(@"D:\Videos\Captures\2020-07-19-14-58-42_slides.mp4");
                    var talkingheadFile = await StorageFile.GetFileFromPathAsync(@"D:\Videos\Captures\2020-07-19-14-58-42_talkinghead.mp4");
                    var metadataFile = await StorageFile.GetFileFromPathAsync(@"D:\Videos\Captures\2020-07-19-14-58-42_meta.json");

                    var slidesStream = await slidesFile.OpenStreamForReadAsync();
                    var talkingheadStream = await talkingheadFile.OpenStreamForReadAsync();
                    var metadataStream = await metadataFile.OpenStreamForReadAsync();

                    var content = new MultipartFormDataContent();
                    content.Add(new StreamContent(slidesStream), "files", "2020-07-19-14-58-42_slides.mp4");
                    content.Add(new StreamContent(talkingheadStream), "files", "2020-07-19-14-58-42_talkinghead.mp4");
                    content.Add(new StreamContent(metadataStream), "files", "2020-07-19-14-58-42_meta.json");
                    content.Add(new StringContent("false"), "progess");

                    var response = await client.PostAsync(apiEndpoint + "/api/recording/upload/" + lecture.Id, content);
                    response.EnsureSuccessStatusCode();

                    slidesStream.Close();
                    talkingheadStream.Close();
                    metadataStream.Close();
                }
            }
        }

        private StreamContent CreateFileContent(Stream stream, string fileName, string contentType)
        {
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("files") { FileName = fileName };
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return fileContent;
        }

        private async Task<T> Get<T>(Uri queryUri)
        {
            using (var httpClientHandler = new HttpClientHandler())
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

                using (var client = new HttpClient(httpClientHandler))
                {
                    var response = await client.GetAsync(queryUri);
                    response.EnsureSuccessStatusCode();

                    var responseBody = await response.Content.ReadAsStringAsync();

                    return JsonConvert.DeserializeObject<T>(responseBody);
                }
            }
        }
    }
}
