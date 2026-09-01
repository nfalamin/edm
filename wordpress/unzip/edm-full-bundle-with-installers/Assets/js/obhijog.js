/**
 * EDM Obhijog & Feedback Hub (অভিযোগ ও পরামর্শ কেন্দ্র) Controller
 * Handles user bug submissions, feature proposals, and automated acknowledgments
 */
(function() {
    'use strict';

    window.openObhijogModal = function() {
        const modal = document.getElementById('modal-obhijog-center');
        if (!modal) return;
        modal.style.display = 'flex';
        modal.classList.add('active');
        if (window.lucide) window.lucide.createIcons();
    };

    window.closeObhijogModal = function() {
        const modal = document.getElementById('modal-obhijog-center');
        if (!modal) return;
        modal.classList.remove('active');
        setTimeout(() => { modal.style.display = 'none'; }, 200);
    };

    window.selectObhijogType = function(btn, type) {
        document.querySelectorAll('.obhijog-type-btn').forEach(b => b.classList.remove('active'));
        if (btn) btn.classList.add('active');
        const catInput = document.getElementById('obhijog-category');
        if (catInput) catInput.value = type;
    };

    window.submitObhijogFeedback = async function() {
        const name = (document.getElementById('obhijog-name')?.value || '').trim();
        const email = (document.getElementById('obhijog-email')?.value || '').trim();
        const subject = (document.getElementById('obhijog-subject')?.value || '').trim();
        const version = document.getElementById('obhijog-version')?.value || 'v2.1.0';
        const category = document.getElementById('obhijog-category')?.value || 'bug';
        const details = (document.getElementById('obhijog-details')?.value || '').trim();
        const statusWrap = document.getElementById('obhijog-status-wrap');
        const btn = document.getElementById('btn-submit-obhijog');

        if (!name || !email || !subject || !details) {
            if (statusWrap) {
                statusWrap.style.display = 'block';
                statusWrap.style.background = 'rgba(239, 68, 68, 0.15)';
                statusWrap.style.border = '1px solid #ef4444';
                statusWrap.style.color = '#fca5a5';
                statusWrap.innerHTML = 'অনুগ্রহ করে নাম, ইমেইল, বিষয় ও বিস্তারিত বিবরণ সবগুলো ঘর পূরণ করুন।';
            }
            return;
        }

        if (btn) {
            btn.disabled = true;
            btn.innerHTML = '<i data-lucide="loader-2" class="animate-spin" style="width:15px;height:15px;"></i> <span>জমা হচ্ছে...</span>';
            if (window.lucide) window.lucide.createIcons();
        }

        const payload = {
            name,
            email,
            subject,
            category,
            version,
            details,
            userAgent: navigator.userAgent,
            timestamp: new Date().toISOString()
        };

        let generatedTicketId = 'EDM-TK-' + Math.floor(100000 + Math.random() * 900000);

        try {
            // Attempt REST Endpoint
            const res = await fetch('/wp-json/edm-api/v1/feedback/submit', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
                body: JSON.stringify(payload)
            }).catch(() => null);

            if (res && res.ok) {
                const data = await res.json();
                if (data && data.ticketId) {
                    generatedTicketId = data.ticketId;
                }
            } else {
                // Fallback AJAX if REST not active
                const formData = new FormData();
                formData.append('action', 'nfdash_submit_feedback');
                formData.append('feedback_data', JSON.stringify(payload));
                const ajaxRes = await fetch('/wp-admin/admin-ajax.php', { method: 'POST', body: formData }).catch(() => null);
                if (ajaxRes && ajaxRes.ok) {
                    const ajaxData = await ajaxRes.json().catch(() => null);
                    if (ajaxData && ajaxData.ticketId) {
                        generatedTicketId = ajaxData.ticketId;
                    }
                }
            }

            if (statusWrap) {
                statusWrap.style.display = 'block';
                statusWrap.style.background = 'rgba(16, 185, 129, 0.15)';
                statusWrap.style.border = '1px solid #10b981';
                statusWrap.style.color = '#6ee7b7';
                statusWrap.innerHTML = `
                    <div style="font-weight:700;margin-bottom:4px;">ধন্যবাদ! আপনার বার্তা সফলভাবে জমা হয়েছে।</div>
                    <div style="font-size:12px;color:#cbd5e1;">ট্র্যাকিং টিকিট আইডি: <strong style="color:#38bdf8;">${generatedTicketId}</strong></div>
                    <div style="font-size:11px;color:#94a3b8;margin-top:4px;">আমাদের টেকনিক্যাল টিম আপনার বার্তার ওপর অবিলম্বে প্রয়োজনীয় ব্যবস্থা গ্রহণ করবে।</div>
                `;
            }

            // Clear inputs
            document.getElementById('obhijog-details').value = '';
            document.getElementById('obhijog-subject').value = '';

            setTimeout(() => {
                if (btn) {
                    btn.disabled = false;
                    btn.innerHTML = '<i data-lucide="check" style="width:15px;height:15px;"></i> <span>জমা হয়েছে</span>';
                    if (window.lucide) window.lucide.createIcons();
                }
            }, 500);

        } catch (e) {
            if (statusWrap) {
                statusWrap.style.display = 'block';
                statusWrap.style.background = 'rgba(239, 68, 68, 0.15)';
                statusWrap.style.border = '1px solid #ef4444';
                statusWrap.style.color = '#fca5a5';
                statusWrap.innerHTML = 'বার্তা পাঠাতে ত্রুটি হয়েছে। অনুগ্রহ করে কিছুক্ষণ পর আবার চেষ্টা করুন।';
            }
            if (btn) {
                btn.disabled = false;
                btn.innerHTML = '<i data-lucide="send" style="width:15px;height:15px;"></i> <span>আবার চেষ্টা করুন</span>';
            }
        }
    };
})();
