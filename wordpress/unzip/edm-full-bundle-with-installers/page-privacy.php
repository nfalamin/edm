<?php
/**
 * Template Name: Privacy Policy & Data Governance Charter
 * Description: 100% pure, legal-grade, zero-spyware Privacy Policy and Data Protection declaration.
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();
?>

<main class="min-h-screen pt-24 pb-24 px-4 sm:px-6 relative overflow-hidden">
    <!-- Ambient Background Blobs -->
    <div class="absolute top-10 left-1/3 w-[600px] h-[600px] bg-blue-600/10 rounded-full blur-[150px] pointer-events-none -z-10"></div>

    <div class="w-full max-w-[96%] 2xl:max-w-[1400px] mx-auto flex flex-col space-y-12">
        
        <!-- Header -->
        <div class="flex flex-col space-y-4 border-b border-white/10 pb-8">
            <div class="inline-flex items-center space-x-2 px-3.5 py-1 rounded-full bg-emerald-500/10 border border-emerald-500/20 w-fit">
                <span class="w-2 h-2 rounded-full bg-emerald-400"></span>
                <span class="text-xs font-mono uppercase tracking-widest text-emerald-400 font-bold">100% Pure Zero-Spyware Commitment</span>
            </div>

            <h1 class="text-3xl sm:text-5xl font-extrabold text-white font-display tracking-tight leading-tight">
                Privacy Policy & Data Governance Charter
            </h1>

            <div class="flex flex-wrap items-center gap-6 text-xs text-slate-400 font-mono">
                <span>Effective Date: <strong>August 22, 2026</strong></span>
                <span>•</span>
                <span>Jurisdiction: <strong>GDPR, CCPA & Global Compliance</strong></span>
                <span>•</span>
                <span>Authority: <strong>Alamin Hossain / EDM Engineering</strong></span>
            </div>
        </div>

        <!-- Privacy Content Sections -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-12 items-start">
            
            <!-- Main Legal Content -->
            <div class="lg:col-span-8 flex flex-col space-y-10 text-slate-300 text-sm sm:text-base leading-relaxed">
                
                <!-- Section 1 -->
                <section class="flex flex-col space-y-3">
                    <h2 class="text-xl sm:text-2xl font-bold text-white font-display flex items-center gap-3">
                        <span class="w-8 h-8 rounded-lg bg-blue-600/20 text-blue-400 flex items-center justify-center text-sm font-mono font-bold">01</span>
                        Our Fundamental Zero-Spyware Privacy Pledge
                    </h2>
                    <p>
                        At <strong>Alamin Hossain Engineering</strong> and <strong>Exclusive Download Manager (EDM)</strong>, user privacy is not an afterthought; it is an architectural foundation. We believe that professional desktop utilities and marketing consultation services must operate with total transparency.
                    </p>
                    <p class="bg-navy-900/60 p-4 rounded-xl border border-white/5 text-xs sm:text-sm text-slate-300">
                        🛡️ <strong>Absolute Guarantee:</strong> EDM does NOT record, track, monitor, or transmit your personal files, download contents, browsing history, DNS queries, or keystrokes. We do NOT bundle third-party adware, toolbars, crypto-miners, or market-research telemetry into our software packages.
                    </p>
                </section>

                <!-- Section 2 -->
                <section class="flex flex-col space-y-3">
                    <h2 class="text-xl sm:text-2xl font-bold text-white font-display flex items-center gap-3">
                        <span class="w-8 h-8 rounded-lg bg-cyan-600/20 text-cyan flex items-center justify-center text-sm font-mono font-bold">02</span>
                        Information We Collect & Local-First Processing
                    </h2>
                    <p>
                        Our desktop application operates on a <strong>Local-First Architecture</strong>. All download state tracking, 32-socket byte-range reconstruction, and SQLite transactional logs reside exclusively on your local computer's physical drive:
                    </p>
                    <ul class="list-disc list-inside space-y-2 text-xs sm:text-sm text-slate-400 pl-2">
                        <li><strong>License Authentication Data:</strong> When activating a Pro or Enterprise license, we transmit only an anonymized cryptographic Hardware ID (HWID hash), license serial key, and timestamp to authenticate your active device limits.</li>
                        <li><strong>Anonymous Crash Telemetry (Optional):</strong> If the application encounters an unhandled exception, you may choose to submit an error log. This log contains only memory stack traces and .NET runtime versions, strictly excluding user identifiers.</li>
                        <li><strong>Website Inquiries & Consultation:</strong> When you submit our contact form, we collect your name, email address, phone number, and project details solely for direct strategic communication.</li>
                    </ul>
                </section>

                <!-- Section 3 -->
                <section class="flex flex-col space-y-3">
                    <h2 class="text-xl sm:text-2xl font-bold text-white font-display flex items-center gap-3">
                        <span class="w-8 h-8 rounded-lg bg-emerald-600/20 text-emerald-400 flex items-center justify-center text-sm font-mono font-bold">03</span>
                        Browser Extension Privacy (Manifest V3 Strict Sandboxing)
                    </h2>
                    <p>
                        Our official browser extensions for Google Chrome, Microsoft Edge, and Mozilla Firefox adhere to the strictest <strong>Manifest V3 Security Standards</strong>:
                    </p>
                    <ul class="list-disc list-inside space-y-2 text-xs sm:text-sm text-slate-400 pl-2">
                        <li>The extension only reads media candidate stream URLs (such as `.mp4`, `.m3u8`, `.iso`, `.zip`) when an active download is intercepted or when you explicitly right-click a link.</li>
                        <li>No web pages, browsing sessions, private messages, or form inputs are ever recorded or stored outside your browser sandbox.</li>
                        <li>Communication between the browser extension and the EDM Windows desktop application occurs strictly over local encrypted Windows Inter-Process Communication (IPC Native Messaging Host).</li>
                    </ul>
                </section>

                <!-- Section 4 -->
                <section class="flex flex-col space-y-3">
                    <h2 class="text-xl sm:text-2xl font-bold text-white font-display flex items-center gap-3">
                        <span class="w-8 h-8 rounded-lg bg-gold/20 text-gold flex items-center justify-center text-sm font-mono font-bold">04</span>
                        Payment Security & Zero-Financial Storage
                    </h2>
                    <p>
                        All commercial software license purchases and consultation deposits are processed through certified, PCI-DSS Level 1 compliant payment gateways (e.g. Stripe, Paddle, bKash, Nagad Merchant Gateway).
                    </p>
                    <p class="text-xs sm:text-sm text-slate-400">
                        We never store, log, or have access to your full credit card numbers, CVV codes, or banking credentials on our web servers.
                    </p>
                </section>

                <!-- Section 5 -->
                <section class="flex flex-col space-y-3">
                    <h2 class="text-xl sm:text-2xl font-bold text-white font-display flex items-center gap-3">
                        <span class="w-8 h-8 rounded-lg bg-indigo-600/20 text-indigo-400 flex items-center justify-center text-sm font-mono font-bold">05</span>
                        GDPR, CCPA & User Rights
                    </h2>
                    <p>
                        In accordance with the European Union General Data Protection Regulation (GDPR) and California Consumer Privacy Act (CCPA), you retain the following inviolable rights:
                    </p>
                    <ul class="list-disc list-inside space-y-1.5 text-xs sm:text-sm text-slate-400 pl-2">
                        <li><strong>Right of Access:</strong> Request a full copy of any personal data associated with your license.</li>
                        <li><strong>Right to Erasure ("Right to be Forgotten"):</strong> Request immediate permanent deletion of your customer record.</li>
                        <li><strong>Right to Non-Discrimination:</strong> Equal service regardless of privacy preference.</li>
                        <li><strong>Zero Sale of Data:</strong> We never sell, rent, or monetize your personal information to advertisers.</li>
                    </ul>
                </section>

                <!-- Section 6 -->
                <section class="flex flex-col space-y-3">
                    <h2 class="text-xl sm:text-2xl font-bold text-white font-display flex items-center gap-3">
                        <span class="w-8 h-8 rounded-lg bg-pink-600/20 text-pink-400 flex items-center justify-center text-sm font-mono font-bold">06</span>
                        Cryptographic Integrity & Authenticode Security
                    </h2>
                    <p>
                        Every release binary of EDM is cryptographically hashed and digitally signed with a verified Microsoft Authenticode Code Signing Certificate. This ensures that the file you download from our servers is identical to our certified master build, free from third-party tampering or network manipulation.
                    </p>
                </section>

            </div>

            <!-- Right Summary Sticky Sidebar -->
            <div class="lg:col-span-4 flex flex-col space-y-6 sticky top-28">
                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex flex-col space-y-4">
                    <h3 class="text-base font-bold text-white flex items-center gap-2">
                        <i class="fa-solid fa-lock text-cyan"></i>
                        Data Protection Officer (DPO)
                    </h3>
                    <p class="text-xs text-slate-400 leading-relaxed">
                        For all privacy inquiries, data deletion requests, or cryptographic audits, contact our lead engineer directly:
                    </p>
                    <div class="text-xs font-mono space-y-2 border-t border-white/5 pt-3">
                        <div><span class="text-slate-500">Lead Architect:</span> <strong class="text-white">Alamin Hossain</strong></div>
                        <div><span class="text-slate-500">Direct Email:</span> <a href="mailto:nfxalamin@gmail.com" class="text-cyan font-bold hover:underline">nfxalamin@gmail.com</a></div>
                        <div><span class="text-slate-500">Direct Phone:</span> <a href="tel:01888567189" class="text-white font-bold hover:underline">01888567189</a></div>
                        <div><span class="text-slate-500">Response SLA:</span> <strong class="text-emerald-400">Within 24 Hours</strong></div>
                    </div>
                </div>

                <div class="glass-panel p-6 rounded-2xl border border-white/10 flex flex-col space-y-3 bg-gradient-to-b from-navy-900 to-navy-950">
                    <h4 class="text-xs uppercase tracking-widest text-gold font-bold">Related Documents</h4>
                    <ul class="space-y-2 text-xs">
                        <li><a href="<?php echo esc_url(home_url('/terms/')); ?>" class="text-slate-300 hover:text-cyan transition-colors flex items-center gap-2"><i class="fa-solid fa-file-contract text-slate-500"></i> Terms of Service & EULA</a></li>
                        <li><a href="<?php echo esc_url(home_url('/edm-download/')); ?>" class="text-slate-300 hover:text-cyan transition-colors flex items-center gap-2"><i class="fa-solid fa-download text-slate-500"></i> SHA-256 Checksum Authority</a></li>
                        <li><a href="<?php echo esc_url(home_url('/about/')); ?>" class="text-slate-300 hover:text-cyan transition-colors flex items-center gap-2"><i class="fa-solid fa-user text-slate-500"></i> About Alamin Hossain</a></li>
                    </ul>
                </div>
            </div>

        </div>

    </div>
</main>

<?php
get_footer();
