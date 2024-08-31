﻿using AutoGen.Core;
using AutoGen.OpenAI;
using AutoGen.OpenAI.Extension;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet_interactive_agent.Agent;


internal class StatePlanner : IAgent
{
    private readonly IAgent _innerAgent;
    public StatePlanner(IAgent innerAgent)
    {
        _innerAgent = innerAgent;
    }

    public static StatePlanner CreateFromOpenAI(ChatClient client, string name = "Planner")
    {
        var innerAgent = new OpenAIChatAgent(
            chatClient: client,
            name: name)
        .RegisterMessageConnector()
        .RegisterPrintMessage();

        return new StatePlanner(innerAgent);
    }

    public string Name => _innerAgent.Name;

    public async Task<IMessage> GenerateReplyAsync(IEnumerable<IMessage> messages, GenerateReplyOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lastMessage = messages.Last() ?? throw new InvalidOperationException("No message to reply to");

        // get the last state from chat history
        var lastState = messages.LastOrDefault(m => m.From == this.Name)?.GetState();

        // create_task -> write_code
        if ((lastState is null
            || lastState.CurrentStep == Step.Succeeded
            || lastState.CurrentStep == Step.NotATask)
            && lastMessage.GetContent() is string content)
        {
            var prompt = $"""
                summarize the task from user question, if it's not a question, say 'I can't help with that'.

                ```question
                {content}
                ```
                """;

            var reply = await this._innerAgent.SendAsync(prompt, [], cancellationToken);

            if (reply.GetContent()?.ToLower().Contains("i can't help with that") is true)
            {
                return State.NOT_A_TASK.ToTextMessage(this.Name);
            }
            else
            {

                return new State
                {
                    CurrentStep = Step.WriteCode,
                    Task = content,
                }.ToTextMessage(this.Name);
            }
        }

        // write_code -> review_code
        if (lastState is State writeCode
            && writeCode.CurrentStep == Step.WriteCode
            && writeCode.Task is string task
            && lastMessage.GetContent() is string code)
        {
            return new State
            {
                CurrentStep = Step.ReviewCode,
                Code = code,
                Task = task,
            }.ToTextMessage(this.Name);
        }

        // review_code
        if (lastState is State reviewCode
            && reviewCode.CurrentStep == Step.ReviewCode
            && reviewCode.Task is string
            && reviewCode.Code is string
            && lastMessage.GetContent() is string comment)
        {
            // review_code -> run_code
            if (comment.ToLower().Contains("the code is good"))
            {
                return new State
                {
                    CurrentStep = Step.RunCode,
                    Task = reviewCode.Task,
                    Code = reviewCode.Code,
                }.ToTextMessage(this.Name);
            }
            else
            {
                // review_code -> fix_comment
                return new State
                {
                    CurrentStep = Step.FixComment,
                    Task = reviewCode.Task,
                    Code = reviewCode.Code,
                    Comment = comment,
                }.ToTextMessage(this.Name);
            }
        }

        // run_code -> run_code_result
        if (lastState is State runCode
            && runCode.CurrentStep == Step.RunCode
            && runCode.Task is string
            && runCode.Code is string
            && lastMessage.GetContent() is string runCodeResult)
        {
            return new State
            {
                CurrentStep = Step.RunCodeResult,
                Task = runCode.Task,
                Code = runCode.Code,
                RunCodeResult = runCodeResult,
            }.ToTextMessage(this.Name);
        }

        // run_code_result
        if (lastState is State runCodeResultState
            && runCodeResultState.CurrentStep == Step.RunCodeResult
            && runCodeResultState.Task is string
            && runCodeResultState.Code is string
            && runCodeResultState.RunCodeResult is string
            && lastMessage.GetContent() is string replyContent)
        {
            // run_code_result -> fix_code
            if (replyContent.ToLower().Contains("code execution failed"))
            {
                return new State
                {
                    CurrentStep = Step.FixCodeError,
                    Task = runCodeResultState.Task,
                    Code = runCodeResultState.Code,
                    Error = replyContent,
                }.ToTextMessage(this.Name);
            }
            else
            {
                // run_code_result -> succeed
                return new State
                {
                    CurrentStep = Step.Succeeded,
                    Task = runCodeResultState.Task,
                    Code = runCodeResultState.Code,
                    RunCodeResult = runCodeResultState.RunCodeResult,
                    Answer = replyContent,
                }.ToTextMessage(this.Name);
            }
        }

        // fix_comment -> review_code
        if (lastState is State fixComment
            && fixComment.CurrentStep == Step.FixComment
            && fixComment.Task is string
            && fixComment.Code is string
            && fixComment.Comment is string
            && lastMessage.GetContent() is string fixedCommentCode)
        {
            return new State
            {
                CurrentStep = Step.ReviewCode,
                Task = fixComment.Task,
                Code = fixedCommentCode,
            }.ToTextMessage(this.Name);
        }

        // fix_code -> review_code
        if (lastState is State fixCodeError
            && fixCodeError.CurrentStep == Step.FixCodeError
            && fixCodeError.Task is string
            && fixCodeError.Code is string
            && fixCodeError.Error is string
            && lastMessage.GetContent() is string error)
        {
            return new State
            {
                CurrentStep = Step.ReviewCode,
                Task = fixCodeError.Task,
                Code = fixCodeError.Code,
                Error = error,
            }.ToTextMessage(this.Name);
        }

        throw new InvalidOperationException("Invalid message type");
    }
}
