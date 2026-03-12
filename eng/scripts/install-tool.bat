@echo off

dotnet tool update -g PlaywrightPomGenerator
set ERROR=%ERRORLEVEL%
if "%ERROR%" NEQ "0" (
    dotnet tool install -g PlaywrightPomGenerator
)
