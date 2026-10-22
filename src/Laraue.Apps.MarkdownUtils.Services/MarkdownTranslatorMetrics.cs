using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Laraue.Apps.MarkdownUtils.Services;

/// <summary>
/// Business metrics of the markdown translator, exported via OpenTelemetry.
/// </summary>
public sealed class MarkdownTranslatorMetrics
{
    public const string MeterName = "Laraue.Apps.MarkdownUtils.Translator";

    private readonly Counter<long> _translations;
    private readonly Histogram<double> _duration;
    private readonly Histogram<int> _documentSize;
    private readonly Counter<long> _tokens;
    private readonly Counter<long> _rateLimited;

    public MarkdownTranslatorMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _translations = meter.CreateCounter<long>(
            "markdown_translator.translations",
            unit: "{translation}",
            description: "Translation requests by source language, target language and result");

        _duration = meter.CreateHistogram<double>(
            "markdown_translator.translation.duration",
            unit: "s",
            description: "Time spent processing a translation request");

        _documentSize = meter.CreateHistogram<int>(
            "markdown_translator.document.size",
            unit: "{char}",
            description: "Size of the translated documents, input and output");

        _tokens = meter.CreateCounter<long>(
            "markdown_translator.tokens",
            unit: "{token}",
            description: "LLM tokens spent on translations");

        _rateLimited = meter.CreateCounter<long>(
            "markdown_translator.rate_limited",
            unit: "{request}",
            description: "Translation requests rejected with 429 by the rate limiter, by the limit that rejected them");
    }

    public void TranslationProcessed(string from, string to, TranslationResult result, TimeSpan duration)
    {
        var tags = new TagList
        {
            { "from", Normalize(from) },
            { "to", Normalize(to) },
            { "result", ToTagValue(result) },
        };

        _translations.Add(1, tags);
        _duration.Record(duration.TotalSeconds, tags);
    }

    public void DocumentTranslated(string from, string to, int inputSize, int outputSize, Usage usage)
    {
        var fromTag = Normalize(from);
        var toTag = Normalize(to);

        _documentSize.Record(inputSize, new TagList { { "from", fromTag }, { "to", toTag }, { "direction", "input" } });
        _documentSize.Record(outputSize, new TagList { { "from", fromTag }, { "to", toTag }, { "direction", "output" } });

        _tokens.Add(usage.PromptTokens, new KeyValuePair<string, object?>("type", "prompt"));
        _tokens.Add(usage.CompletionTokens, new KeyValuePair<string, object?>("type", "completion"));
    }

    public void TranslationRateLimited(RateLimit limit)
    {
        _rateLimited.Add(1, new KeyValuePair<string, object?>("limit", ToTagValue(limit)));
    }

    private static string Normalize(string language) => language.ToLowerInvariant();

    private static string ToTagValue(TranslationResult result) => result switch
    {
        TranslationResult.Success => "success",
        TranslationResult.BadRequest => "bad_request",
        _ => "failure",
    };

    private static string ToTagValue(RateLimit limit) => limit switch
    {
        RateLimit.Client => "client",
        _ => "concurrency",
    };
}

public enum TranslationResult
{
    Success,
    BadRequest,
    Failure,
}

public enum RateLimit
{
    /// <summary>
    /// The limits of one client address.
    /// </summary>
    Client,

    /// <summary>
    /// The limit of translations running at the same time, for all clients together.
    /// </summary>
    Concurrency,
}
