using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;

// プログラム内で使用するモデルIDを定数として定義します。
const string ModelId = "gpt-4o-mini";

// 環境変数からAPIキーを読み込みます。
string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("エラー: 環境変数 'OPENAI_API_KEY' が設定されていません。");
    return;
}

IChatClient openaiClient = new ChatClient(ModelId, Environment.GetEnvironmentVariable("OPENAI_API_KEY")).AsIChatClient();
IChatClient chatClient = new ChatClientBuilder(openaiClient).UseFunctionInvocation().Build();

Console.WriteLine("AIとチャットを始めましょう (終了するには 'exit' と入力)");

ChatHistory history = [];

ChatOptions chatOptions = new()
{
    Tools = [
        // AIFunctionFactory.Create(GetWeather)
    ]
};

while (true)
{
    Console.Write("You: ");
    string? prompt = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(prompt) || prompt.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    history.AddUserMessage(prompt);

    // 5. これまでの会話履歴を含めてOpenAIにリクエストを送信します。
    string assistantMessage = "";
    await foreach (ChatResponseUpdate message in chatClient.GetStreamingResponseAsync(prompt, chatOptions))
    {
        assistantMessage += message;
        Console.Write(message);
    }
    Console.WriteLine(); // 改行を追加して出力を整形
    
    // // 6. AIからの応答を表示し、履歴に追加します。
    history.AddAssistantMessage(assistantMessage);
}
