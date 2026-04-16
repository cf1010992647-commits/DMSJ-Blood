# Inno Setup 安装包

这个目录用于给 `DMSJ_Blood Alcohol.csproj` 生成客户可直接双击安装的 `Setup.exe`。

## 文件说明

- `setup.iss`
  Inno Setup 主脚本
- `build-installer.ps1`
  一键执行 `dotnet publish` 并调用 `ISCC.exe` 生成安装包

## 默认策略

- 发布目标：`win-x64`
- 发布方式：默认自包含 `self-contained`
- 安装位置：`%LocalAppData%\Programs\DMSJ Blood Alcohol`
- 安装包输出：`artifacts\installer`
- 发布产物输出：`artifacts\publish\win-x64`

之所以默认安装到当前用户目录，是为了避免程序把配置文件和日志写到 `Program Files` 时遇到权限问题，同时不需要修改现有业务代码。

## 使用前提

1. 本机已安装 .NET SDK
2. 本机已安装 Inno Setup 6
3. 能在命令行中找到 `ISCC.exe`，或者 Inno Setup 安装在默认目录

## 生成安装包

在仓库根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1 -AppVersion 1.0.0
```

如果你明确希望客户机器自己安装 .NET Desktop Runtime，可以改为框架依赖发布：

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1 -AppVersion 1.0.0 -FrameworkDependent
```

## 产物位置

- 安装包：`artifacts\installer\DMSJ_Blood_Alcohol_Setup_版本号_x64.exe`
- 发布目录：`artifacts\publish\win-x64`
