using System;
using System.Net.Http.Headers;

namespace GenLauncherGO.Features.Updating;

internal sealed record DownloadFileRequest(
    Uri SourceUri,
    string DestinationFilePath,
    long? ExpectedBytes = null,
    bool Resume = true,
    PackageDownloadPauseController? PauseController = null,
    EntityTagHeaderValue? IfRangeEntityTag = null);
