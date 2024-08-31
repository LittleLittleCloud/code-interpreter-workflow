using AutoGen.Core;
using AutoGen.OpenAI;
using AutoGen.OpenAI.Extension;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace dotnet_interactive_agent.Agent;

internal class Coder : IAgent
{
    private readonly IAgent _innerAgent;

    private Coder(IAgent innerAgent)
    {
        _innerAgent = innerAgent;
    }

    public static Coder CreateFromOpenAI(ChatClient client, string name = "coder")
    {
        var innerAgent = new OpenAIChatAgent(
            chatClient: client,
            name: name)
        .RegisterMessageConnector()
        .RegisterPrintMessage();

        return new Coder(innerAgent);
    }

    public string Name => _innerAgent.Name;

    public async Task<IMessage> GenerateReplyAsync(IEnumerable<IMessage> messages, GenerateReplyOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lastMessage = messages.Last() ?? throw new InvalidOperationException("No message to reply to");
        var lastState = messages.LastOrDefault()?.GetState();
        if (lastState is State writeCode
            && writeCode.CurrentStep == Step.WriteCode
            && writeCode.Task is string task)
        {
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

            return await this._innerAgent.SendAsync(prompt, [], cancellationToken);
        }

        if (lastState is State fixCode
            && fixCode.CurrentStep == Step.FixCodeError
            && fixCode.Task is string
            && fixCode.Code is string
            && fixCode.Error is string)
        {
            var prompt = $"""
                ### Task
                {fixCode.Task}

                ### Code
                {fixCode.Code}

                ### Error
                {fixCode.Error}

                Your task is to fix the error in the code. Please write the corrected code and put it in a code block.

                ```<python|csharp|pwsh>
                # Write your corrected code here
                ```

                If you need search web for solution, say 'I need to search for solution'.
                """;

            return await this._innerAgent.SendAsync(prompt, [], cancellationToken);
        }

        if (lastState is State fixComment
            && fixComment.CurrentStep == Step.FixComment
            && fixComment.Code is string
            && fixComment.Comment is string)
        {
            var prompt = $"""
                ### Code
                {fixComment.Code}

                ### Improvement
                {fixComment.Comment}

                Your task is to improve the code based on suggestions. Please write the improved code and put it in a code block.

                ```<python|csharp|pwsh>
                # Write your improved code here
                ```
                """;

            return await this._innerAgent.SendAsync(prompt, [], cancellationToken);
        }

        throw new InvalidOperationException("Unexpected message type");
    }
}
