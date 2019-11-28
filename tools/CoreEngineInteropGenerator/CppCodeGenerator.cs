using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace CoreEngineInteropGenerator
{
    public static class CppCodeGenerator
    {
        private static List<string> enumTypes = new List<string>();

        public static string GenerateInteropCode(CompilationUnitSyntax compilationUnit, IDictionary<string, string> implementationTypes)
        {
            if (compilationUnit == null)
            {
                return string.Empty;
            }

            if (implementationTypes == null)
            {
                return string.Empty;
            }
            
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("#pragma once");
            stringBuilder.AppendLine("#include \"WindowsDirect3D12Renderer.h\"");
            stringBuilder.AppendLine("#include \"../../Common/CoreEngine.h\"");
            stringBuilder.AppendLine();

            var enums = compilationUnit.DescendantNodes().OfType<EnumDeclarationSyntax>();
            
            foreach (var enumNode in enums)
            {
                enumTypes.Add(enumNode.Identifier.ToString());
            }

            var interfaces = compilationUnit.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
            
            foreach (var interfaceNode in interfaces)
            {
                var implementationType = interfaceNode.Identifier.ToString();

                if (implementationTypes.ContainsKey(interfaceNode.Identifier.ToString()))
                {
                    implementationType = implementationTypes[interfaceNode.Identifier.ToString()];
                }

                foreach (var member in interfaceNode.Members)
                {
                    if (member.Kind() == SyntaxKind.MethodDeclaration)
                    {
                        var method = (MethodDeclarationSyntax)member;
                        var parameters = method.ParameterList.Parameters;
                        var functionName = method.Identifier.ToString();
                        var cppReturnType = MapCSharpTypeToC(method.ReturnType.ToString());

                        stringBuilder.Append($"{cppReturnType} {functionName}Interop(void* context");
                        var currentParameterIndex = 1;

                        foreach (var parameter in parameters)
                        {
                            if (currentParameterIndex > 0)
                            {
                                stringBuilder.Append(", ");
                            }

                            if (parameter.Type!.ToString() == "ReadOnlySpan<byte>")
                            {
                                stringBuilder.Append($"void* {parameter.Identifier}, int {parameter.Identifier}Length");
                            }

                            else
                            {
                               stringBuilder.Append($"{MapCSharpTypeToC(parameter.Type.ToString())} {parameter.Identifier}");
                            }

                            currentParameterIndex++;
                        }

                        stringBuilder.AppendLine(")");
                        stringBuilder.AppendLine("{");
                        stringBuilder.AppendLine($"    auto contextObject = ({implementationType}*)context;");
                        stringBuilder.Append("    ");

                        if (method.ReturnType.ToString() != "void") {
                            
                            stringBuilder.Append("return ");
                        }

                        stringBuilder.Append($"contextObject->{functionName}(");

                        currentParameterIndex = 0;

                        foreach (var parameter in parameters)
                        {
                            if (currentParameterIndex > 0)
                            {
                                stringBuilder.Append(", ");
                            }

                            if (parameter.Type!.ToString() == "ReadOnlySpan<byte>")
                            {
                                stringBuilder.Append($"{parameter.Identifier}, {parameter.Identifier}Length");
                            }

                            else
                            {
                                stringBuilder.Append($"{parameter.Identifier}");
                            }

                            currentParameterIndex++;
                        }

                        stringBuilder.AppendLine(")");
                        stringBuilder.AppendLine("}");
                        stringBuilder.AppendLine();
                    }
                }

                stringBuilder.AppendLine($"void Init{interfaceNode.Identifier.ToString().Substring(1)}({implementationType}* context, {interfaceNode.Identifier.ToString().Substring(1)}* service)");
                stringBuilder.AppendLine("{");
                stringBuilder.AppendLine("    service->Context = context;");
                
                foreach (var member in interfaceNode.Members)
                {
                    if (member.Kind() == SyntaxKind.MethodDeclaration)
                    {
                        var method = (MethodDeclarationSyntax)member;
                        var parameters = method.ParameterList.Parameters;
                        var functionName = method.Identifier.ToString();

                        stringBuilder.AppendLine($"    service->{method.Identifier} = {functionName}Interop;");
                    }
                }

                stringBuilder.AppendLine("}");
            }
            
            return stringBuilder.ToString();
        }

        private static string MapCSharpTypeToC(string typeName)
        {
            var result = typeName;

            if (typeName == "uint")
            {
                result = "unsigned int";
            }

            else if (typeName == "bool")
            {
                result = "int";
            }

            if (enumTypes.Contains(typeName))
            {
                result = "enum " + result;
            }
            
            else if (!IsBuiltInType(typeName))
            {
                result = "struct " + result;
            }

            return result;
        }

        private static bool IsBuiltInType(string typeName)
        {
            var builtInTypes = new string[] 
            {
                "void", "bool", "byte", "short", "ushort", "int", "uint", "float", "double", "char"
            };

            return builtInTypes.Contains(typeName);
        }
    }
}