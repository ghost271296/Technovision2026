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
        private const int MaxRetries     = 2;
        private const int TimeoutSeconds = 30;
        private const int MaxTokens      = 1200;

        public OpenAIClientService(string apiKey)
        {
            _client = new OpenAIClient(apiKey).GetChatClient("gpt-4o");
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
                        Temperature         = 0.2f,
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
                    lastEx = new TimeoutException($"Timed out after {TimeoutSeconds}s.", ex);
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("401") || ex.Message.Contains("403") || ex.Message.Contains("400"))
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