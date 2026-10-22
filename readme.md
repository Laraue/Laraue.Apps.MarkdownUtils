# Laraue.Apps.MarkdownUtils

The application provides Web API to makes Markdown files transformation.  
Built with [Laraue.Interpreter.Markdown](https://github.com/win7user10/Laraue.Interpreter) library.

### Endpoints
`POST: api/markdown-transpiler/transpile` - transform Markdown content to HTML  
`POST: api/markdown-translator/translate` - translate Markdown from one language to another 

### Rate limits
Both endpoints are limited per client address; over the limit the API answers `429` with `Retry-After` (seconds).
The limits are in the `RateLimiting` section of `appsettings.json` and are checked on start:
- `Translate` - a token bucket per client, a `DailyLimit` per client and a `ConcurrencyLimit` for all clients together
- `Transpile` - a looser token bucket per client

The client address is taken from `X-Forwarded-For`, only when the request comes from a proxy listed in
`ForwardedHeaders:KnownProxies` (addresses) or `ForwardedHeaders:KnownNetworks` (CIDR, e.g. `172.16.0.0/12` for a docker network).
Without them only loopback is trusted, and all requests coming through the proxy share one limit.

Every request is logged with the client address, user agent and body size (`API request ...` log lines);
the rejections are counted by `markdown_utils_rate_limit_rejections_total`.