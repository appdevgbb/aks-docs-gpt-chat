using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch;
using System.Text.Json;


public static class Program
{
	public static void Main()
	{
		var host = new HostBuilder()
			.ConfigureFunctionsWorkerDefaults()
			.ConfigureAppConfiguration(configuration =>
			{
				var config = configuration.SetBasePath(Directory.GetCurrentDirectory())
					.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);

				var builtConfig = config.Build();
			})
			.ConfigureServices(services =>
			{        
				// add http client to DI container
				services.AddHttpClient();
				services.AddTransient((provider) => CreateKernel(provider));

				// Return JSON with expected lowercase naming
				services.Configure<JsonSerializerOptions>(options =>
				{
					options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
				});
			})
			.Build();

		host.Run();
	}

	private static IKernel CreateKernel(IServiceProvider provider)
	{
		using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
		{
			builder
				.SetMinimumLevel(LogLevel.Warning)
				.AddConsole()
				.AddDebug();
		});

		// Get OpenAI settings from environment variables
		var azureOpenAiChatDeploymentName = Environment.GetEnvironmentVariable("AzureOpenAiChatDeploymentName");
		var azureOpenAiEmbeddingDeploymentName = Environment.GetEnvironmentVariable("AzureOpenAiEmbeddingDeploymentName");
		var azureOpenAiEndpoint = Environment.GetEnvironmentVariable("AzureOpenAiEndpoint");
		var azureOpenAiKey = Environment.GetEnvironmentVariable("AzureOpenAiKey");
		var azureCognitiveSearchEndpoint = Environment.GetEnvironmentVariable("AZURE_COGNITIVE_SEARCH_ENDPOINT") ?? string.Empty;
		var azureCognitiveSearchApiKey = Environment.GetEnvironmentVariable("AZURE_COGNITIVE_SEARCH_APIKEY") ?? string.Empty;
		
		var MemoryStore = new AzureCognitiveSearchMemoryStore(
			azureCognitiveSearchEndpoint,
			azureCognitiveSearchApiKey);

		// Check to see that the environment variables are not null
		if (azureOpenAiChatDeploymentName == null || azureOpenAiEmbeddingDeploymentName == null || azureOpenAiEndpoint == null || azureOpenAiKey == null)
		{
			throw new ArgumentNullException("AzureOpenAiChatDeploymentName, AzureOpenAiEndpoint, or AzureOpenAiKey is null. Please check your local.settings.json file.");
		}

		var kernel = new KernelBuilder()
			.WithAzureChatCompletionService(
				azureOpenAiChatDeploymentName, 
				azureOpenAiEndpoint, 
				azureOpenAiKey)
			.WithAzureTextEmbeddingGenerationService(
				azureOpenAiEmbeddingDeploymentName, 
				azureOpenAiEndpoint, 
				azureOpenAiKey)
			.WithMemoryStorage(MemoryStore)
			.Build();

		return kernel;   
	}
}