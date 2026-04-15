using System;
using System.Threading;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Chat;

namespace UpsMaintenanceApp.Services
{
    public class OpenAIClientService
    {
        private readonly ChatClient _client;
        private const int MaxRetries     = 3;
        private const int TimeoutSeconds = 90;
        private const int MaxTokens      = 2000;

        /// <param name="apiKey">OpenAI API key.</param>
        /// <param name="model">Model ID. Defaults to gpt-4o which supports JSON mode.</param>
        public OpenAIClientService(string apiKey, string model = "gpt-4o")
        {
            _client = new OpenAIClient(apiKey).GetChatClient(model);
        }

        public async Task<string> CallAsync(string systemPrompt, string userPrompt)
        {
            int        attempt = 0;
            Exception? lastEx  = null;

            while (attempt <= MaxRetries)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
                try
                {
                    var options = new ChatCompletionOptions
                    {
                        Temperature         = 0.1f,   // lower = more deterministic / factual
                        MaxOutputTokenCount = MaxTokens,
                        ResponseFormat      = ChatResponseFormat.CreateJsonObjectFormat()
                    };

                    ChatCompletion result = await _client.CompleteChatAsync(
                        new ChatMessage[]
                        {
                            new SystemChatMessage(systemPrompt),
                            new UserChatMessage(userPrompt)
                        },
                        options, cts.Token);

                    if (result.Content.Count == 0)
                        throw new InvalidOperationException("OpenAI returned an empty response.");

                    return result.Content[0].Text;
                }
                catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
                {
                    lastEx = new TimeoutException($"Agent timed out after {TimeoutSeconds}s.", ex);
                }
                catch (Exception ex)
                {
                    // Auth errors are unrecoverable — fail immediately
                    if (ex.Message.Contains("401") || ex.Message.Contains("403") ||
                        ex.Message.Contains("400") || ex.Message.Contains("invalid_api_key"))
                        throw;
                    lastEx = ex;
                }

                attempt++;
                if (attempt <= MaxRetries)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }

            throw lastEx!;
        }
    }
}
