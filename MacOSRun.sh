#!/usr/bin/env bash

cd "./build/MacOS"
./CoreEngineCompiler "../../TestData/TestProject.ceproj"

#cd "./build/MacOS/CoreEngine.app/Contents/CoreClr"
#./CoreEngineHost $1