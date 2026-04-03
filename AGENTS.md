# AGENTS.md
本文件供在本仓库内工作的 agentic coding agents 使用。
目标是先尊重现状，再做最小正确修改，并明确说明验证范围。

## 仓库概况
- 仓库根目录：`D:\WorkSpace\QualityCheckApp.Engine`
- 解决方案：`QualityCheckApp.Engine.sln`
- 主项目：`QualityCheckApp.Engine\QualityCheckApp.Engine.csproj`
- 当前仓库只有一个主线项目，没有独立 demo、测试项目或多项目壳工程。
- 应用类型：WPF 桌面程序，内嵌 WinForms `AxMapControl`，依赖 ArcGIS Engine。
- 目标框架：`.NET Framework 4.5`
- 语言版本：`C# 5`
- 编译平台：`x86`
- 不要擅自升级到 SDK-style、`AnyCPU`、更高 .NET 版本或更高 C# 语法。

## 外部规则文件状态
- 已检查仓库根目录及子目录。
- 没有额外的 `AGENTS.md`。
- 没有 `.cursorrules`。
- 没有 `.cursor/rules/` 目录。
- 没有 `.github/copilot-instructions.md`。
- 因此本文件就是当前仓库唯一的代理协作规则来源。

## 环境前提
- 推荐在 Windows 环境操作。
- 推荐从仓库根目录运行命令。
- 推荐直接使用 MSBuild，不依赖 `dotnet build`。
- 已验证可用的 MSBuild 路径：`C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe`
- 项目默认 ArcGIS SDK 路径：`C:\Program Files (x86)\ArcGIS\DeveloperKit10.2\DotNet`
- ArcGIS 引用通过本机安装目录解析，不通过 NuGet 恢复。
- 当前 `Debug|x86` 和 `Release|x86` 都在解决方案中声明。
- `QualityCheckApp.Engine.csproj` 的 `WarningLevel` 为 `4`。
- 当前仓库没有 `packages.config`、`.editorconfig`、`Directory.Build.props`、规则集或 Roslyn analyzer 配置。

## 构建命令
以下命令默认在仓库根目录执行。

```powershell
# 构建整个解决方案（推荐）
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' 'QualityCheckApp.Engine.sln' /t:Build /p:Configuration=Debug /p:Platform=x86 /verbosity:minimal

# 重新构建整个解决方案
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' 'QualityCheckApp.Engine.sln' /t:Rebuild /p:Configuration=Debug /p:Platform=x86 /verbosity:minimal

# 清理
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' 'QualityCheckApp.Engine.sln' /t:Clean /p:Configuration=Debug /p:Platform=x86 /verbosity:minimal

# 构建 Release
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' 'QualityCheckApp.Engine.sln' /t:Build /p:Configuration=Release /p:Platform=x86 /verbosity:minimal

# 仅构建主项目
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' 'QualityCheckApp.Engine\QualityCheckApp.Engine.csproj' /t:Build /p:Configuration=Debug /p:Platform=x86 /verbosity:minimal

# ArcGIS SDK 不在默认路径时显式覆盖
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' 'QualityCheckApp.Engine.sln' /t:Build /p:Configuration=Debug /p:Platform=x86 /p:ArcGisSdkPath='C:\custom\ArcGIS\DeveloperKit10.2\DotNet' /verbosity:minimal
```

## Lint / Test 现状
- 没有独立 lint 命令。
- 没有 `StyleCop`、`dotnet format`、FxCop、Roslyn analyzer、规则集配置。
- 当前最接近 lint 的检查方式就是完整编译并查看 Warning / Error。
- 当前仓库没有自动化测试项目。
- 没有 `MSTest`、`xUnit`、`NUnit`、`Microsoft.NET.Test.Sdk` 相关项目或引用。
- 因此没有 `dotnet test`、`vstest.console`、`mstest` 的可用入口。

## 单个测试如何运行
- 目前无法运行“单个测试”，因为仓库中根本没有自动化测试用例。
- 如果用户要求“跑一个测试”，要明确说明当前不存在可执行的单测基础设施。
- 不要编造测试命令，也不要把构建命令说成测试命令。
- 更准确的替代说法应为“手工 smoke check”或“手工集成验证”。

## 已知构建现象
- 在当前机器上，`Debug|x86` 全量构建已可执行并产出 `QualityCheckApp.Engine\bin\Debug\QualityCheckApp.Engine.exe`。
- 构建过程中出现 `MSB3644`，提示本机缺少 `.NET Framework 4.5 Targeting Pack`。
- 构建过程中还出现 `MSB3247` 程序集版本冲突警告。
- 这些警告当前不会阻止生成可执行文件，但代理应在汇报中如实说明，不要声称“零警告”。

## 手工验证建议
- 先执行 `Debug|x86` 全量构建。
- 再启动 `QualityCheckApp.Engine\bin\Debug\QualityCheckApp.Engine.exe`。
- 准备一个包含测试目录与 `.gdb` 数据的 ZIP 包。
- 验证 ZIP 选择、解压、结构检查、图层枚举、地图显示、拓扑检测、报告导出等主流程。
- 如果本机没有 ArcGIS 运行环境或示例数据，要在结果中明确写出未验证项。

## 代码组织
- `MainWindow.xaml` 与 `MainWindow.xaml.cs` 承担主要 UI 和交互编排。
- `Services/` 放 ArcGIS 许可、图层读取、拓扑检测、ZIP 处理、报告导出等服务逻辑。
- `Models/` 放简单数据对象和 `INotifyPropertyChanged` 模型。
- `Infrastructure/StaTask.cs` 负责把 ArcGIS COM 相关工作切到 STA 线程。
- `App.xaml.cs` 与 `LicenseInitializer*` 负责程序启动和许可初始化。
- `Program.cs` 保留 WinForms / ArcGIS 相关启动入口配合。

## 修改策略
- 优先做最小正确修改。
- 先复用现有服务、模型和 UI 状态流，再考虑引入新抽象。
- 不要顺手升级框架、项目格式、引用方式或包管理方式。
- 不要在无明确收益时拆大规模文件；当前 `MainWindow.xaml.cs` 偏大是既有现实。
- 如果新增可测试的纯逻辑，优先放在 `Services/` 或 `Models/`，方便未来补测试。

## 代码风格

### Imports
- `using System...` 放最前。
- 再放第三方命名空间，如 `ESRI.ArcGIS.*`、`Microsoft.Win32`。
- 最后放项目内命名空间，如 `QualityCheckApp.Engine.Models`。
- 各组之间保留一个空行。
- 删除未使用的 `using`。
- 仅在确实存在命名冲突时使用别名导入。

### 格式化
- 使用 4 空格缩进。
- 保持现有 C# Allman 大括号风格。
- 类型、方法、属性、`if`、`try`、`catch`、`finally`、`switch` 的左大括号都单独换行。
- 即使语句块只有一行，也保留大括号。
- 继续使用块级 `namespace`，不要引入 file-scoped namespace。
- 倾向一文件一个主类型。
- 不要混用明显不同的排版风格。
- XAML 中保持多行属性对齐，不要把复杂元素压成一行。

### 类型与语法
- 严格按 C# 5 能力写代码。
- 不要使用字符串插值、`nameof`、null 条件运算符、模式匹配、tuple、record、表达式体成员、switch expression。
- 需要格式化字符串时使用 `string.Format(...)`。
- 需要属性名时优先使用 `[CallerMemberName]`；若受现有模式限制，可继续写字符串字面量。
- `var` 只在右值类型非常明显时使用；涉及 ArcGIS COM 时优先显式类型。
- 保持当前异步签名风格，继续使用 `Task`、`CancellationToken`、`IReadOnlyList<T>` 等现有模式。

### 命名
- 类型、方法、属性、事件使用 `PascalCase`。
- 参数和局部变量使用 `camelCase`。
- 私有字段使用 `_camelCase`。
- 接口以 `I` 开头，例如 `IGdbLayerProvider`。
- 异步方法以 `Async` 结尾。
- 事件处理器保持现有 `On...` 命名模式。

### 模型与属性
- 有绑定的属性通常采用“值未变化则直接返回”的写法。
- 字符串属性通常把 `null` 归一成 `string.Empty`。
- `INotifyPropertyChanged` 模型沿用当前手写实现，不要无故引入 MVVM 框架。
- 修改 `Layers`、`TopologyIssues`、`IsBusy`、`SelectedZipPath` 等 UI 相关状态时，要同步触发关联属性通知。

### 错误处理
- 在服务边界尽早校验参数。
- 参数为空或无效时使用 `ArgumentNullException` 或 `ArgumentException`。
- 文件或目录不存在时优先使用 `FileNotFoundException` 或合适的 IO 异常。
- 服务层应保留异常语义，让 UI 层决定如何展示。
- UI 入口通常把异常转换成 `StatusMessage` 或 `MessageBox`，保持这个分层。
- 不要静默吞掉业务异常。
- 仅在清理阶段吞掉明确可接受的异常，例如释放临时资源失败。

### Async / 线程 / COM
- 除 UI 事件处理器外，不要新增 `async void`。
- 继续向下传递 `CancellationToken`。
- ArcGIS COM 相关枚举、打开工作空间、拓扑检查等操作必须放在 STA 线程。
- 优先复用 `StaTask.Run(...)`，不要随意自己起线程破坏一致性。
- 所有 COM 对象都要像现有代码一样在 `finally` 中 `Marshal.ReleaseComObject(...)`。
- 不要长时间缓存 COM 对象引用，除非你非常确定释放策略。
- 许可初始化应集中在现有启动链路，不要分散到各个服务中。

### WPF / WinForms / UI
- 新 UI 代码以现有 WPF 模式为准，并接受 WinForms 地图控件混用这一现实。
- 用户可见文字以中文为主，修改时跟随所在文件语境。
- 视觉改动应保持当前产品风格，不要无端引入完全不同的设计语言。
- 影响地图状态、图层可见性、拓扑结果定位的改动，要留意 UI 与 COM 对象的同步释放。

### 文件修改边界
- 默认不要手改 `*.Designer.cs`。
- 默认不要手改 `.resx`、`.settings`。
- 默认不要提交 `bin/`、`obj/`、`*.user`、`*.suo` 等本地生成物。
- 修改 `.csproj` 前先确认是否确有必要，因为该项目依赖本机 ArcGIS 安装路径。

## 提交前最低检查
- 至少确保受影响代码能通过一次 `Debug|x86` 构建，或明确说明为何无法构建。
- 如果没有自动化测试，就明确写出“已构建，未运行自动化测试，因为仓库没有测试项目”。
- 如果做了 UI、ZIP、ArcGIS 或拓扑流程改动，尽量补一次手工 smoke check，并写清实际验证范围。
- 如果因为本机缺少 ArcGIS、Targeting Pack 或测试数据而无法完整验证，也要明确记录阻塞条件。
