using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Weaver.Application.Attachments;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Attachments;

/// <summary>
/// Reads an upload's <c>file</c> part straight off the request body, so the file streams to
/// storage instead of being buffered first, as model binding an <c>IFormFile</c> would.
/// </summary>
public sealed class MultipartFileReader(IOptions<AttachmentOptions> options)
{
    public const string FilePartName = "file";

    // Room for the multipart boundaries and part headers around a file at the full limit.
    private const long MultipartOverheadBytes = 1024 * 1024;

    // RFC 2046's limit on a boundary's length.
    private const int MaxBoundaryLength = 70;

    private readonly AttachmentOptions _options = options.Value;

    /// <summary>
    /// Hands the <c>file</c> part's file name, content type and body (in that order) to
    /// <paramref name="readFile"/>, which must consume the body before returning. A body that
    /// isn't <c>multipart/form-data</c> or has no such part is a <see cref="DomainValidationException"/>.
    /// </summary>
    public async Task<T> ReadFileAsync<T>(
        HttpRequest request,
        Func<string?, string?, Stream, Task<T>> readFile,
        CancellationToken ct = default)
    {
        RaiseBodyLimit(request.HttpContext);
        var reader = new MultipartReader(BoundaryOf(request), request.Body);
        try
        {
            while (await reader.ReadNextSectionAsync(ct) is { } section)
            {
                if (FilePart(section) is { } disposition)
                {
                    return await readFile(FileNameOf(disposition), section.ContentType, section.Body);
                }
            }
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            throw new AttachmentTooLargeException(_options.MaxFileSizeMegabytes);
        }

        throw new DomainValidationException($"Send the file as a multipart/form-data part named \"{FilePartName}\".");
    }

    /// <summary>
    /// Kestrel's default request limit (about 28.6 MB) would refuse a file under the configured
    /// maximum, so this one request may carry the maximum plus multipart framing. The file's own
    /// size is enforced exactly by the service as it streams.
    /// </summary>
    private void RaiseBodyLimit(HttpContext context)
    {
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = _options.MaxFileSizeBytes + MultipartOverheadBytes;
        }
    }

    private static string BoundaryOf(HttpRequest request)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType) ||
            !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException("An attachment must be uploaded as multipart/form-data.");
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > MaxBoundaryLength)
        {
            throw new DomainValidationException("The multipart/form-data body has no valid boundary.");
        }

        return boundary;
    }

    private static ContentDispositionHeaderValue? FilePart(MultipartSection section) =>
        ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) &&
        disposition.IsFileDisposition() &&
        HeaderUtilities.RemoveQuotes(disposition.Name).Equals(FilePartName, StringComparison.Ordinal)
            ? disposition
            : null;

    /// <summary>The RFC 5987 <c>filename*</c> when sent, since it's the one that carries non-ASCII names intact.</summary>
    private static string? FileNameOf(ContentDispositionHeaderValue disposition)
    {
        var encoded = HeaderUtilities.RemoveQuotes(disposition.FileNameStar);
        return encoded.HasValue && encoded.Length > 0
            ? encoded.Value
            : HeaderUtilities.RemoveQuotes(disposition.FileName).Value;
    }
}
