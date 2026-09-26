# FanApp — 联想散热模式独立控制器

把联想电脑管家（Lenovo PC Manager）里唯一的实用功能——散热/风扇模式切换——剥离成一个 30KB 的原生 Windows 程序，然后卸掉管家的其余部分。

原生 C# WinForms，无 WebView、无 Python、无 Node，直调 WMI，单文件编译。

## 功能

- **性能模式**：安静 / 均衡 / 野兽 三档切换（`SetSmartFanMode`）
- **实时状态**：CPU 温度、双风扇转速、当前模式（2 秒轮询，后台线程，UI 不卡）
- **功能开关**（按机型支持情况自动置灰）：
  - 禁用 Win 键（游戏防误触）
  - 禁用触控板（外接鼠标时）
  - 键盘灯
  - G-Sync 自适应刷新率
  - 极速散热（FanCooling）
  - 水冷（带水冷模块的机型）
- 浅色 / 深色主题，进程内即时切换
- 点 X 缩到托盘，托盘右键真正退出

## 兼容性

WMI 接口来自 `root\WMI` 的 `LENOVO_GAMEZONE_DATA` 类（联想游戏zone驱动暴露）。

| 机型 | 预期效果 |
|------|----------|
| IdeaPad S340（开发机） | 模式切换/风扇/温度/Win键/触控板可用，其余置灰 |
| 拯救者系列（Legion） | 预计全部或大部分可用（含极速散热，部分型号水冷） |
| 其他联想游戏本 | 启动时自动探测，支持的亮、不支持的置灰 |

所有开关都有**写入后回读校验**：写进去 200ms 后读回真实状态，对不上自动置灰——UI 永远不会假装功能可用。

> 探测脚本在 [probe.ps1](probe.ps1) / [list_methods.ps1](list_methods.ps1)，可提权运行查看你机器的全部 48 个接口返回值。

## 编译

需要 Windows + .NET Framework 4（系统自带 csc.exe）：

```bat
cd app
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe ^
  /out:FanApp.exe /win32manifest:app.manifest /win32icon:fan.ico ^
  /r:System.dll /r:System.Drawing.dll /r:System.Management.dll ^
  /r:System.Windows.Forms.dll /r:System.Core.dll FanApp.cs
```

注意：**源文件必须带 UTF-8 BOM**，否则 csc 按 GBK 读源码，中文字符串全部乱码。

图标可自行重生成：`powershell -File gen_icon.ps1`（System.Drawing 画的蓝底白风扇，多尺寸）。

## 实现要点（踩坑记录）

- **UI 线程绝不碰 WMI**：所有 WMI 调用走 `ThreadPool` + `BeginInvoke` 回 UI，否则 2 秒一次的轮询会卡顿开关动画
- **DPI 感知**：`Main` 里 `SetProcessDPIAware()`，否则高分屏被系统拉伸发糊
- **提权**：`app.manifest` 声明 `requireAdministrator`（WMI 写接口需要），双击一次 UAC
- **主题原地切**：切换主题不重启进程，遍历控件重上色 + DWM 深色标题栏（`DwmSetWindowAttribute` attr 20/19）
- **窗口位置**：存注册表 `HKCU\SOFTWARE\FanApp`，切换主题/关闭时保存
- **提权 exe 无法右键固定到任务栏**（Windows 限制），需直接往 `User Pinned\TaskBar` 写 .lnk

## 协议

MIT
