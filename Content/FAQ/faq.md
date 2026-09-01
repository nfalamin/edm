# Frequently Asked Questions (FAQ)

### 1. Why is EDM faster than regular browser downloads?
Browsers typically open a single socket per download. EDM dynamically splits files into up to 32 parallel range chunks, downloading all segments simultaneously to utilize the maximum available bandwidth.

### 2. Does EDM support video downloads from YouTube and Vimeo?
Yes, EDM includes an intelligent stream grabber that detects media playlists (HLS / MPEG-DASH) and muxes video and audio streams automatically.

### 3. Which browsers are supported?
Official Manifest V3 extensions are available for Google Chrome, Microsoft Edge Chromium, Brave, Opera, and Mozilla Firefox.
