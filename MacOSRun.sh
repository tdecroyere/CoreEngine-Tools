#!/usr/bin/env bash

cd "./build/MacOS"
./CoreEngineCompiler $1 $2

#cd "./build/MacOS/CoreEngine.app/Contents/CoreClr"
#./CoreEngineHost $1