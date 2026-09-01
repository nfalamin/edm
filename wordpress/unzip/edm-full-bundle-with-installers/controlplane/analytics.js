// EDM Control Plane — Analytics View Module (Real Database Chart.js Visualizations)
import { apiFetch } from './api.js';

let chartDownloads = null;
let chartPlatforms = null;
let chartUsers = null;
let chartVersions = null;
let chartHourly = null;
let chartSecurity = null;

export async function loadDashboardOverviewCharts() {
    try {
        const dlRes = await apiFetch('/api/v1/admin/analytics/downloads?range=7d');
        renderDownloadsOverviewChart(dlRes.data || []);

        const platRes = await apiFetch('/api/v1/admin/analytics/platforms');
        renderPlatformsOverviewChart(platRes || []);
    } catch (err) {
        console.error('Error loading dashboard overview charts:', err);
    }
}

export async function loadDetailedAnalytics(range = '7d') {
    try {
        const userRes = await apiFetch(`/api/v1/admin/analytics/users?range=${range}`);
        renderUsersGrowthChart(userRes.data || []);

        const verRes = await apiFetch('/api/v1/admin/analytics/versions');
        renderVersionsDistributionChart(verRes || []);

        const hrRes = await apiFetch('/api/v1/admin/analytics/activity');
        renderHourlyActivityChart(hrRes || []);
    } catch (err) {
        console.error('Error loading detailed analytics:', err);
    }
}

export async function loadSecurityAnalytics() {
    try {
        const secRes = await apiFetch('/api/v1/admin/analytics/security');
        renderSecurityAuditChart(secRes || []);
    } catch (err) {
        console.error('Error loading security analytics:', err);
    }
}

function renderDownloadsOverviewChart(data) {
    const ctx = document.getElementById('chart-downloads-overview');
    if (!ctx) return;

    const labels = data.map(d => d.date);
    const completed = data.map(d => d.completed);
    const failed = data.map(d => d.failed);

    if (chartDownloads) chartDownloads.destroy();

    chartDownloads = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels.length ? labels : ['Today'],
            datasets: [
                {
                    label: 'Completed Downloads',
                    data: completed.length ? completed : [0],
                    borderColor: '#3B82F6',
                    backgroundColor: 'rgba(59, 130, 246, 0.15)',
                    fill: true,
                    tension: 0.3
                },
                {
                    label: 'Failed / Retried',
                    data: failed.length ? failed : [0],
                    borderColor: '#EF4444',
                    backgroundColor: 'rgba(239, 68, 68, 0.15)',
                    fill: true,
                    tension: 0.3
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { labels: { color: '#9CA3AF' } } },
            scales: {
                x: { ticks: { color: '#9CA3AF' }, grid: { color: 'rgba(255,255,255,0.05)' } },
                y: { ticks: { color: '#9CA3AF' }, grid: { color: 'rgba(255,255,255,0.05)' } }
            }
        }
    });
}

function renderPlatformsOverviewChart(data) {
    const ctx = document.getElementById('chart-platforms-overview');
    if (!ctx) return;

    const labels = data.map(d => d.platform);
    const counts = data.map(d => d.count);

    if (chartPlatforms) chartPlatforms.destroy();

    chartPlatforms = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels.length ? labels : ['No Data'],
            datasets: [{
                data: counts.length ? counts : [1],
                backgroundColor: ['#3B82F6', '#10B981', '#F59E0B', '#8B5CF6', '#EC4899']
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'bottom', labels: { color: '#9CA3AF' } } }
        }
    });
}

function renderUsersGrowthChart(data) {
    const ctx = document.getElementById('chart-users-growth');
    if (!ctx) return;

    if (chartUsers) chartUsers.destroy();

    chartUsers = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: data.map(d => d.date),
            datasets: [{
                label: 'New Registrations',
                data: data.map(d => d.count),
                backgroundColor: '#10B981'
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                x: { ticks: { color: '#9CA3AF' }, grid: { color: 'rgba(255,255,255,0.05)' } },
                y: { ticks: { color: '#9CA3AF' }, grid: { color: 'rgba(255,255,255,0.05)' } }
            }
        }
    });
}

function renderVersionsDistributionChart(data) {
    const ctx = document.getElementById('chart-versions-dist');
    if (!ctx) return;

    if (chartVersions) chartVersions.destroy();

    chartVersions = new Chart(ctx, {
        type: 'pie',
        data: {
            labels: data.map(d => `v${d.version}`),
            datasets: [{
                data: data.map(d => d.count),
                backgroundColor: ['#3B82F6', '#6366F1', '#EC4899', '#14B8A6']
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'bottom', labels: { color: '#9CA3AF' } } }
        }
    });
}

function renderHourlyActivityChart(data) {
    const ctx = document.getElementById('chart-hourly-activity');
    if (!ctx) return;

    if (chartHourly) chartHourly.destroy();

    chartHourly = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: data.map(d => `${d.hour}:00 UTC`),
            datasets: [{
                label: 'Activity Intensity (Events / Hour)',
                data: data.map(d => d.count),
                backgroundColor: '#60A5FA'
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                x: { ticks: { color: '#9CA3AF' }, grid: { color: 'rgba(255,255,255,0.05)' } },
                y: { ticks: { color: '#9CA3AF' }, grid: { color: 'rgba(255,255,255,0.05)' } }
            }
        }
    });
}

function renderSecurityAuditChart(data) {
    const ctx = document.getElementById('chart-security-audit');
    if (!ctx) return;

    if (chartSecurity) chartSecurity.destroy();

    chartSecurity = new Chart(ctx, {
        type: 'polarArea',
        data: {
            labels: data.map(d => d.action),
            datasets: [{
                data: data.map(d => d.count),
                backgroundColor: [
                    'rgba(239, 68, 68, 0.6)',
                    'rgba(245, 158, 11, 0.6)',
                    'rgba(59, 130, 246, 0.6)',
                    'rgba(16, 185, 129, 0.6)'
                ]
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'right', labels: { color: '#9CA3AF' } } }
        }
    });
}
