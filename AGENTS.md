# AGENTS.md
本文件面向在本仓库中工作的 agentic coding agents。
目标是让代理先遵守仓库现状，再做最小正确修改。

## 仓库概况
- 仓库根目录：`D:\WorkSpace\Work\QualityCheckApp.Engine`
- 解决方案：`QualityCheckApp.Engine.sln`，当前只保留 `QualityCheckApp.Engine` 作为主线项目入口。
- 主程序：`QualityCheckApp.Engine/`，WPF + ArcGIS Engine。
- 仓库当前按单项目维护，不再保留独立的集成演示项目或旧 WinForms 地图壳工程。
- 目标框架：`.NET Framework 4.5`
- 编译平台：`x86`，不要擅自改成 `AnyCPU`。
- 语言版本：`C# 5`，不要使用更高版本语法。
- 依赖不靠 NuGet 恢复；ArcGIS 依赖来自本机安装。

## 外部规则文件状态
- 当前仓库中没有其他可复用的 `AGENTS.md`。
- 当前仓库中没有 `.cursorrules`。
- 当前仓库中没有 `.cursor/rules/` 目录。
- 当前仓库中没有 `.github/copilot-instructions.md`。
- 因此没有额外 Cursor/Copilot 规则需要继承。

## 环境前提
- 推荐在 Windows 上操作。
- 推荐从仓库根目录执行命令。
- 推荐使用 Developer Command Prompt；若 `msbuild` 不在 `PATH`，直接调用完整路径。
- 已验证可用的 MSBuild 路径：`C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe`
- 项目默认 ArcGIS SDK 路径：`C:\Program Files (x86)\ArcGIS\DeveloperKit10.2\DotNet`
- 当前主线 `.csproj` 把 `WarningLevel` 设为 `4`。
- 当前仓库没有 `packages.config`、`Directory.Build.props`、`.editorconfig`、Roslyn analyzer 配置。

## 构建命令
以下命令默认在仓库根目录运行。

```powershell
# 构建整个解决方案（推荐）
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' 'QualityCheckApp.Engine.sln' /t:Build /p:Configuration=Debug /p:Platform=x86 /verbosity:minimal
# 重新构建整个解决方案
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' 'QualityCheckApp.Engine.sln' /t:Rebuild /p:Configuration=Debug /p:Platform=x86 /verbosity:minimal
# 清理输出
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' 'QualityCheckApp.Engine.sln' /t:Clean /p:Configuration=Debug /p:Platform=x86 /verbosity:minimal
# 构建 Release
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' 'QualityCheckApp.Engine.sln' /t:Build /p:Configuration=Release /p:Platform=x86 /verbosity:minimal
# 仅构建主应用
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' 'QualityCheckApp.Engine\QualityCheckApp.Engine.csproj' /t:Build /p:Configuration=Debug /p:Platform=x86
# ArcGIS SDK 不在默认路径时
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' 'QualityCheckApp.Engine.sln' /t:Build /p:Configuration=Debug /p:Platform=x86 /p:ArcGisSdkPath='C:\custom\ArcGIS\DeveloperKit10.2\DotNet'
```

## Lint / Test 现状
- 没有独立 lint 命令。
- 没有 `StyleCop`、`dotnet format`、规则集或 analyzer 包配置。
- 当前最接近“lint”的检查就是完整编译并查看 Warning/Error。
- 当前仓库没有自动化测试项目。
- 没有 `MSTest`、`xUnit`、`NUnit`、`Microsoft.NET.Test.Sdk` 引用。
- 因此没有 `dotnet test`、`vstest.console`、`mstest` 可直接运行的测试入口。

## 单个测试如何运行
- 目前做不到，因为仓库里根本没有自动化测试用例。
- 如果用户要求“跑一个测试”，先明确说明该仓库当前没有单测基础设施。
- 更准确的说法是“手工 smoke check”或“手工集成验证”。

## 手工验证建议
- 先执行 Debug/x86 全量构建。
- 再启动 `QualityCheckApp.Engine\bin\Debug\QualityCheckApp.Engine.exe`。
- 准备一个包含测试目录和 `.gdb` 数据的 ZIP 包。
- 验证 ZIP 选择、解压、结构校验、图层枚举、地图显示几个主流程。

## 代码组织
- `Services/` 放 ArcGIS 许可、图层读取、ZIP 处理等逻辑。
- `Models/` 放简单数据对象和 `INotifyPropertyChanged` 模型。
- `Infrastructure/StaTask.cs` 用于把 ArcGIS 相关工作切到 STA 线程。
- `MainWindow.xaml` 与对应 code-behind 承担 UI 和交互编排。

## 代码风格

### Imports
- `using System...` 放最前。
- 再放第三方命名空间，如 `ESRI.ArcGIS.*`、`Microsoft.Win32`。
- 最后放项目内命名空间，如 `QualityCheckApp.Engine.Models`。
- 不同组之间留一个空行。
- 只有在解决命名冲突时才使用别名导入，例如 `using WinForms = System.Windows.Forms;`。
- 删除未使用的 `using`。

### 格式化
- 使用 4 空格缩进。
- 保持现有 C# Allman 大括号风格。
- 类型、方法、属性、`if`、`try`、`catch`、`finally` 的左大括号都换行。
- 语句块即使只有一行也保留大括号。
- 倾向一文件一个主类型。
- 保持现有块级 `namespace` 写法，不要混入 file-scoped namespace。
- XAML 中沿用多行属性对齐写法，不要压成一行。

### 类型与语法
- 严格按 C# 5 能力写代码。
- 不要引入字符串插值、`nameof`、null 条件运算符、模式匹配、record、tuple、switch expression。
- 需要格式化文本时使用 `string.Format(...)`。
- 需要属性名时优先用 `[CallerMemberName]`；否则只能写字符串字面量。
- `var` 只在右值类型非常明显时使用。
- 涉及 ArcGIS COM 接口时，倾向显式类型，便于辨认生命周期。
- 新增公共 API 时继续使用 `Task`、`CancellationToken`、`IReadOnlyList<T>` 等现有模式。

### 命名
- 类型、方法、属性、事件使用 `PascalCase`。
- 参数和局部变量使用 `camelCase`。
- 私有字段使用 `_camelCase`。
- 接口以 `I` 开头，例如 `IGdbLayerProvider`。
- 异步方法以 `Async` 结尾。
- 事件处理器以 `On...` 或现有 WinForms 事件名模式命名。

### 错误处理
- 在服务边界尽早校验参数。
- 参数为空或无效时使用 `ArgumentException` / `ArgumentNullException`。
- 文件不存在时使用 `FileNotFoundException`。
- 底层服务抛异常，让 UI 层决定如何展示。
- UI 入口通常把异常转换成 `StatusMessage` 或 `MessageBox`，保持这个分层。
- 只在清理阶段吞掉明确可接受的异常，例如临时目录删除失败。
- 不要静默吃掉业务异常。

### Async / 线程 / COM
- 除 UI 事件处理器外，不要新增 `async void`。
- 继续向下传递 `CancellationToken`。
- ArcGIS COM 相关枚举和读取必须放在 STA 线程，优先复用 `StaTask.Run(...)`。
- 所有 COM 对象都要像现有代码一样在 `finally` 中 `Marshal.ReleaseComObject(...)`。
- 不要长时间缓存 COM 对象引用，除非你非常确定释放策略。
- 许可初始化集中在 `ArcGisLicenseInitializer` 和 `App.xaml.cs`，不要分散到各处。

### WPF / WinForms / UI
- 新主应用代码以 WPF 现有模式为准。
- 有绑定的属性通常要实现“值未变化直接返回”。
- 字符串属性通常会把 `null` 归一成 `string.Empty`。
- 修改 `Layers`、`IsBusy`、`SelectedZipPath` 等影响 UI 的状态时，记得同步触发相关属性通知。
- 用户可见文字以中文为主；修改时跟随所在文件语境。

### 生成文件与不建议手改的文件
- 默认不要手改 `*.Designer.cs`。
- 默认不要手改 `.resx`、`.settings`。
- 默认不要提交 `*.csproj.user`、`.suo`、`bin/`、`obj/` 这类本地生成物。

## 修改策略
- 优先做最小正确修改。
- 先复用现有服务和模型，再考虑新抽象。
- 不要顺手升级框架、语言版本或项目格式。
- 不要引入新包管理方式。
- 如果新增可测试逻辑，优先放在 `Services/` 或 `Models/`，便于未来补测试。

## 提交前最低检查
- 至少确保受影响项目能以 `Debug|x86` 构建通过。
- 如果没有自动化测试，就明确说明“已构建，未运行自动化测试，因为仓库没有测试项目”。
- 如果做了 UI 或 ArcGIS 流程改动，尽量补一次手工 smoke check，并明确写出实际验证范围。
