@echo off
setlocal


start "" wt.exe -w new new-tab --title "REGA API" --startingDirectory "%~dp0." cmd /k "dotnet restore Back\AqarInsights.Api\AqarInsights.Api.csproj --source https://api.nuget.org/v3/index.json && dotnet run --no-restore --project Back\AqarInsights.Api\AqarInsights.Api.csproj --launch-profile http" ; new-tab --title "REGA Angular" --startingDirectory "%~dp0Front" cmd /k "npm start"

echo Waiting for API and Angular to start...
set /a "attempts=0"

:wait_for_servers
curl.exe -fsS http://localhost:5032/v1/api/health-check >nul 2>nul
if errorlevel 1 goto retry_startup
curl.exe -fsS http://localhost:4200/ >nul 2>nul
if errorlevel 1 goto retry_startup
goto servers_ready

:retry_startup
set /a attempts+=1
if %attempts% geq 60 goto startup_timeout
timeout /t 2 /nobreak >nul
goto wait_for_servers

:servers_ready
set "CHROME="
if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" set "CHROME=%ProgramFiles%\Google\Chrome\Application\chrome.exe"
if not defined CHROME if exist "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" set "CHROME=%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"
if not defined CHROME if exist "%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe" set "CHROME=%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe"
if not defined CHROME for /f "delims=" %%C in ('where chrome.exe 2^>nul') do if not defined CHROME set "CHROME=%%C"
if not defined CHROME goto chrome_not_found

start "" "%CHROME%" "http://localhost:4200"
start "" "%CHROME%" "http://localhost:5032/scalar/v1"
exit /b 0

:startup_timeout
echo Timed out waiting for the API and Angular. Check their terminal windows for errors.
pause
exit /b 1

:chrome_not_found
echo Chrome was not found. The API and Angular are running in their terminal windows.
pause
exit /b 1

endlocal
