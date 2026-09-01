<?php
/**
 * Obhijog & Feedback Hub (অভিযোগ ও পরামর্শ কেন্দ্র) Modal Component
 * Universal Complaint, Bug Report, and Feature Suggestion System
 * Integrates with EDM ControlPlane API & WPF Desktop Application
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<!-- ══════════════════════════════════════════════════════════════
     OBHIJOG & FEEDBACK HUB MODAL (অভিযোগ ও পরামর্শ কেন্দ্র)
     ══════════════════════════════════════════════════════════════ -->
<div class="modal-backdrop" id="modal-obhijog-center" aria-hidden="true" role="dialog" style="display: none;">
    <div class="modal-card obhijog-modal-card" style="max-width: 620px; width: 95%;">
        <!-- Header -->
        <div class="modal-header" style="background: linear-gradient(135deg, rgba(30, 41, 59, 0.95), rgba(15, 23, 42, 0.98)); padding: 20px 24px; border-bottom: 1px solid rgba(56, 189, 248, 0.2);">
            <div style="display: flex; align-items: center; gap: 12px;">
                <div style="width: 40px; height: 40px; border-radius: 12px; background: rgba(56, 189, 248, 0.15); border: 1px solid #38bdf8; display: flex; align-items: center; justify-content: center; color: #38bdf8;">
                    <i data-lucide="message-square-plus" style="width: 22px; height: 22px;"></i>
                </div>
                <div>
                    <h3 style="font-size: 18px; font-weight: 800; color: #ffffff; margin: 0; line-height: 1.2;">
                        অভিযোগ ও পরামর্শ কেন্দ্র
                    </h3>
                    <p style="font-size: 12px; color: #94a3b8; margin: 4px 0 0 0;">
                        EDM Support, Bug Reporting &amp; Feature Innovation Hub
                    </p>
                </div>
            </div>
            <button type="button" class="btn-icon-only" onclick="window.closeObhijogModal()" aria-label="Close Modal" style="background: transparent; border: none; color: #94a3b8; cursor: pointer; font-size: 20px;">
                <i data-lucide="x" style="width: 20px; height: 20px;"></i>
            </button>
        </div>

        <!-- Body -->
        <div class="modal-body" style="padding: 24px; max-height: 75vh; overflow-y: auto;">
            <!-- Category Tabs -->
            <div style="margin-bottom: 20px;">
                <label style="display: block; font-size: 13px; font-weight: 600; color: #cbd5e1; margin-bottom: 8px;">
                    আপনার বার্তার ধরন নির্বাচন করুন *
                </label>
                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 8px;" id="obhijog-type-selector">
                    <button type="button" class="obhijog-type-btn active" data-type="bug" onclick="window.selectObhijogType(this, 'bug')">
                        <i data-lucide="bug" style="width: 16px; height: 16px; color: #f87171;"></i>
                        <span>বাগ / সফটওয়্যার সমস্যা</span>
                    </button>
                    <button type="button" class="obhijog-type-btn" data-type="feature" onclick="window.selectObhijogType(this, 'feature')">
                        <i data-lucide="lightbulb" style="width: 16px; height: 16px; color: #fbbf24;"></i>
                        <span>নতুন ফিচার পরামর্শ</span>
                    </button>
                    <button type="button" class="obhijog-type-btn" data-type="speed" onclick="window.selectObhijogType(this, 'speed')">
                        <i data-lucide="gauge" style="width: 16px; height: 16px; color: #38bdf8;"></i>
                        <span>স্পিড ও কানেকশন ইস্যু</span>
                    </button>
                    <button type="button" class="obhijog-type-btn" data-type="general" onclick="window.selectObhijogType(this, 'general')">
                        <i data-lucide="help-circle" style="width: 16px; height: 16px; color: #a855f7;"></i>
                        <span>সাধারণ মতামত ও পরামর্শ</span>
                    </button>
                </div>
                <input type="hidden" id="obhijog-category" value="bug">
            </div>

            <!-- User Info Grid -->
            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 14px; margin-bottom: 16px;">
                <div>
                    <label class="form-label" style="font-size: 12px; color: #94a3b8; margin-bottom: 6px; display: block;">আপনার নাম (Name) *</label>
                    <input type="text" id="obhijog-name" class="form-input-full" placeholder="যেমন: Alamin" required style="width: 100%; padding: 10px 12px; background: rgba(15, 23, 42, 0.8); border: 1px solid rgba(51, 65, 85, 0.8); border-radius: 8px; color: #fff; font-size: 13px;">
                </div>
                <div>
                    <label class="form-label" style="font-size: 12px; color: #94a3b8; margin-bottom: 6px; display: block;">ইমেইল ঠিকানা (Email) *</label>
                    <input type="email" id="obhijog-email" class="form-input-full" placeholder="name@example.com" required style="width: 100%; padding: 10px 12px; background: rgba(15, 23, 42, 0.8); border: 1px solid rgba(51, 65, 85, 0.8); border-radius: 8px; color: #fff; font-size: 13px;">
                </div>
            </div>

            <!-- Subject & Version Grid -->
            <div style="display: grid; grid-template-columns: 2fr 1fr; gap: 14px; margin-bottom: 16px;">
                <div>
                    <label class="form-label" style="font-size: 12px; color: #94a3b8; margin-bottom: 6px; display: block;">অভিযোগ বা পরামর্শের বিষয় *</label>
                    <input type="text" id="obhijog-subject" class="form-input-full" placeholder="সংক্ষেপে বিষয়টি লিখুন" required style="width: 100%; padding: 10px 12px; background: rgba(15, 23, 42, 0.8); border: 1px solid rgba(51, 65, 85, 0.8); border-radius: 8px; color: #fff; font-size: 13px;">
                </div>
                <div>
                    <label class="form-label" style="font-size: 12px; color: #94a3b8; margin-bottom: 6px; display: block;">EDM ভার্সন</label>
                    <select id="obhijog-version" class="form-input-full" style="width: 100%; padding: 10px 12px; background: rgba(15, 23, 42, 0.8); border: 1px solid rgba(51, 65, 85, 0.8); border-radius: 8px; color: #fff; font-size: 13px;">
                        <option value="v2.1.0">v2.1.0 (Latest)</option>
                        <option value="v2.0.0">v2.0.0</option>
                        <option value="v1.0.0">v1.0.0</option>
                        <option value="extension">Browser Extension</option>
                    </select>
                </div>
            </div>

            <!-- Details Description -->
            <div style="margin-bottom: 18px;">
                <label class="form-label" style="font-size: 12px; color: #94a3b8; margin-bottom: 6px; display: block;">বিস্তারিত বিবরণ (Details) *</label>
                <textarea id="obhijog-details" rows="4" class="form-input-full" placeholder="সমস্যাটি কীভাবে ঘটেছে বা আপনার নতুন ধারণার বিস্তারিত লিখুন..." required style="width: 100%; padding: 10px 12px; background: rgba(15, 23, 42, 0.8); border: 1px solid rgba(51, 65, 85, 0.8); border-radius: 8px; color: #fff; font-size: 13px; line-height: 1.5; resize: vertical;"></textarea>
            </div>

            <!-- Submission Status Message Container -->
            <div id="obhijog-status-wrap" style="display: none; padding: 12px; border-radius: 8px; margin-bottom: 14px; font-size: 13px;"></div>
        </div>

        <!-- Footer -->
        <div class="modal-footer" style="padding: 16px 24px; background: rgba(15, 23, 42, 0.98); border-top: 1px solid rgba(51, 65, 85, 0.5); display: flex; align-items: center; justify-content: space-between;">
            <span style="font-size: 12px; color: #64748b;">
                <i data-lucide="shield-check" style="width: 14px; height: 14px; vertical-align: middle; color: #38bdf8;"></i>
                EDM ControlPlane সিকিউরড
            </span>
            <div style="display: flex; gap: 10px;">
                <button type="button" class="btn btn-secondary" onclick="window.closeObhijogModal()" style="padding: 9px 18px; font-size: 13px; border-radius: 8px;">বাতিল</button>
                <button type="button" class="btn btn-primary" id="btn-submit-obhijog" onclick="window.submitObhijogFeedback()" style="padding: 9px 20px; font-size: 13px; font-weight: 700; border-radius: 8px; display: flex; align-items: center; gap: 6px;">
                    <i data-lucide="send" style="width: 15px; height: 15px;"></i>
                    <span>জমা দিন</span>
                </button>
            </div>
        </div>
    </div>
</div>

<style>
.obhijog-type-btn {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 10px 12px;
    background: rgba(15, 23, 42, 0.6);
    border: 1px solid rgba(51, 65, 85, 0.6);
    border-radius: 8px;
    color: #cbd5e1;
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.2s ease;
    text-align: left;
}
.obhijog-type-btn:hover {
    background: rgba(56, 189, 248, 0.08);
    border-color: rgba(56, 189, 248, 0.4);
    color: #ffffff;
}
.obhijog-type-btn.active {
    background: rgba(56, 189, 248, 0.15);
    border-color: #38bdf8;
    color: #ffffff;
    box-shadow: 0 0 12px rgba(56, 189, 248, 0.25);
}
</style>
