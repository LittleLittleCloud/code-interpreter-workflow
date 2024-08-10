using AutoGen.Core;
using AutoGen.OpenAI;
using AutoGen.OpenAI.Extension;
using Azure.AI.OpenAI;

namespace dotnet_interactive_agent.Agent;

internal class Coder : IAgent
{
    private readonly IAgent _innerAgent;

    private Coder(IAgent innerAgent)
    {
        _innerAgent = innerAgent;
    }

    public static Coder CreateFromOpenAI(OpenAIClient client, string model, string name = "coder")
    {
        var innerAgent = new OpenAIChatAgent(
            openAIClient: client,
            name: name,
            modelName: model)
        .RegisterMessageConnector()
        .RegisterPrintMessage();

        return new Coder(innerAgent);
    }

    public string Name => _innerAgent.Name;

    public async Task<IMessage> GenerateReplyAsync(IEnumerable<IMessage> messages, GenerateReplyOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lastMessage = messages.Last() ?? throw new InvalidOperationException("No message to reply to");

        if (lastMessage is EventMessage createTask && createTask.Type == EventType.CreateTask)
        {
            var task = createTask.Properties["task"];
            var prompt = $"""
                You are a helpful coder agent, you resolve tasks using python, powershell or csharp code.
                
                Here are rules that you need to follow:
                - always print the result of your code
                - always use code block to wrap your code
                
                ALWAYS put your code in a code block like this:
                ```<python|csharp|pwsh>
                print("Hello World")
                ```
                
                Using the following syntax to install pip packages:
                ```python
                %pip install <package-name>
                ```
                
                Using the following syntax to install nuget packages:
                ```csharp
                #r "nuget:<package-name>"
                ```

                Here is your task:
                {task}

                If the task can be resolved by writing code, please write the code. Otherwise, say 'I cannot resolve this task using code'.
                """;

            var reply = await this._innerAgent.SendAsync(prompt, [], cancellationToken);

            if (reply.GetContent()?.ToLower().Contains("i cannot resolve this task using code") is true)
            {
                return reply.ToEventMessage(EventType.NotCodingTask);
            }

            return reply.ToEventMessage(EventType.WriteCode, new Dictionary<string, string>()
            {
                ["task"] = task,
                ["code"] = reply.GetContent()!
            });
        }

        if (lastMessage is EventMessage fixCodeError && fixCodeError.Type == EventType.FixCodeError)
        {
            var task = fixCodeError.Properties["task"];
            var code = fixCodeError.Properties["code"];
            var error = fixCodeError.Properties["error"];

            var prompt = $"""
                ### Task
                {task}

                ### Code
                {code}

                ### Error
                {error}

                Your task is to fix the error in the code. Please write the corrected code and put it in a code block.

                ```<python|csharp|pwsh>
                # Write your corrected code here
                ```

                If you need search web for solution, say 'I need to search for solution'.
                """;

            var reply = await this._innerAgent.SendAsync(prompt, [], cancellationToken);

            if (reply.GetContent()?.ToLower().Contains("search for solution") is true)
            {
                return reply.ToEventMessage(EventType.SearchSolution, new Dictionary<string, string>()
                {
                    ["task"] = task,
                    ["code"] = code,
                    ["error"] = error,
                });
            }

            return reply.ToEventMessage(EventType.WriteCode, new Dictionary<string, string>()
            {
                ["task"] = task,
                ["code"] = reply.GetContent()!,
            });
        }

        if (lastMessage is EventMessage improveCode && improveCode.Type == EventType.ImproveCode)
        {
            var code = improveCode.Properties["code"];
            var improvement = improveCode.Properties["improvement"];

            var prompt = $"""
                ### Code
                {code}

                ### Improvement
                {improvement}

                Your task is to improve the code based on suggestions. Please write the improved code and put it in a code block.

                ```<python|csharp|pwsh>
                # Write your improved code here
                ```
                """;

            var reply = await this._innerAgent.SendAsync(prompt, [], cancellationToken);

            return reply.ToEventMessage(EventType.WriteCode, new Dictionary<string, string>()
            {
                ["task"] = improveCode.Properties["task"],
                ["code"] = reply.GetContent()!,
            });
        }

        throw new InvalidOperationException("Unexpected message type");
    }
}
