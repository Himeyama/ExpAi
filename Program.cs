using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using OpenAI.Chat;
using OpenTelemetry.Trace;

internal class Program
{
    const string ModelId = "gpt-4o-mini";
    
    private static async Task Main(string[] args)
    {
        // プログラム内で使用するモデルIDを定数として定義します。

        // 環境変数からAPIキーを読み込みます。
        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("エラー: 環境変数 'OPENAI_API_KEY' が設定されていません。");
            return;
        }

        string sourceName = Guid.NewGuid().ToString();
        TracerProvider tracerProvider = OpenTelemetry.Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .AddConsoleExporter()
            .Build();

        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        StdioClientTransport clientTransport = new(new StdioClientTransportOptions
        {
            Name = "filesystem",
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-filesystem", homeDirectory]
        });
        IMcpClient mcpClient = await McpClientFactory.CreateAsync(clientTransport);
        IList<McpClientTool> tools = await mcpClient.ListToolsAsync();

        IChatClient openaiClient = new ChatClient(ModelId, Environment.GetEnvironmentVariable("OPENAI_API_KEY")).AsIChatClient();
        IDistributedCache cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        IChatClient chatClient = new ChatClientBuilder(openaiClient).UseFunctionInvocation().UseDistributedCache(cache).Build();


        List<Microsoft.Extensions.AI.ChatMessage> history = [];

        ChatOptions chatOptions = new()
        {
            ToolMode = ChatToolMode.Auto,
            Tools = [.. tools]
        };

        Console.WriteLine($"AI ({ModelId}) とチャットを始めましょう (終了するには 'exit' と入力)");

        while (true)
        {
            Console.Write("あなた: ");
            string? prompt = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(prompt) || prompt.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            history.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, prompt));

            // 5. これまでの会話履歴を含めてOpenAIにリクエストを送信します。
            string assistantMessage = "";
            await foreach (ChatResponseUpdate message in chatClient.GetStreamingResponseAsync(history, chatOptions))
            {
                assistantMessage += message;
                Console.Write(message);
            }
            Console.WriteLine(); // 改行を追加して出力を整形
            history.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, assistantMessage));

            string historyJson = System.Text.Json.JsonSerializer.Serialize(history, new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                WriteIndented = true
            });
            Console.WriteLine(historyJson);
        }
    }
}