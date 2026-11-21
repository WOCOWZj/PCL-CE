# PCL CE（去区域限制分支）

本仓库是 [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE) 的个人 fork。
在官方代码基础上做了**一处功能性改动：移除「国家和地区」对 Minecraft 下载相关功能的限制**，其余功能、界面与上游保持一致。

- 分支：`dev`
- 相对上游的改动提交：`912b52a feat(region): 关闭中国大陆区域下载禁令`
- 语言：简体中文（`README-EN.md` / `README-ZH_TW.md` 仍是上游原文，未反映本分支改动）

---

## 项目是什么

PCL CE（Plain Craft Launcher · Community Edition）是一个 Minecraft 启动器，由社区基于 PCL 的开源代码二次开发。
本项目使用 **Avalonia + .NET 10** 重写界面层，因此同时支持 Windows、Linux 与 macOS，提供版本安装、文件补全、整合包安装、Mod/资源管理、账号登录、实例启动等完整功能。

上游地址：<https://github.com/PCL-Community/PCL-CE>

---

## 这个分支做了什么

### 背景

上游代码里内置了一套「区域限制」逻辑：当同时满足下面两个条件时，判定为受限地区，禁用 Minecraft 的下载、补全与整合包安装等功能，并弹出提示：

> Minecraft 下载、补全与整合包安装功能在你所在的国家或地区不可用。

1. 系统时区属于中国大陆（`China Standard Time` / `Asia/Shanghai` / `PRC`）
2. 系统「国家或地区」设置为中国大陆（`CN` 或 `156`）

### 改动内容

| 文件 | 改动前 | 改动后 |
| --- | --- | --- |
| `PCL.Core/App/Region/RegionPolicy.cs` | `Evaluate()` 命中「中国时区 + 中国地区」时返回 `Mainland`，`IsRestricted` 为 `true` | 恒定返回 `Other`，任何时区与地区组合都不再受限 |
| `PCL.Launcher.Core/Ui/LogFilter.cs` | 区域判定服务缺失或抛异常时**按受限处理**（fail-closed），等于隐藏的第二道锁 | 改为**放行**（fail-open），避免误拦截下载 |
| `PCL.Core.Test/RegionPolicyTest.cs` | 用例断言「中国时区 + 中国地区」应受限 | 用例改为断言任何组合都不受限 |

被这套开关拦截的功能入口共 9 处：`minecraft-install`、`minecraft-client-save`、`minecraft-loader-install`、`minecraft-repair`、`minecraft-reset`、`minecraft-launch-missing-files`、`modpack-install`、`online-modpack-install`、`auto-modpack-install`。改动后它们全部放行。

### 效果

- 在任何时区、任何「国家或地区」设置下，Minecraft 下载、文件补全、整合包安装、加载器安装、实例修复/重置都可以正常使用；
- 即使区域判定服务异常，也不会再把功能误判为受限；
- 界面层不再出现上述「不可用」提示（因为永远不会判定为受限）。

---

## 构建

> ⚠️ 本仓库需要 **.NET SDK 11.0.x**。`csproj` 中 `LangVersion` 为 `15.0`，且 `PCL.Core.SourceGenerators` 引用 Roslyn 5.12；
> 用 .NET 10 的 SDK 构建会报 `CS1617`（langversion 15.0 无效）与 `CS9057`（生成器编译器版本过新被禁用），导致 `partial` 成员缺少实现。

安装预览版 SDK（不需要管理员权限，装到任意 NTFS 目录）：

```powershell
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile "$env:TEMP\dotnet-install.ps1"
& "$env:TEMP\dotnet-install.ps1" -Channel 11.0 -Quality preview -InstallDir "$env:LOCALAPPDATA\dotnet-sdk11" -NoPath
```

编译与发布（Windows x64）：

```powershell
dotnet build "PCL Community Edition.slnx" -c Release

dotnet publish PCL.Launcher/PCL.Launcher.csproj `
  -c Release -r win-x64 -p:Platform=x64 `
  --no-self-contained -o dist/win-x64

Compress-Archive -Path 'dist/win-x64/*' -DestinationPath 'dist/PCL2_CE_Release_win-x64.zip' -Force
```

Linux / macOS 的打包流程见 `scripts/publish.sh`、`scripts/package-linux.sh`、`scripts/package-macos.sh`。

注意事项：

- **不要把 SDK 或仓库放在 exFAT 分区**。exFAT 不记录文件所有者（`git` 会报 `dubious ownership`），且 SDK 包内的部分文件无法解压，需要放在 NTFS / APFS / ext4 上。
- 首次构建需要访问 NuGet（HTTP 代理环境下请确认能建立 HTTPS 连接，否则 `restore` 会报 `NU1301`）。

---

## 运行

发布产物为框架依赖（`--no-self-contained`），运行需要 **.NET 10 Runtime**：

- Windows：<https://get.dot.net/10>
- Linux / macOS：同上，另需 X11 或 Wayland、macOS 12+

支持的系统版本与上游一致：

| 平台 | 版本 | 架构 |
| --- | --- | --- |
| Windows | Windows 10 1809 (17763) 及以上、Windows 11 | x64、ARM64 |
| Linux | Ubuntu 22.04+、Fedora 43+、Arch 等 glibc 桌面发行版 | x64、ARM64 |
| macOS | macOS 12 及以上 | Apple Silicon、Intel |

---

## 与上游同步

```powershell
git remote add upstream https://github.com/PCL-Community/PCL-CE.git
git fetch upstream
git merge upstream/dev
```

`PCL.Core/App/Region/RegionPolicy.cs` 是本分支唯一的「有意分歧」文件。上游后续若重写该文件，合并时保留「任何时区与地区都不受限」的语义即可；`LogFilter.cs` 与测试文件同理。

## 自动跟进上游

仓库内置 `.github/workflows/sync-upstream.yml`，每周五 18:00 UTC（周六 02:00 UTC+8）自动执行：

1. 拉取上游 `PCL-Community/PCL-CE` 的 `dev`，合并进本仓库 `dev` 并推送；
2. 在上游最新提交上重新应用 `patches/0001-disable-region-restriction.patch`；
3. 用 .NET 11 预览版 SDK 构建 win-x64，创建**草稿** release（tag 形如 `v2.15.1-wocowzj.<上游短哈希>`）。

- 合并冲突或补丁无法应用时，工作流会失败并自动开 issue 提醒手动处理；
- 也可以在 Actions 页面手动触发（`workflow_dispatch`）；
- 本分支的改动集中在 `patches/`，更新方法见 `patches/README.md`。

首次启用：fork 的定时工作流需要在 Actions 页面点一次 "Enable workflow"。

---

## 许可证

沿用上游的分许可结构，本分支未做改变：

- `PCL.Launcher/` 与 `PCL.Launcher.Core/` 使用 [自定义许可证](PCL.Launcher.Core/LICENCE)
- 其余所有目录使用 [Apache License 2.0](LICENSE)

使用、分发或再修改本项目时请遵守上述许可证条款。

---

## 免责声明

- 本仓库是个人学习与技术研究用途的 fork，**不是**官方发行渠道，不代表上游立场；
- 与 PCL CE 官方、Mojang / Microsoft 均无关联；
- 因使用本分支构建的版本产生的任何问题，请勿向上游仓库反馈；
- Minecraft 是 Mojang Studios 的商标，使用本启动器仍需持有正版游戏账号。

---

## 致谢

感谢 [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE) 的开发者与贡献者，以及上游 PCL 项目的原作者。
