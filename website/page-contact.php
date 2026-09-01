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
                <form id="contactForm" class="glass-panel p-8 md:p-10 rounded-3xl border border-white/10 flex flex-col space-y-6">
                    <?php wp_nonce_field( 'contact_form_action', 'contact_nonce' ); ?>
                    
                    <h3 class="text-xl font-bold text-white font-display">Send Direct Message</h3>
                    
                    <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                        <div class="flex flex-col space-y-2">
                            <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold">Your Full Name</label>
                            <input type="text" name="full_name" placeholder="John Doe" required class="w-full bg-navy-950 border border-white/10 rounded-xl px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-cyan transition-colors">
                        </div>
                        <div class="flex flex-col space-y-2">
                            <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold">Business Email</label>
                            <input type="email" name="email" placeholder="john@example.com" required class="w-full bg-navy-950 border border-white/10 rounded-xl px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-cyan transition-colors">
                        </div>
                    </div>

                    <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                        <div class="flex flex-col space-y-2">
                            <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold">Your Website URL</label>
                            <input type="text" name="website" placeholder="https://example.com" class="w-full bg-navy-950 border border-white/10 rounded-xl px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-cyan transition-colors">
                        </div>
                        <div class="flex flex-col space-y-2">
                            <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold">Service Required</label>
                            <select name="service" class="w-full bg-navy-950 border border-white/10 rounded-xl px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-cyan transition-colors">
                                <option value="SEO Strategy & Audit">SEO Strategy & Core Vitals Audit</option>
                                <option value="Google Ads / PPC Management">Google Ads / PPC ROI Management</option>
                                <option value="Social Media Marketing">Social Media Scaling & Meta Ads</option>
                                <option value="EDM Software / SaaS Development">EDM Software & SaaS Architecture</option>
                            </select>
                        </div>
                    </div>

                    <div class="flex flex-col space-y-2">
                        <label class="text-[10px] uppercase tracking-wider text-slate-400 font-bold">Project Details</label>
                        <textarea name="details" rows="5" placeholder="Describe your target objectives, timeline, or current challenge..." required class="w-full bg-navy-950 border border-white/10 rounded-xl px-4 py-3 text-sm text-slate-200 focus:outline-none focus:border-cyan transition-colors resize-none"></textarea>
                    </div>

                    <button type="submit" class="btn-premium btn-premium-primary !py-4 w-full justify-center">
                        <i class="fa-solid fa-paper-plane mr-2"></i>
                        <span>Send Message to Alamin</span>
                    </button>
                    
                    <div id="contactFormResult" class="hidden p-4 rounded-xl text-sm font-semibold"></div>
                </form>
            </div>

        </div>

    </div>
</main>

<?php
get_footer();
