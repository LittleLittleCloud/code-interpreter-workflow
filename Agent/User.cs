using AutoGen.Core;
using AutoGen.OpenAI;
using AutoGen.OpenAI.Extension;
using Azure.AI.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet_interactive_agent.Agent;

internal class User : IAgent
{
    private readonly IAgent _innerAgent;
    public User(IAgent innerAgent)
    {
        _innerAgent = innerAgent;
    }

    public static User CreateFromOpenAI(OpenAIClient client, string model, string name = "user")
    {
        var innerAgent = new OpenAIChatAgent(
            openAIClient: client,
            name: name,
            modelName: model)
        .RegisterMessageConnector()
        .RegisterPrintMessage();

        return new User(innerAgent);
    }

    public string Name => _innerAgent.Name;

    public async Task<IMessage> GenerateReplyAsync(IEnumerable<IMessage> messages, GenerateReplyOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lastMessage = messages.Last() ?? throw new InvalidOperationException("No message to reply to");

        if (lastMessage is EventMessage writeCode && writeCode.Type == EventType.WriteCode)
        {
            var code = writeCode.GetContent() ?? throw new InvalidOperationException("No content in the message");
            var task = writeCode.Properties["task"];

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
                return writeCode.ToEventMessage(EventType.RunCode, new Dictionary<string, string>()
                {
                    ["task"] = task,
                    ["code"] = code,
                });
            }
            else
            {
                return reply.ToEventMessage(EventType.ImproveCode, new Dictionary<string, string>()
                {
                    ["task"] = task,
                    ["code"] = code,
                    ["improvement"] = reply.GetContent()!,
                });
            }
        }

        throw new InvalidOperationException("Invalid message type");
    }
}
