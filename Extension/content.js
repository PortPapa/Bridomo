// content.js
// DOM 인터랙션을 캡처하여 background.js로 전송합니다.

let recording = false;

// background에서 녹화 상태 수신
chrome.runtime.onMessage.addListener((msg) => {
  if (msg.type === "status") {
    recording = msg.isRecording;
  }
});

// 초기 상태 확인
chrome.runtime.sendMessage({ type: "get_status" }, (res) => {
  if (res) recording = res.isRecording;
});

// ──────────────── CSS Selector 생성 ────────────────

function getSelector(el) {
  if (el.id) return `#${el.id}`;

  const parts = [];
  let current = el;

  while (current && current !== document.body && current !== document.documentElement) {
    let selector = current.tagName.toLowerCase();

    if (current.id) {
      selector = `#${current.id}`;
      parts.unshift(selector);
      break;
    }

    if (current.className && typeof current.className === "string") {
      const classes = current.className.trim().split(/\s+/).filter(c => c.length > 0).slice(0, 3);
      if (classes.length > 0) {
        selector += "." + classes.join(".");
      }
    }

    // nth-child for uniqueness
    const parent = current.parentElement;
    if (parent) {
      const siblings = Array.from(parent.children).filter(c => c.tagName === current.tagName);
      if (siblings.length > 1) {
        const index = siblings.indexOf(current) + 1;
        selector += `:nth-child(${index})`;
      }
    }

    parts.unshift(selector);
    current = current.parentElement;
  }

  return parts.join(" > ");
}

// ──────────────── XPath 생성 ────────────────

function getXPath(el) {
  const parts = [];
  let current = el;

  while (current && current.nodeType === Node.ELEMENT_NODE) {
    let index = 1;
    let sibling = current.previousSibling;
    while (sibling) {
      if (sibling.nodeType === Node.ELEMENT_NODE && sibling.tagName === current.tagName) {
        index++;
      }
      sibling = sibling.previousSibling;
    }
    parts.unshift(`${current.tagName.toLowerCase()}[${index}]`);
    current = current.parentNode;
  }

  return "/" + parts.join("/");
}

// ──────────────── 주요 속성 추출 ────────────────

function getAttributes(el) {
  const attrs = {};
  const important = ["id", "class", "name", "type", "href", "src", "action", "value",
                      "placeholder", "data-testid", "aria-label", "role"];

  for (const name of important) {
    const val = el.getAttribute(name);
    if (val) attrs[name] = val;
  }
  return attrs;
}

// ──────────────── 이벤트 전송 ────────────────

function sendEvent(data) {
  if (!recording) return;

  chrome.runtime.sendMessage({
    type: "dom_event",
    data: {
      ...data,
      timestamp: Date.now(),
      url: window.location.href
    }
  }).catch(() => {});
}

// ──────────────── 이벤트 리스너 ────────────────

// 클릭
document.addEventListener("click", (e) => {
  const el = e.target;
  const pos = { x: Math.round(e.clientX), y: Math.round(e.clientY) };

  // Canvas인 경우: canvas 내부 상대 좌표도 저장
  let canvasPos = null;
  if (el.tagName === "CANVAS") {
    const rect = el.getBoundingClientRect();
    canvasPos = {
      // canvas 내부 상대 좌표 (0,0 = canvas 좌상단)
      relX: Math.round(e.clientX - rect.left),
      relY: Math.round(e.clientY - rect.top),
      // canvas 비율 좌표 (0~1, 크기 무관하게 동일 위치)
      ratioX: parseFloat(((e.clientX - rect.left) / rect.width).toFixed(4)),
      ratioY: parseFloat(((e.clientY - rect.top) / rect.height).toFixed(4)),
      // canvas 크기 (재현 시 참고)
      canvasWidth: Math.round(rect.width),
      canvasHeight: Math.round(rect.height)
    };
  }

  sendEvent({
    type: "click",
    selector: getSelector(el),
    xpath: getXPath(el),
    tagName: el.tagName,
    innerText: (el.innerText || "").substring(0, 100),
    attributes: getAttributes(el),
    position: pos,
    canvasPosition: canvasPos
  });
}, true);

// 입력 (debounce)
let inputTimer = null;
document.addEventListener("input", (e) => {
  const el = e.target;
  if (inputTimer) clearTimeout(inputTimer);

  inputTimer = setTimeout(() => {
    // 비밀번호 필드는 값 마스킹
    const isPassword = el.type === "password";
    sendEvent({
      type: "input",
      selector: getSelector(el),
      xpath: getXPath(el),
      tagName: el.tagName,
      value: isPassword ? "***" : (el.value || "").substring(0, 200),
      attributes: getAttributes(el)
    });
  }, 500);
}, true);

// 폼 제출
document.addEventListener("submit", (e) => {
  const el = e.target;
  sendEvent({
    type: "submit",
    selector: getSelector(el),
    xpath: getXPath(el),
    tagName: el.tagName,
    attributes: getAttributes(el)
  });
}, true);

// 페이지 이동 감지
let lastUrl = window.location.href;
const urlObserver = new MutationObserver(() => {
  if (window.location.href !== lastUrl) {
    const oldUrl = lastUrl;
    lastUrl = window.location.href;
    sendEvent({
      type: "navigate",
      selector: "",
      tagName: "",
      innerText: document.title,
      value: oldUrl
    });
  }
});

urlObserver.observe(document.documentElement, { childList: true, subtree: true });

// popstate (뒤로가기/앞으로가기)
window.addEventListener("popstate", () => {
  sendEvent({
    type: "navigate",
    selector: "",
    tagName: "",
    innerText: document.title
  });
});

console.log("[DOMRecorder] Content script loaded");
