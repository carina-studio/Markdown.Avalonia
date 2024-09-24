@echo off

if exist Packages (
	rmdir Packages /s /q
)
mkdir Packages
if %ERRORLEVEL% neq 0 ( 
   exit
)

dotnet build Markdown.Avalonia.Tight -c Release
if %ERRORLEVEL% neq 0 ( 
   exit
)

dotnet pack ColorTextBlock.Avalonia -c Release -o Packages
dotnet pack ColorDocument.Avalonia -c Release -o Packages
dotnet pack Markdown.Avalonia.Tight -c Release -o Packages