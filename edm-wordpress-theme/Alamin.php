<!DOCTYPE html>
<html lang="bn">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>PDL Core | ApexDL AI</title>
    <!-- Font Awesome for Icons -->
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">
    <style>
        /* --- CSS Variables & Reset --- */
        :root {
            --bg-primary: #0b0d15;
            --bg-secondary: #141822;
            --bg-card: rgba(255, 255, 255, 0.04);
            --border-color: rgba(255, 255, 255, 0.08);
            --text-primary: #e8edf5;
            --text-secondary: #8b92a5;
            --accent-1: #7c3aed; /* Purple */
            --accent-2: #06b6d4; /* Cyan */
            --gradient-main: linear-gradient(135deg, #7c3aed, #06b6d4);
            --shadow-glow: 0 8px 32px rgba(124, 58, 237, 0.25);
        }

        * { margin: 0; padding: 0; box-sizing: border-box; font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; }
        body { background: var(--bg-primary); color: var(--text-primary); display: flex; justify-content: center; align-items: center; min-height: 100vh; padding: 15px; }

        /* --- Main Container (Glassmorphism) --- */
        #app {
            width: 100%; max-width: 960px; height: 95vh;
            background: var(--bg-secondary);
            border-radius: 24px;
            border: 1px solid var(--border-color);
            box-shadow: var(--shadow-glow);
            display: flex; flex-direction: column; overflow: hidden;
            position: relative;
            backdrop-filter: blur(12px);
        }

        /* --- Header --- */
        #header {
            padding: 18px 24px;
            background: rgba(11, 13, 21, 0.6);
            border-bottom: 1px solid var(--border-color);
            display: flex; justify-content: space-between; align-items: center;
        }
        .logo { display: flex; align-items: center; gap: 12px; font-weight: 700; font-size: 18px; }
        .logo i { background: var(--gradient-main); -webkit-background-clip: text; -webkit-text-fill-color: transparent; font-size: 22px; }
        .logo span { background: var(--gradient-main); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
        .header-actions { display: flex; gap: 12px; align-items: center; }
        .status-badge { display: flex; align-items: center; gap: 6px; font-size: 12px; color: var(--text-secondary); }
        .status-dot { width: 8px; height: 8px; border-radius: 50%; background: #22c55e; animation: pulse-dot 2s infinite; }
        .btn-icon { background: transparent; border: 1px solid var(--border-color); color: var(--text-secondary); padding: 8px; border-radius: 12px; cursor: pointer; transition: 0.3s; }
        .btn-icon:hover { background: var(--bg-card); color: var(--text-primary); border-color: var(--accent-1); transform: rotate(15deg); }

        /* --- Chat Box --- */
        #chat-box {
            flex: 1; padding: 20px 24px; overflow-y: auto;
            display: flex; flex-direction: column; gap: 16px;
            scroll-behavior: smooth;
        }
        #chat-box::-webkit-scrollbar { width: 6px; }
        #chat-box::-webkit-scrollbar-track { background: transparent; }
        #chat-box::-webkit-scrollbar-thumb { background: var(--border-color); border-radius: 10px; }

        /* --- Message Bubbles --- */
        .msg {
            max-width: 85%; padding: 14px 18px; border-radius: 18px;
            line-height: 1.6; font-size: 15px; word-wrap: break-word;
            animation: fadeInUp 0.4s cubic-bezier(0.16, 1, 0.3, 1) forwards;
            opacity: 0; transform: translateY(15px);
        }
        .user {
            align-self: flex-end;
            background: var(--gradient-main);
            color: #fff;
            border-bottom-right-radius: 4px;
            box-shadow: 0 4px 15px rgba(124, 58, 237, 0.3);
        }
        .ai {
            align-self: flex-start;
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            color: var(--text-primary);
            border-bottom-left-radius: 4px;
        }
        .ai::before {
            content: "PDL Core"; display: block; font-size: 11px; font-weight: 700;
            color: var(--accent-2); margin-bottom: 6px; letter-spacing: 0.5px;
        }
        .system-status {
            align-self: center; background: rgba(255,255,255,0.03); color: var(--text-secondary);
            font-size: 12px; padding: 6px 14px; border-radius: 20px; border: 1px dashed var(--border-color);
            font-family: monospace; text-align: center;
        }
        .system-status .model-switch {
            display: inline-flex; align-items: center; gap: 6px; margin-left: 12px;
            color: var(--text-secondary);
        }
        .system-status select {
            margin-left: 4px; background: transparent; border: 1px solid var(--border-color);
            color: var(--text-primary); border-radius: 999px; padding: 4px 8px; font-size: 12px;
            outline: none; min-width: 160px;
        }
        .settings-group.advanced {
            background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.08);
            border-radius: 16px; padding: 16px;
        }
        .settings-group.advanced .advanced-row {
            display: flex; align-items: center; justify-content: space-between;
            gap: 12px; margin-bottom: 10px;
        }
        .settings-group.advanced .advanced-row label {
            flex: 1; font-size: 13px; color: var(--text-secondary);
        }
        .settings-group.advanced .advanced-row input[type="number"] {
            width: 110px; padding: 8px 10px; border-radius: 12px;
            border: 1px solid var(--border-color); background: var(--bg-secondary); color: var(--text-primary);
        }
        .settings-group.advanced .advanced-row input[type="checkbox"] {
            transform: scale(1.1);
            margin-right: 8px;
        }

        /* --- Typing Indicator --- */
        .typing-indicator {
            align-self: flex-start; background: var(--bg-card); border: 1px solid var(--border-color);
            padding: 12px 18px; border-radius: 18px; border-bottom-left-radius: 4px;
            display: flex; gap: 5px; align-items: center; width: fit-content;
        }
        .typing-dot { width: 8px; height: 8px; background: var(--text-secondary); border-radius: 50%; animation: bounce 1.4s infinite; }
        .typing-dot:nth-child(2) { animation-delay: 0.2s; }
        .typing-dot:nth-child(3) { animation-delay: 0.4s; }

        /* --- Input Area --- */
        #input-area {
            padding: 16px 24px 24px;
            background: rgba(11, 13, 21, 0.4);
            border-top: 1px solid var(--border-color);
            display: flex; gap: 12px; align-items: center;
        }
        #userInput {
            flex: 1; padding: 14px 18px;
            background: var(--bg-card); border: 1px solid var(--border-color);
            border-radius: 16px; outline: none; color: var(--text-primary); font-size: 15px;
            transition: 0.3s; resize: none; height: 54px; overflow-y: hidden;
        }
        #userInput:focus { border-color: var(--accent-1); box-shadow: 0 0 0 3px rgba(124, 58, 237, 0.15); }
        #userInput::placeholder { color: var(--text-secondary); }
        #sendBtn {
            width: 54px; height: 54px; border-radius: 16px;
            background: var(--gradient-main); color: white; border: none;
            cursor: pointer; font-size: 20px; transition: 0.3s; display: flex; justify-content: center; align-items: center;
        }
        #sendBtn:hover { transform: scale(1.05); box-shadow: 0 4px 15px rgba(124, 58, 237, 0.4); }
        #sendBtn:disabled { opacity: 0.5; cursor: not-allowed; transform: none; }
        #attachBtn { width: 54px; height: 54px; border-radius: 16px; background: rgba(255,255,255,0.06); border: 1px solid var(--border-color); color: var(--text-secondary); cursor: pointer; display: flex; justify-content: center; align-items: center; transition: 0.3s; }
        #attachBtn:hover { background: rgba(255,255,255,0.1); color: var(--text-primary); }
        .attachment-preview { margin-top: 10px; padding: 12px 14px; border-radius: 14px; background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.08); color: var(--text-secondary); font-size: 13px; display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }
        .attachment-preview a { color: var(--accent-2); text-decoration: none; }
        .attachment-preview img { max-width: 100px; max-height: 80px; border-radius: 12px; object-fit: cover; }
        .attachment-preview video { max-width: 120px; max-height: 90px; border-radius: 12px; }

        /* --- Settings Modal --- */
        #settings-modal {
            position: absolute; top: 0; right: -100%; width: 340px; height: 100%;
            background: rgba(11, 13, 21, 0.95); backdrop-filter: blur(20px);
            border-left: 1px solid var(--border-color);
            transition: right 0.4s cubic-bezier(0.16, 1, 0.3, 1);
            padding: 20px; display: flex; flex-direction: column; gap: 20px; z-index: 10;
        }
        #settings-modal.open { right: 0; }
        .modal-header { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--border-color); padding-bottom: 15px; }
        .modal-header h3 { font-size: 18px; }
        .settings-group { display: flex; flex-direction: column; gap: 8px; }
        .settings-group label { font-size: 13px; color: var(--text-secondary); font-weight: 600; }
        .settings-group textarea, .settings-group input {
            background: var(--bg-secondary); border: 1px solid var(--border-color);
            border-radius: 12px; padding: 12px; color: var(--text-primary); font-size: 14px; outline: none;
            resize: vertical; font-family: 'Consolas', monospace;
        }
        .settings-group textarea:focus, .settings-group input:focus { border-color: var(--accent-2); }
        .btn-save { background: var(--gradient-main); border: none; color: #fff; padding: 10px; border-radius: 12px; cursor: pointer; font-weight: 600; margin-top: 5px; }
        .btn-danger { background: #dc3545; }
        .btn-sm { padding: 6px 12px; border-radius: 8px; font-size: 12px; align-self: flex-end; }

        /* --- Animations --- */
        @keyframes fadeInUp { to { opacity: 1; transform: translateY(0); } }
        @keyframes pulse-dot { 0%, 100% { opacity: 1; } 50% { opacity: 0.4; } }
        @keyframes bounce { 0%, 60%, 100% { transform: translateY(0); } 30% { transform: translateY(-8px); } }

        /* --- Responsive --- */
        @media (max-width: 600px) {
            #app { height: 98vh; border-radius: 16px; padding: 0; }
            #settings-modal { width: 100%; right: -100%; border-left: none; }
            #settings-modal.open { right: 0; }
            .msg { max-width: 95%; font-size: 14px; }
        }
    </style>
</head>
<body>

<div id="app">
    <!-- Header -->
    <div id="header">
        <div class="logo">
            <i class="fas fa-bolt"></i>
            <span>PDL Core</span>
        </div>
        <div class="header-actions">
            <div class="status-badge"><span class="status-dot"></span> Active</div>
            <button class="btn-icon" onclick="toggleSettings()" title="Settings"><i class="fas fa-sliders-h"></i></button>
        </div>
    </div>

    <!-- Chat Box -->
    <div id="chat-box">
        <div class="system-status">📌 ApexDL Project • Stage 0: Not Started</div>
    </div>

    <!-- Input Area -->
    <div id="input-area">
        <textarea id="userInput" rows="1" placeholder="আপনার প্রশ্ন বা কমান্ড লিখুন..." onkeypress="handleEnter(event)"></textarea>
        <button id="attachBtn" onclick="triggerFileSelect()" title="File / Image / Video"><i class="fas fa-paperclip"></i></button>
        <button id="sendBtn" onclick="sendMessage()"><i class="fas fa-paper-plane"></i></button>
    </div>
    <div class="attachment-preview" id="attachmentPreview" style="display:none;">
        <span id="attachmentLabel">Selected file:</span>
        <button class="btn-sm" style="background: rgba(255,255,255,0.08); border:none; cursor:pointer;" onclick="clearAttachment()">Remove</button>
    </div>
    <input type="file" id="fileInput" style="display:none;" accept="*/*" onchange="handleAttachment(event)">

    <!-- Settings Modal (API, System Prompt, Clear) -->
    <div id="settings-modal">
        <div class="modal-header">
            <h3><i class="fas fa-cog"></i> সেটিংস</h3>
            <button class="btn-icon" onclick="toggleSettings()" style="border:none;"><i class="fas fa-times"></i></button>
        </div>

        <div class="settings-group">
            <label>🔑 DeepSeek API কী</label>
            <input type="password" id="deepseekApiKey" placeholder="platform.deepseek.com থেকে নিন">
            <button class="btn-save btn-sm" onclick="saveDeepseekKey()">সেভ করুন</button>
        </div>

        <div class="settings-group">
            <label>🧠 Google Gemini AI Studio API কী</label>
            <input type="password" id="geminiApiKey" placeholder="Google Cloud AI Studio থেকে নিন">
            <button class="btn-save btn-sm" onclick="saveGeminiKey()">সেভ করুন</button>
        </div>

        <div class="settings-group advanced">
            <label>⚙️ Advanced Options</label>
            <div class="advanced-row">
                <label for="tempInput">Temperature</label>
                <input id="tempInput" type="number" min="0" max="1" step="0.05">
            </div>
            <div class="advanced-row">
                <label for="maxTokensInput">Max Tokens</label>
                <input id="maxTokensInput" type="number" min="256" max="8192" step="64">
            </div>
            <div class="advanced-row">
                <label><input type="checkbox" id="stageContextToggle"> Use stage context</label>
            </div>
            <button class="btn-save btn-sm" onclick="saveAdvancedSettings()">Save Advanced</button>
        </div>

        <div class="settings-group">
            <label>🧠 সিস্টেম প্রম্পট (PDL Core Persona)</label>
            <textarea id="sysPromptInput" rows="6" style="font-size:12px;">You are "PDL Core" (Powerfull Download Manager), a world-class AI Software Architect and Senior Systems Engineer. Your absolute core expertise lies in building advanced, ultra-fast download managers like IDM for PC and Android. 
Expertise: HTML5/CSS3/JS, Chrome Extensions (V3), Native Messaging, C++/C#/Java/Python, HTTP/HTTPS, Multi-threading, File Chunking, SQLite, HLS/m3u8, Android SDK.
CRITICAL RULES: ONLY answer ApexDL project questions. Follow a strict 7-stage lifecycle. Never skip stages. Provide optimized, production-ready code. Tone: Professional, technical, peer-to-peer architect.</textarea>
            <button class="btn-save btn-sm" onclick="saveSystemPrompt()">আপডেট করুন</button>
        </div>

        <div class="settings-group" style="margin-top:auto; border-top:1px solid var(--border-color); padding-top:15px;">
            <button class="btn-danger btn-save" onclick="clearAll()" style="width:100%;">🗑 সম্পূর্ণ হিস্ট্রি মুছুন</button>
        </div>
    </div>
</div>

<script>
    // --- State Management ---
    const KEYS = {
        api: 'pdl_api',
        deepseekApi: 'pdl_api_deepseek',
        geminiApi: 'pdl_api_gemini',
        system: 'pdl_system',
        history: 'pdl_history',
        model: 'pdl_model',
        temp: 'pdl_temp',
        maxTokens: 'pdl_max_tokens',
        stageContext: 'pdl_stage_context'
    };
    
    let DEEPSEEK_API_KEY = localStorage.getItem(KEYS.deepseekApi) || localStorage.getItem(KEYS.api) || '';
    
    let GEMINI_API_KEY = localStorage.getItem(KEYS.geminiApi) || '';

    let selectedModel = localStorage.getItem(KEYS.model) || 'gemini'; 
    let SYSTEM_PROMPT = localStorage.getItem(KEYS.system) || '';
    let chatHistory = JSON.parse(localStorage.getItem(KEYS.history)) || [];
    let temperature = parseFloat(localStorage.getItem(KEYS.temp)) || 0.3;
    let maxTokens = parseInt(localStorage.getItem(KEYS.maxTokens), 10) || 8192;
    let useStageContext = localStorage.getItem(KEYS.stageContext) === 'true';
    let attachmentData = null;

    // Default welcome message initialization
    if (chatHistory.length === 0) {
        chatHistory.push({
            role: 'assistant',
            content: 'হ্যালো! আমি **PDL Core**। আপনার "ApexDL" ডাউনলোড ম্যানেজার প্রজেক্টের জন্য আমি পূর্ণাঙ্গ আর্কিটেক্ট। আপনি কীভাবে শুরু করতে চান? (যেমন: Stage 1 এর প্ল্যানিং)'
        });
        localStorage.setItem(KEYS.history, JSON.stringify(chatHistory));
    }

    // Default Prompt if empty
    if(!SYSTEM_PROMPT) {
        SYSTEM_PROMPT = `You are "PDL Core" (Powerfull Download Manager), a world-class AI Software Architect and Senior Systems Engineer. Your absolute core expertise lies in building advanced, ultra-fast download managers like IDM for PC and Android. 
Expertise: HTML5/CSS3/JS, Chrome Extensions (V3), Native Messaging, C++/C#/Java/Python, HTTP/HTTPS, Multi-threading, File Chunking, SQLite, HLS/m3u8, Android SDK.
CRITICAL RULES: ONLY answer ApexDL project questions. Follow a strict 7-stage lifecycle. Never skip stages. Provide optimized, production-ready code. Tone: Professional, technical, peer-to-peer architect.`;
        localStorage.setItem(KEYS.system, SYSTEM_PROMPT);
    }

    // --- Project State ---
    const projectState = {
        currentStage: 0,
        completedModules: [],
        lastDecision: ''
    };

    function updateProjectStatus() {
        renderChat();
    }

    // --- Init ---
    window.onload = function() {
        renderChat(); 
        if(DEEPSEEK_API_KEY) document.getElementById('deepseekApiKey').placeholder = '✅ Key Saved';
        if(GEMINI_API_KEY) document.getElementById('geminiApiKey').placeholder = '✅ Key Saved';
        document.getElementById('sysPromptInput').value = SYSTEM_PROMPT;
        document.getElementById('tempInput').value = temperature;
        document.getElementById('maxTokensInput').value = maxTokens;
        document.getElementById('stageContextToggle').checked = useStageContext;
    };

    function changeModel() {
        selectedModel = document.getElementById('modelSelect').value;
        localStorage.setItem(KEYS.model, selectedModel);
    }

    function saveAdvancedSettings() {
        temperature = parseFloat(document.getElementById('tempInput').value) || 0.3;
        maxTokens = parseInt(document.getElementById('maxTokensInput').value, 10) || 8192;
        useStageContext = document.getElementById('stageContextToggle').checked;
        localStorage.setItem(KEYS.temp, temperature);
        localStorage.setItem(KEYS.maxTokens, maxTokens);
        localStorage.setItem(KEYS.stageContext, useStageContext);
        alert('✅ Advanced options সেভ করা হয়েছে!');
    }

    function saveDeepseekKey() {
        const key = document.getElementById('deepseekApiKey').value.trim();
        if(!key) return alert('DeepSeek API কী দিন!');
        DEEPSEEK_API_KEY = key;
        localStorage.setItem(KEYS.deepseekApi, DEEPSEEK_API_KEY);
        document.getElementById('deepseekApiKey').value = '';
        document.getElementById('deepseekApiKey').placeholder = '✅ Key Saved';
        alert('✅ DeepSeek API কী সফলভাবে সেভ করা হয়েছে!');
    }

    function saveGeminiKey() {
        const key = document.getElementById('geminiApiKey').value.trim();
        if(!key) return alert('Google AI Studio API কী দিন!');
        GEMINI_API_KEY = key;
        localStorage.setItem(KEYS.geminiApi, GEMINI_API_KEY);
        document.getElementById('geminiApiKey').value = '';
        document.getElementById('geminiApiKey').placeholder = '✅ Key Saved';
        alert('✅ Gemini API কী সফলভাবে সেভ করা হয়েছে!');
    }

    // --- Toggle Settings ---
    function toggleSettings() {
        document.getElementById('settings-modal').classList.toggle('open');
    }

    // --- Markdown Parser ---
    function formatMarkdown(text) {
        if (!text) return '';
        let html = text;
        
        // Escape standard HTML tags first for security
        html = html
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;");

        // Match triple backticks (Code Blocks)
        html = html.replace(/```(\w*)\n([\s\S]*?)\n```/g, function(match, lang, code) {
            return `<pre style="background: rgba(0,0,0,0.4); padding: 14px; border-radius: 12px; margin: 12px 0; overflow-x: auto; font-family: monospace; border: 1px solid var(--border-color);"><code style="font-family: monospace; color: #a78bfa; font-size: 13px; line-height: 1.5; display: block; white-space: pre;">${code}</code></pre>`;
        });

        // Match single backticks (Inline Code)
        html = html.replace(/`([^`]+)`/g, '<code style="background: rgba(255,255,255,0.08); padding: 2px 6px; border-radius: 6px; font-family: monospace; color: #06b6d4;">$1</code>');

        // Match Bold tags (**text**)
        html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');

        return html;
    }

    // --- Render Chat ---
    function renderChat() {
        const chatBox = document.getElementById('chat-box');
        
        // Keep first child (system status) and remove others safely
        while(chatBox.children.length > 1) {
            chatBox.removeChild(chatBox.lastChild);
        }
        
        // Ensure system status header exists, if not create it
        let status = chatBox.querySelector('.system-status');
        if (!status) {
            status = document.createElement('div');
            status.className = 'system-status';
            chatBox.insertBefore(status, chatBox.firstChild);
        }
        
        status.innerHTML = `📌 ApexDL Project • Memory: ${chatHistory.length} messages <span class="model-switch">Model: <select id="modelSelect" onchange="changeModel()"><option value="deepseek"${selectedModel === 'deepseek' ? ' selected' : ''}>DeepSeek</option><option value="gemini"${selectedModel === 'gemini' ? ' selected' : ''}>Gemini AI Studio</option></select></span>`;

        chatHistory.forEach(msg => {
            const div = document.createElement('div');
            div.className = `msg ${msg.role === 'assistant' ? 'ai' : msg.role}`;

            const contentEl = document.createElement('div');
            contentEl.style.whiteSpace = 'pre-wrap';
            
            // Apply markdown styles to AI replies
            if (msg.role === 'assistant') {
                contentEl.innerHTML = formatMarkdown(msg.content);
            } else {
                contentEl.textContent = msg.content || '';
            }
            div.appendChild(contentEl);

            if(msg.attachment) {
                const attachmentEl = document.createElement('div');
                attachmentEl.style.display = 'flex';
                attachmentEl.style.flexDirection = 'column';
                attachmentEl.style.gap = '8px';
                attachmentEl.style.marginTop = '10px';

                const label = document.createElement('div');
                label.textContent = `Attachment: ${msg.attachment.name} (${msg.attachment.type || 'unknown'}) ${formatFileSize(msg.attachment.size)}`;
                label.style.fontSize = '13px';
                label.style.color = 'var(--text-secondary)';
                attachmentEl.appendChild(label);

                if(msg.attachment.dataUrl) {
                    if(msg.attachment.type.startsWith('image/')) {
                        const img = document.createElement('img');
                        img.src = msg.attachment.dataUrl;
                        img.alt = msg.attachment.name;
                        attachmentEl.appendChild(img);
                    } else if(msg.attachment.type.startsWith('video/')) {
                        const video = document.createElement('video');
                        video.src = msg.attachment.dataUrl;
                        video.controls = true;
                        video.style.maxWidth = '100%';
                        attachmentEl.appendChild(video);
                    }
                }

                const link = document.createElement('a');
                link.href = msg.attachment.dataUrl || '#';
                link.download = msg.attachment.name;
                link.textContent = 'Download attachment';
                link.target = '_blank';
                attachmentEl.appendChild(link);

                div.appendChild(attachmentEl);
            }

            chatBox.appendChild(div);
        });
        chatBox.scrollTop = chatBox.scrollHeight;
    }

    function triggerFileSelect() {
        document.getElementById('fileInput').click();
    }

    function handleAttachment(event) {
        const file = event.target.files[0];
        if(!file) return;

        const reader = new FileReader();
        reader.onload = function() {
            attachmentData = {
                name: file.name,
                size: file.size,
                type: file.type,
                lastModified: file.lastModified,
                dataUrl: reader.result
            };
            renderAttachmentPreview();
            event.target.value = '';
        };
        reader.onerror = function() {
            alert('Attachment load failed.');
            event.target.value = '';
        };
        reader.readAsDataURL(file);
    }

    function clearAttachment() {
        attachmentData = null;
        const preview = document.getElementById('attachmentPreview');
        preview.innerHTML = '';
        preview.style.display = 'none';
        document.getElementById('fileInput').value = '';
    }

    function renderAttachmentPreview() {
        const preview = document.getElementById('attachmentPreview');
        preview.innerHTML = '';

        if(!attachmentData) {
            preview.style.display = 'none';
            return;
        }

        const title = document.createElement('div');
        title.textContent = `Selected: ${attachmentData.name} (${formatFileSize(attachmentData.size)})`;
        title.style.flex = '1';
        preview.appendChild(title);

        if(attachmentData.type.startsWith('image/')) {
            const img = document.createElement('img');
            img.src = attachmentData.dataUrl;
            img.alt = attachmentData.name;
            preview.appendChild(img);
        } else if(attachmentData.type.startsWith('video/')) {
            const video = document.createElement('video');
            video.src = attachmentData.dataUrl;
            video.controls = true;
            video.muted = true;
            preview.appendChild(video);
        }

        const removeBtn = document.createElement('button');
        removeBtn.className = 'btn-sm';
        removeBtn.style.background = 'rgba(255,255,255,0.08)';
        removeBtn.style.border = 'none';
        removeBtn.style.cursor = 'pointer';
        removeBtn.textContent = 'Remove';
        removeBtn.onclick = clearAttachment;
        preview.appendChild(removeBtn);

        preview.style.display = 'flex';
    }

    function formatFileSize(bytes) {
        if(bytes < 1024) return `${bytes} B`;
        const kb = bytes / 1024;
        if(kb < 1024) return `${kb.toFixed(1)} KB`;
        const mb = kb / 1024;
        if(mb < 1024) return `${mb.toFixed(1)} MB`;
        return `${(mb/1024).toFixed(1)} GB`;
    }

    // --- Save System Prompt ---
    function saveSystemPrompt() {
        const val = document.getElementById('sysPromptInput').value.trim();
        if(!val) return alert('প্রম্পট খালি থাকতে পারে না!');
        SYSTEM_PROMPT = val;
        localStorage.setItem(KEYS.system, SYSTEM_PROMPT);
        alert('✅ সিস্টেম প্রম্পট আপডেট করা হয়েছে!');
        toggleSettings();
    }

    // --- Clear History ---
    function clearAll() {
        if(confirm('সমস্ত চ্যাট ইতিহাস মুছে ফেলতে চান? (API ও প্রম্পট থাকবে)')) {
            chatHistory = [];
            chatHistory.push({
                role: 'assistant',
                content: 'হ্যালো! আমি **PDL Core**। আপনার "ApexDL" ডাউনলোড ম্যানেজার প্রজেক্টের জন্য আমি পূর্ণাঙ্গ আর্কিটেক্ট। আপনি কীভাবে শুরু করতে চান? (যেমন: Stage 1 এর প্ল্যানিং)'
            });
            localStorage.setItem(KEYS.history, JSON.stringify(chatHistory));
            renderChat();
            toggleSettings();
        }
    }

    // --- Send Message ---
    const STORAGE_KEYS = KEYS;
    const stageNames = ['Not Started', 'Stage 1: Planning & Architecture', 'Stage 2: Core Download Engine', 'Stage 3: Extension & UI', 'Stage 4: Networking & Protocol', 'Stage 5: Advanced Features (Chunking, HLS)', 'Stage 6: Mobile Port (Android)', 'Stage 7: Optimization & Deployment'];

    async function sendMessage() {
        const input = document.getElementById('userInput');
        const message = input.value.trim();
        if(!message) return;

        // API কী ভ্যালিডেশন
        if(selectedModel === 'gemini') {
            if(!GEMINI_API_KEY) {
                const keyInput = document.getElementById('geminiApiKey').value.trim();
                if(!keyInput) { alert('Google AI Studio API কী দিন!'); return; }
                GEMINI_API_KEY = keyInput;
                localStorage.setItem(KEYS.geminiApi, GEMINI_API_KEY);
            }
        } else {
            if(!DEEPSEEK_API_KEY) {
                const keyInput = document.getElementById('deepseekApiKey').value.trim();
                if(!keyInput) { alert('DeepSeek API কী দিন!'); return; }
                DEEPSEEK_API_KEY = keyInput;
                localStorage.setItem(KEYS.deepseekApi, DEEPSEEK_API_KEY);
            }
        }

        const sendBtn = document.getElementById('sendBtn');
        sendBtn.disabled = true;

        // ইউজার মেসেজ হিস্ট্রিতে যোগ
        const userMessage = { role: 'user', content: message };
        if(attachmentData) {
            userMessage.attachment = { ...attachmentData };
        }
        chatHistory.push(userMessage);
        localStorage.setItem(STORAGE_KEYS.history, JSON.stringify(chatHistory));
        renderChat();

        // DOM-এ নিরাপদ উপায়ে লোডিং ইন্ডিকেটর যোগ (appendChild ব্যবহার করে)
        const loadingId = 'loading_' + Date.now();
        const loadingDiv = document.createElement('div');
        loadingDiv.id = loadingId;
        loadingDiv.className = 'msg ai loading';
        loadingDiv.innerHTML = `⏳ PDL Core is thinking (${selectedModel === 'gemini' ? 'Gemini' : 'DeepSeek'})...`;
        document.getElementById('chat-box').appendChild(loadingDiv);
        document.getElementById('chat-box').scrollTop = document.getElementById('chat-box').scrollHeight;

        try {
            let reply = '';

            if(selectedModel === 'gemini') {
                const systemText = SYSTEM_PROMPT + (useStageContext ? `\n\n[PROJECT CONTEXT]\nCurrent Stage: ${stageNames[projectState.currentStage]}\nCompleted Modules: ${projectState.completedModules.length > 0 ? projectState.completedModules.join(', ') : 'None'}` : '');
                const geminiContents = [];

                // শুধুমাত্র চ্যাট হিস্ট্রি ফিল্টার করে পাঠানো হচ্ছে
                chatHistory.forEach(msg => {
                    if (msg.role === 'user') {
                        geminiContents.push({ role: 'user', parts: [{ text: msg.content }] });
                    } else if (msg.role === 'assistant' || msg.role === 'ai') {
                        geminiContents.push({ role: 'model', parts: [{ text: msg.content }] });
                    }
                });

                // v1beta এবং gemini-2.5-flash এন্ডপয়েন্ট ব্যবহার করা হয়েছে
                const url = `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=${GEMINI_API_KEY}`;
                
                const payload = {
                    systemInstruction: { parts: [{ text: systemText }] },
                    contents: geminiContents,
                    generationConfig: {
                        temperature: temperature,
                        maxOutputTokens: maxTokens
                    }
                };

                const response = await fetch(url, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(payload)
                });

                const data = await response.json();
                
                const loadingEl = document.getElementById(loadingId);
                if (loadingEl) loadingEl.remove();

                if(data.candidates && data.candidates.length > 0 && data.candidates[0].content) {
                    reply = data.candidates[0].content.parts[0].text;
                } else {
                    const errDiv = document.createElement('div');
                    errDiv.className = 'msg ai';
                    errDiv.innerHTML = `❌ Gemini API ত্রুটি: ${data.error ? data.error.message : 'কোনো উত্তর পাওয়া যায়নি'}`;
                    document.getElementById('chat-box').appendChild(errDiv);
                }
            } else {
                const response = await fetch('https://api.deepseek.com/chat/completions', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${DEEPSEEK_API_KEY}`
                    },
                    body: JSON.stringify({
                        model: 'deepseek-chat',
                        messages: [
                            { role: 'system', content: SYSTEM_PROMPT + (useStageContext ? `\n\n[PROJECT CONTEXT]\nCurrent Stage: ${stageNames[projectState.currentStage]}\nCompleted Modules: ${projectState.completedModules.length > 0 ? projectState.completedModules.join(', ') : 'None'}` : '') },
                            ...chatHistory.map(msg => ({ role: msg.role, content: msg.content }))
                        ],
                        max_tokens: maxTokens,
                        temperature: temperature
                    })
                });

                const data = await response.json();
                
                const loadingEl = document.getElementById(loadingId);
                if (loadingEl) loadingEl.remove();

                if(data.choices && data.choices.length > 0 && data.choices[0].message && data.choices[0].message.content) {
                    reply = data.choices[0].message.content;
                } else {
                    const errDiv = document.createElement('div');
                    errDiv.className = 'msg ai';
                    errDiv.innerHTML = `❌ DeepSeek API ত্রুটি: ${data.error ? data.error.message : 'কোনো উত্তর পাওয়া যায়নি'}`;
                    document.getElementById('chat-box').appendChild(errDiv);
                }
            }

            if(reply) {
                chatHistory.push({ role: 'assistant', content: reply });
                localStorage.setItem(STORAGE_KEYS.history, JSON.stringify(chatHistory));
                renderChat();

                if(reply.toLowerCase().includes('stage 1 complete') || reply.toLowerCase().includes('completed stage 1')) {
                    projectState.currentStage = 1; projectState.completedModules.push('Stage 1'); updateProjectStatus();
                } else if(reply.toLowerCase().includes('stage 2 complete') || reply.toLowerCase().includes('completed stage 2')) {
                    projectState.currentStage = 2; projectState.completedModules.push('Stage 2'); updateProjectStatus();
                } else if(reply.toLowerCase().includes('stage 3 complete') || reply.toLowerCase().includes('completed stage 3')) {
                    projectState.currentStage = 3; projectState.completedModules.push('Stage 3'); updateProjectStatus();
                } else if(reply.toLowerCase().includes('stage 4 complete') || reply.toLowerCase().includes('completed stage 4')) {
                    projectState.currentStage = 4; projectState.completedModules.push('Stage 4'); updateProjectStatus();
                } else if(reply.toLowerCase().includes('stage 5 complete') || reply.toLowerCase().includes('completed stage 5')) {
                    projectState.currentStage = 5; projectState.completedModules.push('Stage 5'); updateProjectStatus();
                } else if(reply.toLowerCase().includes('stage 6 complete') || reply.toLowerCase().includes('completed stage 6')) {
                    projectState.currentStage = 6; projectState.completedModules.push('Stage 6'); updateProjectStatus();
                } else if(reply.toLowerCase().includes('stage 7 complete') || reply.toLowerCase().includes('completed stage 7')) {
                    projectState.currentStage = 7; projectState.completedModules.push('Stage 7'); updateProjectStatus();
                }

                projectState.lastDecision = message.substring(0, 100) + '...';
                updateProjectStatus();
            }

        } catch (error) {
            const loadingEl = document.getElementById(loadingId);
            if (loadingEl) loadingEl.remove();
            
            const errDiv = document.createElement('div');
            errDiv.className = 'msg ai';
            errDiv.innerHTML = `❌ Error: ${error.message}`;
            document.getElementById('chat-box').appendChild(errDiv);
        }

        input.value = '';
        clearAttachment();
        sendBtn.disabled = false;
        document.getElementById('chat-box').scrollTop = document.getElementById('chat-box').scrollHeight;
    }

    // --- Auto-resize textarea & Enter key ---
    function handleEnter(e) {
        const input = document.getElementById('userInput');
        if(e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
        // Auto resize
        input.style.height = '54px';
        input.style.height = Math.min(input.scrollHeight, 150) + 'px';
    }
</script>
</body>
</html>