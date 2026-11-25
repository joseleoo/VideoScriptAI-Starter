using VideoScriptAI.Application.Configuration;
using VideoScriptAI.Application.Dtos;
using VideoScriptAI.Infrastructure.Persistence;
using Xunit;

namespace VideoScriptAI.Infrastructure.Tests;

public sealed class VideoUrlStorageTests
{
    [Theory]
    [InlineData("file:///C:/video.mp4")]
    [InlineData("ftp://example.com/video.mp4")]
    [InlineData("https://user:pass@example.com/video.mp4")]
    [InlineData("not a url")]
    public async Task SaveUrl_rejects_invalid_urls(string url)
    {
        var options = new StorageOptions(
            Path.Combine(Path.GetTempPath(), $"vsa-url-test-{Guid.NewGuid():N}"),
            1024);
        var storage = new JobFileStorage(options, null!);

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.SaveUrlAsync(new VideoUrl(url), CancellationToken.None));
    }
}
