using Microsoft.AspNetCore.Mvc;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.API.Controllers;

[ApiController]
[Route("api/meetings")]
public sealed class MeetingsController(
    IMeetingApplicationService meetings,
    ISummaryService summaries,
    SummaryOptions summaryOptions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await meetings.ListAsync(cancellationToken));

    [HttpGet("providers")]
    public IActionResult ListProviders() => Ok(summaries.ListProviders());

    [HttpGet("recording")]
    public IActionResult GetRecordingStatus() => Ok(meetings.GetRecordingStatus());

    [HttpGet("{id:guid}", Name = "GetMeeting")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var meeting = await meetings.GetAsync(id, cancellationToken);
        return meeting is null ? NotFound() : Ok(meeting);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2_147_483_648L)]
    public async Task<IActionResult> Create(
        IFormFile? file,
        [FromForm] string? title,
        [FromForm] string? language,
        [FromForm] string? summaryProvider,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Se requiere un archivo de grabación." });

        try
        {
            await using var stream = file.OpenReadStream();
            var meeting = await meetings.CreateFromUploadAsync(
                new UploadedAudio(file.FileName, stream),
                title ?? string.Empty,
                language ?? "auto",
                ResolveProvider(summaryProvider),
                cancellationToken);

            return AcceptedAtAction(nameof(Get), new { id = meeting.Id }, meeting);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("record/start")]
    [Consumes("application/json")]
    public async Task<IActionResult> StartRecording(
        [FromBody] CreateMeetingRequest? request,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return StatusCode(501, new { message = "La grabación en vivo solo está disponible en Windows." });

        try
        {
            var meeting = await meetings.StartRecordingAsync(
                request?.Title ?? string.Empty,
                request?.Language ?? "auto",
                ResolveProvider(request?.SummaryProvider),
                cancellationToken);

            return AcceptedAtAction(nameof(Get), new { id = meeting.Id }, meeting);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or PlatformNotSupportedException)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("record/stop")]
    public async Task<IActionResult> StopRecording(CancellationToken cancellationToken)
    {
        try
        {
            var meeting = await meetings.StopRecordingAsync(cancellationToken);
            return meeting is null
                ? BadRequest(new { message = "No hay una grabación en curso." })
                : AcceptedAtAction(nameof(Get), new { id = meeting.Id }, meeting);
        }
        catch (Exception exception) when (exception is InvalidOperationException or PlatformNotSupportedException)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var cancelled = await meetings.CancelAsync(id, cancellationToken);
        return cancelled ? Accepted() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await meetings.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPut("{id:guid}/segments")]
    public async Task<IActionResult> UpdateSegments(
        Guid id,
        [FromBody] UpdateMeetingSegmentsRequest request,
        CancellationToken cancellationToken)
    {
        var meeting = await meetings.UpdateSegmentsAsync(id, request, cancellationToken);
        return meeting is null ? NotFound() : Ok(meeting);
    }

    [HttpPut("{id:guid}/summary")]
    public async Task<IActionResult> UpdateSummary(
        Guid id,
        [FromBody] UpdateMeetingSummaryRequest request,
        CancellationToken cancellationToken)
    {
        var meeting = await meetings.UpdateSummaryAsync(id, request.Summary ?? string.Empty, cancellationToken);
        return meeting is null ? NotFound() : Ok(meeting);
    }

    [HttpPost("{id:guid}/summary/regenerate")]
    public async Task<IActionResult> RegenerateSummary(
        Guid id,
        [FromBody] RegenerateSummaryRequest? request,
        CancellationToken cancellationToken)
    {
        var meeting = await meetings.RegenerateSummaryAsync(id, request?.SummaryProvider, cancellationToken);
        return meeting is null ? NotFound() : Ok(meeting);
    }

    [HttpGet("{id:guid}/export/{format}")]
    public async Task<IActionResult> Export(Guid id, string format, CancellationToken cancellationToken)
    {
        try
        {
            var export = await meetings.PrepareExportAsync(id, format, cancellationToken);
            if (export is null)
                return NotFound();

            return PhysicalFile(export.Path, export.ContentType, export.FileName);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private string ResolveProvider(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? summaryOptions.GetDefaultProvider().ToString()
            : value;
}
