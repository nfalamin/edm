# EDM STAGE 4 — PROMPTS 11 & 12: UNIVERSAL INGESTION & WEB CRAWLER REPORT

## 1. Executive Summary

The Universal Download Ingestion Layer (`UniversalDownloadIngestionService.cs`) and Advanced Web Crawler Subsystem (`WebCrawlerSubsystem.cs`) have been implemented, unified, and verified.

---

## 2. Universal Download Ingestion Layer (`UniversalDownloadIngestionService.cs`)

1. **Unified DownloadRequest Abstraction:** Standardizes task requests originating from Browser Extensions, Clipboard Monitor, Drag-and-Drop, CLI, Batch Files, and the Drop Target Basket into a single pipeline.
2. **Clipboard URL Monitoring & Regex Extraction:** Detects and parses URL structures with automatic duplicate suppression on subsequent clipboard polls.
3. **Batch File & Drag-Drop Ingestion:** Recursively parses dropped URLs or `.txt`/`.edm` batch link files.
4. **Command-Line Interface (CLI):**
   - Supports `--url <URL>`, `--out <DIR>`, `--filename <NAME>`, `--queue <NAME>`, `--silent`, `--exit`, and `--batch <FILE>`.
   - Emits standardized machine-readable exit codes: `0 = Success`, `1 = MissingRequiredArgs`, `2 = SecurityValidationFailed`.
5. **Zero-Trust Input Sanitization:** Sanitizes filenames, normalizes paths against directory traversal, and enforces scheme allowlisting (`http`, `https`, `ftp`, `ftps`).

---

## 3. Web Crawler & Offline Mirror Engine (`WebCrawlerSubsystem.cs`)

1. **SSRF & Private IP Blocking:** Strictly blocks attempts to crawl localhost, loopback (`127.0.0.1`, `::1`), and RFC1918 private IPv4 subnets (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `169.254.0.0/16`).
2. **Recursive Asset Extraction:** Extracts HTML links, CSS `@import`/`url()`, images, scripts, fonts, and media assets.
3. **Same-Origin & Path Scoping:** Confines traversal strictly to the authorized target domain and max depth.
4. **Offline Mirror Manifest Generation:** Generates `mirror-manifest.json` mapping original web URLs to localized relative disk paths.
5. **Shared Hardened Engine:** Uses EDM's production `SharedHttpClient` and `DownloadService` without duplicating network engines.

---

## 4. Test Suite Summary

Executed under [`Stage4IngestionAndCrawlerTests.cs`](file:///D:/Project%202/10%20AUG%20-%202.07AM/5%20AUG/EDM/EDM.Tests/Services/Stage4IngestionAndCrawlerTests.cs):

```yaml
Suite: Stage4IngestionAndCrawlerTests
Total Tests: 3 / 3 PASSED (100% Success Rate)
Build Configuration: Release (net10.0-windows7.0)
Total Errors: 0
```
