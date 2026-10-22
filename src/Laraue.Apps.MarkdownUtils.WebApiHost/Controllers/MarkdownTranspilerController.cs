using Laraue.Apps.MarkdownUtils.Contracts;
using Laraue.Apps.MarkdownUtils.Services;
using Microsoft.AspNetCore.Mvc;

namespace Laraue.Apps.MarkdownTranspiler.WebApi.Controllers;

[ApiController]
[Route("api/markdown-transpiler")]
public class MarkdownTranspilerController(IMarkdownTranspilerService transpilerService) : ControllerBase
{
    [HttpPost("transpile")]
    public MarkdownTranspileResponse Transpile([FromBody] MarkdownTranspileRequest request)
    {
        return transpilerService.Transpile(request);
    }
}