@echo off
setlocal enabledelayedexpansion

echo ==========================================
echo      GENERUJI APK PRO dumbRemote
echo ==========================================
echo.

:: 1. Nastaveni cest
set "OUTPUT_DIR=_Hotove_APK"
set "PROJECT_NAME=dumbRemote"

:: 2. Uklid starych buildu
if exist "%OUTPUT_DIR%" (
    echo Mazani stare slozky %OUTPUT_DIR%...
    rd /s /q "%OUTPUT_DIR%"
)
mkdir "%OUTPUT_DIR%"

:: 3. Spusteni dotnet publish
echo.
echo Spoustim build (to muze chvili trvat)...
echo.
dotnet publish -f net8.0-android -c Release -p:AndroidPackageFormats=apk

:: 4. Kontrola chyb
if %errorlevel% neq 0 (
    echo.
    echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    echo CHYBA PRI SESTAVOVANI! Zkontroluj vypis vyse.
    echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    pause
    exit /b %errorlevel%
)

:: 5. Kopirovani vysledku
echo.
echo Hledam vygenerovane APK...

:: Cesta k release publish slozce (muze se lisit podle verze .NETu, hledame signed.apk)
for /r "bin\Release\net8.0-android\publish" %%f in (*-Signed.apk) do (
    echo Nalezeno: %%f
    copy "%%f" "%OUTPUT_DIR%\%PROJECT_NAME%.apk"
)

:: Pokud se nenaslo Signed, zkusime najit jakekoliv APK
if not exist "%OUTPUT_DIR%\%PROJECT_NAME%.apk" (
    for /r "bin\Release\net8.0-android\publish" %%f in (*.apk) do (
        echo Nalezeno (nepodepsane/debug): %%f
        copy "%%f" "%OUTPUT_DIR%\%PROJECT_NAME%.apk"
    )
)

echo.
echo ==========================================
echo HOTOVO! APK najdes ve slozce: %OUTPUT_DIR%
echo ==========================================

:: 6. Otevreni slozky
start "" "%OUTPUT_DIR%"

pause
