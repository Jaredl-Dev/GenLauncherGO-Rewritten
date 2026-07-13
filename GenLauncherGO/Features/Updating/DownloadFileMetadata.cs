using System;
using System.Net.Http.Headers;

namespace GenLauncherGO.Features.Updating;

internal sealed record DownloadFileMetadata(
    Uri DownloadUri,
    string FileName,
    long? TotalBytes,
    EntityTagHeaderValue? EntityTag = null);
