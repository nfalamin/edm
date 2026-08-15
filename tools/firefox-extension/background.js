// background.js - Firefox WebExtension Background Script for EDM
const NATIVE_HOST = "com.edm.downloader";
const MEDIA_REGEX = /\.(mp4|m3u8|mpd|webm|mp3|m4a|aac|flac|ts|mkv|pdf|zip|rar|exe|iso)(\?.*)?$/i;

const tabMediaStreams = new Map();

let settings = {
  enabled: true,
  excludedDomains: [],
  fileExtensions: ["mp4", "m3u8", "mpd", "mp3", "webm", "zip", "rar", "exe"]
};

const api = typeof browser !== "undefined" ? browser : chrome;

api.storage.local.get(["edmSettings"]).then((result) => {
  if (result && result.edmSettings) {
    settings = { ...settings, ...result.edmSettings };
  }
}).catch(() => {});

api.contextMenus.create({
  id: "edm_download_link",
  title: "Download with EDM",
  contexts: ["link", "image", "video", "audio"]
});

api.contextMenus.create({
  id: "edm_download_selected",
  title: "Download selected with EDM",
  contexts: ["selection"]
});

api.contextMenus.create({
  id: "edm_download_all",
  title: "Download all links with EDM",
  contexts: ["page"]
});

api.contextMenus.onClicked.addListener((info, tab) => {
  if (!tab) return;

  if (info.menuItemId === "edm_download_link" && info.linkUrl) {
    sendToNativeHost(info.linkUrl, deriveFilename(info.linkUrl), "Context Menu Link");
  } else if (info.menuItemId === "edm_download_selected" && info.selectionText) {
    const urls = info.selectionText.match(/https?:\/\/[^\s]+/g);
    if (urls) {
      urls.forEach(url => sendToNativeHost(url, deriveFilename(url), "Selected Link"));
    }
  } else if (info.menuItemId === "edm_download_all" && tab.id) {
    api.tabs.sendMessage(tab.id, { action: "EXTRACT_ALL_LINKS" }).then((response) => {
      if (response && response.links) {
        response.links.forEach(url => sendToNativeHost(url, deriveFilename(url), "Download All"));
      }
    }).catch(() => {});
  }
});

api.webRequest.onBeforeRequest.addListener(
  (details) => {
    if (!settings.enabled || !details.url || !details.tabId || details.tabId < 0) return;

    if (MEDIA_REGEX.test(details.url)) {
      let streams = tabMediaStreams.get(details.tabId) || [];
      if (!streams.some(s => s.url === details.url)) {
        let quality = "Media Stream";
        if (details.url.endsWith(".m3u8")) quality = "HLS Stream (M3U8)";
        else if (details.url.endsWith(".mpd")) quality = "DASH Stream (MPD)";
        else if (details.url.includes("1080")) quality = "1080p Full HD";
        else if (details.url.includes("720")) quality = "720p HD";

        streams.push({
          url: details.url,
          quality: quality,
          filename: deriveFilename(details.url),
          timestamp: Date.now()
        });

        tabMediaStreams.set(details.tabId, streams);

        api.tabs.sendMessage(details.tabId, {
          action: "MEDIA_DETECTED",
          streams: streams
        }).catch(() => {});
      }
    }
  },
  { urls: ["<all_urls>"] }
);

api.runtime.onMessage.addListener((request, sender) => {
  if (request.action === "START_EDM_DOWNLOAD") {
    sendToNativeHost(request.url, request.filename, request.quality);
    return Promise.resolve({ status: "SENT_TO_EDM" });
  } else if (request.action === "GET_MEDIA_STREAMS") {
    const tabId = sender.tab ? sender.tab.id : null;
    const streams = tabId ? (tabMediaStreams.get(tabId) || []) : [];
    return Promise.resolve({ streams: streams, settings: settings });
  } else if (request.action === "UPDATE_SETTINGS") {
    settings = { ...settings, ...request.settings };
    api.storage.local.set({ edmSettings: settings });
    return Promise.resolve({ status: "UPDATED" });
  }
  return true;
});

api.downloads.onCreated.addListener((item) => {
  try {
    if (!settings.enabled) return;

    const url = item.url || "";
    // Handle Blob URL safe fallback: Blob URIs cannot be fetched directly by external HTTP engines
    if (url.startsWith("blob:")) {
      console.info("[EDM Firefox Background] Blob URL detected. Allowing browser native engine to process safely.");
      return;
    }

    if (!url || !/^https?:\/\//i.test(url)) return;

    try {
      const hostname = new URL(url).hostname;
      if (settings.excludedDomains.some(d => hostname.includes(d))) return;
    } catch (e) {}

    const correlationId = "edm_corr_ff_" + Date.now() + "_" + Math.random().toString(36).substring(2, 9);
    const filename = item.filename || deriveFilename(url);

    sendToNativeHostWithHandshake(correlationId, url, filename, "Browser Intercept", (success) => {
      if (success) {
        api.downloads.cancel(item.id).catch((err) => {
          console.warn("[EDM Firefox Background] Download cancel notice:", err);
        });
      } else {
        console.warn("[EDM Firefox Background] EDM Host handoff failed. Allowing native browser download to continue safely.");
      }
    });
  } catch (err) {
    console.error("[EDM Firefox Background] Interception error:", err);
  }
});

function sendToNativeHost(url, filename, quality) {
  const correlationId = "edm_corr_ff_" + Date.now() + "_" + Math.random().toString(36).substring(2, 9);
  sendToNativeHostWithHandshake(correlationId, url, filename, quality, null);
}

function sendToNativeHostWithHandshake(correlationId, url, filename, quality, callback) {
  if (api.cookies && api.cookies.getAll) {
    api.cookies.getAll({ url: url }).then((cookies) => {
      const cookieHeader = (cookies || []).map((c) => `${c.name}=${c.value}`).join("; ");
      dispatchNativeMessageWithHandshake(correlationId, url, filename, quality, cookieHeader, callback);
    }).catch(() => {
      dispatchNativeMessageWithHandshake(correlationId, url, filename, quality, "", callback);
    });
    return;
  }
  dispatchNativeMessageWithHandshake(correlationId, url, filename, quality, "", callback);
}

function dispatchNativeMessageWithHandshake(correlationId, url, filename, quality, cookies, callback) {
  const message = {
    action: "DOWNLOAD_REQUEST",
    correlationId: correlationId,
    url: url,
    filename: filename,
    quality: quality,
    cookies: cookies,
    source: "FirefoxExtension",
    timestamp: new Date().toISOString()
  };

  try {
    api.runtime.sendNativeMessage(NATIVE_HOST, message).then((response) => {
      const isSuccess = response && response.success === true;
      if (callback) callback(isSuccess);
    }).catch((err) => {
      console.warn("[EDM Firefox] Native Messaging warning:", err);
      if (callback) callback(false);
    });
  } catch (e) {
    if (callback) callback(false);
  }
}

function deriveFilename(url) {
  try {
    const parsed = new URL(url);
    const pathname = parsed.pathname;
    const segments = pathname.split("/").filter(Boolean);
    if (segments.length > 0) {
      const last = segments[segments.length - 1];
      if (last.includes(".")) return decodeURIComponent(last);
    }
  } catch (e) {}
  return "EDM_Media_" + Date.now() + ".mp4";
}
