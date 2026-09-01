/**
 * EDM Official AI Support Assistant Bot Knowledge & Interaction Engine
 * Features Offline Knowledge Base & Live EDM Technical Assistant
 */
(function() {
    'use strict';

    const EDM_KNOWLEDGE_BASE = [
        {
            keywords: ['speed', 'turbo', '32', 'socket', 'acceleration', 'fast', 'স্পিড', 'গতি', '৩২'],
            response: `<strong>⚡ ৩২-সকেট টার্বো অ্যাক্সিলারেশন কীভাবে কাজ করে:</strong><br><br>
            সাধারণ ব্রাউজারগুলো ১টি সিঙ্গেল কানেকশন (১ সকেট) দিয়ে ফাইল ডাউনলোড করে, যার ফলে সার্ভার স্পিড লিমিট থাকলে গতি কমে যায়।<br><br>
            <strong>EDM এর কৌশল:</strong> EDM যেকোনো ফাইলকে ডাইনামিক্যালি ৩২টি পৃথক ব্লকে ভাগ করে ৩২টি প্যারালাল HTTP রেঞ্জ কানেকশন দিয়ে একই সাথে ডাউনলোড করে। এর ফলে সার্ভার থ্রটলিং বাইপাস হয়ে সর্বোচ্চ ৩২ গুণ গতি পাওয়া যায়!`
        },
        {
            keywords: ['video', 'youtube', '4k', '8k', 'grabber', 'ভিডিও', 'ইউটিউব', 'ডাউনলোড'],
            response: `<strong>📥 4K/8K ভিডিও ও মিডিয়া স্ট্রিমিং ডাউনলোড:</strong><br><br>
            EDM এর মধ্যে রয়েছে বিল্ট-ইন <strong>Dynamic Stream Sniffer</strong>।<br>
            • আপনি যখন YouTube, Facebook, Vimeo বা যেকোনো ওয়েবসাইটে ভিডিও প্লে করবেন, EDM স্বয়ংক্রিয়ভাবে রেজোলিউশন ও অডিও-ভিডিও স্ট্রিম সনাক্ত করে ডাউনলোড বার প্রদর্শন করে।<br>
            • এটি HLS / MPEG-DASH অ্যাডাপটিভ স্ট্রিম থেকে হাই-রেজোলিউশন ভিডিও ও অডিও ট্র্যাক আলাদা ডাউনলোড করে স্বয়ংক্রিয়ভাবে মার্জ (.mp4 / .mkv) করে দেয়!`
        },
        {
            keywords: ['extension', 'chrome', 'edge', 'firefox', 'brave', 'opera', 'এক্সটেনশন', 'ব্রাউজার'],
            response: `<strong>🧩 ব্রাউজার এক্সটেনশন সেটআপ ও হ্যান্ডঅফ:</strong><br><br>
            EDM অফিসিয়াল <strong>Manifest V3</strong> ব্রাউজার এক্সটেনশন সাপোর্ট করে।<br>
            ১. EDM সফটওয়্যার ইনস্টল করার পর আপনার ব্রাউজারে (Chrome/Edge/Firefox) EDM Extension যোগ করুন।<br>
            ২. ব্রাউজারে যেকোনো ডাউনলোড লিংকে ক্লিক করলেই EDM NativeHost এর মাধ্যমে স্বয়ংক্রিয়ভাবে ডাউনলোড হ্যান্ডঅফ হয়ে ৩২টি সকেটে হাই-স্পিড ডাউনলোড শুরু হবে!`
        },
        {
            keywords: ['resume', 'crash', 'pause', 'আটকে', 'বন্ধ', 'রেজিউম', 'পজ'],
            response: `<strong>🛡️ ক্র্যাশ-প্রুফ অটোমেটিক Resume প্রযুক্তি:</strong><br><br>
            • বিদ্যুৎ চলে গেলে, পিসি হঠাৎ ক্র্যাশ করলে বা ইন্টারনেট ডিসকানেক্ট হলেও আপনার কোনো ডাউনলোড ডেটা নষ্ট হবে না।<br>
            • EDM প্রতিটি রেঞ্জ চাঙ্কের ডাউনলোড করা বাইট স্টেট <strong>SQLite ট্রানজ্যাকশন ডাটাবেজে</strong> রিয়েল-টাইমে সেভ রাখে।<br>
            • ইন্টারনেট সংযোগ ফিরলে যেটুকু অংশ বাকি ছিল ঠিক সেখান থেকেই আবার ডাউনলোড চলতে শুরু করে!`
        },
        {
            keywords: ['price', 'pricing', 'subscription', 'license', 'টাকা', 'দাম', 'লাইসেন্স', 'পেমেন্ট'],
            response: `<strong>💳 লাইসেন্স ও রিজিওনাল প্রাইসিং:</strong><br><br>
            • <strong>বাংলাদেশ স্পেশাল অফার:</strong> মাত্র ৳৬৩/মাস (বিকাশ / নগদ / কার্ড সাপোর্ট)।<br>
            • <strong>এশিয়ান দেশসমূহ:</strong> $2.99/মাস।<br>
            • <strong>গ্লোবাল টিয়ার:</strong> $4.99/মাস।<br>
            • আপনি ৩০ দিনের জন্য কোনো কার্ড ছাড়াই আনলিমিটেড টার্বো স্পিডে EDM ফ্রি ট্রায়াল ব্যবহার করতে পারবেন!`
        },
        {
            keywords: ['obhijog', 'complaint', 'bug', 'idea', 'problem', 'অভিযোগ', 'সমস্যা', 'বাগ', 'পরামর্শ'],
            response: `<strong>💬 অভিযোগ ও পরামর্শ কেন্দ্রে স্বাগতম:</strong><br><br>
            আপনার কি EDM সফটওয়্যারে কোনো সমস্যা হচ্ছে বা নতুন কোনো আইডিয়া দিতে চান?<br>
            অনুগ্রহ করে উপরের <strong><a href="javascript:void(0)" onclick="window.openObhijogModal()" style="color:#38bdf8;text-decoration:underline;">অভিযোগ ও পরামর্শ কেন্দ্র</a></strong> বাটনে ক্লিক করে ফর্মটি সাবমিট করুন। আমাদের টিম সরাসরি আপনার টিকিটটি রিভিউ করবে!`
        }
    ];

    window.toggleEdmBot = function() {
        const panel = document.getElementById('edm-bot-panel');
        if (!panel) return;
        if (panel.style.display === 'none' || !panel.style.display) {
            panel.style.display = 'flex';
            if (window.lucide) window.lucide.createIcons();
            const input = document.getElementById('edm-bot-input');
            if (input) input.focus();
        } else {
            panel.style.display = 'none';
        }
    };

    window.handleBotQuickPill = function(btn) {
        if (!btn) return;
        const text = btn.textContent.replace(/^[^\w\u0980-\u09FF]+/, '').trim();
        window.processBotQuery(text);
    };

    window.handleBotSubmit = function(e) {
        if (e) e.preventDefault();
        const input = document.getElementById('edm-bot-input');
        if (!input) return;
        const query = input.value.trim();
        if (!query) return;
        input.value = '';
        window.processBotQuery(query);
    };

    window.processBotQuery = function(query) {
        const msgs = document.getElementById('edm-bot-messages');
        if (!msgs) return;

        // 1. Add User Message
        const userRow = document.createElement('div');
        userRow.className = 'bot-msg-row outgoing';
        userRow.innerHTML = `<div class="bot-msg-bubble">${escapeHtml(query)}</div>`;
        msgs.appendChild(userRow);
        msgs.scrollTop = msgs.scrollHeight;

        // 2. Typing Indicator
        const typingRow = document.createElement('div');
        typingRow.className = 'bot-msg-row incoming bot-typing-row';
        typingRow.innerHTML = `
            <div class="bot-msg-avatar"><i data-lucide="bot" style="width:14px;height:14px;color:#38bdf8;"></i></div>
            <div class="bot-msg-bubble" style="font-size:12px;color:#94a3b8;">
                <span class="animate-pulse">EDM AI উত্তর লিখছে...</span>
            </div>
        `;
        msgs.appendChild(typingRow);
        if (window.lucide) window.lucide.createIcons();
        msgs.scrollTop = msgs.scrollHeight;

        // 3. Find Answer in Knowledge Base
        setTimeout(() => {
            typingRow.remove();

            let answer = null;
            const qLower = query.toLowerCase();

            for (const item of EDM_KNOWLEDGE_BASE) {
                const match = item.keywords.some(k => qLower.includes(k.toLowerCase()));
                if (match) {
                    answer = item.response;
                    break;
                }
            }

            if (!answer) {
                answer = `আমি আপনার প্রশ্নটি বুঝতে পেরেছি। EDM সম্পর্কিত আরও বিস্তারিত জানতে বা সরাসরি ডেভেলপারের সাথে যোগাযোগ করতে অনুগ্রহ করে আমাদের 
                <a href="javascript:void(0)" onclick="window.openObhijogModal()" style="color:#38bdf8;font-weight:700;text-decoration:underline;">অভিযোগ ও পরামর্শ কেন্দ্রে</a> 
                মেসেজ পাঠান। অথবা <strong>"স্পিড", "ভিডিও", "এক্সটেনশন", "রেজিউম"</strong> লিখে জিজ্ঞাসা করুন।`;
            }

            const botRow = document.createElement('div');
            botRow.className = 'bot-msg-row incoming';
            botRow.innerHTML = `
                <div class="bot-msg-avatar"><i data-lucide="bot" style="width:14px;height:14px;color:#38bdf8;"></i></div>
                <div class="bot-msg-bubble" style="font-size:13px;line-height:1.5;">${answer}</div>
            `;
            msgs.appendChild(botRow);
            if (window.lucide) window.lucide.createIcons();
            msgs.scrollTop = msgs.scrollHeight;
        }, 450);
    };

    function escapeHtml(str) {
        return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#039;");
    }
})();
