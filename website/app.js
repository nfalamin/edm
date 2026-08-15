/* ==========================================================================
   EDM DOWNLOAD MANAGER — OFFICIAL WEBSITE INTERACTIVE LOGIC (app.js)
   Real-Time Download Simulation, Live Speed Waveform, Tabs, & Theme Toggle
   ========================================================================== */

document.addEventListener('DOMContentLoaded', () => {

    // 1. Theme Toggle
    const themeToggleBtn = document.getElementById('themeToggleBtn');
    const themeIcon = themeToggleBtn.querySelector('.theme-icon');
    let isDark = true;

    themeToggleBtn.addEventListener('click', () => {
        isDark = !isDark;
        if (isDark) {
            document.body.classList.remove('light-theme');
            document.body.classList.add('dark-theme');
            themeIcon.textContent = '🌙';
        } else {
            document.body.classList.remove('dark-theme');
            document.body.classList.add('light-theme');
            themeIcon.textContent = '☀️';
        }
    });

    // 2. Window Preview Tabs
    const previewTabs = document.querySelectorAll('.preview-tab');
    const tabPanels = document.querySelectorAll('.tab-panel');

    previewTabs.forEach(tab => {
        tab.addEventListener('click', () => {
            previewTabs.forEach(t => t.classList.remove('active'));
            tabPanels.forEach(p => p.classList.remove('active'));

            tab.classList.add('active');
            const targetId = tab.getAttribute('data-target');
            const targetPanel = document.getElementById(targetId);
            if (targetPanel) {
                targetPanel.classList.add('active');
            }
        });
    });

    // 3. Mini Sidebar Waveform Animation
    const sidebarWaveCanvas = document.getElementById('sidebarWaveCanvas');
    if (sidebarWaveCanvas) {
        const ctx = sidebarWaveCanvas.getContext('2d');
        let step = 0;

        function drawSidebarWave() {
            ctx.clearRect(0, 0, sidebarWaveCanvas.width, sidebarWaveCanvas.height);
            ctx.beginPath();
            ctx.strokeStyle = '#38BDF8';
            ctx.lineWidth = 1.5;

            for (let x = 0; x < sidebarWaveCanvas.width; x++) {
                const y = 20 + Math.sin((x + step) * 0.08) * 8 + Math.cos((x + step * 1.2) * 0.05) * 4;
                if (x === 0) ctx.moveTo(x, y);
                else ctx.lineTo(x, y);
            }
            ctx.stroke();
            step += 1.5;
            requestAnimationFrame(drawSidebarWave);
        }
        drawSidebarWave();
    }

    // 4. Detail Graph Canvas Animation
    const detailGraphCanvas = document.getElementById('detailGraphCanvas');
    if (detailGraphCanvas) {
        const ctx = detailGraphCanvas.getContext('2d');
        const points = [];
        const maxPoints = 50;

        for (let i = 0; i < maxPoints; i++) {
            points.push(8 + Math.random() * 6);
        }

        function drawDetailGraph() {
            ctx.clearRect(0, 0, detailGraphCanvas.width, detailGraphCanvas.height);
            
            // Grid background
            ctx.strokeStyle = 'rgba(23, 43, 74, 0.4)';
            ctx.lineWidth = 1;
            for (let y = 15; y < detailGraphCanvas.height; y += 25) {
                ctx.beginPath();
                ctx.moveTo(0, y);
                ctx.lineTo(detailGraphCanvas.width, y);
                ctx.stroke();
            }

            // Draw Area Fill
            const stepX = detailGraphCanvas.width / (maxPoints - 1);
            ctx.beginPath();
            ctx.moveTo(0, detailGraphCanvas.height);

            points.forEach((val, i) => {
                const x = i * stepX;
                const y = detailGraphCanvas.height - (val / 16) * (detailGraphCanvas.height - 15);
                ctx.lineTo(x, y);
            });

            ctx.lineTo(detailGraphCanvas.width, detailGraphCanvas.height);
            ctx.closePath();

            const grad = ctx.createLinearGradient(0, 0, 0, detailGraphCanvas.height);
            grad.addColorStop(0, 'rgba(99, 102, 241, 0.45)');
            grad.addColorStop(1, 'rgba(99, 102, 241, 0.02)');
            ctx.fillStyle = grad;
            ctx.fill();

            // Draw Stroke Line
            ctx.beginPath();
            points.forEach((val, i) => {
                const x = i * stepX;
                const y = detailGraphCanvas.height - (val / 16) * (detailGraphCanvas.height - 15);
                if (i === 0) ctx.moveTo(x, y);
                else ctx.lineTo(x, y);
            });
            ctx.strokeStyle = '#818CF8';
            ctx.lineWidth = 2;
            ctx.stroke();

            // Shift points smoothly
            points.shift();
            const lastVal = points[points.length - 1] || 12;
            const nextVal = Math.min(15.5, Math.max(7, lastVal + (Math.random() * 2 - 1)));
            points.push(nextVal);

            setTimeout(() => requestAnimationFrame(drawDetailGraph), 150);
        }
        drawDetailGraph();
    }

    // 5. Interactive Real-Time Download Simulator
    const simUrlInput = document.getElementById('simUrlInput');
    const simStartBtn = document.getElementById('simStartBtn');
    const simPauseBtn = document.getElementById('simPauseBtn');
    const simResetBtn = document.getElementById('simResetBtn');

    const simPct = document.getElementById('simPct');
    const simSpeed = document.getElementById('simSpeed');
    const simAvg = document.getElementById('simAvg');
    const simEta = document.getElementById('simEta');
    const simProgressBar = document.getElementById('simProgressBar');
    const simSegmentsGrid = document.getElementById('simSegmentsGrid');
    const simCanvas = document.getElementById('simCanvas');

    const segmentCount = 16;
    const segmentsData = [];

    // Initialize 16 Segment UI Boxes
    for (let i = 0; i < segmentCount; i++) {
        const box = document.createElement('div');
        box.className = 'sim-seg-box';
        box.textContent = (i + 1).toString();
        simSegmentsGrid.appendChild(box);
        segmentsData.push({ progress: 0, status: 'queued', element: box });
    }

    let isSimRunning = false;
    let simInterval = null;
    let currentPct = 0;
    let speedSamples = [];
    const maxSimPoints = 60;
    const simGraphPoints = new Array(maxSimPoints).fill(0);

    function renderSimSegments() {
        segmentsData.forEach((seg, idx) => {
            if (seg.progress >= 100) {
                seg.element.style.background = '#2E86FF';
                seg.element.style.color = '#FFF';
            } else if (seg.progress > 0) {
                seg.element.style.background = '#06B6D4';
                seg.element.style.color = '#FFF';
            } else {
                seg.element.style.background = 'var(--bg-secondary)';
                seg.element.style.color = 'var(--text-muted)';
            }
        });
    }

    function drawSimGraph(currentSpeed) {
        if (!simCanvas) return;
        const ctx = simCanvas.getContext('2d');
        ctx.clearRect(0, 0, simCanvas.width, simCanvas.height);

        simGraphPoints.shift();
        simGraphPoints.push(currentSpeed);

        const stepX = simCanvas.width / (maxSimPoints - 1);
        const maxScale = 50; // max 50 MB/s

        // Area
        ctx.beginPath();
        ctx.moveTo(0, simCanvas.height);
        simGraphPoints.forEach((spd, i) => {
            const x = i * stepX;
            const y = simCanvas.height - (spd / maxScale) * (simCanvas.height - 15);
            ctx.lineTo(x, y);
        });
        ctx.lineTo(simCanvas.width, simCanvas.height);
        ctx.closePath();

        const grad = ctx.createLinearGradient(0, 0, 0, simCanvas.height);
        grad.addColorStop(0, 'rgba(46, 134, 255, 0.4)');
        grad.addColorStop(1, 'rgba(46, 134, 255, 0.02)');
        ctx.fillStyle = grad;
        ctx.fill();

        // Stroke
        ctx.beginPath();
        simGraphPoints.forEach((spd, i) => {
            const x = i * stepX;
            const y = simCanvas.height - (spd / maxScale) * (simCanvas.height - 15);
            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        });
        ctx.strokeStyle = '#38BDF8';
        ctx.lineWidth = 2.5;
        ctx.stroke();
    }

    function tickSimulator() {
        if (!isSimRunning) return;

        // Progress increment
        const delta = 0.35 + Math.random() * 0.45;
        currentPct = Math.min(100, currentPct + delta);

        // Speed calculation (ramping up to ~35-42 MB/s with turbo acceleration)
        const targetSpeed = currentPct < 5 ? 8 : (currentPct > 95 ? 12 : 32 + (Math.random() * 9 - 4));
        speedSamples.push(targetSpeed);
        if (speedSamples.length > 50) speedSamples.shift();

        const avgSpeedVal = speedSamples.reduce((a, b) => a + b, 0) / speedSamples.length;
        const remainingSeconds = Math.max(0, Math.round(((100 - currentPct) / 100) * 45));

        // Update UI
        simPct.textContent = `${currentPct.toFixed(1)}%`;
        simProgressBar.style.width = `${currentPct}%`;
        simSpeed.textContent = `${targetSpeed.toFixed(1)} MB/s`;
        simAvg.textContent = `${avgSpeedVal.toFixed(1)} MB/s`;

        const mins = Math.floor(remainingSeconds / 60);
        const secs = remainingSeconds % 60;
        simEta.textContent = `00:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;

        // Distribute progress across segments
        const activeSegmentIndex = Math.min(segmentCount - 1, Math.floor((currentPct / 100) * segmentCount));
        for (let i = 0; i < segmentCount; i++) {
            if (i < activeSegmentIndex) {
                segmentsData[i].progress = 100;
            } else if (i === activeSegmentIndex) {
                const segFraction = (currentPct % (100 / segmentCount)) / (100 / segmentCount);
                segmentsData[i].progress = Math.round(segFraction * 100);
            } else {
                segmentsData[i].progress = 0;
            }
        }
        renderSimSegments();
        drawSimGraph(targetSpeed);

        if (currentPct >= 100) {
            isSimRunning = false;
            clearInterval(simInterval);
            simStartBtn.disabled = false;
            simPauseBtn.disabled = true;
            simSpeed.textContent = `0.0 MB/s`;
            simEta.textContent = `✓ Completed`;
            drawSimGraph(0);
        }
    }

    simStartBtn.addEventListener('click', () => {
        if (currentPct >= 100) {
            currentPct = 0;
        }
        isSimRunning = true;
        simStartBtn.disabled = true;
        simPauseBtn.disabled = false;
        simPauseBtn.textContent = 'Pause';
        clearInterval(simInterval);
        simInterval = setInterval(tickSimulator, 100);
    });

    simPauseBtn.addEventListener('click', () => {
        if (isSimRunning) {
            isSimRunning = false;
            clearInterval(simInterval);
            simPauseBtn.textContent = 'Resume';
            simSpeed.textContent = '0.0 MB/s (Paused)';
            drawSimGraph(0);
        } else {
            isSimRunning = true;
            simPauseBtn.textContent = 'Pause';
            simInterval = setInterval(tickSimulator, 100);
        }
    });

    simResetBtn.addEventListener('click', () => {
        isSimRunning = false;
        clearInterval(simInterval);
        currentPct = 0;
        speedSamples = [];
        simGraphPoints.fill(0);
        simPct.textContent = '0.0%';
        simProgressBar.style.width = '0%';
        simSpeed.textContent = '0.0 MB/s';
        simAvg.textContent = '0.0 MB/s';
        simEta.textContent = '--:--:--';
        simStartBtn.disabled = false;
        simPauseBtn.disabled = true;
        simPauseBtn.textContent = 'Pause';
        segmentsData.forEach(s => { s.progress = 0; });
        renderSimSegments();
        drawSimGraph(0);
    });

    // 6. FAQ Accordion
    const faqItems = document.querySelectorAll('.faq-item');
    faqItems.forEach(item => {
        const questionBtn = item.querySelector('.faq-question');
        questionBtn.addEventListener('click', () => {
            const isOpen = item.classList.contains('active');
            faqItems.forEach(i => i.classList.remove('active'));
            if (!isOpen) {
                item.classList.add('active');
            }
        });
    });

});
