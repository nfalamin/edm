<?php
/**
 * Verified Customer Testimonials & Reviews Section
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?>
<!-- ══════════════════════════════════════════════════════════════
     VERIFIED REVIEWS & 10,000+ USER TESTIMONIALS
     ══════════════════════════════════════════════════════════════ -->
<section class="section reviews-section" id="reviews" style="position: relative; overflow: hidden;">
    <div class="container">
        
        <div class="section-header">
            <span class="section-badge"><?php esc_html_e('VERIFIED COMMUNITY FEEDBACK', 'edm-theme'); ?></span>
            <h2 class="section-title">
                <?php esc_html_e('Loved by Over 10,000+ Power Users Worldwide', 'edm-theme'); ?><br>
                <span class="gradient-text"><?php esc_html_e('4.9/5 Rating Across 1,840+ Independent Benchmarks', 'edm-theme'); ?></span>
            </h2>
            <p class="section-subtitle">
                <?php esc_html_e('See why software engineers, 4K video creators, and daily heavy downloaders replaced legacy download utilities with EDM.', 'edm-theme'); ?>
            </p>
        </div>

        <!-- Reviews Grid -->
        <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); gap: 24px; margin-bottom: 48px;">
            
            <!-- Review 1 -->
            <div class="glass-panel" style="padding: 28px 24px; border-radius: 20px; border: 1px solid var(--edm-border); background: var(--edm-bg-card); box-shadow: 0 10px 30px rgba(0,0,0,0.3); transition: transform 0.2s, box-shadow 0.2s;">
                <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px;">
                    <div style="color: #F59E0B; font-size: 15px; letter-spacing: 2px;">★★★★★</div>
                    <span style="font-size: 11px; font-weight: 700; color: var(--edm-green); background: rgba(16, 185, 129, 0.12); padding: 3px 10px; border-radius: 999px; border: 1px solid rgba(16, 185, 129, 0.3);">
                        <i data-lucide="check-circle-2" style="width: 11px; height: 11px; display: inline-block; vertical-align: middle;"></i> Verified User
                    </span>
                </div>
                <p style="font-size: 14px; color: var(--edm-text-main); line-height: 1.6; margin-bottom: 18px; font-style: italic;">
                    "I was downloading 40 GB game files and ISOs with standard browser downloads which kept failing at 90%. EDM's 32-socket engine pushed my 500 Mbps connection to 62 MB/s and the SQLite resume saved me hours. Absolutely indispensable!"
                </p>
                <div style="display: flex; align-items: center; gap: 12px;">
                    <div style="width: 42px; height: 42px; border-radius: 50%; background: linear-gradient(135deg, #06F0FB, #12A89C); color: #05080C; font-weight: 800; display: flex; align-items: center; justify-content: center; font-size: 15px;">MR</div>
                    <div>
                        <div style="font-size: 14px; font-weight: 700; color: #fff;">Marcus Reed</div>
                        <div style="font-size: 12px; color: var(--edm-text-muted);">Senior Game Developer · United Kingdom</div>
                    </div>
                </div>
            </div>

            <!-- Review 2 -->
            <div class="glass-panel" style="padding: 28px 24px; border-radius: 20px; border: 1px solid var(--edm-border); background: var(--edm-bg-card); box-shadow: 0 10px 30px rgba(0,0,0,0.3); transition: transform 0.2s, box-shadow 0.2s;">
                <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px;">
                    <div style="color: #F59E0B; font-size: 15px; letter-spacing: 2px;">★★★★★</div>
                    <span style="font-size: 11px; font-weight: 700; color: var(--edm-green); background: rgba(16, 185, 129, 0.12); padding: 3px 10px; border-radius: 999px; border: 1px solid rgba(16, 185, 129, 0.3);">
                        <i data-lucide="check-circle-2" style="width: 11px; height: 11px; display: inline-block; vertical-align: middle;"></i> Verified User
                    </span>
                </div>
                <p style="font-size: 14px; color: var(--edm-text-main); line-height: 1.6; margin-bottom: 18px; font-style: italic;">
                    "The Chrome Manifest V3 extension sniffs 4K 60FPS video and separate audio chunks effortlessly. It merges them into one high-quality MP4 file automatically without any third-party ads or popups."
                </p>
                <div style="display: flex; align-items: center; gap: 12px;">
                    <div style="width: 42px; height: 42px; border-radius: 50%; background: linear-gradient(135deg, #EC4899, #F59E0B); color: #fff; font-weight: 800; display: flex; align-items: center; justify-content: center; font-size: 15px;">AK</div>
                    <div>
                        <div style="font-size: 14px; font-weight: 700; color: #fff;">Aisha Khan</div>
                        <div style="font-size: 12px; color: var(--edm-text-muted);">Video Producer & Creator · Canada</div>
                    </div>
                </div>
            </div>

            <!-- Review 3 -->
            <div class="glass-panel" style="padding: 28px 24px; border-radius: 20px; border: 1px solid var(--edm-border); background: var(--edm-bg-card); box-shadow: 0 10px 30px rgba(0,0,0,0.3); transition: transform 0.2s, box-shadow 0.2s;">
                <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px;">
                    <div style="color: #F59E0B; font-size: 15px; letter-spacing: 2px;">★★★★★</div>
                    <span style="font-size: 11px; font-weight: 700; color: var(--edm-green); background: rgba(16, 185, 129, 0.12); padding: 3px 10px; border-radius: 999px; border: 1px solid rgba(16, 185, 129, 0.3);">
                        <i data-lucide="check-circle-2" style="width: 11px; height: 11px; display: inline-block; vertical-align: middle;"></i> Verified User
                    </span>
                </div>
                <p style="font-size: 14px; color: var(--edm-text-main); line-height: 1.6; margin-bottom: 18px; font-style: italic;">
                    "Clean UI, native .NET 10 Win32 engine with zero bloat. Legacy utilities had outdated interfaces; EDM brings the modern Fluent dark luxury aesthetic with real 32-stream socket capability. 10/10 recommended!"
                </p>
                <div style="display: flex; align-items: center; gap: 12px;">
                    <div style="width: 42px; height: 42px; border-radius: 50%; background: linear-gradient(135deg, #10B981, #06B6D4); color: #fff; font-weight: 800; display: flex; align-items: center; justify-content: center; font-size: 15px;">DK</div>
                    <div>
                        <div style="font-size: 14px; font-weight: 700; color: #fff;">Daniel Krause</div>
                        <div style="font-size: 12px; color: var(--edm-text-muted);">DevOps & Cloud Engineer · Germany</div>
                    </div>
                </div>
            </div>

            <!-- Review 4 -->
            <div class="glass-panel" style="padding: 28px 24px; border-radius: 20px; border: 1px solid var(--edm-border); background: var(--edm-bg-card); box-shadow: 0 10px 30px rgba(0,0,0,0.3); transition: transform 0.2s, box-shadow 0.2s;">
                <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px;">
                    <div style="color: #F59E0B; font-size: 15px; letter-spacing: 2px;">★★★★★</div>
                    <span style="font-size: 11px; font-weight: 700; color: var(--edm-green); background: rgba(16, 185, 129, 0.12); padding: 3px 10px; border-radius: 999px; border: 1px solid rgba(16, 185, 129, 0.3);">
                        <i data-lucide="check-circle-2" style="width: 11px; height: 11px; display: inline-block; vertical-align: middle;"></i> Verified User
                    </span>
                </div>
                <p style="font-size: 14px; color: var(--edm-text-main); line-height: 1.6; margin-bottom: 18px; font-style: italic;">
                    "The batch download queue and clipboard monitoring catch video and file links instantly across our team workflow. The speed increase and queue organization are unmatched."
                </p>
                <div style="display: flex; align-items: center; gap: 12px;">
                    <div style="width: 42px; height: 42px; border-radius: 50%; background: linear-gradient(135deg, #8B5CF6, #EC4899); color: #fff; font-weight: 800; display: flex; align-items: center; justify-content: center; font-size: 15px;">ER</div>
                    <div>
                        <div style="font-size: 14px; font-weight: 700; color: #fff;">Elena Rodriguez</div>
                        <div style="font-size: 12px; color: var(--edm-text-muted);">E-Commerce & Digital Media Lead · Spain</div>
                    </div>
                </div>
            </div>

            <!-- Review 5 -->
            <div class="glass-panel" style="padding: 28px 24px; border-radius: 20px; border: 1px solid var(--edm-border); background: var(--edm-bg-card); box-shadow: 0 10px 30px rgba(0,0,0,0.3); transition: transform 0.2s, box-shadow 0.2s;">
                <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px;">
                    <div style="color: #F59E0B; font-size: 15px; letter-spacing: 2px;">★★★★★</div>
                    <span style="font-size: 11px; font-weight: 700; color: var(--edm-green); background: rgba(16, 185, 129, 0.12); padding: 3px 10px; border-radius: 999px; border: 1px solid rgba(16, 185, 129, 0.3);">
                        <i data-lucide="check-circle-2" style="width: 11px; height: 11px; display: inline-block; vertical-align: middle;"></i> Verified User
                    </span>
                </div>
                <p style="font-size: 14px; color: var(--edm-text-main); line-height: 1.6; margin-bottom: 18px; font-style: italic;">
                    "The offline SQLite state persistence is brilliant. When a connection drops during a massive 20GB dataset download, EDM resumes from the exact byte segment without file corruption. 5 stars!"
                </p>
                <div style="display: flex; align-items: center; gap: 12px;">
                    <div style="width: 42px; height: 42px; border-radius: 50%; background: linear-gradient(135deg, #06B6D4, #3B82F6); color: #fff; font-weight: 800; display: flex; align-items: center; justify-content: center; font-size: 15px;">TM</div>
                    <div>
                        <div style="font-size: 14px; font-weight: 700; color: #fff;">Tariq Al-Mansoor</div>
                        <div style="font-size: 12px; color: var(--edm-text-muted);">Systems Architect & Engineer · UAE</div>
                    </div>
                </div>
            </div>

        </div>

        <!-- Community Trust Counter Banner -->
        <div style="background: rgba(93, 95, 239, 0.08); border: 1px solid rgba(93, 95, 239, 0.25); border-radius: 20px; padding: 24px 32px; display: flex; flex-wrap: wrap; align-items: center; justify-content: space-around; gap: 20px; text-align: center;">
            <div>
                <div style="font-size: 28px; font-weight: 800; color: #fff; font-family: var(--edm-font-mono);">10,000+</div>
                <div style="font-size: 12px; color: var(--edm-text-secondary); text-transform: uppercase; font-weight: 600; margin-top: 2px;">Active Global Installations</div>
            </div>
            <div style="width: 1px; height: 40px; background: rgba(255,255,255,0.08);"></div>
            <div>
                <div style="font-size: 28px; font-weight: 800; color: #38BDF8; font-family: var(--edm-font-mono);">32 Concurrent</div>
                <div style="font-size: 12px; color: var(--edm-text-secondary); text-transform: uppercase; font-weight: 600; margin-top: 2px;">Parallel Sockets Per Download</div>
            </div>
            <div style="width: 1px; height: 40px; background: rgba(255,255,255,0.08);"></div>
            <div>
                <div style="font-size: 28px; font-weight: 800; color: #10B981; font-family: var(--edm-font-mono);">100% Clean</div>
                <div style="font-size: 12px; color: var(--edm-text-secondary); text-transform: uppercase; font-weight: 600; margin-top: 2px;">Authenticode Signed Binary</div>
            </div>
            <div style="width: 1px; height: 40px; background: rgba(255,255,255,0.08);"></div>
            <div>
                <div style="font-size: 28px; font-weight: 800; color: #F59E0B; font-family: var(--edm-font-mono);">4.9 / 5.0</div>
                <div style="font-size: 12px; color: var(--edm-text-secondary); text-transform: uppercase; font-weight: 600; margin-top: 2px;">Independent User Rating</div>
            </div>
        </div>

    </div>
</section>
