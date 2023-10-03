using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using HtmlAgilityPack;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Orchestration;
using HttpSummarization.Models;

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

            if (mainContent == null)
            {
                mainContent = htmlDocument.DocumentNode.SelectSingleNode("//body");
            }

            var mainContentHtml = mainContent.InnerHtml;
            var mainContentText = mainContent.InnerText;

            var lineChunks = TextChunker.SplitPlainTextLines(mainContentText, 200);
            var paragraphChunks = TextChunker.SplitPlainTextParagraphs(lineChunks, 500);

            var pluginDirectory = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Plugins");
            var plugin = _kernel.ImportSemanticSkillFromDirectory(pluginDirectory, "SemanticPlugin");
 
            var functionName = "Summarize";
            var function = plugin[functionName];

            var summaryTexts = new List<string>();

            for (var i = 0; i < paragraphChunks.Count; i++)
            {
                
                var paragraph = paragraphChunks[i];
                
                var context = new ContextVariables();
                context.Set("text", paragraph);
                var summaryResponse = await _kernel.RunAsync(context, function);
                var summaryText = summaryResponse.ToString();
                summaryTexts.Add(summaryText);

                var summary = new Summary(
                    title: mainDocTitle,
                    summaryText: summaryText,
                    originalText: paragraph,
                    articleUrl: docUri,
                    links: null
                );

                Console.WriteLine("summary: {summaryText}");
                Console.WriteLine("--------------------");
            }
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString(summaryTexts[0]);

            return response;
        }
    }
}
