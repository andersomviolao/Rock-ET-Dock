@echo off
setlocal
pushd "%~dp0"
dotnet run --project "src\Dock.App\Dock.App.csproj" -- --settings
popd
