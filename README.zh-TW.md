# ElectroScroll

[English](README.md)

ElectroScroll 是一個實驗性的 Windows 滾輪工具，用來替一般有段落感的滾輪加入「依速度觸發的加速與慣性尾巴」。它的目標是模擬類似 Logitech MX Master 系列高速 free-spin 滾輪的效率，但保留慢速單格滾動的精準感。

核心設計很單純：慢慢滾或只滾一格時維持原生手感；快速撥動滾輪時，才轉成更遠、更平滑、會自然衰退的慣性滑動。

## 目前狀態

ElectroScroll 仍是 prototype。它會安裝 user-level low-level mouse hook，偵測滾輪速度，並對目標視窗送出合成的 `WM_MOUSEWHEEL` 訊息。它不使用 `SendInput`。

調參時請保守一點測試。不同應用程式對合成滾輪訊息的支援程度不完全相同。

## 功能

- 低速精準：慢速滾輪輸入會原樣放行。
- 速度觸發：只有滾輪速度超過門檻時才由 ElectroScroll 接管。
- 飛輪慣性：快速撥動會轉成平滑、逐漸衰退的輸出封包。
- 預設模式：`Precise`、`Balanced`、`Free-spin`。
- 應用程式設定檔：內建瀏覽器與 Codex/ChatGPT 類桌面 WebView app profile，也可透過 `settings.json` 設定更多 profile。
- 反向急停：慣性滑動時往反方向滾動，會立刻取消原本的慣性。
- 修飾鍵放行：Ctrl/Shift/Alt/Win 搭配滾輪時維持原生行為。
- 全螢幕與遊戲放行：預設會放行已知遊戲程序與全螢幕視窗。
- 多螢幕處理：副螢幕上可改送到 root window，避免 child window 與 DPI 對位問題。
- 效能模式：要求 1 ms timer resolution、在支援時停用 EcoQoS execution throttling，並使用低延遲 GC。
- 雙語 UI：英文與繁體中文。
- 可選監測線圖：input/output 線圖預設關閉，只有啟用時才取樣與繪圖。
- 系統匣支援與單一實例啟動。

## 需求

- Windows 10 或 Windows 11。
- 從原始碼建置需要 .NET 9 SDK。
- 執行 framework-dependent build 需要 .NET 9 Desktop Runtime。

## 建置

在 repository 根目錄執行：

```powershell
dotnet build .\ElectroScroll.csproj -c Release -o .\bin\Release
```

執行：

```powershell
.\bin\Release\ElectroScroll.exe
```

只開 UI、不安裝全域 mouse hook 的安全模式：

```powershell
.\bin\Release\ElectroScroll.exe --no-hook
```

## 發布包

若要產生可放到 GitHub Releases 的 Windows x64 self-contained zip：

```powershell
.\scripts\package-release.ps1 -Version 0.1.0
```

輸出位置：

```text
artifacts\ElectroScroll-0.1.0-win-x64.zip
```

zip 內容包含 single-file executable、`README.md`、`README.zh-TW.md` 與
`LICENSE`。

## 使用方式

1. 啟動 `ElectroScroll.exe`。
2. 把滑鼠移到想滾動的應用程式上。
3. 先試 `Precise` 預設。
4. 如果想要更明顯的慣性尾巴，再試 `Balanced` 或 `Free-spin`。
5. 調整後按 `Save` 儲存。

狀態卡會顯示目前速度、boost、慣性速度、目標程序、目標視窗，以及 ElectroScroll 目前是 `Native`、`Intercepting`，還是 `Bypassed`。

## 設定檔

設定會儲存在：

```text
%APPDATA%\ElectroScroll\settings.json
```

目前 UI 只會編輯 default tuning profile。進階使用者可以直接修改 `settings.json`，調整特定應用程式 profile 或已知遊戲程序名稱。手動修改前建議先關閉 ElectroScroll。

## 診斷紀錄

`記錄` 勾選框會啟用低成本的檔案診斷，預設關閉。啟用後，ElectroScroll
會記錄滾輪輸入、目標視窗解析、bypass 原因、profile 決策、輸出封包，以及
`PostMessage` 結果：

```text
%APPDATA%\ElectroScroll\logs\electroscroll.log
```

log 會由背景 timer 批次寫入，達到設定大小後會輪替成 `electroscroll.1.log`。

## 參數調整指南

- `Step`：每一格滾輪的基礎距離。調高後，即使還沒進入慣性也會滾得更遠。
- `Threshold`：進入接管模式所需的滾輪速度。如果太容易觸發慣性，請調高。
- `Acceleration`：超過門檻後，額外速度轉成 boost 的力道。
- `Max boost`：加速倍率上限。
- `Impulse time`：滾輪爆發轉成慣性速度的時間。越低越立即。
- `Friction`：慣性衰退所需時間。
- `Flywheel`：快速撥動後額外保留的尾巴。越高越接近 free-spin。
- `Direct share`：每次快速滾動有多少會立即位移，而不是轉成慣性。
- `Smoothness`：慣性會被拆成多少較小的滾輪封包。舊程式如果不吃小封包，可降到 `1`。

## 效能與安全

ElectroScroll 盡量讓 hook path 保持很小：

- root window 的 process/title 查詢會依 HWND 快取；
- 有動作時 physics 使用 4 ms timer；
- UI metrics 會節流；
- 診斷線圖與檔案紀錄預設關閉；
- 其他軟體注入的 low-level wheel event 會被 hook 放行；
- 產生的輸出只使用 `PostMessage`。

如果滾動行為不對，可以取消勾選 `Enabled`、從系統匣結束程式，或用 `--no-hook` 啟動。

## 已知限制

- 目前沒有 installer。
- 雖然已有 per-app profile matching，UI 目前只編輯 default profile。
- 有些應用程式對合成 `WM_MOUSEWHEEL` 訊息支援不佳。
- 權限較高的系統管理員視窗可能需要 ElectroScroll 以相同權限執行。
- 尚未實作 Raw Input/HID per-device 處理。
- 高刷新輸出使用 timer，不是 display vsync。
- 遊戲放行屬於 best-effort，玩遊戲時建議保持 bypass 啟用。

## 非官方聲明

ElectroScroll 與 Logitech、Microsoft、OpenAI，或此 repository 提到的任何滑鼠、應用程式廠商皆無關聯。

## 貢獻

歡迎回報 issue 或提供實驗結果。調整滾動行為時，請盡量附上：

- 目標應用程式與螢幕配置；
- 當時使用的 preset/profile；
- app 顯示的是 `Native`、`Intercepting` 還是 `Bypassed`；
- 哪一種滾輪動作讓手感不對。
- 如果重現問題時有啟用 `記錄`，請附上 diagnostic log。

## 授權

本專案使用 MIT License 授權。詳見 [LICENSE](LICENSE)。
