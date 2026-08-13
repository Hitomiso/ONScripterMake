rmdir bin /S /Q
dotnet publish -c Release -r win-x64 -o .\bin\win-x64
dotnet publish -c Release -r win-x86 -o .\bin\win-x86
dotnet publish -c Release -r win-arm64 -o .\bin\win-arm64
dotnet publish -c Release -r linux-arm64 -o .\bin\linux-arm64
dotnet publish -c Release -r linux-arm -o .\bin\linux-arm
dotnet publish -c Release -r osx-arm64 -o .\bin\osx-arm64
dotnet publish -c Release -r osx-x64 -o .\bin\osx-x64