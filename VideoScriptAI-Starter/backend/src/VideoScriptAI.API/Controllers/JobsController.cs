using Microsoft.AspNetCore.Mvc;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.API.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(IJobApplicationService jobs) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await jobs.ListJobsAsync(cancellationToken));

    [HttpGet("{id:guid}", Name = "GetJob")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobs.GetJobAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2_147_483_648L)]
    public async Task<IActionResult> Create(
        IFormFile? file,
        [FromForm] string? language,
        [FromForm] string? model,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "A video file is required." });

        try
        {
            await using var stream = file.OpenReadStream();
            var job = await jobs.CreateJobAsync(
                new UploadedVideo(file.FileName, stream),
                language ?? "auto",
                model ?? "small",
                cancellationToken);

            return AcceptedAtAction(nameof(Get), new { id = job.Id }, job);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("from-url")]
    [Consumes("application/json")]
    public async Task<IActionResult> CreateFromUrl(
        [FromBody] CreateJobFromUrlRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "A request body is required." });

        try
        {
            var job = await jobs.CreateJobFromUrlAsync(
                new VideoUrl(request.Url ?? string.Empty),
                request.Language ?? "auto",
                request.Model ?? "small",
                cancellationToken);

            return AcceptedAtAction(nameof(Get), new { id = job.Id }, job);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var cancelled = await jobs.CancelJobAsync(id, cancellationToken);
        return cancelled ? Accepted() : NotFound();
    }

    [HttpPut("{id:guid}/transcript")]
    public async Task<IActionResult> UpdateTranscript(
        Guid id,
        [FromBody] UpdateTranscriptRequest request,
        CancellationToken cancellationToken)
    {
        var job = await jobs.UpdateTranscriptAsync(id, request, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpGet("{id:guid}/export/{format}")]
    public async Task<IActionResult> Export(
        Guid id,
        string format,
        CancellationToken cancellationToken)
    {
        try
        {
            var export = await jobs.PrepareExportAsync(id, format, cancellationToken);
            if (export is null)
                return NotFound();

            return PhysicalFile(export.Path, export.ContentType, export.FileName);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
