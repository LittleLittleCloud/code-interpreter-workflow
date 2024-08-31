using AutoGen.Core;
using AutoGen.DotnetInteractive.Extension;
using Json.Schema.Generation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace dotnet_interactive_agent;

public enum Step
{
    CreateTask = 0,
    WriteCode = 1,
    ReviewCode = 10,
    FixComment = 11,
    RunCode = 2,
    RunCodeResult = 3,
    Succeeded = 4,
    FixCodeError = 5,
    SearchSolution = 6,
    SearchSolutionResult = 7,
    ImproveCode = 8,
    NotATask = 9,
}

[Title("state")]
public class State
{
    [Description("current step")]
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Step CurrentStep { get; set; }

    [Description("task")]
    public string? Task { get; set; }

    [Description("code")]
    public string? Code { get; set; }

    [Description("code execution error")]
    public string? Error { get; set; }

    [Description("code execution result")]
    public string? RunCodeResult { get; set; }

    [Description("code review comment")]
    public string? Comment { get; set; }

    [Description("final answer")]
    public string? Answer { get; set; }

    [Description("web search result")]
    public string? WebSearchResult { get; set; }

    public static State NOT_A_TASK = new State { CurrentStep = Step.NotATask };
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
    private readonly IAgent planner;

    public STMOrchestrator(IAgent user, IAgent assistant, IAgent runner, IAgent coder, IAgent planner)
    {
        this.user = user;
        this.assistant = assistant;
        this.runner = runner;
        this.coder = coder;
        this.planner = planner;
    }

    public async Task<IAgent?> GetNextSpeakerAsync(OrchestrationContext context, CancellationToken cancellationToken = default)
    {
        var lastMessage = context.ChatHistory.LastOrDefault();
        var lastState = context.ChatHistory.LastOrDefault(x => x.From == this.planner.Name)?.GetState();

        // start
        if ((lastState is null
            || lastState.CurrentStep == Step.NotATask
            || lastState.CurrentStep == Step.Succeeded)
            && lastMessage?.From == this.user.Name)
        {
            return this.planner;
        }

        if (lastMessage?.From != this.planner.Name)
        {
            return this.planner;
        }

        if (lastState is State state)
        {
            return state.CurrentStep switch
            {
                Step.CreateTask => user,
                Step.WriteCode => coder,
                Step.RunCode => runner,
                Step.RunCodeResult => assistant,
                Step.ReviewCode => user,
                Step.NotATask => user,
                Step.Succeeded => user,
                Step.FixCodeError => coder,
                Step.FixComment => coder,
                _ => throw new InvalidOperationException("Invalid state type"),
            };
        }

        throw new InvalidOperationException("Invalid message type");
    }
}
