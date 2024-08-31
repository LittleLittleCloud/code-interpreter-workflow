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

internal class Assistant : IAgent
{
    private readonly IAgent _innerAgent;

    private Assistant(IAgent innerAgent)
    {
        _innerAgent = innerAgent;
    }

    public static Assistant CreateFromOpenAI(ChatClient client, string name = "assistant")
    {
        var innerAgent = new OpenAIChatAgent(
            chatClient: client,
            name: name)
        .RegisterMessageConnector()
        .RegisterPrintMessage();

        return new Assistant(innerAgent);
    }

    public string Name => _innerAgent.Name;

    public async Task<IMessage> GenerateReplyAsync(IEnumerable<IMessage> messages, GenerateReplyOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lastMessage = messages.Last() ?? throw new InvalidOperationException("No message to reply to");
        var lastState = messages.LastOrDefault()?.GetState();

        if (lastState is State runCodeResult
            && runCodeResult.CurrentStep == Step.RunCodeResult
            && runCodeResult.Task is string
            && runCodeResult.RunCodeResult is string
            && runCodeResult.Code is string)
        {
            var prompt = $"""
                You are a helpful assistant agent, you generate the final answer based on the code execution result.
                
                Here is the task:
                {runCodeResult.Task}
                
                Here is the code execution result:
                {runCodeResult.RunCodeResult}

                If the code execute successfully, please generate the final answer. Otherwise, say 'code execution failed'.
                """;

            return await this._innerAgent.SendAsync(prompt, [], cancellationToken);
        }

        throw new InvalidOperationException("Invalid message type");
    }
}
