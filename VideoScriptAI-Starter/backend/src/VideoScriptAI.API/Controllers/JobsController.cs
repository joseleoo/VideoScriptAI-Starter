using Microsoft.AspNetCore.Mvc;
using VideoScriptAI.Application.Abstractions;

namespace VideoScriptAI.API.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(IJobQueue queue) : ControllerBase
{
    // Starter endpoint to verify the API and in-memory queue.
    // The upload/use-case endpoint is implemented in the next milestone.
    [HttpPost("queue-test")]
    public async Task<IActionResult> QueueTest(CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await queue.EnqueueAsync(id, cancellationToken);
        return Accepted(new { jobId = id, status = "queued" });
    }
}
