<?php
/**
 * EDM Official AI Support Assistant Bot Component
 * Dedicated AI Assistant for EDM Software Questions, Setup, & Troubleshooting
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<!-- ══════════════════════════════════════════════════════════════
     FLOATING EDM AI SUPPORT ASSISTANT BOT
     ══════════════════════════════════════════════════════════════ -->
<div class="edm-bot-wrapper" id="edm-bot-wrapper">
    <!-- Floating Trigger Button -->
    <button type="button" class="edm-bot-trigger" id="edm-bot-trigger" onclick="window.toggleEdmBot()" aria-label="Open EDM Support AI">
        <div class="edm-bot-avatar-wrap">
            <i data-lucide="bot" style="width: 26px; height: 26px; color: #38bdf8;"></i>
            <span class="edm-bot-pulse-dot"></span>
        </div>
        <span class="edm-bot-trigger-label">EDM AI Support</span>
    </button>

    <!-- Chat Panel Window -->
    <div class="edm-bot-panel" id="edm-bot-panel" style="display: none;">
        <!-- Header -->
        <div class="edm-bot-header">
            <div style="display: flex; align-items: center; gap: 10px;">
                <div style="width: 36px; height: 36px; border-radius: 10px; background: rgba(56, 189, 248, 0.15); border: 1px solid #38bdf8; display: flex; align-items: center; justify-content: center; color: #38bdf8;">
                    <i data-lucide="sparkles" style="width: 18px; height: 18px;"></i>
                </div>
                <div>
                    <h4 style="margin: 0; font-size: 15px; font-weight: 800; color: #ffffff;">EDM AI Assistant</h4>
                    <span style="font-size: 11px; color: #34d399; display: flex; align-items: center; gap: 4px;">
                        <span style="width: 6px; height: 6px; border-radius: 50%; background: #34d399; display: inline-block;"></span>
                        Online • 24/7 EDM Specialist
                    </span>
                </div>
            </div>
            <div style="display: flex; align-items: center; gap: 6px;">
                <button type="button" class="btn-icon-only" onclick="window.openObhijogModal()" title="অভিযোগ ও পরামর্শ কেন্দ্র" style="background: rgba(251, 191, 36, 0.1); border: 1px solid rgba(251, 191, 36, 0.4); color: #fbbf24; border-radius: 6px; padding: 4px 8px; font-size: 11px; cursor: pointer; display: flex; align-items: center; gap: 4px;">
                    <i data-lucide="message-square" style="width: 12px; height: 12px;"></i>
                    <span>অভিযোগ</span>
                </button>
                <button type="button" class="btn-icon-only" onclick="window.toggleEdmBot()" aria-label="Close Chat" style="background: transparent; border: none; color: #94a3b8; cursor: pointer;">
                    <i data-lucide="x" style="width: 18px; height: 18px;"></i>
                </button>
            </div>
        </div>

        <!-- Messages Body -->
        <div class="edm-bot-messages" id="edm-bot-messages">
            <!-- Welcome Bot Message -->
            <div class="bot-msg-row incoming">
                <div class="bot-msg-avatar">
                    <i data-lucide="bot" style="width: 14px; height: 14px; color: #38bdf8;"></i>
                </div>
                <div class="bot-msg-bubble">
                    <p style="margin: 0 0 8px 0; font-weight: 600; color: #fff;">
                        স্বাগতম! আমি EDM (Exclusive Download Manager) এর স্পেশালিস্ট এআই অ্যাসিস্ট্যান্ট।
                    </p>
                    <p style="margin: 0 0 8px 0; color: #cbd5e1; font-size: 12px; line-height: 1.5;">
                        সফটওয়্যার ডাউনলোড, ৩২x স্পিড অ্যাক্সিলারেশন, ব্রাউজার এক্সটেনশন বা যেকোনো টেকনিক্যাল প্রশ্নের উত্তর পেতে আমাকে জিজ্ঞাসা করুন:
                    </p>
                    <!-- Quick Suggestions -->
                    <div class="bot-quick-pills">
                        <button type="button" onclick="window.handleBotQuickPill(this)">⚡ ৩২x স্পিড কীভাবে কাজ করে?</button>
                        <button type="button" onclick="window.handleBotQuickPill(this)">📥 4K ভিডিও কীভাবে ডাউনলোড করব?</button>
                        <button type="button" onclick="window.handleBotQuickPill(this)">🧩 ব্রাউজার এক্সটেনশন সেটআপ</button>
                        <button type="button" onclick="window.handleBotQuickPill(this)">🛡️ ক্র্যাশ হলে Resume কীভাবে হয়?</button>
                    </div>
                </div>
            </div>
        </div>

        <!-- Footer Input Bar -->
        <div class="edm-bot-footer">
            <form id="edm-bot-form" onsubmit="window.handleBotSubmit(event)" style="display: flex; gap: 8px; width: 100%;">
                <input type="text" id="edm-bot-input" placeholder="EDM সম্পর্কে প্রশ্ন লিখুন..." autocomplete="off" style="flex: 1; padding: 10px 14px; background: rgba(15, 23, 42, 0.9); border: 1px solid rgba(51, 65, 85, 0.8); border-radius: 20px; color: #fff; font-size: 13px;">
                <button type="submit" class="btn btn-primary" style="border-radius: 50%; width: 40px; height: 40px; padding: 0; display: flex; align-items: center; justify-content: center; flex-shrink: 0;">
                    <i data-lucide="send" style="width: 16px; height: 16px;"></i>
                </button>
            </form>
        </div>
    </div>
</div>

<style>
.edm-bot-wrapper {
    position: fixed;
    bottom: 24px;
    right: 24px;
    z-index: 99999;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}
.edm-bot-trigger {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 10px 18px 10px 12px;
    background: linear-gradient(135deg, #0f172a, #1e293b);
    border: 1px solid rgba(56, 189, 248, 0.4);
    border-radius: 30px;
    color: #ffffff;
    cursor: pointer;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.5), 0 0 20px rgba(56, 189, 248, 0.2);
    transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}
.edm-bot-trigger:hover {
    transform: translateY(-3px) scale(1.02);
    border-color: #38bdf8;
    box-shadow: 0 12px 36px rgba(0, 0, 0, 0.6), 0 0 30px rgba(56, 189, 248, 0.4);
}
.edm-bot-avatar-wrap {
    position: relative;
    width: 38px;
    height: 38px;
    border-radius: 50%;
    background: rgba(56, 189, 248, 0.12);
    display: flex;
    align-items: center;
    justify-content: center;
}
.edm-bot-pulse-dot {
    position: absolute;
    top: 0;
    right: 0;
    width: 10px;
    height: 10px;
    border-radius: 50%;
    background: #34d399;
    border: 2px solid #0f172a;
    animation: pulseGlow 2s infinite;
}
@keyframes pulseGlow {
    0%, 100% { transform: scale(1); opacity: 1; }
    50% { transform: scale(1.2); opacity: 0.7; }
}
.edm-bot-trigger-label {
    font-size: 13px;
    font-weight: 700;
    letter-spacing: 0.3px;
}
.edm-bot-panel {
    position: absolute;
    bottom: 64px;
    right: 0;
    width: 380px;
    max-width: calc(100vw - 32px);
    height: 520px;
    max-height: calc(100vh - 120px);
    background: rgba(15, 23, 42, 0.96);
    backdrop-filter: blur(20px);
    border: 1px solid rgba(56, 189, 248, 0.3);
    border-radius: 20px;
    box-shadow: 0 20px 50px rgba(0, 0, 0, 0.7), 0 0 40px rgba(56, 189, 248, 0.15);
    display: flex;
    flex-direction: column;
    overflow: hidden;
    animation: botSlideUp 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}
@keyframes botSlideUp {
    from { opacity: 0; transform: translateY(20px) scale(0.95); }
    to { opacity: 1; transform: translateY(0) scale(1); }
}
.edm-bot-header {
    padding: 16px 18px;
    background: rgba(30, 41, 59, 0.8);
    border-bottom: 1px solid rgba(51, 65, 85, 0.6);
    display: flex;
    align-items: center;
    justify-content: space-between;
}
.edm-bot-messages {
    flex: 1;
    padding: 16px;
    overflow-y: auto;
    display: flex;
    flex-direction: column;
    gap: 14px;
}
.bot-msg-row {
    display: flex;
    gap: 8px;
    align-items: flex-start;
}
.bot-msg-row.incoming .bot-msg-bubble {
    background: rgba(30, 41, 59, 0.9);
    border: 1px solid rgba(51, 65, 85, 0.6);
    border-radius: 14px 14px 14px 2px;
    padding: 12px 14px;
    color: #e2e8f0;
    max-width: 88%;
}
.bot-msg-row.outgoing {
    justify-content: flex-end;
}
.bot-msg-row.outgoing .bot-msg-bubble {
    background: linear-gradient(135deg, #0284c7, #2563eb);
    border-radius: 14px 14px 2px 14px;
    padding: 10px 14px;
    color: #ffffff;
    max-width: 85%;
    font-size: 13px;
}
.bot-msg-avatar {
    width: 24px;
    height: 24px;
    border-radius: 50%;
    background: rgba(56, 189, 248, 0.2);
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
}
.bot-quick-pills {
    display: flex;
    flex-direction: column;
    gap: 6px;
    margin-top: 8px;
}
.bot-quick-pills button {
    background: rgba(15, 23, 42, 0.8);
    border: 1px solid rgba(56, 189, 248, 0.3);
    border-radius: 8px;
    color: #38bdf8;
    padding: 6px 10px;
    font-size: 11px;
    font-weight: 600;
    cursor: pointer;
    text-align: left;
    transition: all 0.2s;
}
.bot-quick-pills button:hover {
    background: rgba(56, 189, 248, 0.15);
    border-color: #38bdf8;
    color: #ffffff;
}
.edm-bot-footer {
    padding: 12px 16px;
    background: rgba(15, 23, 42, 0.95);
    border-top: 1px solid rgba(51, 65, 85, 0.6);
}
</style>
