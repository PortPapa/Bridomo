// popup.js

const toggleBtn = document.getElementById("toggleBtn");
const statusDot = document.getElementById("statusDot");
const statusText = document.getElementById("statusText");
const wsUrlInput = document.getElementById("wsUrl");
const statsBar = document.getElementById("statsBar");
const eventCount = document.getElementById("eventCount");
const clickCount = document.getElementById("clickCount");
const inputCount = document.getElementById("inputCount");

let isRecording = false;
let stats = { total: 0, clicks: 0, inputs: 0 };

function updateUI(recording, connected) {
  isRecording = recording;

  if (recording && connected) {
    statusDot.className = "dot green";
    statusText.textContent = "녹화 중";
    toggleBtn.textContent = "Stop";
    toggleBtn.className = "btn btn-stop";
    wsUrlInput.disabled = true;
    statsBar.style.display = "flex";
  } else if (recording && !connected) {
    statusDot.className = "dot orange";
    statusText.textContent = "연결 중...";
    toggleBtn.textContent = "Stop";
    toggleBtn.className = "btn btn-stop";
    wsUrlInput.disabled = true;
  } else {
    statusDot.className = "dot red";
    statusText.textContent = "대기";
    toggleBtn.textContent = "Run";
    toggleBtn.className = "btn btn-start";
    wsUrlInput.disabled = false;
    if (stats.total === 0) statsBar.style.display = "none";
  }
}

function updateStats(res) {
  if (res && res.eventCount !== undefined) {
    stats.total = res.eventCount || 0;
    stats.clicks = res.clickCount || 0;
    stats.inputs = res.inputCount || 0;
    eventCount.textContent = stats.total;
    clickCount.textContent = stats.clicks;
    inputCount.textContent = stats.inputs;
    if (stats.total > 0) statsBar.style.display = "flex";
  }
}

// 초기 상태
chrome.runtime.sendMessage({ type: "get_status" }, (res) => {
  if (res) {
    wsUrlInput.value = res.wsUrl || "ws://127.0.0.1:8889";
    updateUI(res.isRecording, res.isConnected);
    updateStats(res);
  }
});

// 주기적 상태 업데이트
setInterval(() => {
  chrome.runtime.sendMessage({ type: "get_status" }, (res) => {
    if (res) {
      updateUI(res.isRecording, res.isConnected);
      updateStats(res);
    }
  });
}, 1000);

// 토글 버튼
toggleBtn.addEventListener("click", () => {
  if (isRecording) {
    chrome.runtime.sendMessage({ type: "stop_recording" }, () => {
      updateUI(false, false);
    });
  } else {
    const url = wsUrlInput.value.trim();
    chrome.runtime.sendMessage({ type: "set_ws_url", wsUrl: url });
    chrome.runtime.sendMessage({ type: "start_recording", wsUrl: url }, () => {
      updateUI(true, false);
      setTimeout(() => {
        chrome.runtime.sendMessage({ type: "get_status" }, (res) => {
          if (res) updateUI(res.isRecording, res.isConnected);
        });
      }, 1500);
    });
  }
});
