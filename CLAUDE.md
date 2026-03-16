# ImeLocker

Windows 11 输入法锁定工具。根据前台窗口自动切换/记忆输入法状态，解决多应用切换时输入法混乱问题。

## 技术栈

- .NET 9.0 (`net9.0-windows`), C# latest, WPF + WinForms 混合
- P/Invoke 全部使用 `[LibraryImport]` source generator（非 `[DllImport]`），指针类型统一用 `nint`
- NuGet: YamlDotNet（配置）、Serilog + Serilog.Sinks.File（日志）
- `EnableWindowsTargeting=true` 支持 Linux 交叉编译
- Inno Setup 6（安装包打包，via `amake/innosetup` 容器）

## 编译

```bash
# Linux (podman 容器化交叉编译 + 打包安装程序)
./build.sh                     # 默认 Release win-x64，编译 + Inno Setup 打包
./build.sh Debug win-arm64     # 指定配置和运行时
```

**必须使用 `./build.sh`**，不要直接 `dotnet build`。

产出：
- `publish/` — 单文件自包含可执行文件及依赖 DLL
- `output/ImeLocker-Setup.exe` — Inno Setup 安装包

无测试项目。构建使用两个容器镜像：`mcr.microsoft.com/dotnet/sdk:9.0`（编译）和 `amake/innosetup`（打包安装程序）。

## 架构

```
SetWinEventHook(EVENT_SYSTEM_FOREGROUND)
        │
  WindowMonitor ──事件──▶ AppOrchestrator
                              │
                    ┌─────────┼─────────┐
                    ▼         ▼         ▼
              查 ConfigMgr  保存旧状态  更新托盘图标
                    │
              匹配分组 / 默认模式
                    │
            ┌───────┴───────┐
            ▼               ▼
        Preset 模式     Remember 模式
            │               │
       ImeController (选择适配器)
            │
      ┌─────┴──────┐
      ▼            ▼
  Standard    Wechat
  Adapter     Adapter
```

### 核心流程

1. `WindowMonitor` 通过 `SetWinEventHook` 监听 `EVENT_SYSTEM_FOREGROUND`，解析 HWND→PID/TID/进程名
2. `AppOrchestrator` 收到事件后：保存旧窗口 IME 状态（Remember 模式）→ 延迟 80ms → 查配置匹配分组 → 执行切换（带 3 次重试）
3. `ImeController` 根据进程名选择适配器：WeChat 进程用 `PostMessage WM_IME_CONTROL`，其余用标准 `ImmSetConversionStatus`
4. `SystemTrayApp` 响应 `WindowSwitched` 事件更新托盘图标（显示当前 IME 状态）和菜单

### 模块职责

| 目录 | 职责 |
|------|------|
| `Native/` | Windows API 的 P/Invoke 声明，纯静态类，internal 访问级别 |
| `Core/` | 窗口监控、IME 状态读写、适配器模式、编排逻辑 |
| `Config/` | YAML 配置模型、读写 + FileSystemWatcher 热重载、进程扫描 |
| `UI/` | WinForms 托盘（SystemTrayApp）+ WPF 配置窗口（MVVM）+ 动态图标生成（TrayIconGenerator） |

## 关键设计决策

- **适配器模式处理输入法差异**：`IImeAdapter` 接口，`WechatImeAdapter` 优先匹配（`CanHandle` 检查进程名），`StandardImeAdapter` 作为兜底（`CanHandle` 永远返回 true）
- **WPF + WinForms 共存**：WinForms 的 `NotifyIcon` 是唯一稳定的托盘方案，WPF 用于配置窗口的数据绑定。`Program.cs` 使用 `Application.Run(ApplicationContext)` 驱动消息循环
- **ConfigWindow 隐藏而非关闭**：`OnClosing` 中 `e.Cancel = true; Hide()`，避免重复创建 WPF 窗口
- **进程名不含 `.exe` 后缀**：`WindowMonitor` 用 `Path.GetFileNameWithoutExtension` 提取，配置文件中的 `processName` 也不带后缀（如 `Code` 而非 `Code.exe`）
- **Remember 模式记忆表**：`Dictionary<string, ImeState>` 按进程名存储，每 5 分钟清理已退出进程
- **托盘图标动态生成**：`TrayIconGenerator` 使用 GDI+ 运行时绘制，根据 `IME_CMODE_NATIVE` 标志区分中/英文状态，检测注册表 `SystemUsesLightTheme` 适配明暗主题，监听 `UserPreferenceChanged` 事件实时响应主题切换
- **应用图标**：`app.ico` 由 `generate_icon.py`（Pillow）生成，同时用于 exe 嵌入图标（`ApplicationIcon`）和 WPF 窗口图标

## 配置

运行时配置位于 `%APPDATA%/ImeLocker/config.yaml`，首次运行自动创建默认配置。日志位于 `%APPDATA%/ImeLocker/logs/`，按天滚动保留 7 天。

安装包由 Inno Setup 打包（`installer/ImeLocker.iss`），卸载时清理：安装目录、`%APPDATA%\ImeLocker\`、注册表 `HKCU\...\Run\ImeLocker`。

两种模式：
- **Preset**：切换到窗口时强制设置预设的键盘布局和转换模式
- **Remember**：记忆该应用上次离开时的 IME 状态，切回时恢复

## 编码规范

- 文件级命名空间（`namespace Foo;`）
- record 用于不可变数据（`WindowInfo`、`ImeState`）
- 日志统一用 `Serilog.Log.Logger` 静态访问
- Native 层常量集中在 `Constants.cs`，不散落在调用处
- YAML 枚举序列化使用 camelCase（`WithEnumNamingConvention(CamelCaseNamingConvention.Instance)`）
