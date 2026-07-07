@echo off
setlocal enabledelayedexpansion

set APPNAME=TaiXiu3XucXac
set OUTEXE=%APPNAME%.exe

set VBC=
for %%v in (v4.0.30319 v4.0.30128 v4.0.21006 v4.0.20506) do (
    if exist "%WINDIR%\Microsoft.NET\Framework\%%v\vbc.exe" (
        set "VBC=%WINDIR%\Microsoft.NET\Framework\%%v\vbc.exe"
    )
)
if "%VBC%"=="" (
    if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\vbc.exe" (
        set "VBC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\vbc.exe"
    )
)

if "%VBC%"=="" (
    echo [LOI] Khong tim thay vbc.exe cho .NET Framework 4.x
    pause
    exit /b 1
)

echo Dung trinh bien dich: %VBC%
echo Dang build %OUTEXE% ...
echo.

"%VBC%" /nologo /target:winexe /out:%OUTEXE% /optimize+ /optionstrict+ /optionexplicit+ ^
    /reference:System.dll,System.Windows.Forms.dll,System.Drawing.dll,Microsoft.VisualBasic.dll ^
    Program.vb Form1.vb TaiXiuGame.vb NetworkHub.vb NetworkPeer.vb

if errorlevel 1 (
    echo.
    echo [LOI] Build that bai.
    pause
    exit /b 1
) else (
    echo.
    echo [OK] Build thanh cong: %OUTEXE%
    if exist "Assets" (
        echo Da thay thu muc Assets ^(sprite xuc xac^) canh file exe - OK.
    ) else (
        echo [CANH BAO] Khong thay thu muc Assets canh file .bat nay.
        echo            Game van chay binh thuong nhung se fallback ve hinh khoi + so
        echo            thay vi sprite xuc xac that. Dat thu muc Assets (dice_1.png..
        echo            dice_6.png, dice_roll_1.png..dice_roll_4.png) cung cap voi %OUTEXE%.
    )
)

pause
