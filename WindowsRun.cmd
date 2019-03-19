@ECHO OFF
pushd .\build\Windows
.\CoreEngineCompiler.exe %1 %2
popd
@ECHO ON