using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using System.IO;
using System.Globalization;

namespace MonitorPublicapisOrg
{
    class AzureFunction
    {
        private static readonly HttpClient client = new HttpClient();

        private static string HttpResponseToLog(HttpResponseMessage response, string readContent)
        {
            string result = "";
            string n = "\n";

            result += response.ToString() + n + n;
            result += readContent;
            result += response.TrailingHeaders + n;
            result += response.RequestMessage + n;

            return result;
        }

        public static Stream StringToStream(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);

            writer.Write(s);
            writer.Flush();
            stream.Position = 0;

            return stream;
        }

        [FunctionName("LogPublicapisOrgRandom")]
        public static async Task RunAsync([TimerTrigger("* * * * *")] TimerInfo myTimer, ILogger log)
        {
            string blobNameContainingTime = DateTime.Now.ToString("yyyy/MM/dd/HH'-'mm", CultureInfo.InvariantCulture) + ".txt";//.txt files devided up in folders yyyy/MM/dd/
            string connectionToStorageString = "UseDevelopmentStorage=true";
            string storageContainerName = "log-publicapis-org-random";
            string site = @"https://api.publicapis.org/random?auth=null";
            string wholeHttpResponseAsString;

            //answer from site
            HttpResponseMessage httpResponse = await client.GetAsync(site);

            //fill httpResponseAsString based on response code 
            try
            {
                var resp = httpResponse.EnsureSuccessStatusCode();//status code == OK, else err
                string responseBody = await httpResponse.Content.ReadAsStringAsync();
                wholeHttpResponseAsString = HttpResponseToLog(httpResponse, responseBody);
            }
            catch (HttpRequestException exption)
            {
                wholeHttpResponseAsString = exption.ToString();//save err message
            }

            //upload log
            Stream textToUpoadAsStream = StringToStream(wholeHttpResponseAsString);

            BlobContainerClient blobContainerClient = new BlobContainerClient(connectionToStorageString, storageContainerName);
            blobContainerClient.CreateIfNotExists();//ensure container exists in Azure
            blobContainerClient.UploadBlob(blobNameContainingTime, textToUpoadAsStream);//send
        }
    }
}
