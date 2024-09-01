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

var yourName = "User";

var serverConfig = new ChatRoomServerConfiguration
{
    RoomConfig = roomConfig,
    YourName = yourName,
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
var model = "gpt-4o-mini";
var openAIClient = new OpenAIClient(openAIApiKey);
var chatClient = openAIClient.GetChatClient(model);

var coder = Coder.CreateFromOpenAI(chatClient);
var assistant = Assistant.CreateFromOpenAI(chatClient);
var runner = Runner.CreateFromOpenAI(kernel);
var planner = Planner.CreateFromOpenAI(chatClient);

// the user agent's name must match with ChatRoomServerConfiguration.YourName field.
// When chatroom starts, it will be replaced by a built-in user agent which takes your input and sends it to groupchat.
var userAgent = new DefaultReplyAgent(yourName, "<dummy>");

var orchestrator = new Orchestrator(userAgent, assistant, runner, coder, planner);

var groupChat = new GroupChat(
    members: [coder, planner, assistant, runner, userAgent],
    orchestrator: orchestrator);

// add weather groupchat to chatroom
await client.RegisterAutoGenGroupChatAsync("dotnet-interactive-chat", groupChat);
await host.WaitForShutdownAsync();
