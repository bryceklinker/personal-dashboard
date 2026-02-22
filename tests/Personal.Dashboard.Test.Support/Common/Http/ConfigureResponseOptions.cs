using System.Net;

namespace Personal.Dashboard.Test.Support.Common.Http;

public record ConfigureResponseOptions(
    HttpStatusCode? Status = null,
    Func<HttpRequestMessage, Task>? CaptureAsync = null,
    Action<HttpRequestMessage>? Capture = null
);