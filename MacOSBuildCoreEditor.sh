#!/usr/bin/env bash

macosTempDirectory="./build/temp/macos"
outputDirectory="MacOS/CoreEditor.app/Contents"

mkdir -p $macosTempDirectory > /dev/null
mkdir -p "./build/"$outputDirectory > /dev/null
mkdir -p "./build/"$outputDirectory"/MacOS" > /dev/null

copyFiles() {
    echo [93mCopy files...[0m

    cp "./src/CoreEditor/MacOS/Info.plist" "./build/"$outputDirectory
    cp $macosTempDirectory"/CoreEditor" "./build/"$outputDirectory"/MacOS"
    cp -R $macosTempDirectory"/CoreEditor.dSYM" "./build/"$outputDirectory"/MacOS"

    #rm -R $tempDirectory
}

showErrorMessage() {
    echo [91mError: Build has failed![0m
}

compileHost() {
    cd $macosTempDirectory
    echo [93mCompiling MacOS Executable...[0m
    swiftc "../../../src/CoreEditor/MacOS/"*".swift" -Onone -g -o "CoreEditor" -debug-info-format=dwarf -swift-version 5 -target x86_64-apple-macosx10.15 -L "." -I "." -Xlinker -rpath -Xlinker "@executable_path/../Frameworks"
    
    if [ $? != 0 ]; then
        showErrorMessage
        exit 1
    fi

    cd "../../.."
}

compileHost
copyFiles

echo [92mSuccess: Compilation done.[0m
exit 0