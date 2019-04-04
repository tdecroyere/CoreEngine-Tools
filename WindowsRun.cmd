@ECHO OFF
pushd .\build\Windows
.\CoreEngineCompiler.exe "..\..\TestData\TestProject.ceproj" %1
popd
@ECHO ON