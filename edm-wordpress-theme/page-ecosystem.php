<?php
/**
 * Template Name: Full-Stack Architecture Ecosystem
 * Description: Dedicated engineering blueprint and architecture page showcasing the complete EDM ecosystem.
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();

$version = edm_get_latest_version();
$download_url = edm_get_download_url();
?>

<!-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
     HERO BANNER: SYSTEM ARCHITECTURE
     â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â• -->
<section class="page-banner" style="background: radial-gradient(circle at 50% 0%, rgba(6, 240, 251, 0.12), transparent 70%), #05080C; padding: 70px 0 50px; border-bottom: 1px solid rgba(255,255,255,0.06);">
    <div class="container" style="max-width: 1200px; margin: 0 auto; padding: 0 20px;">
        <?php edm_render_breadcrumbs(esc_html__('System Architecture', 'edm-theme')); ?>
        <div style="display: inline-flex; align-items: center; gap: 8px; background: rgba(6,240,251,0.1); border: 1px solid rgba(6,240,251,0.25); padding: 4px 12px; border-radius: 20px; color: #06F0FB; font-size: 11px; font-weight: 700; text-transform: uppercase; margin-bottom: 16px;">
            <span style="width: 6px; height: 6px; border-radius: 50%; background: #06F0FB; box-shadow: 0 0 8px #06F0FB;"></span>
            Engineering Blueprint â€¢ .NET 10 & WordPress Enterprise
        </div>
        <h1 style="font-size: clamp(2rem, 4vw, 3rem); font-weight: 900; color: #FFF; line-height: 1.15; margin-bottom: 14px;">
            EDM Full-Stack <span style="background: linear-gradient(135deg, #06F0FB, #3B82F6); -webkit-background-clip: text; -webkit-text-fill-color: transparent;">Ecosystem Architecture</span>
        </h1>
        <p style="font-size: 16px; color: #94A3B8; max-width: 760px; line-height: 1.6;">
            A unified, battle-tested systems architecture connecting high-performance C# / WPF desktop clients, native browser extension messaging, zero-trust RESTful control plane APIs, SQLite/EF Core databases, and real-time geographic telemetry.
        </p>
    </div>
</section>

<!-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
     INTERACTIVE 4-TIER ARCHITECTURAL STACK
     â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â• -->
<section style="padding: 60px 0; background: #05080C;">
    <div class="container" style="max-width: 1200px; margin: 0 auto; padding: 0 20px;">

        <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 24px; margin-bottom: 50px;">
            
            <!-- Tier 1: Client Application -->
            <div style="background: rgba(11, 15, 20, 0.95); border: 1px solid rgba(6,240,251,0.2); border-radius: 16px; padding: 24px; box-shadow: 0 8px 30px rgba(0,0,0,0.5);">
                <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 16px;">
                    <div style="width: 44px; height: 44px; border-radius: 12px; background: rgba(6,240,251,0.15); display: flex; align-items: center; justify-content: center; color: #06F0FB; font-size: 20px;">
                        <i data-lucide="monitor"></i>
                    </div>
                    <div>
                        <span style="font-size: 10px; font-weight: 700; color: #06F0FB; text-transform: uppercase; letter-spacing: 0.5px;">Tier 1 â€¢ Desktop Core</span>
                        <h3 style="font-size: 18px; font-weight: 800; color: #FFF; margin: 0;">EDM Desktop Client</h3>
                    </div>
                </div>
                <p style="font-size: 13px; color: #94A3B8; line-height: 1.5; margin-bottom: 16px;">
                    Built on <strong>C# / .NET 10.0 WPF</strong> featuring a 32-socket multi-threaded download engine, Write-Ahead Logging (WAL) state persistence, and direct NTFS pre-allocation.
                </p>
                <div style="display: flex; flex-wrap: wrap; gap: 6px;">
                    <span style="font-size: 10px; background: rgba(255,255,255,0.06); padding: 3px 8px; border-radius: 4px; color: #CBD5E1;">.NET 10 WPF</span>
                    <span style="font-size: 10px; background: rgba(255,255,255,0.06); padding: 3px 8px; border-radius: 4px; color: #CBD5E1;">32-Socket Engine</span>
                    <span style="font-size: 10px; background: rgba(255,255,255,0.06); padding: 3px 8px; border-radius: 4px; color: #CBD5E1;">WAL Logging</span>
                </div>
            </div>

            <!-- Tier 2: Browser Integration -->
            <div style="background: rgba(11, 15, 20, 0.95); border: 1px solid rgba(59,130,246,0.2); border-radius: 16px; padding: 24px; box-shadow: 0 8px 30px rgba(0,0,0,0.5);">
                <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 16px;">
                    <div style="width: 44px; height: 44px; border-radius: 12px; background: rgba(59,130,246,0.15); display: flex; align-items: center; justify-content: center; color: #3B82F6; font-size: 20px;">
                        <i data-lucide="puzzle"></i>
                    </div>
                    <div>
                        <span style="font-size: 10px; font-weight: 700; color: #3B82F6; text-transform: uppercase; letter-spacing: 0.5px;">Tier 2 â€¢ Native Host</span>
                        <h3 style="font-size: 18px; font-weight: 800; color: #FFF; margin: 0;">Browser Extensions</h3>
                    </div>
                </div>
                <p style="font-size: 13px; color: #94A3B8; line-height: 1.5; margin-bottom: 16px;">
                    Manifest V3 extensions for <strong>Chrome, Edge, Firefox</strong> with standard stdio Native Messaging Protocol for 4K video sniffing and zero-latency click interception.
                </p>
                <div style="display: flex; flex-wrap: wrap; gap: 6px;">
                    <span style="font-size: 10px; background: rgba(255,255,255,0.06); padding: 3px 8px; border-radius: 4px; color: #CBD5E1;">Manifest V3</span>
                    <span style="font-size: 10px; background: rgba(255,255,255,0.06); padding: 3px 8px; border-radius: 4px; color: #CBD5E1;">Native Messaging</span>
                    <span style="font-size: 10px; background: rgba(255,255,255,0.06); padding: 3px 8px; border-radius: 4px; color: #CBD5E1;">4K Video Sniffer</span>
                </div>
            </div>

            <!-- Tier 3: Control Plane API -->
            <div style="background: rgba(11, 15, 20, 0.95); border: 1px solid rgba(245,158,11,0.2); border-radius: 16px; padding: 24px; box-shadow: 0 8px 30px rgba(0,0,0,0.5);">
                <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 16px;">
                    <div style="width: 44px; height: 44px; border-radius: 12px; background: rgba(245,158,11,0.15); display: flex; align-items: center; justify-content: center; color: #F59E0B; font-size: 20px;">
                        <i data-lucide="server"></i>
                    </div>
                    <div>
                        <span style="font-size: 10px; font-weight: 700; color: #F59E0B; text-transform: uppercase; letter-spacing: 0.5px;">Tier 3 â€¢ Backend API</span>
                        <h3 style="font-size: 18px; font-weight: 800; color: #FFF; margin: 0;">ASP.NET Core 10 Web API</h3>
                    </div>
                </div>
                <p style="font-size: 13px; color: #94A3B8; line-height: 1.5; margin-bottom: 16px;">
                    Central SaaS control plane with JWT authentication, licensing, automatic delta updates, and privacy-safe device telemetry aggregation.
                </p>
                <div style="display: flex; flex-wrap: wrap; gap: 6px;">
                    <span style="font-size: 10px; background: rgba(255,255,255,0.06); padding: 3px 8px; border-radius: 4px; color: #CBD5E1;">ASP.NET Core 10</span>
                    <span style="font-size: 10px; background: rgba(255,255,255,0.06); padding: 3px 8px; border-radius: 4px; color: #CBD5E1;">JWT / RSA256</span>
                    <span style="font-size: 10px; background: rgba(255,255,255,0.06); padding: 3px 8px; border-radius: 4px; color: #CBD5E1;">EF Core 10</span>
                </div>
            </div>

            <!-- Tier 4: Geographic Telemetry -->
            <div style="background: rgba(11, 15, 20, 0.95); border: 1px solid rgba(16,185,129,0.2); border-radius: 16px; padding: 24px; box-shadow: 0 8px 30px rgba(0,0,0,0.5);">
                <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 16px;">
                    <div style="width: 44px; height: 44px; border-radius: 12px; background: rgba(16,185,129,0.15); display: flex; align-items: center; justify-content: center; color: #10B981; font-size: 20px;">
                        <i data-lucide="globe"></i>
                    </div>
                    <div>
                        <span style="font-size: 10px; font-weight: 700; color: #10B981; text-transform: uppercase; letter-spacing: 0.5px;">Tier 4 â€¢ Telemetry Vector Map</span>
                        <h3 style="font-size: 18px; font-weight: 800; color: #FFF; margin: 0;">Sovereign Geographic Engine</h3>
                    </div>
                </div>
                <p style="font-size: 13px; color: #94A3B8; line-height: 1.5; margin-bottom: 16px;">
                    Real-time GeoJSON vector map with ISO 3166-1 flag metadata, choropleth heat intensity scaling, and live WebSocket telemetry pulses.
                </p>
                <div style="display: flex; flex-wrap: wrap; gap: 6px;">
                    <span style="font-size: 10px; background: rgba(255,255,255,0.06); padding: 3px 8px; border-radius: 4px; color: #CBD5E1;">GeoJSON Vector</span>
                    <span style="font-size: 10px; background: rgba(255,255,255,0.06); padding: 3px 8px; border-radius: 4px; color: #CBD5E1;">ISO Flags</span>
                    <span style="font-size: 10px; background: rgba(255,255,255,0.06); padding: 3px 8px; border-radius: 4px; color: #CBD5E1;">Choropleth Heat</span>
                </div>
            </div>

        </div>

        <!-- Call to Action Banner -->
        <div style="background: linear-gradient(135deg, rgba(6,240,251,0.08), rgba(59,130,246,0.12)); border: 1px solid rgba(6,240,251,0.3); border-radius: 16px; padding: 36px; text-align: center;">
            <h2 style="font-size: 24px; font-weight: 800; color: #FFF; margin-bottom: 10px;">Experience the High-Speed Engine in Action</h2>
            <p style="font-size: 14px; color: #94A3B8; max-width: 600px; margin: 0 auto 24px; line-height: 1.5;">
                Download the verified production release of EDM for Windows 10/11 x64 and unlock 32x parallel download performance today.
            </p>
            <div style="display: flex; justify-content: center; gap: 14px; flex-wrap: wrap;">
                <a href="<?php echo esc_url($download_url); ?>" class="btn btn-primary" download style="display: inline-flex; align-items: center; gap: 8px;">
                    <i data-lucide="download"></i> Download EDM v<?php echo esc_html($version); ?>
                </a>
                <a href="<?php echo esc_url(home_url('/dashboard/')); ?>" class="btn btn-outline" style="display: inline-flex; align-items: center; gap: 8px;">
                    <i data-lucide="layout-dashboard"></i> Open Control Plane
                </a>
            </div>
        </div>

    </div>
</section>

<?php
get_footer();