#!/usr/bin/env bash

cd "./build/MacOS"
dotnet ./CoreEngineCompiler.dll "../../TestData/TestProject.ceproj" $1

#cd "./build/MacOS/CoreEngine.app/Contents/CoreClr"
#./CoreEngineHost $1