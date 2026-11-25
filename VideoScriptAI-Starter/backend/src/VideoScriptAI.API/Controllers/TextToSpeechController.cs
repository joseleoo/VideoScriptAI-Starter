using Microsoft.AspNetCore.Mvc;
using VideoScriptAI.Application.Abstractions;
using VideoScriptAI.Application.Dtos;

namespace VideoScriptAI.API.Controllers;

[ApiController]
[Route("api/tts")]
public sealed class TextToSpeechController(ITextToSpeechService speech) : ControllerBase
{
    [HttpGet("voices")]
    public async Task<IActionResult> ListVoices(CancellationToken cancellationToken) =>
        Ok(await speech.GetVoicesAsync(cancellationToken));

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Synthesize(
        [FromBody] SynthesizeSpeechRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "A request body is required." });

        try
        {
            var result = await speech.SynthesizeAsync(request, cancellationToken);
            return PhysicalFile(result.Path, result.ContentType, result.FileName);
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or InvalidOperationException
                                          or InvalidDataException
                                          or FileNotFoundException)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
