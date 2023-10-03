using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using HtmlAgilityPack;
using Microsoft.SemanticKernel.Text;

namespace HttpSummarization
{
    public class ProcessHtml
    {
        private readonly ILogger _logger;
        private readonly IKernel _kernel;

        public ProcessHtml(ILoggerFactory loggerFactory, IKernel kernel)
        {
            _logger = loggerFactory.CreateLogger<ProcessHtml>();
            _kernel = kernel;
        }

        [Function("ProcessHtml")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] 
            HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("ProcessHtml function triggered.");

            // Read docUri and Id from request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var requestBodyJson = JsonDocument.Parse(requestBody);
            var docUri = requestBodyJson.RootElement.GetProperty("docUri").GetString();
            
            // Check for missing docUri property
            if (docUri == null)
            {
                throw new ArgumentNullException("docUri is null. Please check your request body.");
            }

            if (!Uri.TryCreate(docUri, UriKind.Absolute, out var uri) || uri.Host == null)
            {
                throw new ArgumentException("docUri is not a valid URL. Please check your request body.");
            }

            // create a http client request and call docuri endpoint
            var request = new HttpRequestMessage(HttpMethod.Get, docUri);
            var client = new HttpClient();
            var httpDocResponse = await client.SendAsync(request);
            var html = await httpDocResponse.Content.ReadAsStringAsync();

            
            // // parse the html with htmlagilitypack
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var mainDocTitle = htmlDocument.DocumentNode.SelectSingleNode("//title").InnerText;
            var mainContent = htmlDocument.DocumentNode.SelectSingleNode("//main[@id='main']");
            var mainContentHtml = mainContent.InnerHtml;
            var mainContentText = mainContent.InnerText;

            // if(uri.Host == "learn.microsoft.com")
            // {
                
            // }
            
            var lineChunks = TextChunker.SplitPlainTextLines(mainContentText, 120);
            var paragraphChunks = TextChunker.SplitPlainTextParagraphs(lineChunks, 120);

            for (var i = 0; i < paragraphChunks.Count; i++)
            {
                Console.WriteLine(paragraphChunks[i]);

                if (i < paragraphChunks.Count - 1)
                {
                    Console.WriteLine("------------------------");
                }
            }
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString(paragraphChunks.ToString());

            return response;
        }
    }
}
