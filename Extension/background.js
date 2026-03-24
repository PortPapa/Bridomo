// background.js
// WebSocket 클라이언트. content.js에서 받은 DOM 이벤트를 WPF 앱으로 전송합니다.

let ws = null;
let isRecording = false;
let wsUrl = "ws://127.0.0.1:8889";
let reconnectTimer = null;
let eventCount = 0;
let clickCount = 0;
let inputCount = 0;

// WebSocket 연결
function connect() {
  if (ws && ws.readyState === WebSocket.OPEN) return;

  try {
    ws = new WebSocket(wsUrl);

    ws.onopen = () => {
      console.log("[DOMRecorder] WebSocket connected to", wsUrl);
      broadcastStatus();
    };

    ws.onclose = () => {
      console.log("[DOMRecorder] WebSocket disconnected");
      ws = null;
      broadcastStatus();
      // 자동 재연결 (녹화 중이면)
      if (isRecording) {
        reconnectTimer = setTimeout(connect, 3000);
      }
    };

    ws.onerror = (err) => {
      console.log("[DOMRecorder] WebSocket error");
      ws = null;
    };
  } catch (e) {
    console.error("[DOMRecorder] Connection failed:", e);
  }
}

function disconnect() {
  if (reconnectTimer) {
    clearTimeout(reconnectTimer);
    reconnectTimer = null;
  }
  if (ws) {
    ws.close();
    ws = null;
  }
  broadcastStatus();
}

// 상태를 popup과 content에 알림
function broadcastStatus() {
  const connected = ws !== null && ws.readyState === WebSocket.OPEN;
  const status = {
    type: "status",
    isRecording,
    isConnected: connected
  };

  // 배지 업데이트
  if (isRecording && connected) {
    chrome.action.setBadgeText({ text: "REC" });
    chrome.action.setBadgeBackgroundColor({ color: "#3fb950" });
  } else if (isRecording) {
    chrome.action.setBadgeText({ text: "..." });
    chrome.action.setBadgeBackgroundColor({ color: "#d29922" });
  } else {
    chrome.action.setBadgeText({ text: "" });
  }

  // 모든 탭의 content script에 알림
  chrome.tabs.query({}, (tabs) => {
    for (const tab of tabs) {
      chrome.tabs.sendMessage(tab.id, status).catch(() => {});
    }
  });
}

// content.js에서 받은 DOM 이벤트를 WebSocket으로 전송
chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (msg.type === "dom_event" && isRecording) {
    if (ws && ws.readyState === WebSocket.OPEN) {
      ws.send(JSON.stringify(msg.data));
    }
    eventCount++;
    if (msg.data?.type === "click") clickCount++;
    if (msg.data?.type === "input" || msg.data?.type === "change") inputCount++;
    sendResponse({ ok: true });
  }
  else if (msg.type === "get_status") {
    sendResponse({
      isRecording,
      isConnected: ws !== null && ws.readyState === WebSocket.OPEN,
      wsUrl,
      eventCount,
      clickCount,
      inputCount
    });
  }
  else if (msg.type === "start_recording") {
    isRecording = true;
    eventCount = 0; clickCount = 0; inputCount = 0;
    wsUrl = msg.wsUrl || wsUrl;
    connect();
    broadcastStatus();
    sendResponse({ ok: true });
  }
  else if (msg.type === "stop_recording") {
    isRecording = false;
    disconnect();
    broadcastStatus();
    sendResponse({ ok: true });
  }
  else if (msg.type === "set_ws_url") {
    wsUrl = msg.wsUrl;
    // 저장
    chrome.storage.local.set({ wsUrl });
    sendResponse({ ok: true });
  }

  return true; // async response
});

// 저장된 설정 로드
chrome.storage.local.get(["wsUrl"], (result) => {
  if (result.wsUrl) wsUrl = result.wsUrl;
});

// ── 동적 아이콘 생성 ──
function generateIcon(size) {
  const canvas = new OffscreenCanvas(size, size);
  const ctx = canvas.getContext('2d');
  const s = size / 128;

  // Background
  ctx.fillStyle = '#0d1117';
  ctx.fillRect(0, 0, size, size);

  // Border
  ctx.strokeStyle = '#30363d';
  ctx.lineWidth = Math.max(1, 2 * s);
  ctx.strokeRect(2 * s, 2 * s, size - 4 * s, size - 4 * s);

  // "LTI" text
  ctx.fillStyle = '#58a6ff';
  ctx.font = `bold ${Math.max(8, 42 * s)}px Consolas, monospace`;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText('LTI', size / 2, size / 2 + 2 * s);

  // Green dot
  ctx.fillStyle = '#3fb950';
  ctx.beginPath();
  ctx.arc(100 * s, 28 * s, Math.max(2, 8 * s), 0, Math.PI * 2);
  ctx.fill();

  return ctx.getImageData(0, 0, size, size);
}

// 아이콘 설정
try {
  chrome.action.setIcon({
    imageData: {
      16: generateIcon(16),
      48: generateIcon(48),
      128: generateIcon(128)
    }
  });
} catch(e) {
  console.log("[DOMRecorder] Icon generation skipped:", e.message);
}
