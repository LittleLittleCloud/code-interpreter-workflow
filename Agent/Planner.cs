using AutoGen.Core;
using AutoGen.OpenAI;
using AutoGen.OpenAI.Extension;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet_interactive_agent.Agent;

internal class Planner : IAgent
{
    private readonly IAgent _innerAgent;
    public Planner(IAgent innerAgent)
    {
        _innerAgent = innerAgent;
    }

    public static Planner CreateFromOpenAI(ChatClient client, string name = "Planner")
    {
        var innerAgent = new OpenAIChatAgent(
            chatClient: client,
            name: name)
        .RegisterMessageConnector()
        .RegisterPrintMessage();

        return new Planner(innerAgent);
    }

    public string Name => _innerAgent.Name;

    public async Task<IMessage> GenerateReplyAsync(IEnumerable<IMessage> messages, GenerateReplyOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lastMessage = messages.Last() ?? throw new InvalidOperationException("No message to reply to");

        if (lastMessage.GetState() is State writeCode
            && writeCode.CurrentStep == Step.WriteCode
            && writeCode.Task is string task
            && writeCode.Code is string code)
        {
            var prompt = $"""
                ### Code
                {code}

                Review the code above to determine if the code
                - have security vulnerabilities
                - might break the system
                - print the result of the code
                - pip install block not comes first

                If the code is good, simply say 'The code is good' without anything else. Otherwise, suggest how to improve the code.
                """;

            var reply = await this._innerAgent.SendAsync(prompt, [], cancellationToken);

            if (reply.GetContent()?.ToLower().Contains("the code is good") is true)
            {
                var state = new State
                {
                    CurrentStep = Step.RunCode,
                    Task = task,
                    Code = code,
                };

                return state.ToTextMessage(this.Name);
            }
            else
            {
                var state = new State
                {
                    CurrentStep = Step.ImproveCode,
                    Task = task,
                    Code = code,
                    Comment = reply.GetContent()!,
                };

                return state.ToTextMessage(this.Name);
            }
        }

        throw new InvalidOperationException("Invalid message type");
    }
}
