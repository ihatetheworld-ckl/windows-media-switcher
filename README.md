# Windows 媒体输出切换器

托盘常驻、低空闲占用的 **WinUI 3 / Windows App SDK** 应用：全局热键弹出右下角 Liquid Glass 设备列表，一键切换默认播放设备。支持从 GitHub Releases 自动更新。

> **重要：** 本仓库可在任意系统编写/打包源码，但 **必须在 Windows 上构建与运行**（Linux 无法编译 WinUI）。

## 功能

| 功能 | 说明 |
|------|------|
| 全局热键 | 默认 **`Win+Ctrl+V`**（设置页可录制修改）。`RegisterHotKey` 失败或需拦截系统组合时，用 `WH_KEYBOARD_LL` 兜底并吞键 |
| 设备弹窗 | 右下角、置顶、无边框 Liquid Glass（暗色半透明 + 强模糊 + 仅顶边 1px 高光，圆角 ~36）；左键 / Enter 切换；失焦关闭 |
| 设置 | Glass 滑杆 + 热键 + 行为 + 开机自启 + **检查更新** |
| 托盘 | 左键弹出、右键菜单（切换 / 设置 / 退出） |
| 自动更新 | 读取 GitHub Releases latest，下载 `WindowsMediaSwitcher-win-x64.zip` 并就地替换后重启 |
| 持久化 | `%LocalAppData%\WindowsMediaSwitcher\settings.json` |

## 环境要求（Windows）

1. **Visual Studio 2022**（17.8+ 推荐）工作负载「Windows 应用程序开发」
2. **.NET 8 SDK**
3. **Windows 10 1809+** / Windows 11
4. 默认自包含 `WindowsAppSDKSelfContained=true`

## 本地运行（unpackaged）

```powershell
cd windows-media-switcher
dotnet restore
dotnet build src\WindowsMediaSwitcher\WindowsMediaSwitcher.csproj -c Debug -r win-x64
dotnet run --project src\WindowsMediaSwitcher\WindowsMediaSwitcher.csproj -c Debug -r win-x64
```

## 发布

```powershell
.\scripts\publish-win-x64.ps1
```

产物：`dist\WindowsMediaSwitcher-win-x64.zip`。推送 `v*` 标签会触发 Actions 发布 Release。

## 自测清单

1. 启动后托盘图标出现，主窗口不可见。  
2. 按 `Win+Ctrl+V` → 右下角弹出设备列表（长设备名完整或带省略号 + 悬停提示；列表可滚动）。  
3. 弹窗外框无整圈白色矩形边，仅顶边细高光。  
4. 托盘右键 → 设置 → 设置窗口正常打开。  
5. 设置中「检查更新」可查询 GitHub Releases。  
6. 修改热键后新组合生效。  
7. 退出后热键注销、进程结束。

## 已知限制

- **IPolicyConfig** 为未文档化 COM 接口，用于设置默认播放设备。  
- `RegisterHotKey` **无法抢走** 系统已占用的组合（如 `Win+V` 剪贴板）。本应用对配置热键额外安装低级键盘钩子并吞键；部分受保护快捷键仍可能无法拦截。  
- Liquid Glass 为 Acrylic + 自定义暗色烟熏层与顶边 1px 高光的近似。  
- 自动更新需可访问 `api.github.com`；更新时会短暂退出进程并由 `apply-update.cmd` 复制文件后重启。  
- Win10 上 DWM 圆角/去边框能力弱于 Win11，边框抑制效果因系统版本而异。

## License

可选；本 MVP 交付物未附带 LICENSE。
