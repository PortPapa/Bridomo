# Bridomo

**Browser traffic inspector & macro builder for Chrome**

HTTP/HTTPS 트래픽을 실시간으로 캡처하고, DOM 이벤트를 기록하여 자동화 매크로를 생성하는 데스크톱 앱입니다.

## Features

### Network Traffic Capture
- HTTP/HTTPS 프록시로 모든 브라우저 요청 실시간 캡처
- 요청/응답 헤더, 바디, 상태 코드, 응답 시간 상세 분석
- JSON 자동 포맷팅 및 정렬

### Smart Filtering
- 호스트/URL 텍스트 검색
- HTTP 메서드 필터 (GET, POST, PUT, DELETE, PATCH 등)
- 상태 코드 필터 (200, 404, 2xx, 5xx 등)
- 광고/추적 API 자동 숨김 (Google Analytics, Facebook, Hotjar 등)
- 사용자 정의 정크 URL 패턴 관리

### DOM Event Recording
- Chrome 확장 프로그램으로 클릭, 입력, 스크롤 등 사용자 인터랙션 기록
- CSS 선택자, XPath, 요소 위치, 텍스트 값 저장
- 타임스탬프 기반 API 호출 ↔ DOM 이벤트 자동 매칭

### Macro Export
- DOM 이벤트 + 매칭된 API 호출을 JSON으로 내보내기
- 기록된 사용자 행동을 자동화 시나리오로 변환
- Chrome 확장 프로그램으로 재생 가능한 매크로 생성

### Additional
- **HTTPS 인증서 관리** - 자체 서명 루트 CA 자동 생성/설치
- **원클릭 Chrome 실행** - 프록시 설정이 적용된 Chrome 인스턴스 바로 실행
- **다국어 지원** - 한국어, English
- **상하/좌우 레이아웃** - 작업 스타일에 맞게 전환
- **자동 업데이트** - 새 버전 출시 시 자동 다운로드 및 적용

## Installation

### Setup (Recommended)
[Releases](https://github.com/PortPapa/Bridomo/releases) 페이지에서 `Setup.exe`를 다운로드하여 실행하세요.
- 바탕화면 및 시작 메뉴에 바로가기 생성
- 자동 업데이트 지원
- Windows 설정에서 제거 가능

### Portable
Releases 페이지에서 `Portable.zip`을 다운로드하여 원하는 위치에 압축을 풀고 실행하세요.

## Quick Start

1. **Bridomo 실행** 후 `Run` 버튼 클릭
2. **Chrome** 버튼으로 프록시 적용된 브라우저 실행
3. 웹사이트 탐색 - 트래픽이 실시간으로 캡처됨
4. **Macro** 버튼으로 DOM 이벤트 + API 호출 내보내기

## Chrome Extension

DOM 이벤트 기록을 위해 내장 Chrome 확장 프로그램을 설치하세요:

1. Chrome에서 `chrome://extensions` 접속
2. **개발자 모드** 활성화
3. **압축해제된 확장 프로그램을 로드합니다** 클릭
4. 프로젝트 내 `Extension` 폴더 선택

## Tech Stack

- **Runtime**: .NET 8.0 (WPF)
- **Architecture**: MVVM + Dependency Injection
- **Proxy Engine**: [Titanium.Web.Proxy](https://github.com/justcoding121/titanium-web-proxy)
- **Installer & Updates**: [Velopack](https://velopack.io)
- **Extension**: Chrome Manifest V3

## Build from Source

```bash
# Clone
git clone https://github.com/PortPapa/Bridomo.git
cd Bridomo

# Build
dotnet build -c Release

# Publish (self-contained)
dotnet publish -c Release --self-contained -r win-x64 -o ./publish

# Package installer (requires vpk: dotnet tool install -g vpk)
vpk pack --packId LocalTrafficInspector --packVersion 1.0.0 --packDir ./publish --mainExe LocalTrafficInspector.exe --icon app_icon.ico --shortcuts Desktop,StartMenuRoot
```

## License

MIT
