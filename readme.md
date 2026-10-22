# Laraue.Apps.MarkdownUtils

The application provides Web API to makes Markdown files transformation.  
Built with [Laraue.Interpreter.Markdown](https://github.com/win7user10/Laraue.Interpreter) library.

### Endpoints
`POST: api/markdown-transpiler/transpile` - transform Markdown content to HTML  
`POST: api/markdown-translator/translate` - translate Markdown from one language to another 

### Rate limits
`translate` is limited per client address; over the limit the API answers `429` with `Retry-After` (seconds).
`transpile` is not limited. The limits are in the `RateLimiting` section of `appsettings.json` and are checked on start:
- `Translate:Windows` - per client, all of them apply (by default 3 per minute, 15 per 10 minutes, 30 per hour, 50 per day;
  fixed windows starting at the client's first request)
- `Translate:ConcurrencyLimit` - translations running at the same time, for all clients together

The client address is taken from `X-Forwarded-For`, only when the request comes from a proxy listed in
`ForwardedHeaders:KnownProxies` (addresses) or `ForwardedHeaders:KnownNetworks` (CIDR, e.g. `172.16.0.0/12` for a docker network).
Without them only loopback is trusted, and all requests coming through the proxy share one limit.

The rejections are counted by `markdown_translator_rate_limited_total{limit="client|concurrency"}`.
