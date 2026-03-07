@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo    西林民高部署工具 - 发布脚本
echo ========================================
echo.

set PROJECT_DIR=%~dp0部署工具\DeployTool
set OUTPUT_DIR=%~dp0Release

for /f "tokens=2 delims==" %%a in ('findstr /r "Version" "%PROJECT_DIR%\DeployTool.csproj" ^| findstr /r "^[[:space:]]*<Version>"') do (
    set VERSION=%%~a
)
set VERSION=%VERSION:"=%
set VERSION=%VERSION: =%
set VERSION=%VERSION:	=%

if "%VERSION%"=="" (
    echo 无法读取版本号，请检查 DeployTool.csproj 文件
    pause
    exit /b 1
)

echo 当前版本: v%VERSION%
echo.

set /p CONFIRM="确认发布 v%VERSION% 版本? (Y/N): "
if /i not "%CONFIRM%"=="Y" (
    echo 已取消发布
    pause
    exit /b 0
)

echo.
echo [1/4] 清理输出目录...
if exist "%OUTPUT_DIR%" rd /s /q "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%"

echo [2/4] 编译项目...
cd /d "%PROJECT_DIR%"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o "%OUTPUT_DIR%\DeployTool-v%VERSION%"

if %errorlevel% neq 0 (
    echo 编译失败!
    pause
    exit /b 1
)

echo [3/4] 复制Data目录...
if exist "%~dp0部署工具\Data" (
    xcopy /E /I /Y "%~dp0部署工具\Data" "%OUTPUT_DIR%\DeployTool-v%VERSION%\Data"
)

echo [4/4] 创建压缩包...
cd /d "%OUTPUT_DIR%"
powershell -Command "Compress-Archive -Path 'DeployTool-v%VERSION%' -DestinationPath 'DeployTool-v%VERSION%.zip' -Force"

echo.
echo ========================================
echo    发布完成!
echo ========================================
echo.
echo 输出目录: %OUTPUT_DIR%
echo 压缩包: DeployTool-v%VERSION%.zip
echo.

set /p TAG="是否创建Git标签 v%VERSION%? (Y/N): "
if /i "%TAG%"=="Y" (
    git tag -a "v%VERSION%" -m "Release v%VERSION%"
    echo Git标签 v%VERSION% 已创建
    echo.
    echo 请运行以下命令推送到GitHub:
    echo   git push origin v%VERSION%
    echo.
    echo 然后在GitHub上创建Release并上传:
    echo   %OUTPUT_DIR%\DeployTool-v%VERSION%.zip
)

pause
