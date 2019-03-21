@ECHO OFF

IF NOT EXIST build\Windows mkdir build\Windows
pushd build\Windows

ECHO [93mCompiling CoreEngine Compiler...[0m
dotnet.exe build /nologo -c Debug -v Q -o "." "..\..\src\CoreEngineCompiler"

@IF %ERRORLEVEL% == 0 (
   GOTO Copy_Files
)
@IF NOT %ERRORLEVEL% == 0 (
   GOTO CompileError
)

:CompileError
   ECHO [91mError: Build has failed![0m
   EXIT 1

:Copy_Files
   ECHO [93mCopy files...[0m
   REM COPY *.dll ..\Windows > NUL
   REM COPY *.pdb ..\Windows > NUL
   GOTO End
   
:End
    ECHO [92mSuccess: Compilation done.[0m
    popd
    RD /S /Q build\temp
    @ECHO ON