/**
 * EDM Control Plane — Central Mock Data Store & Placeholder API
 */

window.EDM_MOCK_DATA = {
    // ══════════════════════════════════════════════════════════════
    // 1. DASHBOARD OVERVIEW & KPIS
    // ══════════════════════════════════════════════════════════════
    overview: {
        totalUsers: { value: "24,582", change: "+12.4%", trend: "up", period: "vs May 17, 2025", sparkline: [18, 20, 19, 22, 21, 24, 25, 24.5] },
        activeUsers: { value: "8,765", change: "+8.1%", trend: "up", period: "vs May 17, 2025", sparkline: [6.2, 6.8, 7.1, 7.5, 8.0, 8.4, 8.76] },
        premiumUsers: { value: "6,421", change: "+15.3%", trend: "up", period: "vs May 17, 2025", sparkline: [4.5, 4.8, 5.2, 5.6, 5.9, 6.2, 6.42] },
        trialUsers: { value: "2,344", change: "-3.2%", trend: "down", period: "vs May 17, 2025", sparkline: [2.8, 2.7, 2.6, 2.5, 2.4, 2.4, 2.34] },
        monthlyRevenue: { value: "$18,765", change: "+20.7%", trend: "up", period: "vs May 17, 2025", sparkline: [12, 13.5, 14.8, 16.2, 17.5, 18.7] },
        activeDownloads: { value: "1,234", change: "+6.7%", trend: "up", period: "vs May 17, 2025", sparkline: [980, 1050, 1120, 1180, 1200, 1234] },
        
        versionAdoption: { value: "78.7%", change: "+13.6%", period: "vs May 17, 2025" },
        currentVersion: "v2.1.0",
        systemStatus: "Operational",
        allSystemsNormal: true
    },

    // ══════════════════════════════════════════════════════════════
    // 2. USERS DATASET
    // ══════════════════════════════════════════════════════════════
    users: [
        { id: "USR-9821", name: "Alexander Wright", email: "a.wright@enterprise.io", country: "United States", countryCode: "US", plan: "Premium", status: "Active", trial: "Completed", devices: 3, lastActive: "Just now", created: "2024-11-12", hwid: "HWID-8F92-4B21-99A1", downloadsCount: 1420, bandwidthUsed: "2.4 TB" },
        { id: "USR-9822", name: "Sophia Chen", email: "sophia.chen@techlabs.com", country: "Singapore", countryCode: "SG", plan: "Premium", status: "Active", trial: "Completed", devices: 2, lastActive: "12 mins ago", created: "2025-01-04", hwid: "HWID-7A11-3C98-21F5", downloadsCount: 890, bandwidthUsed: "1.1 TB" },
        { id: "USR-9823", name: "Marcus Vance", email: "demo@example.com", country: "Germany", countryCode: "DE", plan: "Trial", status: "Active", trial: "4 days left", devices: 1, lastActive: "2 mins ago", created: "2025-06-14", hwid: "HWID-1E44-99D2-66C3", downloadsCount: 45, bandwidthUsed: "68 GB" },
        { id: "USR-9824", name: "Elena Rostova", email: "elena.rostova@cybernet.ru", country: "Kazakhstan", countryCode: "KZ", plan: "Premium", status: "Active", trial: "Completed", devices: 4, lastActive: "45 mins ago", created: "2024-08-20", hwid: "HWID-4B88-12EE-557A", downloadsCount: 2310, bandwidthUsed: "4.8 TB" },
        { id: "USR-9825", name: "Liam O'Connor", email: "liam.dev@dublintech.ie", country: "Ireland", countryCode: "IE", plan: "Free", status: "Active", trial: "Expired", devices: 1, lastActive: "1 day ago", created: "2025-02-18", hwid: "HWID-99AA-77CC-12FF", downloadsCount: 112, bandwidthUsed: "140 GB" },
        { id: "USR-9826", name: "Tariq Al-Mansoor", email: "tariq@gulfmedia.ae", country: "United Arab Emirates", countryCode: "AE", plan: "Premium", status: "Suspended", trial: "Completed", devices: 2, lastActive: "3 days ago", created: "2024-10-09", hwid: "HWID-33CC-8812-44BB", downloadsCount: 512, bandwidthUsed: "850 GB" },
        { id: "USR-9827", name: "Kaito Tanaka", email: "tanaka.k@tokyo-stream.jp", country: "Japan", countryCode: "JP", plan: "Premium", status: "Active", trial: "Completed", devices: 2, lastActive: "5 hours ago", created: "2025-03-01", hwid: "HWID-66DD-9922-11EE", downloadsCount: 1650, bandwidthUsed: "3.1 TB" }
    ],

    // ══════════════════════════════════════════════════════════════
    // 3. DEVICES DATASET
    // ══════════════════════════════════════════════════════════════
    devices: [
        { id: "DEV-8801", deviceName: "DESKTOP-ALEX-PRO", user: "Alexander Wright (USR-9821)", os: "Windows 11 Pro 23H2 (x64)", edmVersion: "v2.1.0", deviceId: "EDM-WIN-9821-A", country: "United States", ip: "198.51.100.42", lastActive: "Just now", status: "Active" },
        { id: "DEV-8802", deviceName: "LAPTOP-DEV-SOPHIA", user: "Sophia Chen (USR-9822)", os: "Windows 11 Home (ARM64)", edmVersion: "v2.1.0", deviceId: "EDM-WIN-9822-B", country: "Singapore", ip: "203.0.113.88", lastActive: "12 mins ago", status: "Active" },
        { id: "DEV-8803", deviceName: "WORKSTATION-MARCUS", user: "Marcus Vance (USR-9823)", os: "Windows 10 Pro 22H2 (x64)", edmVersion: "v2.0.9", deviceId: "EDM-WIN-9823-C", country: "Germany", ip: "192.0.2.14", lastActive: "2 mins ago", status: "Active" },
        { id: "DEV-8804", deviceName: "MEDIA-RIG-ELENA", user: "Elena Rostova (USR-9824)", os: "Windows 11 Enterprise (x64)", edmVersion: "v2.1.0", deviceId: "EDM-WIN-9824-D", country: "Kazakhstan", ip: "198.51.100.19", lastActive: "45 mins ago", status: "Active" },
        { id: "DEV-8805", deviceName: "STUDIO-SURFACE-PRO", user: "Liam O'Connor (USR-9825)", os: "Windows 11 Pro (x64)", edmVersion: "v2.0.8", deviceId: "EDM-WIN-9825-E", country: "Ireland", ip: "203.0.113.5", lastActive: "1 day ago", status: "Active" }
    ],

    // ══════════════════════════════════════════════════════════════
    // 4. USER ACTIVITY FEED
    // ══════════════════════════════════════════════════════════════
    userActivities: [
        { id: "ACT-101", user: "Marcus Vance", type: "DOWNLOAD_START", desc: "Started 4K Video Download (4.2 GB)", ip: "192.0.2.14", time: "1 min ago", severity: "INFO" },
        { id: "ACT-102", user: "Sophia Chen", type: "LICENSE_VALIDATE", desc: "License check passed (HWID match)", ip: "203.0.113.88", time: "12 mins ago", severity: "SUCCESS" },
        { id: "ACT-103", user: "Alexander Wright", type: "EXTENSION_ATTACH", desc: "Attached EDM Chrome Manifest V3 Bridge", ip: "198.51.100.42", time: "25 mins ago", severity: "INFO" },
        { id: "ACT-104", user: "Unknown (Guest)", type: "LOGIN_FAILED", desc: "Brute force blocked from Tor exit node", ip: "185.220.101.5", time: "3 hours ago", severity: "WARNING" }
    ],

    // ══════════════════════════════════════════════════════════════
    // 5. DOWNLOAD TELEMETRY & EXTENSION
    // ══════════════════════════════════════════════════════════════
    downloadTelemetry: {
        totalCaptured: "1,420,890 files",
        totalBandwidth: "482.4 TB",
        avgSpeed: "74.8 MB/s",
        activeThreads: "9,842",
        formatDistribution: [
            { format: "Video (MP4/MKV/M3U8)", share: "54%", count: "767,280" },
            { format: "Archives (ZIP/RAR/7Z)", share: "22%", count: "312,590" },
            { format: "Executables/ISOs", share: "14%", count: "198,920" },
            { format: "Audio (FLAC/MP3)", share: "6%", count: "85,250" },
            { format: "Documents", share: "4%", count: "56,850" }
        ],
        browserExtensions: [
            { browser: "Google Chrome", version: "v3.2.0 (MV3)", activeUsers: "16,420", status: "Healthy" },
            { browser: "Microsoft Edge", version: "v3.2.0 (MV3)", activeUsers: "5,180", status: "Healthy" },
            { browser: "Mozilla Firefox", version: "v3.1.8", activeUsers: "2,410", status: "Healthy" },
            { browser: "Brave Browser", version: "v3.2.0 (MV3)", activeUsers: "572", status: "Healthy" }
        ]
    },

    // ══════════════════════════════════════════════════════════════
    // 6. RELEASES & UPDATE CENTER
    // ══════════════════════════════════════════════════════════════
    releases: [
        {
            version: "v2.1.0",
            name: "Quantum Stream & Chromium V3 Turbo",
            date: "Jun 18, 2025",
            type: "RECOMMENDED",
            status: "Active / Production",
            file: "EDM-Setup-2.1.0-x64.exe",
            size: "2.4 MB",
            sha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            downloads: 18450,
            minSupportedVersion: "v1.9.0",
            notes: "• Turbocharged multi-threaded download engine with 32 connections.\n• Seamless Chrome / Edge Manifest V3 interceptor integration.\n• Smart dynamic bandwidth throttle & automated video stream parser.",
            publishedBy: "Admin (Super Admin)"
        },
        {
            version: "v2.0.9",
            name: "Resilience & Security Patch",
            date: "Jun 10, 2025",
            type: "RECOMMENDED",
            status: "Archived",
            file: "EDM-Setup-2.0.9-x64.exe",
            size: "2.3 MB",
            sha256: "872983acbe4f1029c782194bbda4598129031023912aedbc910248102394bb21",
            downloads: 42100,
            minSupportedVersion: "v1.8.5",
            notes: "• Resolved video stream segment concatenation in high-bitrate 4K.\n• Enhanced license hardware ID revalidation mechanism.\n• Minor memory optimization during multi-gigabyte file assemblage.",
            publishedBy: "Admin (Super Admin)"
        },
        {
            version: "v2.0.8",
            name: "Minor Optimizations & Localization",
            date: "May 28, 2025",
            type: "OPTIONAL",
            status: "Archived",
            file: "EDM-Setup-2.0.8-x64.exe",
            size: "2.2 MB",
            sha256: "45a8921cbda298371923bbca9120391209381029381203918230918203912803",
            downloads: 38900,
            minSupportedVersion: "v1.8.0",
            notes: "• Added support for 8 additional UI languages.\n• Optimized dark theme rendering on high-DPI displays.",
            publishedBy: "Admin (Super Admin)"
        }
    ],

    // ══════════════════════════════════════════════════════════════
    // 7. SUBSCRIPTIONS, TRIALS, LICENSES, PROMOTIONS
    // ══════════════════════════════════════════════════════════════
    plans: [
        { id: "plan-free", name: "Free Edition", monthlyPrice: "$0", yearlyPrice: "$0", billingPeriod: "Forever", activeUsers: "15,817", conversion: "12.4%", features: ["Max 8 concurrent connections", "Basic video stream detection", "Single device registration", "Standard download speeds", "Community support"] },
        { id: "plan-premium", name: "EDM Pro / Lifetime", monthlyPrice: "$4.99", yearlyPrice: "$39.99", lifetimePrice: "$59.99", billingPeriod: "Monthly / Annual / Lifetime", activeUsers: "8,765", conversion: "38.2%", features: ["Max 32 ultra-fast connections", "4K/8K media stream ripper", "Up to 5 registered devices", "Smart dynamic scheduler & queue", "Priority CDN download nodes", "Zero ads & 24/7 VIP support"] }
    ],

    trials: {
        funnel: [
            { period: "1-Day Active", count: 820, conversion: "48%" },
            { period: "3-Day Active", count: 640, conversion: "42%" },
            { period: "5-Day Active", count: 510, conversion: "38%" },
            { period: "7-Day Expiring", count: 374, conversion: "35%" }
        ],
        expiringSoon: [
            { user: "Marcus Vance", email: "demo@example.com", daysLeft: 4, action: "Send Reminder" },
            { user: "Oliver Queen", email: "oliver@star.org", daysLeft: 1, action: "Send Offer Discount" }
        ]
    },

    licenses: [
        { key: "EDM-PRO-9821-44B1-8899", user: "Alexander Wright", status: "Active", devicesBound: 3, maxDevices: 5, expires: "2026-11-12" },
        { key: "EDM-PRO-9822-77AA-1209", user: "Sophia Chen", status: "Active", devicesBound: 2, maxDevices: 5, expires: "2026-01-04" },
        { key: "EDM-PRO-9826-33CC-8812", user: "Tariq Al-Mansoor", status: "Revoked", devicesBound: 0, maxDevices: 5, expires: "Revoked" }
    ],

    promotions: [
        { code: "SUMMER50", discount: "50% OFF", type: "Percentage", uses: "1,420 / 2,000", status: "Active", expires: "2025-07-31" },
        { code: "EDMPRO10", discount: "$10 OFF", type: "Fixed Amount", uses: "890 / 1,000", status: "Active", expires: "2025-08-15" }
    ],

    countryPricing: [
        { country: "United States", code: "US", currency: "USD", monthly: "$4.99", yearly: "$39.99", status: "Active", users: "9,420", revenue: "$8,450" },
        { country: "United Kingdom", code: "GB", currency: "GBP", monthly: "£4.49", yearly: "£34.99", status: "Active", users: "3,110", revenue: "$2,890" },
        { country: "Germany (EU)", code: "DE", currency: "EUR", monthly: "€4.99", yearly: "€39.99", status: "Active", users: "4,200", revenue: "$3,650" },
        { country: "Bangladesh", code: "BD", currency: "BDT", monthly: "৳350", yearly: "৳2,800", status: "Active", users: "1,850", revenue: "$840" },
        { country: "India", code: "IN", currency: "INR", monthly: "₹299", yearly: "₹2,499", status: "Active", users: "2,980", revenue: "$1,320" },
        { country: "Brazil", code: "BR", currency: "BRL", monthly: "R$ 19.90", yearly: "R$ 159.90", status: "Active", users: "1,240", revenue: "$680" },
        { country: "Japan", code: "JP", currency: "JPY", monthly: "¥680", yearly: "¥5,400", status: "Active", users: "1,782", revenue: "$935" }
    ],

    // ══════════════════════════════════════════════════════════════
    // 8. NOTIFICATIONS & EMAIL & ANNOUNCEMENTS
    // ══════════════════════════════════════════════════════════════
    notifications: [
        { id: "NT-01", title: "EDM 2.1.0 is now available", audience: "All Users", type: "In-App", status: "Active", sentCount: "24,582", date: "2025-06-18", read: false },
        { id: "NT-02", title: "Your Trial is expiring in 2 days", audience: "Trial Users", type: "Email & In-App", status: "Scheduled", sentCount: "1,420", date: "2025-06-20", read: false },
        { id: "NT-03", title: "50% Summer Flash Sale for Premium Lifetime", audience: "Free Users", type: "Email Campaign", status: "Sent", sentCount: "15,817", date: "2025-06-15", read: true }
    ],

    emailCampaigns: [
        { id: "CMP-01", name: "Summer Pro Upgrade Campaign", audience: "Free Tier (15.8k)", openRate: "42.8%", clickRate: "18.4%", status: "Sent" },
        { id: "CMP-02", name: "Version 2.1.0 Feature Spotlight", audience: "All Subscribers", openRate: "61.2%", clickRate: "34.1%", status: "Scheduled" }
    ],

    announcements: [
        { id: "ANN-01", title: "Scheduled Network Infrastructure Maintenance", severity: "Warning", active: true, target: "Global Desktop Clients" }
    ],

    // ══════════════════════════════════════════════════════════════
    // 9. SYSTEM HEALTH & AUDIT & SECURITY
    // ══════════════════════════════════════════════════════════════
    systemHealth: [
        { name: "Authentication Service", status: "Operational", latency: "98ms", uptime: "99.99%", checked: "Just now" },
        { name: "Update Server", status: "Operational", latency: "120ms", uptime: "99.98%", checked: "Just now" },
        { name: "License Server", status: "Operational", latency: "110ms", uptime: "100.0%", checked: "Just now" },
        { name: "Database", status: "Operational", latency: "85ms", uptime: "99.99%", checked: "Just now" },
        { name: "Notification Service", status: "Operational", latency: "150ms", uptime: "99.95%", checked: "Just now" },
        { name: "Email Service", status: "Operational", latency: "90ms", uptime: "99.97%", checked: "Just now" },
        { name: "Payment Service", status: "Operational", latency: "130ms", uptime: "100.0%", checked: "Just now" }
    ],

    recentActivities: [
        { icon: "user", bg: "#3B82F6", title: "New user registered", desc: "demo@example.com", time: "2 minutes ago" },
        { icon: "user-check", bg: "#10B981", title: "Release v2.1.0 published", desc: "Version 2.1.0 is now live", time: "15 minutes ago" },
        { icon: "user-check", bg: "#10B981", title: "User subscription upgraded", desc: "user@example.com upgraded to Premium", time: "1 hour ago" },
        { icon: "database", bg: "#10B981", title: "System backup completed", desc: "Daily system backup completed successfully", time: "2 hours ago" },
        { icon: "bell", bg: "#EF4444", title: "Failed login attempt", desc: "Unusual login attempt detected", time: "3 hours ago" }
    ],

    auditLogs: [
        { timestamp: "2025-06-18 14:32:10", admin: "Admin (Super)", action: "RELEASE_PUBLISH", target: "v2.1.0", ip: "198.51.100.42", result: "SUCCESS" },
        { timestamp: "2025-06-18 12:15:04", admin: "Admin (Super)", action: "USER_SUSPEND", target: "USR-9826", ip: "198.51.100.42", result: "SUCCESS" },
        { timestamp: "2025-06-18 10:02:45", admin: "System", action: "AUTO_BACKUP", target: "DB_PRIMARY_SNAPSHOT", ip: "127.0.0.1", result: "SUCCESS" },
        { timestamp: "2025-06-17 18:44:22", admin: "Admin (Super)", action: "PRICE_MODIFY", target: "BD_REGIONAL_PLAN", ip: "198.51.100.42", result: "SUCCESS" }
    ],

    tickets: [
        { id: "TCK-401", user: "Alexander Wright", type: "Bug Report", priority: "High", status: "In Progress", subject: "Browser extension occasional timeout on Mega links", created: "2025-06-18 08:30" },
        { id: "TCK-402", user: "Sophia Chen", type: "Feature Request", priority: "Medium", status: "Open", subject: "Auto-sort downloaded files into date-based subfolders", created: "2025-06-17 19:14" },
        { id: "TCK-403", user: "Liam O'Connor", type: "Feedback", priority: "Low", status: "Resolved", subject: "Excellent download speed improvements in v2.1.0!", created: "2025-06-16 11:20" }
    ],

    featureFlags: [
        { key: "ff_video_engine_v3", name: "Next-Gen 8K Video Stream Sniffer", desc: "Direct hardware-accelerated segment merging", rollout: 100, enabled: true },
        { key: "ff_smart_scheduler", name: "Dynamic Bandwidth Scheduler", desc: "Auto-detect network idle times and ramp up batch queues", rollout: 75, enabled: true },
        { key: "ff_extension_mv3_turbo", name: "Manifest V3 Stream Interceptor", desc: "Zero-copy native messaging protocol for Chromium/Firefox", rollout: 100, enabled: true },
        { key: "ff_p2p_mesh_speedup", name: "EDM Torrent/P2P Swarm Accelerator", desc: "Experimental hybrid swarm downloading", rollout: 25, enabled: false }
    ]
};
