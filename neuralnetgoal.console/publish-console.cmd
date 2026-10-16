@echo off
rem Double-click to build self-contained versions of the console (they need no .NET installed):
rem   publish\console-win-x64    Windows
rem   publish\console-osx-arm64  Mac with Apple Silicon (M1/M2/M3/M4)
rem   publish\console-osx-x64    Mac with an Intel processor
rem On a Mac, first run:  chmod +x neuralnetgoal.console  and  codesign -s - --force neuralnetgoal.console
rem (see the README, "Running on a Mac"). For the Mac the simplest way is to install the .NET 10 SDK and use dotnet run.
cd /d "%~dp0.."
for %%R in (win-x64 osx-arm64 osx-x64) do (
  echo.
  echo === %%R ===
  dotnet publish neuralnetgoal.console -c Release -r %%R --self-contained -p:PublishSingleFile=true -o publish\console-%%R
  if errorlevel 1 ( echo *** BUILD FAILED for %%R *** )
)
echo.
echo Done. The programs are in %cd%\publish
pause
