@ECHO OFF
pushd .\build\Windows
.\CoreEngineCompiler.exe "..\..\TestData\TestProject.ceproj"
popd
@ECHO ON