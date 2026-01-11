namespace UniChat.Api.Services;

public interface IFileStorage
{
    Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType)> GetAsync(string objectKey, CancellationToken ct = default);
    Task DeleteAsync(string objectKey, CancellationToken ct = default);
}
