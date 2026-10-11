rmdir bin /S /Q
dotnet publish -c Release -r win-x64 -o .\bin\win-x64
dotnet publish -c Release -r win-x86 -o .\bin\win-x86
dotnet publish -c Release -r win-arm64 -o .\bin\win-arm64
dotnet publish -c Release -r linux-arm64 -o .\bin\linux-arm64
dotnet publish -c Release -r linux-arm -o .\bin\linux-arm
dotnet publish -c Release -r osx-arm64 -o .\bin\osx-arm64
dotnet publish -c Release -r osx-x64 -o .\bin\osx-x64
mkdir bin\release-out
copy bin\win-x64\onsmake.exe bin\release-out\onsmake-win-x64.exe
copy bin\win-x86\onsmake.exe bin\release-out\onsmake-win-x86.exe
copy bin\win-arm64\onsmake.exe bin\release-out\onsmake-win-arm64.exe
copy bin\linux-arm64\onsmake bin\release-out\onsmake-linux-arm64
copy bin\linux-arm\onsmake bin\release-out\onsmake-linux-arm
copy bin\osx-arm64\onsmake bin\release-out\onsmake-osx-arm64
copy bin\osx-x64\onsmake bin\release-out\onsmake-osx-x64
