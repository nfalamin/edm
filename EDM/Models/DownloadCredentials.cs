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

        public DownloadCredentials(string username, string password)
        {
            Username = username ?? string.Empty;
            Password = password ?? string.Empty;
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Username);

        /// <summary>Builds a standard RFC 7617 HTTP Basic authentication header value.</summary>
        public AuthenticationHeaderValue ToBasicAuthHeader()
        {
            var raw = $"{Username}:{Password}";
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
            return new AuthenticationHeaderValue("Basic", encoded);
        }

        public static DownloadCredentials? FromInput(string? username, string? password)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            return new DownloadCredentials(username.Trim(), password ?? string.Empty);
        }
    }
}
