using AutoGen.Core;

namespace dotnet_interactive_agent;

public enum EventType
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

public class EventMessage : IMessage, ICanGetTextContent
{
    public EventMessage(EventType type, IMessage message, Dictionary<string, string>? properties)
    {
        this.From = message.From;
        Type = type;
        Message = message;
        Properties = properties ?? new Dictionary<string, string>();
    }

    public Dictionary<string, string> Properties { get; }

    public IMessage Message { get; }

    public EventType Type { get; }

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
    public static EventMessage ToEventMessage(this IMessage message, EventType type, Dictionary<string, string>? properties = null)
    {
        if (message is EventMessage em)
        {
            return new EventMessage(type, em.Message, properties);
        }

        return new EventMessage(type, message, properties);
    }
}

public class EventMessageMiddleware : IMiddleware
{
    public string? Name => nameof(EventMessageMiddleware);

    public async Task<IMessage> InvokeAsync(MiddlewareContext context, IAgent agent, CancellationToken cancellationToken = default)
    {
        var messages = context.Messages.Select(m =>
        {
            return m switch
            {
                EventMessage eventMessage => eventMessage.Message,
                _ => m,
            };
        }).ToList();


        return await agent.GenerateReplyAsync(messages, context.Options, cancellationToken);
    }
}

public class EventDrivenOrchestrator : IOrchestrator
{
    private readonly IAgent user;
    private readonly IAgent assistant;
    private readonly IAgent runner;
    private readonly IAgent coder;
    private readonly IAgent webSearch;

    public EventDrivenOrchestrator(IAgent user, IAgent assistant, IAgent runner, IAgent coder, IAgent webSearch)
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

        if (lastMessage is EventMessage eventMessage)
        {
            return eventMessage.Type switch
            {
                EventType.CreateTask => coder,
                EventType.WriteCode => user,
                EventType.RunCode => runner,
                EventType.ExecuteResult => assistant,
                EventType.NotCodingTask => user,
                EventType.Succeeded => user,
                EventType.FixCodeError => coder,
                EventType.ImproveCode => coder,
                EventType.SearchSolution => webSearch,
                EventType.SearchSolutionResult => coder,
                _ => throw new InvalidOperationException("Invalid event type"),
            };
        }

        throw new InvalidOperationException("Invalid message type");
    }
}
