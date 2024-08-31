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

        if (lastMessage.GetState() is State createTask
            && createTask.CurrentStep == Step.CreateTask
            && createTask.Task is string task)
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

            var reply = await this._innerAgent.SendAsync(prompt, [], cancellationToken);

            if (reply.GetContent()?.ToLower().Contains("i cannot resolve this task using code") is true)
            {
                var noCodingState = new State
                {
                    CurrentStep = Step.NotCodingTask,
                    Task = task,
                };

                return noCodingState.ToTextMessage(this.Name);
            }

            var state = new State
            {
                CurrentStep = Step.WriteCode,
                Task = task,
                Code = reply.GetContent()!,
            };

            return state.ToTextMessage(this.Name);
        }

        if (lastMessage.GetState() is State fixError
            && fixError.CurrentStep == Step.FixCodeError
            && fixError.Task is string task2
            && fixError.Code is string code
            && fixError.Error is string error)
        {
            var prompt = $"""
                ### Task
                {task2}

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
                var searchSolutionState = new State
                {
                    CurrentStep = Step.SearchSolution,
                    Task = task2,
                    Code = code,
                    Error = error,
                };

                return searchSolutionState.ToTextMessage(this.Name);
            }

            var state = new State
            {
                CurrentStep = Step.WriteCode,
                Task = task2,
                Code = reply.GetContent()!,
            };

            return state.ToTextMessage(this.Name);
        }

        if (lastMessage.GetState() is State improveCode
            && improveCode.CurrentStep == Step.ImproveCode
            && improveCode.Code is string code2
            && improveCode.Comment is string comment)
        {
            var prompt = $"""
                ### Code
                {code2}

                ### Improvement
                {comment}

                Your task is to improve the code based on suggestions. Please write the improved code and put it in a code block.

                ```<python|csharp|pwsh>
                # Write your improved code here
                ```
                """;

            var reply = await this._innerAgent.SendAsync(prompt, [], cancellationToken);
            var state = new State
            {
                CurrentStep = Step.WriteCode,
                Task = improveCode.Task,
                Code = reply.GetContent()!,
            };

            return state.ToTextMessage(this.Name);
        }

        throw new InvalidOperationException("Unexpected message type");
    }
}
