<?php
/**
 * Portfolio Template Part: EDM Flagship Product Showcase Section
 * Positioned mid-page to demonstrate full-stack software engineering excellence.
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$edm_hub_url  = esc_url(home_url('/edm/'));
$download_url = function_exists('edm_get_download_url') ? edm_get_download_url() : esc_url(get_template_directory_uri() . '/downloads/EDM-Setup-v2.1.0.exe');
$version      = function_exists('edm_get_latest_version') ? edm_get_latest_version() : '2.1.0';
?>
<!-- ══════════════════════════════════════════════════════════════
     EDM FLAGSHIP SOFTWARE SHOWCASE (PORTFOLIO INTEGRATION)
     ══════════════════════════════════════════════════════════════ -->
<section id="edm-showcase" class="py-20 md:py-28 px-4 sm:px-6 relative overflow-hidden bg-gradient-to-b from-navy-900/40 via-navy-950 to-navy-900/40 border-y border-white/5">
    
    <!-- Background Ambient Glow -->
    <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[700px] h-[500px] bg-gradient-to-tr from-blue-600/15 via-cyan-500/10 to-indigo-600/15 rounded-full blur-[140px] pointer-events-none -z-10"></div>
    <div class="absolute -top-24 right-10 w-80 h-80 bg-blue-500/10 rounded-full blur-3xl pointer-events-none -z-10"></div>

    <div class="max-w-7xl mx-auto flex flex-col space-y-12">
        
        <!-- Section Header -->
        <div class="flex flex-col md:flex-row md:items-end justify-between gap-6 reveal">
            <div class="flex flex-col space-y-3 max-w-2xl">
                <div class="inline-flex items-center space-x-2 px-3.5 py-1 rounded-full bg-blue-500/10 border border-blue-400/20 text-cyan text-xs font-semibold uppercase tracking-wider w-fit">
                    <span class="w-2 h-2 rounded-full bg-cyan animate-ping"></span>
                    <span>Flagship Engineering Showcase · v<?php echo esc_html($version); ?></span>
                </div>
                <h2 class="text-3xl sm:text-4xl md:text-5xl font-extrabold text-white font-display tracking-tight leading-tight">
                    Exclusive Download Manager <br class="hidden sm:inline">
                    <span class="bg-gradient-to-r from-blue-400 via-cyan-300 to-indigo-400 bg-clip-text text-transparent">32-Socket Turbo Architecture</span>
                </h2>
                <p class="text-slate-400 text-sm sm:text-base leading-relaxed">
                    A multi-threaded Windows desktop accelerator with dynamic socket slicing, zero-click browser sniffing, and automated media segment reassembly.
                </p>
            </div>
            
            <div class="flex items-center gap-3">
                <a href="<?php echo $edm_hub_url; ?>" class="btn-premium btn-premium-primary !px-6 !py-3 !text-xs whitespace-nowrap">
                    <span>Explore Product Hub</span>
                    <i class="fa-solid fa-arrow-right ml-2 text-xs"></i>
                </a>
            </div>
        </div>

        <!-- Master Showcase Glassmorphic Card -->
        <div class="glass-panel p-6 sm:p-8 md:p-10 rounded-3xl border border-white/10 shadow-2xl relative overflow-hidden reveal">
            
            <div class="grid grid-cols-1 lg:grid-cols-12 gap-8 lg:gap-12 items-center">
                
                <!-- Left Details & Metric Cards -->
                <div class="lg:col-span-5 flex flex-col space-y-6">
                    
                    <!-- Quick Stats Badges -->
                    <div class="grid grid-cols-2 gap-3 sm:gap-4">
                        <div class="bg-navy-900/90 p-4 rounded-2xl border border-white/5 flex flex-col space-y-1">
                            <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Download Speed</span>
                            <div class="flex items-baseline space-x-1.5">
                                <span id="edm-live-speed-val" class="text-2xl sm:text-3xl font-extrabold text-cyan font-mono">48.6</span>
                                <span class="text-xs font-bold text-slate-400">MB/s</span>
                            </div>
                            <span class="text-[10px] text-emerald-400 font-semibold flex items-center gap-1">
                                <i class="fa-solid fa-bolt text-[9px]"></i> 32x Acceleration Active
                            </span>
                        </div>

                        <div class="bg-navy-900/90 p-4 rounded-2xl border border-white/5 flex flex-col space-y-1">
                            <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Active Sockets</span>
                            <div class="flex items-baseline space-x-1.5">
                                <span class="text-2xl sm:text-3xl font-extrabold text-white font-mono">32</span>
                                <span class="text-xs font-bold text-slate-400">/ 32</span>
                            </div>
                            <span class="text-[10px] text-blue-400 font-semibold flex items-center gap-1">
                                <i class="fa-solid fa-network-wired text-[9px]"></i> 0% Packet Loss
                            </span>
                        </div>
                    </div>

                    <!-- Core Feature Highlights -->
                    <div class="flex flex-col space-y-3">
                        <div class="flex items-center space-x-3 p-3 rounded-xl bg-white/[0.02] border border-white/5">
                            <div class="w-9 h-9 rounded-lg bg-blue-500/10 flex items-center justify-center text-blue-400 shrink-0">
                                <i class="fa-solid fa-microchip text-sm"></i>
                            </div>
                            <div>
                                <h4 class="text-xs sm:text-sm font-bold text-white">Dynamic Segment Slicing</h4>
                                <p class="text-[11px] text-slate-400">Splits large payload streams into 32 simultaneous HTTP ranges.</p>
                            </div>
                        </div>

                        <div class="flex items-center space-x-3 p-3 rounded-xl bg-white/[0.02] border border-white/5">
                            <div class="w-9 h-9 rounded-lg bg-cyan-500/10 flex items-center justify-center text-cyan shrink-0">
                                <i class="fa-solid fa-film text-sm"></i>
                            </div>
                            <div>
                                <h4 class="text-xs sm:text-sm font-bold text-white">4K/8K Stream Grabber</h4>
                                <p class="text-[11px] text-slate-400">Captures DASH, M3U8, and HLS video with audio segment multiplexing.</p>
                            </div>
                        </div>

                        <div class="flex items-center space-x-3 p-3 rounded-xl bg-white/[0.02] border border-white/5">
                            <div class="w-9 h-9 rounded-lg bg-amber-500/10 flex items-center justify-center text-gold shrink-0">
                                <i class="fa-solid fa-puzzle-piece text-sm"></i>
                            </div>
                            <div>
                                <h4 class="text-xs sm:text-sm font-bold text-white">Manifest V3 Extension</h4>
                                <p class="text-[11px] text-slate-400">Zero-latency Native Messaging for Chrome, Edge, and Firefox.</p>
                            </div>
                        </div>
                    </div>

                    <!-- CTA Buttons -->
                    <div class="flex flex-wrap items-center gap-3 pt-2">
                        <a href="<?php echo $download_url; ?>" class="btn-premium btn-premium-primary flex-1 !text-center !py-3 !text-xs" download>
                            <i class="fa-solid fa-download mr-2"></i>
                            <span>Download Setup (.exe)</span>
                        </a>
                        <a href="<?php echo $edm_hub_url; ?>" class="btn-premium btn-premium-outline !py-3 !px-5 !text-xs">
                            <span>Live Demo</span>
                        </a>
                    </div>
                </div>

                <!-- Right Live Animated Telemetry Canvas Simulator -->
                <div class="lg:col-span-7 flex flex-col space-y-4">
                    
                    <!-- Oscilloscope Screen Frame -->
                    <div class="bg-navy-950/95 rounded-2xl border border-white/10 p-4 sm:p-6 shadow-inner relative overflow-hidden flex flex-col space-y-4">
                        
                        <!-- Top HUD Bar -->
                        <div class="flex items-center justify-between border-b border-white/10 pb-3">
                            <div class="flex items-center space-x-2">
                                <span class="w-2.5 h-2.5 rounded-full bg-red-500/80 inline-block"></span>
                                <span class="w-2.5 h-2.5 rounded-full bg-amber-500/80 inline-block"></span>
                                <span class="w-2.5 h-2.5 rounded-full bg-emerald-500/80 inline-block"></span>
                                <span class="text-xs font-mono text-slate-400 ml-2">edm_telemetry_stream.bin</span>
                            </div>
                            <div class="flex items-center space-x-3 text-[11px] font-mono">
                                <span class="text-emerald-400 flex items-center gap-1.5">
                                    <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                                    <span>LIVE 60 FPS</span>
                                </span>
                                <span class="text-slate-500">|</span>
                                <span class="text-slate-400">LATENCY: <strong class="text-white">1.8ms</strong></span>
                            </div>
                        </div>

                        <!-- Canvas Wave Graph -->
                        <div class="relative w-full h-48 sm:h-56 md:h-64 rounded-xl overflow-hidden bg-navy-900/60 border border-white/5 flex items-center justify-center">
                            <canvas id="edm-showcase-canvas" class="w-full h-full block"></canvas>
                            
                            <!-- Overlay Watermark / Grid Lines -->
                            <div class="absolute inset-0 pointer-events-none flex flex-col justify-between p-3 opacity-20">
                                <div class="border-b border-dashed border-white/30 w-full"></div>
                                <div class="border-b border-dashed border-white/30 w-full"></div>
                                <div class="border-b border-dashed border-white/30 w-full"></div>
                                <div class="border-b border-dashed border-white/30 w-full"></div>
                            </div>
                        </div>

                        <!-- Bottom 32-Stream Progress Matrix -->
                        <div class="flex flex-col space-y-2 pt-1">
                            <div class="flex justify-between text-[11px] font-mono text-slate-400">
                                <span>32 Dynamic Socket Slices</span>
                                <span id="edm-live-progress-pct" class="text-cyan font-bold">78% Buffered</span>
                            </div>
                            <div class="grid grid-cols-16 sm:grid-cols-32 gap-1 h-3.5 w-full bg-navy-900 p-1 rounded-md border border-white/5" id="edm-socket-bars-container">
                                <!-- 32 Mini Bars dynamically rendered via JS -->
                            </div>
                        </div>

                    </div>

                </div>

            </div>

        </div>

    </div>
</section>

<!-- ══════════════════════════════════════════════════════════════
     EDM SHOWCASE INTERACTIVE TELEMETRY SCRIPT (SELF-CONTAINED)
     ══════════════════════════════════════════════════════════════ -->
<script>
(function() {
    'use strict';
    
    document.addEventListener('DOMContentLoaded', function() {
        const canvas = document.getElementById('edm-showcase-canvas');
        if (!canvas) return;

        const ctx = canvas.getContext('2d');
        const speedValEl = document.getElementById('edm-live-speed-val');
        const progressPctEl = document.getElementById('edm-live-progress-pct');
        const socketBarsContainer = document.getElementById('edm-socket-bars-container');

        // Render 32 mini socket bars
        if (socketBarsContainer && socketBarsContainer.children.length === 0) {
            for (let i = 0; i < 32; i++) {
                const bar = document.createElement('div');
                bar.className = 'h-full rounded-sm bg-gradient-to-t from-blue-600 to-cyan-400 transition-all duration-300';
                bar.style.opacity = (0.5 + Math.random() * 0.5).toFixed(2);
                socketBarsContainer.appendChild(bar);
            }
        }

        let animationFrameId;
        let step = 0;
        let baseSpeed = 48.6;
        let progress = 78;

        function resizeCanvas() {
            const rect = canvas.getBoundingClientRect();
            canvas.width = rect.width * (window.devicePixelRatio || 1);
            canvas.height = rect.height * (window.devicePixelRatio || 1);
            ctx.scale(window.devicePixelRatio || 1, window.devicePixelRatio || 1);
        }

        window.addEventListener('resize', resizeCanvas);
        resizeCanvas();

        function drawWave() {
            const width = canvas.getBoundingClientRect().width;
            const height = canvas.getBoundingClientRect().height;

            ctx.clearRect(0, 0, width, height);

            step += 0.04;

            // Draw Background Multi-Socket Gradient Fill
            const gradient = ctx.createLinearGradient(0, 0, width, height);
            gradient.addColorStop(0, 'rgba(56, 189, 248, 0.25)'); // Cyan
            gradient.addColorStop(0.5, 'rgba(93, 95, 239, 0.15)'); // Purple
            gradient.addColorStop(1, 'rgba(6, 182, 212, 0.02)');

            ctx.beginPath();
            ctx.moveTo(0, height);

            for (let x = 0; x <= width; x += 4) {
                const y1 = Math.sin(x * 0.015 + step) * 22;
                const y2 = Math.cos(x * 0.03 - step * 1.5) * 12;
                const y3 = Math.sin(x * 0.008 + step * 0.8) * 8;
                const finalY = (height / 2) + y1 + y2 + y3;
                ctx.lineTo(x, finalY);
            }

            ctx.lineTo(width, height);
            ctx.closePath();
            ctx.fillStyle = gradient;
            ctx.fill();

            // Draw Top Wave Stroke (Cyan/Blue)
            ctx.beginPath();
            for (let x = 0; x <= width; x += 4) {
                const y1 = Math.sin(x * 0.015 + step) * 22;
                const y2 = Math.cos(x * 0.03 - step * 1.5) * 12;
                const y3 = Math.sin(x * 0.008 + step * 0.8) * 8;
                const finalY = (height / 2) + y1 + y2 + y3;
                if (x === 0) ctx.moveTo(x, finalY);
                else ctx.lineTo(x, finalY);
            }
            ctx.strokeStyle = '#38BDF8';
            ctx.lineWidth = 2.5;
            ctx.shadowColor = '#38BDF8';
            ctx.shadowBlur = 12;
            ctx.stroke();
            ctx.shadowBlur = 0; // Reset shadow

            // Secondary Harmonic Wave (Indigo/Gold Accent)
            ctx.beginPath();
            for (let x = 0; x <= width; x += 6) {
                const ySecondary = (height / 2) + Math.cos(x * 0.02 + step * 1.2) * 16 + Math.sin(x * 0.04 - step) * 8;
                if (x === 0) ctx.moveTo(x, ySecondary);
                else ctx.lineTo(x, ySecondary);
            }
            ctx.strokeStyle = 'rgba(129, 131, 255, 0.7)';
            ctx.lineWidth = 1.5;
            ctx.stroke();

            // Fluctuating Speed Numbers every 30 frames
            if (Math.floor(step * 10) % 15 === 0 && speedValEl) {
                const delta = (Math.random() * 2.4 - 1.2);
                const currentSpeed = (baseSpeed + delta).toFixed(1);
                speedValEl.textContent = currentSpeed;

                if (socketBarsContainer) {
                    const bars = socketBarsContainer.children;
                    const randIdx = Math.floor(Math.random() * bars.length);
                    if (bars[randIdx]) {
                        bars[randIdx].style.opacity = (0.3 + Math.random() * 0.7).toFixed(2);
                    }
                }
            }

            animationFrameId = requestAnimationFrame(drawWave);
        }

        // Intersection Observer to run animation only when visible
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    if (!animationFrameId) {
                        drawWave();
                    }
                } else {
                    if (animationFrameId) {
                        cancelAnimationFrame(animationFrameId);
                        animationFrameId = null;
                    }
                }
            });
        }, { threshold: 0.1 });

        observer.observe(canvas);
    });
})();
</script>
