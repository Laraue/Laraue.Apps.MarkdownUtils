using System.Diagnostics;
using Laraue.Apps.MarkdownUtils.Contracts;
using Laraue.Core.Exceptions.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Laraue.Apps.MarkdownUtils.Services;

public interface IMarkdownTranslatorService
{
    public Task<MarkdownTranslateResponse> Translate(MarkdownTranslateRequest request);
}

public class MarkdownTranslatorService(
    IOpenAiClient openAiClient,
    ILogger<MarkdownTranslatorService> logger,
    IOptions<MarkdownTranslatorOptions> options,
    MarkdownTranslatorMetrics metrics)
    : IMarkdownTranslatorService
{
    private const string SystemPrompt = @"Perform the translation of markdown starting from the next row from {0} to {1}.
Keep original formatting layout";
    
    public async Task<MarkdownTranslateResponse> Translate(MarkdownTranslateRequest request)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var result = TranslationResult.Failure;

        try
        {
            var response = await TranslateInternal(request);
            result = TranslationResult.Success;

            return response;
        }
        catch (BadRequestException)
        {
            result = TranslationResult.BadRequest;
            throw;
        }
        finally
        {
            metrics.TranslationProcessed(request.From, request.To, result, Stopwatch.GetElapsedTime(startedAt));
        }
    }

    private async Task<MarkdownTranslateResponse> TranslateInternal(MarkdownTranslateRequest request)
    {
        if (request.From == request.To)
            throw new BadRequestException(
                nameof(request.To),
                "Translation could not be performed to the same language as selected in 'From'");

        var prompt = string.Format(SystemPrompt, request.From, request.To);
        
        var response = await openAiClient.ChatCompletion(new ChatCompletionRequest
        {
            MaxTokens = Constraints.MaxFreeTranslationOutput,
            Messages =
            [
                new Message { Role = "system", Content = prompt },
                new Message { Role = "user", Content = request.Content },
            ],
            Model = options.Value.Model,
            Stream = false,
            Thinking = new Thinking
            {
                Type = options.Value.Thinking ? "enabled" : "disabled"
            },
        });
        
        logger.LogInformation("Translation completed. Usage: {Usage}", response.Usage);

        var content = response.Choices[0].Message.Content;

        metrics.DocumentTranslated(request.From, request.To, request.Content.Length, content.Length, response.Usage);

        return new MarkdownTranslateResponse
        {
            Content = content
        };
    }
}