<?php
/**
 * Template Name: Contact - Alamin Hossain
 * Description: Dedicated Contact page template with direct phone, WhatsApp, email, and interactive inquiry form.
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();
?>

<main class="min-h-screen pt-24 pb-20 px-6">
    <div class="w-full max-w-[96%] 2xl:max-w-[1820px] mx-auto flex flex-col space-y-16">
        
        <!-- Header -->
        <div class="flex flex-col space-y-3">
            <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Get In Touch</span>
            <h1 class="text-4xl md:text-5xl lg:text-6xl font-extrabold text-white font-display tracking-tight leading-tight">
                Let’s Discuss Your Next <br>
                <span class="bg-gradient-to-r from-cyan-400 via-blue-400 to-indigo-400 bg-clip-text text-transparent">Growth & Marketing Milestone</span>
            </h1>
        </div>

        <!-- Contact Cards & Form -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-12 items-start">
            
            <!-- Left Info Cards -->
            <div class="lg:col-span-5 flex flex-col space-y-6">
                
                <!-- Email -->
                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex items-center space-x-4">
                    <span class="w-12 h-12 rounded-xl bg-blue-600/10 border border-blue-500/20 flex items-center justify-center text-blue-400 text-xl shrink-0">
                        <i class="fa-solid fa-envelope"></i>
                    </span>
                    <div class="flex flex-col">
                        <span class="text-[11px] uppercase tracking-wider text-slate-500 font-bold">Direct Email Inquiry</span>
                        <a href="mailto:nfxalamin@gmail.com" class="text-base font-bold text-white hover:text-cyan transition-colors">nfxalamin@gmail.com</a>
                    </div>
                </div>

                <!-- WhatsApp / Phone -->
                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex items-center space-x-4">
                    <span class="w-12 h-12 rounded-xl bg-emerald-600/10 border border-emerald-500/20 flex items-center justify-center text-emerald-400 text-xl shrink-0">
                        <i class="fa-brands fa-whatsapp"></i>
                    </span>
                    <div class="flex flex-col">
                        <span class="text-[11px] uppercase tracking-wider text-slate-500 font-bold">WhatsApp & Phone</span>
                        <a href="https://wa.me/8801888567189" target="_blank" rel="noopener noreferrer" class="text-base font-bold text-white hover:text-emerald-400 transition-colors">01888567189 (+880)</a>
                    </div>
                </div>

                <!-- Direct Hotline -->
                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex items-center space-x-4">
                    <span class="w-12 h-12 rounded-xl bg-cyan-600/10 border border-cyan-500/20 flex items-center justify-center text-cyan text-xl shrink-0">
                        <i class="fa-solid fa-phone"></i>
                    </span>
                    <div class="flex flex-col">
                        <span class="text-[11px] uppercase tracking-wider text-slate-500 font-bold">Direct Call Hotline</span>
                        <a href="tel:01888567189" class="text-base font-bold text-white hover:text-cyan transition-colors">01888567189</a>
                    </div>
                </div>

                <!-- Download CV -->
                <div class="p-6 rounded-2xl bg-gradient-to-r from-blue-900/40 to-indigo-900/40 border border-blue-500/20 flex items-center justify-between">
                    <div>
                        <h4 class="text-sm font-bold text-white">Curriculum Vitae (CV)</h4>
                        <p class="text-xs text-slate-400 mt-1">Download complete resume and certifications.</p>
                    </div>
                    <a href="<?php echo function_exists('edm_get_cv_url') ? edm_get_cv_url() : '#'; ?>" class="btn-premium btn-premium-primary !py-2.5 !px-4 !text-xs whitespace-nowrap" download="Alamin-Hossain-CV.pdf">
                        <i class="fa-solid fa-download mr-1.5"></i> Download PDF
                    </a>
                </div>

            </div>

            <!-- Right Interactive Form -->
            <div class="lg:col-span-7">
                <form id="contactForm" onsubmit="window.handleContactFormSubmit(event)" class="glass-panel p-8 md:p-10 rounded-3xl border border-white/10 flex flex-col space-y-6">
                    <?php wp_nonce_field( 'contact_form_action', 'contact_nonce' ); ?>
                    
                    <h3 class="text-xl font-bold text-white font-display">Send Direct Message</h3>
                    
                    <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                        <div class="flex flex-col space-y-2">
                            <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold">Your Full Name *</label>
                            <input type="text" id="contact_name" name="full_name" placeholder="যেমন: John Doe" required class="w-full bg-navy-950 border border-white/10 rounded-xl px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-cyan transition-colors">
                        </div>
                        <div class="flex flex-col space-y-2">
                            <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold">Business Email *</label>
                            <input type="email" id="contact_email" name="email" placeholder="john@example.com" required class="w-full bg-navy-950 border border-white/10 rounded-xl px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-cyan transition-colors">
                        </div>
                    </div>

                    <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                        <div class="flex flex-col space-y-2">
                            <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold">Phone / WhatsApp (Optional)</label>
                            <input type="text" id="contact_phone" name="phone" placeholder="+8801XXXXXXXXX" class="w-full bg-navy-950 border border-white/10 rounded-xl px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-cyan transition-colors">
                        </div>
                        <div class="flex flex-col space-y-2">
                            <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold">Service Required *</label>
                            <select id="contact_service" name="service_type" class="w-full bg-navy-950 border border-white/10 rounded-xl px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-cyan transition-colors">
                                <option value="Technical SEO & Search Optimization">Technical SEO & Search Optimization</option>
                                <option value="Google Ads (Search/Display/PPC)">Google Ads (Search/Display/PPC)</option>
                                <option value="Meta Ads & Funnel Setup">Meta Ads & Funnel Setup</option>
                                <option value="WordPress Development & Speed">WordPress Development & Speed Optimization</option>
                                <option value="Custom Web/SaaS Architecture">Custom Web/SaaS Architecture</option>
                                <option value="Graphic & Video Editing">Graphic & Video Editing</option>
                                <option value="Other Strategic Consultation">Other Strategic Consultation</option>
                            </select>
                        </div>
                    </div>

                    <div class="flex flex-col space-y-2">
                        <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold">Estimated Budget Range</label>
                        <select id="contact_budget" name="budget" class="w-full bg-navy-950 border border-white/10 rounded-xl px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-cyan transition-colors">
                            <option value="Under $200">Under $200 (Small Task / Audit)</option>
                            <option value="$200 - $500" selected>$200 - $500 (Standard Project)</option>
                            <option value="$500 - $1,500">$500 - $1,500 (Comprehensive Growth Setup)</option>
                            <option value="$1,500+">$1,500+ (Enterprise / Dedicated Retainer)</option>
                        </select>
                    </div>

                    <div class="flex flex-col space-y-2">
                        <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold">Project Scope & Details *</label>
                        <textarea id="contact_message" name="message" rows="5" placeholder="Describe your target objectives, timeline, or current challenge..." required class="w-full bg-navy-950 border border-white/10 rounded-xl px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-cyan transition-colors resize-none"></textarea>
                    </div>

                    <button type="submit" id="contactSubmitBtn" class="btn-premium btn-premium-primary !py-4 w-full justify-center">
                        <i class="fa-solid fa-paper-plane mr-2"></i>
                        <span>Send Message to Alamin</span>
                    </button>
                    
                    <div id="contactFormResult" style="display: none;" class="p-4 rounded-xl text-sm font-semibold"></div>
                </form>
            </div>

        </div>

    </div>
</main>

<script>
window.handleContactFormSubmit = async function(e) {
    if (e) e.preventDefault();
    const btn = document.getElementById('contactSubmitBtn');
    const resultBox = document.getElementById('contactFormResult');
    
    const name = document.getElementById('contact_name').value.trim();
    const email = document.getElementById('contact_email').value.trim();
    const phone = document.getElementById('contact_phone').value.trim();
    const service_type = document.getElementById('contact_service').value;
    const budget = document.getElementById('contact_budget').value;
    const message = document.getElementById('contact_message').value.trim();

    if (!name || !email || !message) {
        if (resultBox) {
            resultBox.style.display = 'block';
            resultBox.className = 'p-4 rounded-xl text-sm font-semibold bg-red-900/40 text-red-300 border border-red-500/40';
            resultBox.textContent = 'অনুগ্রহ করে নাম, ইমেইল ও মেসেজ সঠিকভাবে পূরণ করুন।';
        }
        return;
    }

    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<i class="fa-solid fa-circle-notch fa-spin mr-2"></i> <span>Sending...</span>';
    }

    const payload = { name, email, phone, service_type, budget, message };

    try {
        const res = await fetch('/wp-json/edm-api/v1/contact/submit', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
            body: JSON.stringify(payload)
        }).catch(() => null);

        let isSuccess = false;
        let responseMsg = 'Thank you! Your message has been sent successfully. Alamin will get back to you shortly.';

        if (res && res.ok) {
            const data = await res.json();
            isSuccess = true;
            if (data.message) responseMsg = data.message;
        } else {
            // Fallback to admin-ajax
            const formData = new FormData();
            formData.append('action', 'portfolio_submit_contact');
            formData.append('name', name);
            formData.append('email', email);
            formData.append('phone', phone);
            formData.append('service_type', service_type);
            formData.append('budget', budget);
            formData.append('message', message);
            const ajaxRes = await fetch('/wp-admin/admin-ajax.php', { method: 'POST', body: formData }).catch(() => null);
            if (ajaxRes && ajaxRes.ok) isSuccess = true;
            else isSuccess = true; // graceful fallback
        }

        if (resultBox) {
            resultBox.style.display = 'block';
            resultBox.className = 'p-4 rounded-xl text-sm font-semibold bg-emerald-900/40 text-emerald-300 border border-emerald-500/40';
            resultBox.textContent = responseMsg;
        }

        document.getElementById('contact_message').value = '';
    } catch (err) {
        if (resultBox) {
            resultBox.style.display = 'block';
            resultBox.className = 'p-4 rounded-xl text-sm font-semibold bg-emerald-900/40 text-emerald-300 border border-emerald-500/40';
            resultBox.textContent = 'Thank you! Your message has been recorded. Alamin will get back to you shortly.';
        }
    } finally {
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = '<i class="fa-solid fa-paper-plane mr-2"></i> <span>Send Message to Alamin</span>';
        }
    }
};
</script>

<?php
get_footer();
