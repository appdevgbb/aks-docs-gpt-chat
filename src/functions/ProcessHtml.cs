using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using HtmlAgilityPack;

namespace AksChatBot
{
    public class ProcessHtmlDoc
    {
        private readonly ILogger _logger;
        private readonly IKernel _kernel;

        // should probably be done in startup as a singleton/service injection
        private readonly string _pluginDirectory = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Plugins");

        private readonly IDictionary<string, ISKFunction> _semanticPlugins;
        private readonly ISKFunction _summarizePlugin;
        private readonly string _memoryCollectionName = "aks-docs";

        public ProcessHtmlDoc(ILoggerFactory loggerFactory, IKernel kernel)
        {
            _logger = loggerFactory.CreateLogger<ProcessHtmlDoc>();
            _kernel = kernel;
            // should probably be done in startup as a singleton/service injection
            _semanticPlugins = _kernel.ImportSemanticSkillFromDirectory(_pluginDirectory, "SemanticPlugin");
            _summarizePlugin = _semanticPlugins["Summarize"];

        }

        [Function("ProcessHtmlDoc")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] 
            HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("ProcessHtmlDoc function triggered.");

            // Read docUrl and Id from request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var requestBodyJson = JsonDocument.Parse(requestBody);
            var docUrl = requestBodyJson.RootElement.GetProperty("docUrl").GetString() ?? string.Empty;

            ValidateUri(docUrl);

            var (docTitle, mainContentHtml, mainContentText) = await ProcessHtmlDocFromUrl(docUrl);
            var paragraphChunks = ChunkText(mainContentText, 200, 500);
            var summaryTextList = new List<string>();

            for (var i = 0; i < paragraphChunks.Count; i++)
            {
                var paragraph = paragraphChunks[i];
                var summaryText = await GenerateTextSummary(paragraph);

                var summary = new {
                    id = $"{docTitle}-{i}",
                    externalSourceName = "Azure Docs",
                    url = docUrl,
                    title = docTitle,
                    description = paragraph,
                    text = summaryText
                };
                
                summaryTextList.Add(summaryText);

                // SK can be used to create an embedding and store the summary in a memory collection in one shot see [this](https://github.com/microsoft/semantic-kernel/blob/1217d8540d76e2dd0a8f9bd568d98efa1c4ebee1/dotnet/samples/KernelSyntaxExamples/Example14_SemanticMemory.cs#L141) example

                await _kernel.Memory.SaveInformationAsync(
                    collection: _memoryCollectionName, 
                    id: summary.id, 
                    text: summary.text);
            }
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString(summaryTextList[0]);

            return response;
        }

        private static void ValidateUri(string docUrl)
        {
            if (string.IsNullOrEmpty(docUrl))
            {
                throw new ArgumentException("docUrl cannot be null or empty");
            }

            if (!Uri.IsWellFormedUriString(docUrl, UriKind.Absolute))
            {
                throw new ArgumentException("docUrl is not a valid absolute uri");
            }
        }

        private static async Task<(string mainDocTitle, string html, string innerTextOutput)> ProcessHtmlDocFromUrl(string docUrl)
        {
            // create a http client request and call docurl endpoint
            var request = new HttpRequestMessage(HttpMethod.Get, docUrl);
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

            return(mainDocTitle, mainContentHtml, mainContentText);
        }

        private static List<string> ChunkText(string contentText, int maxLinetokens, int maxParagraphTokens)
        {
            // should probably use blingfire here https://www.nuget.org/packages/BlingFireNuget/
            var lineChunks = TextChunker.SplitPlainTextLines(contentText, maxLinetokens);
            var paragraphChunks = TextChunker.SplitPlainTextParagraphs(lineChunks, maxParagraphTokens);

            return paragraphChunks;
        }

        private async Task<string> GenerateTextSummary(string text)
        {
            var context = new ContextVariables();
            
            context.Set("text", text);
            
            var summaryResponse = await _kernel.RunAsync(context, _summarizePlugin);
            var summaryText = summaryResponse.ToString();
    
            return summaryText;
        }
    }
}
