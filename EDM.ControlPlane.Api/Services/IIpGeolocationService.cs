using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EDM.ControlPlane.Api.Services
{
    public record GeoLocationResult(
        string CountryCode,
        string Region,
        string CountryName,
        bool IsHighConfidence,
        string ResolutionSource);

    public interface IIpGeolocationService
    {
        string ProviderName { get; }
        Task<GeoLocationResult> ResolveLocationAsync(string? ipAddress, IDictionary<string, string>? headers);
    }

    public class HeaderAndRangeGeoLocationService : IIpGeolocationService
    {
        public string ProviderName => "EdgeHeadersAndSubnetRanges";

        public Task<GeoLocationResult> ResolveLocationAsync(string? ipAddress, IDictionary<string, string>? headers)
        {
            // 1. Edge & Reverse-Proxy Headers (e.g. Cloudflare, NGINX, CloudFront)
            if (headers != null)
            {
                if (headers.TryGetValue("CF-IPCountry", out var cfCountry) && !string.IsNullOrWhiteSpace(cfCountry) && cfCountry.Length == 2)
                {
                    return Task.FromResult(new GeoLocationResult(
                        CountryCode: cfCountry.ToUpperInvariant(),
                        Region: MapRegion(cfCountry),
                        CountryName: MapCountryName(cfCountry),
                        IsHighConfidence: true,
                        ResolutionSource: "CF-IPCountry Header"));
                }

                if (headers.TryGetValue("X-Country-Code", out var xCountry) && !string.IsNullOrWhiteSpace(xCountry) && xCountry.Length == 2)
                {
                    return Task.FromResult(new GeoLocationResult(
                        CountryCode: xCountry.ToUpperInvariant(),
                        Region: MapRegion(xCountry),
                        CountryName: MapCountryName(xCountry),
                        IsHighConfidence: true,
                        ResolutionSource: "X-Country-Code Header"));
                }
            }

            // 2. Known ISP / Regional Subnet Ranges Fallback
            if (!string.IsNullOrWhiteSpace(ipAddress))
            {
                if (ipAddress.StartsWith("103.145.") || ipAddress.StartsWith("103.205.") || ipAddress.StartsWith("103.230."))
                    return Task.FromResult(new GeoLocationResult("BD", "South Asia", "Bangladesh", true, "SubnetRange"));
                if (ipAddress.StartsWith("103.21.") || ipAddress.StartsWith("103.22.") || ipAddress.StartsWith("49.204."))
                    return Task.FromResult(new GeoLocationResult("IN", "South Asia", "India", true, "SubnetRange"));
                if (ipAddress.StartsWith("111.119.") || ipAddress.StartsWith("175.107."))
                    return Task.FromResult(new GeoLocationResult("PK", "South Asia", "Pakistan", true, "SubnetRange"));
                if (ipAddress.StartsWith("82.165.") || ipAddress.StartsWith("212.58."))
                    return Task.FromResult(new GeoLocationResult("GB", "Europe", "United Kingdom", true, "SubnetRange"));
                if (ipAddress.StartsWith("142.250.") || ipAddress.StartsWith("172.217.") || ipAddress.StartsWith("8.8."))
                    return Task.FromResult(new GeoLocationResult("US", "North America", "United States", true, "SubnetRange"));
            }

            // 3. Fallback: Default Bangladesh Direct
            return Task.FromResult(new GeoLocationResult("BD", "South Asia", "Bangladesh", false, "DefaultRegionalFallback"));
        }

        private static string MapRegion(string countryCode)
        {
            return countryCode.ToUpperInvariant() switch
            {
                "BD" or "IN" or "PK" or "NP" or "LK" or "BT" or "MV" => "South Asia",
                "TH" or "VN" or "ID" or "MY" or "PH" or "SG" or "JP" or "KR" or "CN" or "TW" or "HK" or "ASIA" => "Asia",
                "US" or "CA" or "MX" => "North America",
                "GB" or "DE" or "FR" or "IT" or "ES" or "NL" or "SE" or "NO" or "PL" => "Europe",
                "AE" or "SA" or "QA" or "KW" or "OM" or "BH" => "Middle East",
                "ZA" or "NG" or "EG" or "KE" or "GH" => "Africa",
                "BR" or "AR" or "CL" or "CO" or "PE" => "South America",
                "AU" or "NZ" => "Oceania",
                _ => "Global"
            };
        }

        private static string MapCountryName(string countryCode)
        {
            return countryCode.ToUpperInvariant() switch
            {
                "BD" => "Bangladesh",
                "IN" => "India",
                "PK" => "Pakistan",
                "US" => "United States",
                "GB" => "United Kingdom",
                "DE" => "Germany",
                "FR" => "France",
                "ZA" => "South Africa",
                _ => countryCode
            };
        }
    }
}
