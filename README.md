# Windows 媒体输出切换器（MVP）

托盘常驻、低空闲占用的 **WinUI 3 / Windows App SDK** 应用：全局热键弹出右下角 Liquid Glass 设备列表，一键切换默认播放设备。

> **重要：** 本仓库可在任意系统编写/打包源码，但 **必须在 Windows 上构建与运行**（Linux 无法编译 WinUI）。

## 功能（已锁定，不做功能膨胀）

| 功能 | 说明 |
|------|------|
| 全局热键 | 默认 `Win+Shift+V`，设置页可录制修改 |
| 设备弹窗 | 右下角、置顶、无边框 Liquid Glass；左键 / Enter 切换并关闭；失焦关闭 |
| 设置 | Glass 滑杆（强度/模糊/高光/透明度/圆角）+ 热键 + 行为 + **开机自启** |
| 托盘 | 左键弹出、右键菜单（切换 / 设置 / 退出） |
| 持久化 | `%LocalAppData%\WindowsMediaSwitcher\settings.json` |

## 环境要求（Windows）

1. **Visual Studio 2022**（17.8+ 推荐）工作负载：
   - 「使用 C++ 的桌面开发」中与 Windows SDK 相关组件，或
   - **「Windows 应用程序开发」**（WinUI）
2. **.NET 8 SDK**
3. **Windows 10 1809+** / Windows 11（开发与运行）
4. Windows App SDK 运行时（本项目默认 **自包含** `WindowsAppSDKSelfContained=true`，发布后一般无需单独安装）

## 本地运行（unpackaged，推荐）

```powershell
cd windows-media-switcher
dotnet restore
dotnet build src\WindowsMediaSwitcher\WindowsMediaSwitcher.csproj -c Debug -r win-x64
dotnet run --project src\WindowsMediaSwitcher\WindowsMediaSwitcher.csproj -c Debug -r win-x64
```

`WindowsPackageType=None`：无需 MSIX 签名即可直接跑。若改用打包（MSIX），需额外配置 `Package.appxmanifest` 与证书。

## 发布

```powershell
# 在仓库根目录
.\scripts\publish-win-x64.ps1
```

产物：

- `artifacts\publish\win-x64\` — 可执行目录  
- `dist\WindowsMediaSwitcher-win-x64.zip` — 压缩包  

等价手动命令：

```powershell
dotnet publish src\WindowsMediaSwitcher\WindowsMediaSwitcher.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false `
  -p:WindowsPackageType=None `
  -p:WindowsAppSDKSelfContained=true `
  -o artifacts\publish\win-x64
```

## 自测清单

1. 启动后任务栏托盘出现图标，主窗口不可见。  
2. 按 `Win+Shift+V` → 右下角弹出设备列表。  
3. 点击非当前设备 → 系统默认播放设备切换成功 → 弹窗关闭。  
4. 再次打开弹窗，焦点移走 → 弹窗关闭（若开启「失焦关闭」）。  
5. 设置中修改热键并保存 → 新热键生效，旧热键失效。  
6. 调整 Glass 滑杆 → 预览即时更新；重启后设置仍在。  
7. 勾选「开机时启动」→ 注销/登录后托盘自动出现（`HKCU\...\Run`）。  
8. 托盘右键 → 退出 → 进程结束，热键注销。

## 已知限制

- **IPolicyConfig** 为未文档化 COM 接口，用于设置默认播放设备；Windows 大版本升级存在理论破坏风险。无需管理员权限即可切换当前用户默认设备；企业组策略可能禁止更改。  
- 热键若与其它软件冲突，`RegisterHotKey` 失败，托盘仍可用。  
- Liquid Glass 为 Acrylic/Mica + 自定义暗色烟熏层与顶边 1px 高光的近似，非 Apple 私有 API。  
- 空闲时无后台轮询：仅托盘消息泵 + OS 热键回调。

## 目录结构

```
windows-media-switcher/
  WindowsMediaSwitcher.sln
  src/WindowsMediaSwitcher/   # WinUI 工程
  scripts/publish-win-x64.ps1
  README.md
  DELIVERABLE.md
```

## License

可选；本 MVP 交付物未附带 LICENSE。
