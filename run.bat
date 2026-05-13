@echo off
setlocal

cd /d "%~dp0"
dotnet run --project "src\Dock.App\Dock.App.csproj"

endlocal
