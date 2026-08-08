@echo off

pushd %~dp0..\..

dotnet pack src\PlaywrightPomGenerator.Cli\PlaywrightPomGenerator.Cli.csproj -o src\PlaywrightPomGenerator.Cli\nupkg
if %ERRORLEVEL% NEQ 0 (
    echo Build failed.
    popd
    exit /b %ERRORLEVEL%
)

dotnet tool uninstall -g PlaywrightPomGenerator 2>nul

dotnet tool install -g PlaywrightPomGenerator --add-source src\PlaywrightPomGenerator.Cli\nupkg
if %ERRORLEVEL% NEQ 0 (
    echo Install failed.
    popd
    exit /b %ERRORLEVEL%
)

popd
