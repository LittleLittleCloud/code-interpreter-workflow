using AutoGen.Core;
using AutoGen.DotnetInteractive;
using ChatRoom.SDK;
using dotnet_interactive_agent;
using dotnet_interactive_agent.Agent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;

var roomConfig = new RoomConfiguration
{
    Room = "room",
    Port = 30000,
};

var serverConfig = new ChatRoomServerConfiguration
{
    RoomConfig = roomConfig,
    YourName = "User",
    ServerConfig = new ServerConfiguration
    {
        Urls = "http://localhost:50001",
    },
};

using var host = Host.CreateDefaultBuilder()
    .UseChatRoomServer(serverConfig)
    .Build();

await host.StartAsync();

var client = host.Services.GetRequiredService<ChatPlatformClient>();


using var kernel = DotnetInteractiveKernelBuilder
    .CreateDefaultInProcessKernelBuilder()
    .AddPowershellKernel()
    .AddPythonKernel("python3")
    .Build();

var openAIApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new ArgumentNullException("OPENAI_API_KEY is not found");
var model = "gpt-4o";
var openAIClient = new OpenAIClient(openAIApiKey);
var chatClient = openAIClient.GetChatClient(model);

var BING_API_KEY = Environment.GetEnvironmentVariable("BING_API_KEY") ?? throw new ArgumentNullException("BING_API_KEY is not found");

var coder = Coder.CreateFromOpenAI(chatClient);
var planner = Planner.CreateFromOpenAI(chatClient);
var assistant = Assistant.CreateFromOpenAI(chatClient);
var runner = Runner.CreateFromOpenAI(kernel);
var webSearch = WebSearch.CreateFromOpenAI(chatClient, BING_API_KEY);

// the user agent's name must match with ChatRoomServerConfiguration.YourName field.
// When chatroom starts, it will be replaced by a built-in user agent.
var userAgent = new DefaultReplyAgent("User", "<dummy>");

var orchestrator = new STMOrchestrator(planner, assistant, runner, coder, webSearch);

var groupChat = new GroupChat(
    members: [coder, planner, assistant, runner, userAgent, webSearch],
    orchestrator: orchestrator);

// add weather groupchat to chatroom
await client.RegisterAutoGenGroupChatAsync("code-chat", groupChat);
await host.WaitForShutdownAsync();

//var state = new State
//{
//    NextStep = Step.CreateTask,
//    Task = task,
//};

//var chatHistory = new List<IMessage>()
//{
//    state.ToTextMessage(user.Name)
//};

//await foreach (var msg in groupChat.SendAsync(chatHistory, maxRound: 20))
//{
//    if (msg.GetState() is State finalReply
//        && finalReply.NextStep == Step.Succeeded
//        && finalReply.Task is string userTask
//        && finalReply.Code is string code
//        && finalReply.Result is string result
//        && finalReply.Answer is string finalAnswer)
//    {
//        Console.WriteLine("Chat completed successfully");

//        // code execution result

//        finalAnswer = $"""
//            Task:
//            {userTask}

//            Code:
//            {code}

//            Execution Result:
//            {result}

//            Final Answer:
//            {finalAnswer}
//            """;

//        Console.WriteLine(finalAnswer);
//        break;
//    }

//    if (msg.GetState() is State notCodeingState
//        && notCodeingState.NextStep == Step.NotCodingTask)
//    {
//        Console.WriteLine("Exit chat because the task is not coding task");
//        break;
//    }
//}

