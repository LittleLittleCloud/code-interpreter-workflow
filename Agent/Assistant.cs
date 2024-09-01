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

        if (lastState is State succeed
            && succeed.CurrentStep == Step.Succeeded
            && succeed.Task is string
            && succeed.RunCodeResult is string
            && succeed.Code is string)
        {
            var prompt = $"""
                You are a helpful assistant agent, you generate the final answer based on the code execution result.
                
                Here is the task:
                {succeed.Task}
                
                Here is the code execution result:
                {succeed.RunCodeResult}
                """;

            return await this._innerAgent.SendAsync(prompt, [], cancellationToken);
        }

        if (lastState is State notCodingTask
            && notCodingTask.CurrentStep == Step.NotATask)
        {
            var prompt = $"""
                The task can't be solved by coding, please ask another question.
                """;

            return new TextMessage(Role.Assistant, prompt, from: this.Name);
        }

        if (lastState is State reviewCode
            && reviewCode.CurrentStep == Step.ReviewCode
            && reviewCode.Task is string task
            && reviewCode.Code is string code)
        {
            var prompt = $"""
                Here is the code to review:

                {code}

                Approve and run the code by saying "the code is good". Otherwise, make suggestions to improve the code.
                """;

            return new TextMessage(Role.Assistant, prompt, from: this.Name);
        }

        throw new InvalidOperationException("Invalid message type");
    }
}
