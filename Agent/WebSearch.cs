using AutoGen.Core;
using AutoGen.OpenAI;
using AutoGen.OpenAI.Extension;
using Azure.AI.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using AutoGen.SemanticKernel;
using Microsoft.SemanticKernel;

namespace dotnet_interactive_agent.Agent;

internal class WebSearch : IAgent
{
    private readonly IAgent _innerAgent;

    private WebSearch(IAgent innerAgent)
    {
        _innerAgent = innerAgent;
    }

    public static WebSearch CreateFromOpenAI(OpenAIClient client, string model, string bingApiKey, string name = "web-search")
    {
        var bingSearch = new BingConnector(bingApiKey);
        var webSearchPlugin = new WebSearchEnginePlugin(bingSearch);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(webSearchPlugin);
        var skPluginMiddleware = new KernelPluginMiddleware(
            kernel,
            kernelPlugin: KernelPluginFactory.CreateFromObject(webSearchPlugin));

        var innerAgent = new OpenAIChatAgent(
            openAIClient: client,
            name: name,
            modelName: model)
            .RegisterMessageConnector()
            .RegisterMiddleware(skPluginMiddleware)
            .RegisterPrintMessage();

        return new WebSearch(innerAgent);
    }
    public string Name => _innerAgent.Name;

    public async Task<IMessage> GenerateReplyAsync(IEnumerable<IMessage> messages, GenerateReplyOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lastMessage = messages.Last() ?? throw new InvalidOperationException("No message to reply to");

        if (lastMessage is EventMessage searchSolution && searchSolution.Type is EventType.SearchSolution)
        {
            var task = searchSolution.Properties["task"];
            var code = searchSolution.Properties["code"];
            var error = searchSolution.Properties["error"];

            var prompt = $"""
                You are a helpful web search agent, you search the solution based on the task and code.
                
                Here is the task:
                {task}
                
                Here is the code:
                {code}
                
                Here is the error message:
                {error}
                
                Please search the solution based on the task, code and error message from web.
                """;

            var reply = await this._innerAgent.SendAsync(prompt, [], cancellationToken);

            return reply.ToEventMessage(EventType.SearchSolutionResult, new Dictionary<string, string>()
            {
                ["task"] = task,
                ["code"] = code,
                ["error"] = error,
                ["solution"] = reply.GetContent()!
            });
        }

        throw new InvalidOperationException("Invalid message type");
    }
}
