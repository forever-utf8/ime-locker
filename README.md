# ImeLocker

Windows 11 输入法锁定工具。自动根据前台应用切换或记忆输入法状态，告别窗口切换后输入法混乱的烦恼。

## 功能

- **预设模式 (Preset)**：切换到指定应用时，自动设置为预设的键盘布局和输入模式（如编程工具自动切英文）
- **记忆模式 (Remember)**：记住每个应用上次离开时的输入法状态，切回时自动恢复（如聊天工具保持中文）
- **分组管理**：将应用按使用场景分组，每组独立配置切换策略
- **系统托盘**：右键快速查看/切换当前应用所属分组，双击打开配置窗口
- **热重载**：手动编辑 YAML 配置文件后即时生效，无需重启
- **微信输入法兼容**：针对微信输入法的非标准行为提供专用适配器
- **开机自启**：一键设置，开机后自动在后台运行

## 安装

### 从源码编译

需要 [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)。

```bash
git clone https://github.com/forever-utf8/ime-locker.git
cd ime-locker
dotnet publish src/ImeLocker/ImeLocker.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

输出文件位于 `src/ImeLocker/bin/Release/net9.0-windows/win-x64/publish/ImeLocker.exe`。

### Linux 容器化交叉编译

需要 [Podman](https://podman.io/)。

```bash
./build.sh                     # 默认 Release win-x64
./build.sh Release win-arm64   # ARM64 目标
```

输出到 `publish/` 目录。

## 使用

1. 运行 `ImeLocker.exe`，托盘区出现图标
2. **右键托盘图标**：查看分组列表，点击可将当前应用移入指定分组
3. **双击托盘图标**：打开配置窗口，管理分组和应用
4. 首次运行自动创建默认配置，编程工具（VS Code、Windows Terminal、IntelliJ IDEA）预设为英文输入

## 配置

配置文件位于 `%APPDATA%\ImeLocker\config.yaml`，支持热重载。

```yaml
version: 1
defaultMode: preset

presetIme:
  keyboardLayout: "0x08040804"  # 微软拼音
  conversionMode: native        # 中文模式

groups:
  - name: "编程工具"
    mode: preset
    preset:
      keyboardLayout: "0x04090409"  # English (US)
      conversionMode: alphanumeric
    apps:
      - processName: "Code"
      - processName: "rider64"
      - processName: "WindowsTerminal"

  - name: "聊天工具"
    mode: remember
    apps:
      - processName: "WeChat"
      - processName: "Telegram"
```

### 配置说明

| 字段 | 说明 |
|------|------|
| `defaultMode` | 未匹配任何分组时的默认策略：`preset`（使用预设）或 `remember`（记忆状态） |
| `presetIme` | 默认预设的键盘布局和转换模式 |
| `groups[].mode` | 分组策略：`preset` 或 `remember` |
| `groups[].preset` | 仅 `preset` 模式需要，指定键盘布局和转换模式 |
| `groups[].apps[].processName` | 进程名，不含 `.exe` 后缀 |

### 常用键盘布局

| 布局 | 值 |
|------|------|
| English (US) | `0x04090409` |
| 微软拼音 | `0x08040804` |

## 日志

位于 `%APPDATA%\ImeLocker\logs\`，按天滚动，保留 7 天。排查输入法切换问题时可查看。

## 技术栈

- .NET 9.0 (C#)、WPF + WinForms
- Windows API: `SetWinEventHook`、`ImmSetConversionStatus`、`ActivateKeyboardLayout`
- YamlDotNet、Serilog

## License

MIT
