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

    [Fact]
    public async Task DeleteAsync_removes_stored_video_and_rejects_outside_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"vsa-delete-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "uploads"));

        try
        {
            var storage = new JobFileStorage(new StorageOptions(root, 1024 * 1024), null!);
            var path = Path.Combine(root, "uploads", "sample.mp4");
            await File.WriteAllTextAsync(path, "video");

            await storage.DeleteAsync(path, CancellationToken.None);
            Assert.False(File.Exists(path));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                storage.DeleteAsync(Path.Combine(root, "outside.mp4"), CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
