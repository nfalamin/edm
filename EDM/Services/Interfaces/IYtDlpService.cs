using System;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public interface IYtDlpService
    {
        Task DownloadAsync(string url, string outputPath, string formatArg, Action<int, string> progress, CancellationToken ct);
    }
}