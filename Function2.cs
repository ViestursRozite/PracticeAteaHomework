using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using System.IO;
using System.Globalization;
using Azure.Data.Tables;

namespace MonitorPublicapisOrg
{
    class AzureFunction
    {
        private static readonly HttpClient client = new HttpClient();

        private static string HttpResponseToLog(HttpResponseMessage response, string readContent)
        {
            string result = "";
            string n = "\n";
            
            result += response.RequestMessage + n;
            result += response.ToString() + n + n;
            result += readContent;
            result += response.TrailingHeaders + n;

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
            string nowTime = DateTime.UtcNow.ToString("yyyy/MM/dd/HH'-'mm", CultureInfo.InvariantCulture);//used for sub-devison of blob logs ↓
            string txtFormat = ".txt";
            
            string unixTimeUTC = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();//Date in seconds used to query date intervals from storage
            
            string blobNameContainingTime =  nowTime + txtFormat;//.txt files devided up in folders yyyy/MM/dd/

            string connectionToStorageString = "UseDevelopmentStorage=true";

            string blobStorageName = "log-publicapis-org-ran";
            string tableStorageName = "logPublicapisOrgRandom";

            string site = @"https://api.publicapis.org/random?auth=null";
            string wholeHttpResponseAsString;
            bool isSucsess;

            //answer from site
            HttpResponseMessage httpResponse = await client.GetAsync(site);

            //Determine if sucsess or failure
            try
            {
                var resp = httpResponse.EnsureSuccessStatusCode();//status code == OK, else err
                isSucsess = true;
                string responseBody = await httpResponse.Content.ReadAsStringAsync();
                wholeHttpResponseAsString = HttpResponseToLog(httpResponse, responseBody);
            }
            catch (HttpRequestException exption)
            {
                isSucsess = false;
                wholeHttpResponseAsString = exption.ToString();//save err message
            }

            //Store success/failure attempt log in the table

            var tableClient = new TableClient(connectionToStorageString, tableStorageName);
            tableClient.CreateIfNotExists();//ensure container exists in Azure

            var row = new TableEntity($"{isSucsess}", $"{unixTimeUTC}");//has to have partitionKey rowKey
            tableClient.AddEntity(row);//send

            //Store full payload in the blob

            Stream textToUpoadAsStream = StringToStream(wholeHttpResponseAsString);

            var blobContainerClient = new BlobContainerClient(connectionToStorageString, blobStorageName);
            blobContainerClient.CreateIfNotExists();//ensure container exists in Azure
            blobContainerClient.UploadBlob(blobNameContainingTime, textToUpoadAsStream);//send
        }
    }
}
