namespace Deosrc.Tammi.Installation;

/// <summary>
/// How much of a download has arrived.
/// </summary>
/// <param name="BytesReceived">The bytes received so far.</param>
/// <param name="TotalBytes">The size of the download.</param>
public readonly record struct DownloadProgress(long BytesReceived, long TotalBytes);
