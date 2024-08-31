using AutoGen.Core;
using AutoGen.DotnetInteractive.Extension;
using Json.Schema.Generation;
using System.Text.Json;

namespace dotnet_interactive_agent;

public enum Step
{
    CreateTask = 0,
    WriteCode = 1,
    RunCode = 2,
    ExecuteResult = 3,
    Succeeded = 4,
    FixCodeError = 5,
    SearchSolution = 6,
    SearchSolutionResult = 7,
    ImproveCode = 8,
    NotCodingTask = 9,
}

[Title("state")]
public class State
{
    [Description("current step")]
    [Required]
    public Step CurrentStep { get; set; }

    [Description("task")]
    public string? Task { get; set; }

    [Description("code")]
    public string? Code { get; set; }

    [Description("code execution error")]
    public string? Error { get; set; }

    [Description("code execution result")]
    public string? Result { get; set; }

    [Description("code review comment")]
    public string? Comment { get; set; }

    [Description("final answer")]
    public string? Answer { get; set; }

    [Description("web search result")]
    public string? WebSearchResult { get; set; }
}

public class EventMessage : IMessage, ICanGetTextContent
{
    public EventMessage(Step type, IMessage message, Dictionary<string, string>? properties)
    {
        this.From = message.From;
        Type = type;
        Message = message;
        Properties = properties ?? new Dictionary<string, string>();
    }

    public Dictionary<string, string> Properties { get; }

    public IMessage Message { get; }

    public Step Type { get; }

    public string? From { get; set; }

    public string? GetContent()
    {
        return Message.GetContent();
    }

    public override string ToString()
    {
        return Message?.ToString() ?? string.Empty;
    }
}

public static class MessageExtension
{
    public static State? GetState(this IMessage message)
    {
        // if content contains ```task and ```, then it is a task message
        if (message.ExtractCodeBlock("```task", "```") is string task)
        {
            return new State
            {
                CurrentStep = Step.CreateTask,
                Task = task,
            };
        }

        if (message.GetContent() is string json)
        {
            try
            {
                return JsonSerializer.Deserialize<State>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }

    public static TextMessage ToTextMessage(this State state, string from)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });

        return new TextMessage(Role.Assistant, json, from);
    }
}

public class STMOrchestrator : IOrchestrator
{
    private readonly IAgent user;
    private readonly IAgent assistant;
    private readonly IAgent runner;
    private readonly IAgent coder;
    private readonly IAgent webSearch;

    public STMOrchestrator(IAgent user, IAgent assistant, IAgent runner, IAgent coder, IAgent webSearch)
    {
        this.user = user;
        this.assistant = assistant;
        this.runner = runner;
        this.coder = coder;
        this.webSearch = webSearch;
    }

    public async Task<IAgent?> GetNextSpeakerAsync(OrchestrationContext context, CancellationToken cancellationToken = default)
    {
        var lastMessage = context.ChatHistory.LastOrDefault();
        if (lastMessage is null)
        {
            return user;
        }

        if (lastMessage.GetState() is State eventMessage)
        {
            return eventMessage.CurrentStep switch
            {
                Step.CreateTask => coder,
                Step.WriteCode => user,
                Step.RunCode => runner,
                Step.ExecuteResult => assistant,
                Step.NotCodingTask => null,
                Step.Succeeded => null,
                Step.FixCodeError => coder,
                Step.ImproveCode => coder,
                Step.SearchSolution => webSearch,
                Step.SearchSolutionResult => coder,
                _ => throw new InvalidOperationException("Invalid event type"),
            };
        }

        throw new InvalidOperationException("Invalid message type");
    }
}
