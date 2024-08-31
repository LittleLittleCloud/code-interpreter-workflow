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

        if (lastMessage.GetState() is State runCodeResult
            && runCodeResult.CurrentStep == Step.ExecuteResult
            && runCodeResult.Task is string task
            && runCodeResult.Result is string result
            && runCodeResult.Code is string code)
        {
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
                var fixCodeError = new State
                {
                    Code = code,
                    Task = task,
                    CurrentStep = Step.FixCodeError,
                    Error = result,
                };

                return fixCodeError.ToTextMessage(this.Name);
            }
            else
            {
                var succeed = new State
                {
                    Code = code,
                    Task = task,
                    CurrentStep = Step.Succeeded,
                    Result = result,
                    Answer = reply.GetContent()!,
                };

                return succeed.ToTextMessage(this.Name);
            }
        }

        throw new InvalidOperationException("Invalid message type");
    }
}
