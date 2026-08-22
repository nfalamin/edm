using System;
using System.Net.Http.Headers;
using System.Text;

namespace EDM.Models
{
    /// <summary>
    /// Optional per-download HTTP authentication (Basic auth). Used for links that
    /// sit behind a login wall (e.g. private file shares, internal servers, some
    /// premium hosting providers) - a feature IDM calls "Authorization" on the
    /// download properties dialog.
    /// </summary>
    public sealed class DownloadCredentials
    {
        public string Username { get; }
        public string Password { get; }
        public string Domain { get; }

        public DownloadCredentials(string username, string password, string? domain = null)
        {
            if (!string.IsNullOrWhiteSpace(username) && username.Contains('\\'))
            {
                var parts = username.Split('\\', 2);
                Domain = parts[0];
                Username = parts[1];
            }
            else
            {
                Domain = domain ?? string.Empty;
                Username = username ?? string.Empty;
            }
            Password = password ?? string.Empty;
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Username);

        /// <summary>Builds a standard RFC 7617 HTTP Basic authentication header value.</summary>
        public AuthenticationHeaderValue ToBasicAuthHeader()
        {
            var raw = !string.IsNullOrEmpty(Domain) ? $"{Domain}\\{Username}:{Password}" : $"{Username}:{Password}";
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
            return new AuthenticationHeaderValue("Basic", encoded);
        }

        /// <summary>Builds a standard System.Net.NetworkCredential for NTLM, Kerberos, and Digest auth.</summary>
        public System.Net.NetworkCredential ToNetworkCredential()
        {
            return string.IsNullOrEmpty(Domain)
                ? new System.Net.NetworkCredential(Username, Password)
                : new System.Net.NetworkCredential(Username, Password, Domain);
        }

        public static DownloadCredentials? FromInput(string? username, string? password, string? domain = null)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            return new DownloadCredentials(username.Trim(), password ?? string.Empty, domain);
        }
    }
}

