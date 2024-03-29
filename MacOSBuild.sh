#!/usr/bin/env bash

outputDirectory="./Build/MacOS/"

mkdir -p $outputDirectory > /dev/null
mkdir -p $outputDirectory/Tools > /dev/null

cd $outputDirectory

copyFiles() {
    echo [93mCopy files...[0m

    cp -R "../../external/ShaderConductor/MacOS/" "./Tools/ShaderConductor"
    chmod +x "./Tools/ShaderConductorCmd"

    # cp -R "../../external/Compressonator/MacOS/" "./Tools/Compressonator"

    # cp "../../src/Host/MacOS/Info.plist" "../"$outputDirectory
    # cp * "../"$outputDirectory"/CoreClr"
    #cp *".dll" "../"$outputDirectory"/Frameworks"
    #cp *".dylib" "../"$outputDirectory"/Frameworks"
    #cp *".a" "../"$outputDirectory"/Frameworks"
    #cp "mscorlib.dll" "../"$outputDirectory"/Frameworks"
    # cp "System.Private.CoreLib.dll" "../"$outputDirectory"/Frameworks"
    # cp "System.Runtime.dll" "../"$outputDirectory"/Frameworks"
    # cp "System.Console.dll" "../"$outputDirectory"/Frameworks"
    # cp "System.Threading.dll" "../"$outputDirectory"/Frameworks"
    # cp "System.Runtime.Extensions.dll" "../"$outputDirectory"/Frameworks"
    # cp "System.Text.Encoding.Extensions.dll" "../"$outputDirectory"/Frameworks"
    # cp "CoreEngine.dll" "../"$outputDirectory"/Frameworks"
    # cp *".dll" "../"$outputDirectory"/CoreClr"
    # "/Applications/Xcode.app/Contents/Developer/Toolchains/XcodeDefault.xctoolchain/usr/lib/swift/macosx"

    #rm -R $tempDirectory
}

echo [93mCompiling CoreEngine Compiler...[0m

dotnet build --nologo -c Debug -v Q -o "." "..\..\src\CoreEngineCompiler"

currentDir=$(
  cd $(dirname "$0")
  pwd
)

# sudo rm /usr/local/bin/CoreEngineCompiler
# sudo ln -s $currentDir/CoreEngineCompiler /usr/local/bin/CoreEngineCompiler

if [ $? -eq 0 ]; then
    copyFiles

    echo [92mSuccess: Compilation done.[0m
fi