using System;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public interface IExternalBackendService
    {
        Task<bool> ValidateExecutableAsync(string path);
        Task StartAria2cAsync(string aria2cPath, string uri, string outputPath, string extraArgs, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
        Task StartYtDlpWithAria2Async(string ytDlpPath, string aria2cPath, string url, string outputDir, string formatArgs, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    }
}
