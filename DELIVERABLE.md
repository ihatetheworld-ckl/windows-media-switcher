# DELIVERABLE — Windows 媒体输出切换器 MVP

## 路径

| 项 | 绝对路径 |
|----|----------|
| 工程目录 | `/workspace/windows-media-switcher` |
| 解决方案 | `/workspace/windows-media-switcher/WindowsMediaSwitcher.sln` |
| 项目 | `/workspace/windows-media-switcher/src/WindowsMediaSwitcher/WindowsMediaSwitcher.csproj` |
| 源码 zip | `/workspace/windows-media-switcher-src.zip` |
| 发布脚本 | `/workspace/windows-media-switcher/scripts/publish-win-x64.ps1` |
| 本文档 | `/workspace/windows-media-switcher/DELIVERABLE.md` |

## 构建说明（必须在 Windows）

本 Linux 交割机 **不能** 编译/运行 WinUI 3。请在 Windows 上：

```powershell
cd <解压后的 windows-media-switcher>
.\scripts\publish-win-x64.ps1
```

或：

```powershell
dotnet publish src\WindowsMediaSwitcher\WindowsMediaSwitcher.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false `
  -p:WindowsPackageType=None `
  -p:WindowsAppSDKSelfContained=true `
  -o artifacts\publish\win-x64
```

发布 zip（脚本生成）：`dist\WindowsMediaSwitcher-win-x64.zip`

## MVP 自测清单

- [ ] 热键（默认 Win+Shift+V）弹出右下角设备列表  
- [ ] 左键 / Enter 选择设备 → 默认播放设备切换成功 → 弹窗关闭  
- [ ] 失焦关闭（设置可关）  
- [ ] 设置：Glass 滑杆 + 热键录制 + 行为开关，持久化到 `%LocalAppData%\WindowsMediaSwitcher\settings.json`  
- [ ] 开机自启开关（HKCU Run）→ 登录后托盘常驻  
- [ ] 空闲无多余后台服务；退出时注销热键  

## 实现要点

- **Unpackaged** WinAppSDK（`WindowsPackageType=None`）  
- 热键：`RegisterHotKey` / `UnregisterHotKey`，设置变更时重注册  
- 音频：`IMMDeviceEnumerator` 枚举 eRender；`IPolicyConfig`（未文档化）设默认设备  
- 弹窗：WinUI Window + ToolWindow/Topmost + DisplayArea 工作区右下角  
- 托盘：`H.NotifyIcon.WinUI`  
- 自启：`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`  

## 已知限制

1. **PolicyConfig**：未文档化 COM；跨大版本 Windows 有兼容风险；通常 **无需提升权限**，但组策略可拦截。  
2. 热键冲突时注册失败（托盘仍可用）。  
3. Glass 为 Acrylic + 自定义暗色烟熏/高光近似，非系统原生「液态玻璃」API。  
4. 本交割环境未执行 `dotnet build`（Linux 无 WinUI 工具链）— 源码按 WinUI 3 / net8.0-windows 约定编写，需在 Windows 验证编译。
