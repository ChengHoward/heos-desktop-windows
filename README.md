<div align="center">

<img src="Assets/182.png" alt="HEOS Desktop" width="320" />

# HEOS Desktop（Windows）

适用于 Windows 的 **HEOS** 局域网控制桌面客户端（非官方）。  
基于 **WPF** 与 **WPF-UI（Fluent）**，通过局域网与兼容设备通信。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)](https://www.microsoft.com/windows)

</div>

---

## 简介

HEOS Desktop 用于在 **与音箱/功放同一局域网** 的前提下，发现 HEOS 设备、管理已保存设备、查看与控制当前播放等（具体能力随版本迭代）。应用通过 **TCP（默认端口 1255）** 与设备交换 JSON 指令，并支持 **SSDP** 等方式辅助发现设备。

本项目 **不是** Denon、Marantz、Sound United 或其关联公司的官方软件，亦不受其技术支持。

---

## 运行要求

### 使用预编译包时

| 包类型 | 适用系统 | 本机还需安装 |
|--------|-----------|----------------|
| **`*-win-x64.zip`** / **`*-win-x64-single.zip`** | 64 位 Windows（常见台式机、笔记本） | 一般 **无需** 安装 .NET（自包含） |
| **`*-win-arm64.zip`** / **`*-win-arm64-single.zip`** | ARM64 Windows（如部分 Surface 等） | 一般 **无需** 安装 .NET（自包含） |
| **`*-win-x64-fd.zip`** | 64 位 Windows | 需安装 **[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)**（选择 Windows x64） |
| **`*-win-arm64-fd.zip`** | ARM64 Windows | 需安装 **.NET 8 Desktop Runtime**（选择 Windows Arm64） |

- **单文件包**（`*-single`）：主程序多为单个可执行文件，首次启动可能略慢（解压运行时）。  
- **框架依赖包**（`*-fd`）：体积更小，但必须安装与构建目标一致的 **Desktop** 运行时（非仅 ASP.NET / 通用运行时）。

### 从源码开发与调试时

- **Windows 10/11**（WPF 桌面）
- **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**（建议与项目 `TargetFramework` 一致）
- **Visual Studio 2022** 或 **VS Code + C# 扩展**（可选）

---

## 下载

1. 打开本仓库 GitHub 页面顶栏或右侧的 **「Releases」**。  
2. 选择对应版本，按你的 CPU 架构与是否需要自带运行时选择压缩包（见上表）。  
3. 解压后运行 **`HeosWpf.exe`**（单文件包同样运行该名称的可执行文件）。

> **提示**：推送以 `v` 开头的标签（如 `v1.0.0`）时，本仓库的 GitHub Actions 会构建并上传多种形态的压缩包到同一 Release。

---

## 从源码运行

```bash
git clone <本仓库地址>
cd heos-desktop-windows
dotnet restore
dotnet run --project src/HeosWpf/HeosWpf.csproj
```

发布本地版本示例（自包含 x64）：

```bash
dotnet publish src/HeosWpf/HeosWpf.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

---

## 功能概览（当前方向）

- 局域网 **设备发现 / 手动 IP** 添加与已保存设备管理  
- **连接状态** 与断线后的重连策略（可在设置中调整）  
- **正在播放** 信息展示与控制（随版本扩展）  
- **Fluent 风格** 导航与设置页  

具体功能以当前版本界面与行为为准。

---

## 免责声明

1. **非官方产品**：本软件由社区或个人独立开发，与 Denon、Marantz、Bowers & Wilkins、Sound United 及其关联公司 **无任何隶属、赞助或认可关系**。名称 **HEOS** 为相应权利方商标，仅用于描述兼容性与使用场景。  
2. **协议与固件**：HEOS 设备行为以厂商文档与固件为准；不同型号、固件版本下指令与能力可能不一致，**不保证**全部功能在所有设备上可用。  
3. **网络与安全**：请在 **可信局域网** 内使用；开放端口、防火墙与路由配置由用户自行负责，开发者不对因网络配置或第三方攻击导致的损失承担责任。  
4. **按现状提供**：软件在适用法律允许范围内 **按「现状」提供**，不作任何明示或默示担保（包括但不限于适销性、特定用途适用性、不侵权）。使用本软件的风险由用户自行承担。  
5. **责任限制**：在法律允许的最大范围内，开发者对因使用或无法使用本软件而产生的任何直接、间接、偶然或后果性损害 **不承担责任**。  

若你不同意上述条款，请勿下载或使用本软件。

---

## 相关链接

- [.NET 下载](https://dotnet.microsoft.com/download)  
- [WPF-UI](https://github.com/lepoco/wpfui)  
- HEOS 控制协议说明可参考厂商公开文档（如 HEOS CLI Protocol Specification）及 Denon 支持站点上的 **HEOS Control Protocol (CLI)** 说明（版本与链接以官方为准）。

---

## 许可证

若仓库根目录包含 `LICENSE` 文件，以该文件为准；若尚未添加，请在分发前自行选择并补充合适的开源或专有许可证。
