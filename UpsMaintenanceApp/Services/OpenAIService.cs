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
        private const int TimeoutSeconds = 150;

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
                        Temperature    = 0.2f,
                        ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
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
                    // Auth / config errors are unrecoverable — fail immediately without retry
                    string msg = ex.Message;
                    bool unrecoverable =
                        msg.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase) ||
                        msg.Contains("Incorrect API key",    StringComparison.OrdinalIgnoreCase) ||
                        msg.Contains("401") ||
                        msg.Contains("403") ||
                        (msg.Contains("400") && (
                            msg.Contains("invalid_model",           StringComparison.OrdinalIgnoreCase) ||
                            msg.Contains("model_not_found",         StringComparison.OrdinalIgnoreCase) ||
                            msg.Contains("context_length_exceeded", StringComparison.OrdinalIgnoreCase) ||
                            msg.Contains("json_validate_failed",    StringComparison.OrdinalIgnoreCase)));
                    if (unrecoverable) throw;
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
