// EDM Chrome WebExtension Service Worker
chrome.downloads.onCreated.addListener((downloadItem) => {
    chrome.runtime.sendNativeMessage('com.edm.downloader', {
        action: 'intercept',
        url: downloadItem.url,
        filename: downloadItem.filename
    }, (response) => {
        if (response && response.status === 'handed_off') {
            chrome.downloads.cancel(downloadItem.id);
        }
    });
});
