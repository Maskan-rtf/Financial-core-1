using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using BuildingBlocks.Domain.Abstractions;
using Core.Infrastructure.Common;
using Microsoft.Extensions.Configuration;

namespace Core.Infrastructure.Storage;

public sealed class LiaraObjectStorage : ILiaraObjectStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonS3? _presignGetClient;
    private readonly string _bucketName;
    private readonly string _endpointUrl;
    private readonly IClock _clock;
    private readonly bool _useVirtualHostPresign;

    public LiaraObjectStorage(IConfiguration configuration, IClock clock)
    {
        _clock = clock;

        _bucketName = configuration["LiaraStorage:BucketName"]
                      ?? throw new InvalidOperationException(InfrastructureMessages.StorageBucketMissing);
        var accessKey = configuration["LiaraStorage:AccessKey"]
                        ?? throw new InvalidOperationException(InfrastructureMessages.StorageAccessKeyMissing);
        var secretKey = configuration["LiaraStorage:SecretKey"]
                        ?? throw new InvalidOperationException(InfrastructureMessages.StorageSecretKeyMissing);
        _endpointUrl = configuration["LiaraStorage:EndpointUrl"]?.TrimEnd('/')
                       ?? throw new InvalidOperationException(InfrastructureMessages.StorageEndpointMissing);
        _useVirtualHostPresign = configuration.GetValue("LiaraStorage:UseVirtualHostPresign", true);

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = _endpointUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        };
        _s3Client = new AmazonS3Client(credentials, config);

        if (_useVirtualHostPresign)
        {
            var presignConfig = new AmazonS3Config
            {
                ServiceURL = _endpointUrl,
                ForcePathStyle = false,
                AuthenticationRegion = "us-east-1"
            };
            _presignGetClient = new AmazonS3Client(credentials, presignConfig);
        }
    }

    public Task<(string Url, DateTimeOffset ExpiresAtUtc)> PresignPutAsync(string key, string contentType, TimeSpan expiresIn, CancellationToken cancellationToken)
    {
        var expiresAt = _clock.UtcNow.Add(expiresIn);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            ContentType = contentType
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult((url, expiresAt));
    }

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, cancellationToken);
    }

    public async Task EnsureBrowserCorsAsync(IEnumerable<string> allowedOrigins, CancellationToken cancellationToken)
    {
        var origins = allowedOrigins
            .Select(x => (x ?? string.Empty).Trim().TrimEnd('/'))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (origins.Count == 0)
            return;

        await _s3Client.PutCORSConfigurationAsync(new PutCORSConfigurationRequest
        {
            BucketName = _bucketName,
            Configuration = new CORSConfiguration
            {
                Rules =
                [
                    new CORSRule
                    {
                        AllowedOrigins = origins,
                        AllowedMethods = ["GET", "PUT", "POST", "HEAD", "DELETE"],
                        AllowedHeaders = ["*"],
                        ExposeHeaders = ["ETag", "x-amz-request-id", "x-amz-version-id"],
                        MaxAgeSeconds = 3600
                    }
                ]
            }
        }, cancellationToken);
    }

    public Task<(string Url, DateTimeOffset ExpiresAtUtc)> PresignGetAsync(string key, TimeSpan expiresIn, CancellationToken cancellationToken)
    {
        var expiresAt = _clock.UtcNow.Add(expiresIn);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,
            Protocol = Protocol.HTTPS
        };
        var client = _presignGetClient ?? _s3Client;
        var url = client.GetPreSignedURL(request);
        return Task.FromResult((url, expiresAt));
    }

    public async Task<ObjectMetadata?> GetObjectMetadataAsync(string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        try
        {
            var response = await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = key
            }, cancellationToken);

            return new ObjectMetadata(response.ContentLength, response.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (IsMissingObject(ex))
        {
            return null;
        }
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var response = await _s3Client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        }, cancellationToken);

        return response.ResponseStream;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);

            var metadata = await GetObjectMetadataAsync(key, cancellationToken);
            if (metadata is { ContentLength: > 0 })
                return true;
        }

        return false;
    }

    private static bool IsMissingObject(AmazonS3Exception ex)
        => ex.StatusCode is System.Net.HttpStatusCode.NotFound
           || string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase)
           || string.Equals(ex.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase);

    public string GetPermanentUrl(string key)
    {
        var endpointUri = new Uri(_endpointUrl);
        var host = endpointUri.Host;
        return $"https://{_bucketName}.{host}/{key}";
    }
}

