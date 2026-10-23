using System.Net;

namespace PokeRogue.DataTool.Download;

/// <summary>HTTP downloads with an on-disk cache, limited parallelism and retries.</summary>
public sealed class Downloader : IDisposable
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate;

    public Downloader(int maxParallel = 6)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; PokeRogue-DataTool/1.0)");
        _gate = new SemaphoreSlim(maxParallel);
    }

    public bool Force { get; init; }

    /// <summary>Downloads <paramref name="url"/> to <paramref name="destination"/> unless it is already cached. Returns false on 404.</summary>
    public async Task<bool> FetchAsync(string url, string destination, CancellationToken ct = default)
    {
        if (!Force && File.Exists(destination) && new FileInfo(destination).Length > 0)
            return true;

        await _gate.WaitAsync(ct);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        return false;

                    response.EnsureSuccessStatusCode();
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    var temp = destination + ".part";
                    await using (var file = File.Create(temp))
                        await response.Content.CopyToAsync(file, ct);

                    File.Move(temp, destination, overwrite: true);
                    return true;
                }
                catch (Exception ex) when (attempt < 4 && ex is HttpRequestException or TaskCanceledException)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetStringAsync(string url, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await _http.GetStringAsync(url, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }
}
