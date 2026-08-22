# EDM Production Store Publication & Code Signing Guide

This guide provides the exact step-by-step instructions for completing external code signing and store publishing for **EDM (Exclusive Download Manager)**.

---

## 1. Authenticode Code Signing (Windows Executables & DLLs)

### Prerequisites:
- EV (Extended Validation) Code Signing Certificate (`.pfx` file or Hardware Token).

### Steps:
1. Export your EV certificate as `.pfx` or connect your Hardware Security Module (HSM).
2. Set environment variables in your terminal / CI pipeline:
   ```cmd
   set EDM_SIGNING_CERT_PATH=C:\Path\To\YourCertificate.pfx
   set EDM_SIGNING_CERT_PASSWORD=YourSecurePassword
   ```
3. Run the automated production signing script:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\SignRelease.ps1
   ```
4. Verify digital signatures:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\VerifyReleaseSignature.ps1
   ```

---

## 2. Chrome Web Store Publication

### Artifact:
- `Output/EDM_Chrome_Extension_v1.0.0.zip` (Manifest V3)

### Steps:
1. Log in to the [Chrome Web Store Developer Dashboard](https://chrome.google.com/webstore/devconsole/).
2. Click **Add new item** and upload `Output/EDM_Chrome_Extension_v1.0.0.zip`.
3. Complete the Store Listing metadata (Name, Summary, Detailed Description, Screenshots).
4. Fill out the **Privacy practices** tab (declare `downloads` and `nativeMessaging` permissions).
5. Submit for Review.
6. Once published, copy your Extension ID (e.g. `knldjmfmopnpolahpmmgbagdohdnhkda`) and update `allowed_origins` in `%AppData%\EDM\NativeHost\com.edm.downloader.json`.

---

## 3. Firefox Add-ons (AMO) Publication

### Artifact:
- `Output/EDM_Firefox_Extension_v1.0.0.zip` (WebExtension)

### Steps:
1. Log in to the [Mozilla Add-on Developer Hub](https://addons.mozilla.org/developers/).
2. Click **Submit a New Add-on**.
3. Select **On this site** (Public distribution).
4. Upload `Output/EDM_Firefox_Extension_v1.0.0.zip`.
5. Verify that `gecko.id` matches `edm-extension@edm.app`.
6. Complete store description and submit for automatic/manual review.
