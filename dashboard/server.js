const express = require('express');
const http = require('http');
const WebSocket = require('ws');
const path = require('path');

const app = express();
const server = http.createServer(app);
const wss = new WebSocket.Server({ server });

const PORT = process.env.PORT || 3000;
const ACCESS_TOKEN = 'secret123'; // Access token for authentication

// Serve static dashboard files
app.use(express.static(path.join(__dirname, 'public')));

// Store connected agents: key = agentId, value = socket
const connectedAgents = new Map();
const dashboardSockets = new Set();

wss.on('connection', (ws, req) => {
    const urlParams = new URLSearchParams(req.url.split('?')[1]);
    const type = urlParams.get('type');
    const token = urlParams.get('token');

    // Token Authentication
    if (token !== ACCESS_TOKEN) {
        console.log(`Unauthorized connection attempt from ${req.socket.remoteAddress}`);
        ws.close(4001, 'Unauthorized');
        return;
    }

    if (type === 'agent') {
        console.log('New agent socket connected. Awaiting registration payload...');

        ws.on('message', (message, isBinary) => {
            if (ws.agentId) {
                // Once registered, relay agent streams/responses to dashboards targeting this agent
                broadcastToTargetedDashboards(ws.agentId, message, isBinary);
            } else {
                // Process registration payload
                if (!isBinary) {
                    try {
                        const msg = JSON.parse(message.toString());
                        if (msg.type === 'register') {
                            const agentId = `${msg.hostname}@${msg.ip}`;
                            
                            // If an agent with the same ID is already connected, close the old one
                            if (connectedAgents.has(agentId)) {
                                console.log(`Replacing duplicate agent: ${agentId}`);
                                connectedAgents.get(agentId).close();
                            }

                            ws.agentId = agentId;
                            ws.ip = msg.ip;
                            ws.hostname = msg.hostname;
                            ws.screenWidth = msg.screenWidth || 1920;
                            ws.screenHeight = msg.screenHeight || 1080;

                            connectedAgents.set(agentId, ws);
                            console.log(`Agent registered successfully: ${agentId}`);

                            // Notify dashboards of updated agent list
                            broadcastAgentList();
                        }
                    } catch (err) {
                        console.error('Failed to parse registration payload:', err);
                        ws.close(4003, 'Invalid registration payload');
                    }
                }
            }
        });

        ws.on('close', () => {
            if (ws.agentId) {
                console.log(`Agent disconnected: ${ws.agentId}`);
                connectedAgents.delete(ws.agentId);

                // Notify dashboards targeting this agent that it has gone offline
                for (const db of dashboardSockets) {
                    if (db.targetAgentId === ws.agentId) {
                        db.targetAgentId = null;
                        db.send(JSON.stringify({ type: 'status', connected: false }));
                    }
                }
                broadcastAgentList();
            }
        });

        ws.on('error', (err) => {
            console.error('Agent socket error:', err);
        });

    } else if (type === 'dashboard') {
        dashboardSockets.add(ws);
        ws.targetAgentId = null;
        console.log(`Dashboard client connected. Total dashboard connections: ${dashboardSockets.size}`);

        // Immediately send the active agent list to the new dashboard
        sendAgentList(ws);

        ws.on('message', (message, isBinary) => {
            if (!isBinary) {
                try {
                    const msg = JSON.parse(message.toString());
                    
                    // Handle agent targeting selection
                    if (msg.action === 'select_agent') {
                        const targetId = msg.targetId;
                        if (targetId && connectedAgents.has(targetId)) {
                            const agent = connectedAgents.get(targetId);
                            ws.targetAgentId = targetId;
                            console.log(`Dashboard ${req.socket.remoteAddress} is now controlling: ${targetId}`);
                            
                            // Send online status confirmation for selected agent
                            ws.send(JSON.stringify({
                                type: 'status',
                                connected: true,
                                screenWidth: agent.screenWidth,
                                screenHeight: agent.screenHeight,
                                targetId: targetId
                            }));
                        } else {
                            ws.targetAgentId = null;
                            ws.send(JSON.stringify({ type: 'status', connected: false }));
                        }
                        return;
                    }
                } catch (e) {
                    // Ignore JSON parse errors for direct text/shell commands and fall through to forward
                }
            }

            // Forward dashboard command to the selected target agent
            if (ws.targetAgentId && connectedAgents.has(ws.targetAgentId)) {
                const agent = connectedAgents.get(ws.targetAgentId);
                if (agent.readyState === WebSocket.OPEN) {
                    agent.send(message, { binary: isBinary });
                }
            } else {
                ws.send(JSON.stringify({ type: 'error', message: 'No target agent selected.' }));
            }
        });

        ws.on('close', () => {
            dashboardSockets.delete(ws);
            console.log(`Dashboard client disconnected. Total: ${dashboardSockets.size}`);
        });

        ws.on('error', (err) => {
            console.error('Dashboard socket error:', err);
        });
    } else {
        ws.close(4002, 'Unknown client type');
    }
});

// Helper: Broadcast current agent list to all dashboard clients
function broadcastAgentList() {
    const listPayload = JSON.stringify(getAgentListPayload());
    for (const ws of dashboardSockets) {
        if (ws.readyState === WebSocket.OPEN) {
            ws.send(listPayload);
        }
    }
}

// Helper: Send current agent list to a specific dashboard
function sendAgentList(ws) {
    if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify(getAgentListPayload()));
    }
}

// Helper: Format agent list for transfer
function getAgentListPayload() {
    const list = [];
    for (const [id, agent] of connectedAgents.entries()) {
        list.push({
            id: id,
            ip: agent.ip,
            hostname: agent.hostname
        });
    }
    return { type: 'agent_list', agents: list };
}

// Helper: Relay binary/text frames from target agent to dashboards controlling it
function broadcastToTargetedDashboards(agentId, message, isBinary) {
    for (const ws of dashboardSockets) {
        if (ws.targetAgentId === agentId && ws.readyState === WebSocket.OPEN) {
            ws.send(message, { binary: isBinary });
        }
    }
}

server.listen(PORT, '0.0.0.0', () => {
    console.log(`==================================================`);
    console.log(`  Relay & Dashboard Server running on port ${PORT}  `);
    console.log(`  Access dashboard: http://localhost:${PORT}        `);
    console.log(`==================================================`);
});

// ==================================================
//   UDP Broadcast Beacon for Auto-Discovery (Port 3001)
// ==================================================
const dgram = require('dgram');
const os = require('os');
const udpSocket = dgram.createSocket('udp4');

udpSocket.bind(() => {
    udpSocket.setBroadcast(true);
    console.log('UDP broadcast socket initialized for client auto-discovery.');
    
    // Broadcast every 2 seconds
    setInterval(() => {
        try {
            const nets = os.networkInterfaces();
            let localIp = '127.0.0.1';
            
            // Loop network interfaces to find the active Wi-Fi / Local Ethernet IPv4 address
            for (const name of Object.keys(nets)) {
                for (const net of nets[name]) {
                    // Skip internal (loopback) and non-IPv4 addresses
                    if (net.family === 'IPv4' && !net.internal) {
                        localIp = net.address;
                        break;
                    }
                }
                if (localIp !== '127.0.0.1') break;
            }
            
            const message = Buffer.from(`controlhub_relay:${localIp}:${PORT}`);
            udpSocket.send(message, 0, message.length, 3001, '255.255.255.255', (err) => {
                if (err) {
                    console.error('UDP broadcast failed:', err);
                }
            });
        } catch (e) {
            console.error('UDP discovery logic error:', e);
        }
    }, 2000);
});
