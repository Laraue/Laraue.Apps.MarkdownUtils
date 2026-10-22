using Laraue.Apps.MarkdownTranspiler.WebApi.RateLimiting;
using Laraue.Apps.MarkdownUtils.Contracts;
using Laraue.Apps.MarkdownUtils.Services;
using Microsoft.AspNetCore.Mvc;

namespace Laraue.Apps.MarkdownTranspiler.WebApi.Controllers;

[ApiController]
[Route("api/markdown-translator")]
public class MarkdownTranslatorController(IMarkdownTranslatorService translatorService) : ControllerBase
{
    [HttpPost("translate")]
    [RateLimitedOperation(RateLimitedOperation.Translate)]
    public Task<MarkdownTranslateResponse> Translate([FromBody] MarkdownTranslateRequest request)
    {
        return translatorService.Translate(request);
    }
}