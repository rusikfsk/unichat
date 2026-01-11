using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace UniChat.Api.Services;

public sealed class MinioOptions
{
    public string Endpoint { get; init; } = default!;
    public string AccessKey { get; init; } = default!;
    public string SecretKey { get; init; } = default!;
    public bool UseSsl { get; init; } = false;
    public string Bucket { get; init; } = "unichat";
}

public sealed class MinioFileStorage : IFileStorage
{
    private readonly IMinioClient _minio;
    private readonly MinioOptions _opt;

    public MinioFileStorage(IOptions<MinioOptions> opt)
    {
        _opt = opt.Value;

        _minio = new MinioClient()
            .WithEndpoint(_opt.Endpoint)
            .WithCredentials(_opt.AccessKey, _opt.SecretKey)
            .WithSSL(_opt.UseSsl)
            .Build();
    }

    public async Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureBucket(ct);

        var put = new PutObjectArgs()
            .WithBucket(_opt.Bucket)
            .WithObject(objectKey)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType);

        await _minio.PutObjectAsync(put, ct);
    }

    public async Task<(Stream Stream, string ContentType)> GetAsync(string objectKey, CancellationToken ct = default)
    {
        await EnsureBucket(ct);

        // MinIO SDK читает через callback — копируем в MemoryStream.
        // Для очень больших файлов лучше делать streaming (можно сделаем позже).
        var ms = new MemoryStream();
        string ctOut = "application/octet-stream";

        var stat = await _minio.StatObjectAsync(new StatObjectArgs()
            .WithBucket(_opt.Bucket)
            .WithObject(objectKey), ct);

        ctOut = stat.ContentType ?? ctOut;

        var get = new GetObjectArgs()
            .WithBucket(_opt.Bucket)
            .WithObject(objectKey)
            .WithCallbackStream(stream => stream.CopyTo(ms));

        await _minio.GetObjectAsync(get, ct);
        ms.Position = 0;
        return (ms, ctOut);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken ct = default)
    {
        await EnsureBucket(ct);

        var rm = new RemoveObjectArgs()
            .WithBucket(_opt.Bucket)
            .WithObject(objectKey);

        await _minio.RemoveObjectAsync(rm, ct);
    }

    private async Task EnsureBucket(CancellationToken ct)
    {
        var exists = await _minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(_opt.Bucket), ct);
        if (!exists)
            await _minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(_opt.Bucket), ct);
    }
}
