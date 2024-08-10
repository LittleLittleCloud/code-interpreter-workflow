using AutoGen.Core;
using AutoGen.DotnetInteractive;
using Azure.AI.OpenAI;
using dotnet_interactive_agent;
using dotnet_interactive_agent.Agent;

using var kernel = DotnetInteractiveKernelBuilder
    .CreateDefaultInProcessKernelBuilder()
    .AddPowershellKernel()
    .AddPythonKernel("python3")
    .Build();

var openAIApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new ArgumentNullException("OPENAI_API_KEY is not found");
var model = "gpt-4o-mini";
var openAIClient = new OpenAIClient(openAIApiKey);

var BING_API_KEY = Environment.GetEnvironmentVariable("BING_API_KEY") ?? throw new ArgumentNullException("BING_API_KEY is not found");

var coder = Coder.CreateFromOpenAI(openAIClient, model);
var user = User.CreateFromOpenAI(openAIClient, model);
var assistant = Assistant.CreateFromOpenAI(openAIClient, model);
var runner = Runner.CreateFromOpenAI(kernel);
var webSearch = WebSearch.CreateFromOpenAI(openAIClient, model, BING_API_KEY);

var orchestrator = new EventDrivenOrchestrator(user, assistant, runner, coder, webSearch);

var groupChat = new GroupChat(
    members: [coder, user, assistant, runner],
    orchestrator: orchestrator);

var task = """
    Download AAPL/META/MSFT stock price from Yahoo Finance and save it to a CSV file. Then open it in VSCode.
    """;

var chatHistory = new List<IMessage>()
{
    new TextMessage(Role.Assistant, task, user.Name).ToEventMessage(EventType.CreateTask, new Dictionary<string, string>()
    {
        ["task"] = task,
    }),
};

await foreach (var msg in groupChat.SendAsync(chatHistory, maxRound: 20))
{
    if (msg is EventMessage finalReply && finalReply.Type == EventType.Succeeded)
    {
        Console.WriteLine("Chat completed successfully");
        break;
    }

    if (msg is EventMessage notCodingTask && notCodingTask.Type == EventType.NotCodingTask)
    {
        Console.WriteLine("Exit chat because the task is not coding task");
        break;
    }
}

