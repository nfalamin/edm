<?php
/**
 * Dashboard Action Modals Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<!-- 1. USER CREATE/EDIT MODAL -->
<div class="dash-modal-backdrop" id="modal-user" style="display: none;" role="dialog" aria-modal="true">
    <div class="dash-modal-card">
        <div class="dash-modal-header">
            <h3 id="modal-user-title"><?php esc_html_e('Create / Edit User', 'edm-theme'); ?></h3>
            <button type="button" class="btn-close-modal" onclick="if(window.edmDashboard) window.edmDashboard.closeModal('modal-user');"><i data-lucide="x"></i></button>
        </div>
        <div class="dash-modal-body">
            <form id="form-user-crud" onsubmit="event.preventDefault(); if(window.edmDashboard) window.edmDashboard.saveUser();">
                <div class="form-group">
                    <label><?php esc_html_e('Full Name', 'edm-theme'); ?></label>
                    <input type="text" id="user-input-name" class="form-control" placeholder="Jane Doe" required />
                </div>
                <div class="form-group">
                    <label><?php esc_html_e('Email Address', 'edm-theme'); ?></label>
                    <input type="email" id="user-input-email" class="form-control" placeholder="user@company.com" required />
                </div>
                <div class="form-row-2">
                    <div class="form-group">
                        <label><?php esc_html_e('Plan Tier', 'edm-theme'); ?></label>
                        <select id="user-input-plan" class="form-control">
                            <option value="Trial">Trial (30 Days)</option>
                            <option value="Premium">EDM Pro Lifetime</option>
                            <option value="Enterprise">Enterprise Fleet</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label><?php esc_html_e('Account Status', 'edm-theme'); ?></label>
                        <select id="user-input-status" class="form-control">
                            <option value="Active">Active</option>
                            <option value="Suspended">Suspended</option>
                        </select>
                    </div>
                </div>
                <div class="form-group">
                    <label><?php esc_html_e('Country / Region', 'edm-theme'); ?></label>
                    <input type="text" id="user-input-country" class="form-control" placeholder="United States" value="United States" />
                </div>
                <div class="dash-modal-footer">
                    <button type="button" class="btn btn-outline" onclick="if(window.edmDashboard) window.edmDashboard.closeModal('modal-user');"><?php esc_html_e('Cancel', 'edm-theme'); ?></button>
                    <button type="submit" class="btn btn-primary"><?php esc_html_e('Save User Record', 'edm-theme'); ?></button>
                </div>
            </form>
        </div>
    </div>
</div>

<!-- 2. RELEASE & ARTIFACT UPLOAD MODAL -->
<div class="dash-modal-backdrop" id="modal-release" style="display: none;" role="dialog" aria-modal="true">
    <div class="dash-modal-card" style="max-width: 600px;">
        <div class="dash-modal-header">
            <h3><?php esc_html_e('Publish New Release / Browser Extension', 'edm-theme'); ?></h3>
            <button type="button" class="btn-close-modal" onclick="if(window.edmDashboard) window.edmDashboard.closeModal('modal-release');"><i data-lucide="x"></i></button>
        </div>
        <div class="dash-modal-body">
            <form id="form-release-crud" onsubmit="event.preventDefault(); if(window.edmDashboard) window.edmDashboard.publishRelease();">
                <div class="form-row-2">
                    <div class="form-group">
                        <label><?php esc_html_e('Platform / Product', 'edm-theme'); ?></label>
                        <select id="release-input-platform" class="form-control">
                            <option value="WindowsDesktop">EDM Windows Desktop (Installer .exe/.zip)</option>
                            <option value="ChromeExtension">Google Chrome Extension (MV3 .zip)</option>
                            <option value="EdgeExtension">Microsoft Edge Extension (MV3 .zip)</option>
                            <option value="FirefoxExtension">Mozilla Firefox Extension (WebExt .zip)</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label><?php esc_html_e('Version Tag', 'edm-theme'); ?></label>
                        <input type="text" id="release-input-ver" class="form-control" placeholder="v2.2.0" required />
                    </div>
                </div>
                <div class="form-row-2">
                    <div class="form-group">
                        <label><?php esc_html_e('Severity Tier', 'edm-theme'); ?></label>
                        <select id="release-input-severity" class="form-control">
                            <option value="Recommended">Recommended Update</option>
                            <option value="Optional">Optional Update</option>
                            <option value="Critical">Critical (Mandatory Security Patch)</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label><?php esc_html_e('Min Supported Version', 'edm-theme'); ?></label>
                        <input type="text" id="release-input-minver" class="form-control" placeholder="v1.9.0" value="v1.9.0" />
                    </div>
                </div>
                <div class="form-group">
                    <label><?php esc_html_e('Binary / Extension Package (.exe, .zip, .msi)', 'edm-theme'); ?></label>
                    <input type="file" id="release-input-file" class="form-control" accept=".exe,.zip,.msi,.tar.gz" onchange="if(window.edmDashboard) window.edmDashboard.handleReleaseFileSelect(this);" />
                    <small style="color: var(--color-text-muted); font-size: 11px;"><?php esc_html_e('Max 500 MB. Server automatically computes SHA-256 hash & verifies Authenticode signature.', 'edm-theme'); ?></small>
                </div>
                <div class="form-group">
                    <label><?php esc_html_e('Release Notes / Changelog', 'edm-theme'); ?></label>
                    <textarea id="release-input-notes" class="form-control" rows="3" placeholder="• Added 32-socket dynamic range engine&#10;• Manifest V3 stream sniffer integration" required></textarea>
                </div>
                <div class="dash-modal-footer">
                    <button type="button" class="btn btn-outline" onclick="if(window.edmDashboard) window.edmDashboard.closeModal('modal-release');"><?php esc_html_e('Cancel', 'edm-theme'); ?></button>
                    <button type="submit" class="btn btn-primary"><i data-lucide="upload" style="width: 14px; height: 14px;"></i> <?php esc_html_e('Upload & Publish Release', 'edm-theme'); ?></button>
                </div>
            </form>
        </div>
    </div>
</div>

<!-- 3. PROMOTIONAL OFFER / COUPON MODAL -->
<div class="dash-modal-backdrop" id="modal-promotion" style="display: none;" role="dialog" aria-modal="true">
    <div class="dash-modal-card">
        <div class="dash-modal-header">
            <h3><?php esc_html_e('Create Promotional Offer / Coupon', 'edm-theme'); ?></h3>
            <button type="button" class="btn-close-modal" onclick="if(window.edmDashboard) window.edmDashboard.closeModal('modal-promotion');"><i data-lucide="x"></i></button>
        </div>
        <div class="dash-modal-body">
            <form id="form-promotion-crud" onsubmit="event.preventDefault(); if(window.edmDashboard) window.edmDashboard.savePromotion();">
                <div class="form-row-2">
                    <div class="form-group">
                        <label><?php esc_html_e('Coupon Code', 'edm-theme'); ?></label>
                        <input type="text" id="promo-input-code" class="form-control" placeholder="SUMMER50" style="text-transform: uppercase;" required />
                    </div>
                    <div class="form-group">
                        <label><?php esc_html_e('Discount Value', 'edm-theme'); ?></label>
                        <input type="text" id="promo-input-discount" class="form-control" placeholder="50% OFF" required />
                    </div>
                </div>
                <div class="form-row-2">
                    <div class="form-group">
                        <label><?php esc_html_e('Discount Type', 'edm-theme'); ?></label>
                        <select id="promo-input-type" class="form-control">
                            <option value="Percentage">Percentage (% Discount)</option>
                            <option value="Fixed">Fixed Amount ($ Discount)</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label><?php esc_html_e('Usage Limit (Max Claims)', 'edm-theme'); ?></label>
                        <input type="number" id="promo-input-maxuses" class="form-control" placeholder="1000" value="1000" />
                    </div>
                </div>
                <div class="form-row-2">
                    <div class="form-group">
                        <label><?php esc_html_e('Expiration Date', 'edm-theme'); ?></label>
                        <input type="date" id="promo-input-expiry" class="form-control" required />
                    </div>
                    <div class="form-group">
                        <label><?php esc_html_e('Initial Status', 'edm-theme'); ?></label>
                        <select id="promo-input-status" class="form-control">
                            <option value="Active">Active (Publish to /edm Hero Banner)</option>
                            <option value="Draft">Draft / Inactive</option>
                        </select>
                    </div>
                </div>
                <div class="dash-modal-footer">
                    <button type="button" class="btn btn-outline" onclick="if(window.edmDashboard) window.edmDashboard.closeModal('modal-promotion');"><?php esc_html_e('Cancel', 'edm-theme'); ?></button>
                    <button type="submit" class="btn btn-primary"><?php esc_html_e('Save & Activate Offer', 'edm-theme'); ?></button>
                </div>
            </form>
        </div>
    </div>
</div>

<!-- 4. LANDING CMS HERO & CONTENT MODAL -->
<div class="dash-modal-backdrop" id="modal-content-hero" style="display: none;" role="dialog" aria-modal="true">
    <div class="dash-modal-card" style="max-width: 620px;">
        <div class="dash-modal-header">
            <h3><?php esc_html_e('Edit EDM Public Landing Content (/edm)', 'edm-theme'); ?></h3>
            <button type="button" class="btn-close-modal" onclick="if(window.edmDashboard) window.edmDashboard.closeModal('modal-content-hero');"><i data-lucide="x"></i></button>
        </div>
        <div class="dash-modal-body">
            <form id="form-content-hero" onsubmit="event.preventDefault(); if(window.edmDashboard) window.edmDashboard.saveLandingContent();">
                <div class="form-group">
                    <label><?php esc_html_e('Announcement Pill Text', 'edm-theme'); ?></label>
                    <input type="text" id="cms-input-pill" class="form-control" placeholder="Exclusive Download Manager • Production Build v2.1.0" required />
                </div>
                <div class="form-group">
                    <label><?php esc_html_e('Hero Headline Title', 'edm-theme'); ?></label>
                    <input type="text" id="cms-input-title" class="form-control" placeholder="The Fastest Download Manager for Windows" required />
                </div>
                <div class="form-group">
                    <label><?php esc_html_e('Hero Subtitle / Description', 'edm-theme'); ?></label>
                    <textarea id="cms-input-subtitle" class="form-control" rows="3" placeholder="Turbocharge your files, high-bitrate video streams, and large archives..." required></textarea>
                </div>
                <div class="form-row-2">
                    <div class="form-group">
                        <label><?php esc_html_e('Primary CTA Button Text', 'edm-theme'); ?></label>
                        <input type="text" id="cms-input-cta-primary" class="form-control" placeholder="Download EDM for Windows" required />
                    </div>
                    <div class="form-group">
                        <label><?php esc_html_e('Sniffer Search Placeholder', 'edm-theme'); ?></label>
                        <input type="text" id="cms-input-sniffer-placeholder" class="form-control" placeholder="Paste any download link, YouTube/Vimeo video URL..." required />
                    </div>
                </div>
                <div class="dash-modal-footer">
                    <button type="button" class="btn btn-outline" onclick="if(window.edmDashboard) window.edmDashboard.closeModal('modal-content-hero');"><?php esc_html_e('Cancel', 'edm-theme'); ?></button>
                    <button type="submit" class="btn btn-primary"><i data-lucide="save" style="width: 14px; height: 14px;"></i> <?php esc_html_e('Publish Changes to /edm', 'edm-theme'); ?></button>
                </div>
            </form>
        </div>
    </div>
</div>

<!-- 5. 30-DAY TRIAL POLICY & LICENSING CONFIG MODAL -->
<div class="dash-modal-backdrop" id="modal-trial-config" style="display: none;" role="dialog" aria-modal="true">
    <div class="dash-modal-card">
        <div class="dash-modal-header">
            <h3><?php esc_html_e('30-Day Trial & Licensing Architecture Config', 'edm-theme'); ?></h3>
            <button type="button" class="btn-close-modal" onclick="if(window.edmDashboard) window.edmDashboard.closeModal('modal-trial-config');"><i data-lucide="x"></i></button>
        </div>
        <div class="dash-modal-body">
            <form id="form-trial-config" onsubmit="event.preventDefault(); if(window.edmDashboard) window.edmDashboard.saveTrialConfig();">
                <div class="form-row-2">
                    <div class="form-group">
                        <label><?php esc_html_e('Default Trial Duration (Days)', 'edm-theme'); ?></label>
                        <input type="number" id="trial-input-duration" class="form-control" value="30" min="1" max="90" required />
                    </div>
                    <div class="form-group">
                        <label><?php esc_html_e('Grace Period (Days)', 'edm-theme'); ?></label>
                        <input type="number" id="trial-input-grace" class="form-control" value="3" min="0" max="14" required />
                    </div>
                </div>
                <div class="form-row-2">
                    <div class="form-group">
                        <label><?php esc_html_e('Max Bound Devices (Per Key)', 'edm-theme'); ?></label>
                        <input type="number" id="trial-input-maxdevices" class="form-control" value="5" min="1" max="20" required />
                    </div>
                    <div class="form-group">
                        <label><?php esc_html_e('Max Offline Tolerance (Hours)', 'edm-theme'); ?></label>
                        <input type="number" id="trial-input-offline" class="form-control" value="72" min="12" max="336" required />
                    </div>
                </div>
                <div class="form-group">
                    <label style="display: flex; align-items: center; gap: 8px; cursor: pointer;">
                        <input type="checkbox" id="trial-input-hwid-enforce" checked />
                        <span><?php esc_html_e('Enforce Hardware Fingerprint (HWID) Cryptographic Binding', 'edm-theme'); ?></span>
                    </label>
                </div>
                <div class="dash-modal-footer">
                    <button type="button" class="btn btn-outline" onclick="if(window.edmDashboard) window.edmDashboard.closeModal('modal-trial-config');"><?php esc_html_e('Cancel', 'edm-theme'); ?></button>
                    <button type="submit" class="btn btn-primary"><?php esc_html_e('Apply Trial Policy', 'edm-theme'); ?></button>
                </div>
            </form>
        </div>
    </div>
</div>

<!-- 6. NOTIFICATION DRAWER / MODAL -->
<div class="dash-modal-backdrop" id="modal-notifications" style="display: none;" role="dialog" aria-modal="true">
    <div class="dash-modal-card">
        <div class="dash-modal-header">
            <h3><?php esc_html_e('System Notifications & Dispatches', 'edm-theme'); ?></h3>
            <button type="button" class="btn-close-modal" onclick="if(window.edmDashboard) window.edmDashboard.closeModal('modal-notifications');"><i data-lucide="x"></i></button>
        </div>
        <div class="dash-modal-body" id="notif-drawer-content" style="max-height: 400px; overflow-y: auto;"></div>
        <div class="dash-modal-footer">
            <button type="button" class="btn btn-primary" onclick="if(window.edmDashboard) window.edmDashboard.closeModal('modal-notifications');"><?php esc_html_e('Close', 'edm-theme'); ?></button>
        </div>
    </div>
</div>
