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

internal class Assistant : IAgent
{
    private readonly IAgent _innerAgent;

    private Assistant(IAgent innerAgent)
    {
        _innerAgent = innerAgent;
    }

    public static Assistant CreateFromOpenAI(OpenAIClient client, string model, string name = "assistant")
    {
        var innerAgent = new OpenAIChatAgent(
            openAIClient: client,
            name: name,
            modelName: model)
        .RegisterMessageConnector()
        .RegisterPrintMessage();

        return new Assistant(innerAgent);
    }

    public string Name => _innerAgent.Name;

    public async Task<IMessage> GenerateReplyAsync(IEnumerable<IMessage> messages, GenerateReplyOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lastMessage = messages.Last() ?? throw new InvalidOperationException("No message to reply to");

        if (lastMessage is EventMessage runCodeResult && runCodeResult.Type == EventType.ExecuteResult)
        {
            var task = runCodeResult.Properties["task"];
            var result = runCodeResult.GetContent() ?? throw new InvalidOperationException("No content in the message");
            var code = runCodeResult.Properties["code"];
            var prompt = $"""
                You are a helpful assistant agent, you generate the final answer based on the code execution result.
                
                Here is the task:
                {task}
                
                Here is the code execution result:
                {result}

                If the code execute successfully, please generate the final answer. Otherwise, say 'code execution failed'.
                """;

            var reply = await this._innerAgent.SendAsync(prompt, [], cancellationToken);

            if (reply.GetContent()?.ToLower().Contains("code execution failed") is true)
            {
                return reply.ToEventMessage(EventType.FixCodeError, new Dictionary<string, string>()
                {
                    ["task"] = task,
                    ["code"] = code,
                    ["error"] = result,
                });
            }
            else
            {
                return reply.ToEventMessage(EventType.Succeeded, new Dictionary<string, string>()
                {
                    ["task"] = task,
                    ["code"] = code,
                    ["result"] = result,
                });
            }
        }

        throw new InvalidOperationException("Invalid message type");
    }
}
