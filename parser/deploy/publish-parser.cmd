@echo off
rem Double-click to build the parser API for deployment.
rem Result: <repo>\publish\parser-api  (upload its contents to /opt/neuralnetgoal-incoming/ with FileZilla)
cd /d "%~dp0\..\.."
echo Building... this takes about 30 seconds.
echo.
dotnet publish parser\neuralnetgoal.parser.api -c Release -o publish\parser-api -p:UseAppHost=false
if errorlevel 1 (
  echo.
  echo *** BUILD FAILED - do not upload. Read the messages above. ***
) else (
  echo.
  echo Done. The files are in:  %cd%\publish\parser-api
)
echo.
pause
