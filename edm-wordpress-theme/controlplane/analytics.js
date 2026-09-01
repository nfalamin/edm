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

function getChartTheme() {
    const isDark = typeof document !== 'undefined' && document.documentElement.getAttribute('data-theme') !== 'light' && !document.body?.classList.contains('light-theme');
    return {
        textColor: isDark ? '#94A3B8' : '#475569',
        gridColor: isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.06)',
        tooltipBg: isDark ? '#0B0F14' : '#FFFFFF',
        tooltipTitle: isDark ? '#F0F0F0' : '#0F172A',
        tooltipBody: isDark ? '#7F8488' : '#475569',
        tooltipBorder: isDark ? '#26292D' : '#E2E8F0'
    };
}

function renderDownloadsOverviewChart(data) {
    const ctx = document.getElementById('chart-downloads-overview');
    if (!ctx) return;

    const theme = getChartTheme();
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
            plugins: {
                legend: { labels: { color: theme.textColor, font: { size: 11 } } },
                tooltip: {
                    backgroundColor: theme.tooltipBg,
                    titleColor: theme.tooltipTitle,
                    bodyColor: theme.tooltipBody,
                    borderColor: theme.tooltipBorder,
                    borderWidth: 1,
                    cornerRadius: 8
                }
            },
            scales: {
                x: { ticks: { color: theme.textColor }, grid: { color: theme.gridColor } },
                y: { ticks: { color: theme.textColor }, grid: { color: theme.gridColor }, beginAtZero: true }
            }
        }
    });
}

function renderPlatformsOverviewChart(data) {
    const ctx = document.getElementById('chart-platforms-overview');
    if (!ctx) return;

    const theme = getChartTheme();
    const labels = data.map(d => d.platform);
    const counts = data.map(d => d.count);

    if (chartPlatforms) chartPlatforms.destroy();

    chartPlatforms = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels.length ? labels : ['No Data'],
            datasets: [{
                data: counts.length ? counts : [1],
                backgroundColor: ['#3B82F6', '#10B981', '#F59E0B', '#8B5CF6', '#EC4899'],
                borderWidth: 0
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '65%',
            plugins: {
                legend: { position: 'bottom', labels: { color: theme.textColor, font: { size: 11 } } },
                tooltip: {
                    backgroundColor: theme.tooltipBg,
                    titleColor: theme.tooltipTitle,
                    bodyColor: theme.tooltipBody,
                    borderColor: theme.tooltipBorder,
                    borderWidth: 1,
                    cornerRadius: 8
                }
            }
        }
    });
}

function renderUsersGrowthChart(data) {
    const ctx = document.getElementById('chart-users-growth');
    if (!ctx) return;

    const theme = getChartTheme();
    if (chartUsers) chartUsers.destroy();

    chartUsers = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: data.map(d => d.date),
            datasets: [{
                label: 'New Registrations',
                data: data.map(d => d.count),
                backgroundColor: '#10B981',
                borderRadius: 4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { labels: { color: theme.textColor, font: { size: 11 } } },
                tooltip: {
                    backgroundColor: theme.tooltipBg,
                    titleColor: theme.tooltipTitle,
                    bodyColor: theme.tooltipBody,
                    borderColor: theme.tooltipBorder,
                    borderWidth: 1,
                    cornerRadius: 8
                }
            },
            scales: {
                x: { ticks: { color: theme.textColor }, grid: { display: false } },
                y: { ticks: { color: theme.textColor }, grid: { color: theme.gridColor }, beginAtZero: true }
            }
        }
    });
}

function renderVersionsDistributionChart(data) {
    const ctx = document.getElementById('chart-versions-dist');
    if (!ctx) return;

    const theme = getChartTheme();
    if (chartVersions) chartVersions.destroy();

    chartVersions = new Chart(ctx, {
        type: 'pie',
        data: {
            labels: data.map(d => `v${d.version}`),
            datasets: [{
                data: data.map(d => d.count),
                backgroundColor: ['#3B82F6', '#6366F1', '#EC4899', '#14B8A6'],
                borderWidth: 0
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { position: 'bottom', labels: { color: theme.textColor, font: { size: 11 } } },
                tooltip: {
                    backgroundColor: theme.tooltipBg,
                    titleColor: theme.tooltipTitle,
                    bodyColor: theme.tooltipBody,
                    borderColor: theme.tooltipBorder,
                    borderWidth: 1,
                    cornerRadius: 8
                }
            }
        }
    });
}

function renderHourlyActivityChart(data) {
    const ctx = document.getElementById('chart-hourly-activity');
    if (!ctx) return;

    const theme = getChartTheme();
    if (chartHourly) chartHourly.destroy();

    chartHourly = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: data.map(d => `${d.hour}:00 UTC`),
            datasets: [{
                label: 'Activity Intensity (Events / Hour)',
                data: data.map(d => d.count),
                backgroundColor: '#60A5FA',
                borderRadius: 3
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { labels: { color: theme.textColor, font: { size: 11 } } },
                tooltip: {
                    backgroundColor: theme.tooltipBg,
                    titleColor: theme.tooltipTitle,
                    bodyColor: theme.tooltipBody,
                    borderColor: theme.tooltipBorder,
                    borderWidth: 1,
                    cornerRadius: 8
                }
            },
            scales: {
                x: { ticks: { color: theme.textColor }, grid: { display: false } },
                y: { ticks: { color: theme.textColor }, grid: { color: theme.gridColor }, beginAtZero: true }
            }
        }
    });
}

function renderSecurityAuditChart(data) {
    const ctx = document.getElementById('chart-security-audit');
    if (!ctx) return;

    const theme = getChartTheme();
    if (chartSecurity) chartSecurity.destroy();

    chartSecurity = new Chart(ctx, {
        type: 'polarArea',
        data: {
            labels: data.map(d => d.action),
            datasets: [{
                data: data.map(d => d.count),
                backgroundColor: [
                    'rgba(239, 68, 68, 0.65)',
                    'rgba(245, 158, 11, 0.65)',
                    'rgba(59, 130, 246, 0.65)',
                    'rgba(16, 185, 129, 0.65)'
                ],
                borderWidth: 0
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { position: 'right', labels: { color: theme.textColor, font: { size: 11 } } },
                tooltip: {
                    backgroundColor: theme.tooltipBg,
                    titleColor: theme.tooltipTitle,
                    bodyColor: theme.tooltipBody,
                    borderColor: theme.tooltipBorder,
                    borderWidth: 1,
                    cornerRadius: 8
                }
            }
        }
    });
}
