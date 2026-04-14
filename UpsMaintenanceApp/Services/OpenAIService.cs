using System;
using System.Threading;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Chat;
 
namespace UpsMaintenanceApp.Services
{
    public class OpenAIService
    {
        private readonly ChatClient _client;
        private const int MaxRetries     = 2;
        private const int TimeoutSeconds = 30;
 
        public OpenAIService(string apiKey)
        {
            var openAiClient = new OpenAIClient(apiKey);
            _client = openAiClient.GetChatClient("gpt-4o");
        }
 
        /// <summary>
        /// Sends a prompt to gpt-4o and returns the JSON string response.
        /// Retries up to 2 times on failure, with exponential backoff.
        /// Throws the last exception if all attempts fail.
        /// </summary>
        public async Task<string> AnalyzeAsync(string prompt)
        {
            int        attempt = 0;
            Exception? lastEx  = null;
 
            while (attempt <= MaxRetries)
            {
                using var cts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(TimeoutSeconds));
                try
                {
                    var options = new ChatCompletionOptions
                    {
                        Temperature    = 0.2f,
                        ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
                    };
 
                    ChatCompletion result = await _client.CompleteChatAsync(
                        new ChatMessage[] { new UserChatMessage(prompt) },
                        options,
                        cts.Token);
 
                    if (result.Content.Count == 0)
                        throw new InvalidOperationException("OpenAI returned an empty response.");
                    return result.Content[0].Text;
                }
                catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
                {
                    lastEx = new TimeoutException(
                        $"OpenAI request timed out after {TimeoutSeconds}s.", ex);
                }
                catch (Exception ex)
                {
                    string msg = ex.Message;
                    if (msg.Contains("401") || msg.Contains("403") || msg.Contains("400"))
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