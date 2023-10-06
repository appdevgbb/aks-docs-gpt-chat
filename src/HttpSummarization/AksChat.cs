using System.Net;
using System.Text.Json;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Memory;
using HtmlAgilityPack;

namespace HttpSummarization
{
    public class AksChat
    {
        private readonly ILogger _logger;
        private readonly IKernel _kernel;

        // should probably be done in startup as a singleton/service injection
        private readonly string _pluginDirectory = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Plugins");

        private readonly IDictionary<string, ISKFunction> _semanticPlugins;
        private readonly ISKFunction _responsePlugin;
        private readonly string _memoryCollectionName = "aks-docs";
 
        public AksChat(ILoggerFactory loggerFactory, IKernel kernel)
        {
            _logger = loggerFactory.CreateLogger<AksChat>();
            _kernel = kernel;
            // should probably be done in startup as a singleton/service injection
            _semanticPlugins = _kernel.ImportSemanticSkillFromDirectory(_pluginDirectory, "SemanticPlugin");
            _responsePlugin = _semanticPlugins["Response"];
        }

        [Function("AksChat")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] 
            HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("AksChat function triggered.");

            // Read query and Id from request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var requestBodyJson = JsonDocument.Parse(requestBody);
            var query = requestBodyJson.RootElement.GetProperty("query").GetString() ?? string.Empty;

            var relevantInfo = await SearchMemoryAsync(query);

            var context = new ContextVariables();
            
            context.Set("query", query);
            context.Set("info", relevantInfo);
            
            var chatResponse = await _kernel.RunAsync(context, _responsePlugin);
            var responseText = chatResponse.ToString();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString(responseText);

            return response;
        }

        private async Task<string> SearchMemoryAsync(string query)
        {
            var result = new StringBuilder();
            result.Append("[START RELEVANT INFO]");

            var memories = _kernel.Memory.SearchAsync(_memoryCollectionName, query, limit: 5, minRelevanceScore: 0.8);

            await foreach (MemoryQueryResult memory in memories)
            {
                var relevance = memory.Relevance;
                var text = memory.Metadata.Text;

                result.Append("\n" + text + "\n");
                
                Console.WriteLine("-------------");
                Console.WriteLine($"Info: {text}");
                Console.WriteLine($"Info: {relevance}");
                Console.WriteLine("-------------");
            }

            result.Append("\n[END RELEVANT INFO]");
            return result.ToString();
        }    
    }
}
