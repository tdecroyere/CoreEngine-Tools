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
    public static class CHeaderCodeGenerator
    {
        private static List<string> enumTypes = new List<string>();
        
        public static string GenerateHeaderCode(CompilationUnitSyntax compilationUnit)
        {
            if (compilationUnit == null)
            {
                return string.Empty;
            }
            
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("#pragma once");
            stringBuilder.AppendLine("#include \"CoreEngine.h\"");
            stringBuilder.AppendLine();

            var enums = compilationUnit.DescendantNodes().OfType<EnumDeclarationSyntax>();
            
            foreach (var enumNode in enums)
            {
                enumTypes.Add(enumNode.Identifier.ToString());

                stringBuilder.AppendLine($"enum {enumNode.Identifier} : int");
                stringBuilder.AppendLine("{");
                var currentParameterIndex = 0;

                foreach (var member in enumNode.Members)
                {
                    if (currentParameterIndex > 0)
                    {
                        stringBuilder.AppendLine(", ");
                    }

                    stringBuilder.Append($"    {member.Identifier}");
                    currentParameterIndex++;
                }

                stringBuilder.AppendLine();
                stringBuilder.AppendLine("};");
                stringBuilder.AppendLine();
            }

            var structs = compilationUnit.DescendantNodes().OfType<StructDeclarationSyntax>();
            
            foreach (var structNode in structs)
            {
                stringBuilder.AppendLine($"struct {structNode.Identifier}");
                stringBuilder.AppendLine("{");

                foreach (var member in structNode.Members)
                {
                    if (member.Kind() == SyntaxKind.PropertyDeclaration)
                    {
                        var property = (PropertyDeclarationSyntax)member;
                        stringBuilder.AppendLine($"    {MapCSharpTypeToC(property.Type.ToString())} {property.Identifier};");
                    }
                }

                stringBuilder.AppendLine();
                stringBuilder.AppendLine("};");
                stringBuilder.AppendLine();
            }

            var interfaces = compilationUnit.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
            
            foreach (var interfaceNode in interfaces)
            {
                var functionNameList = new List<string>();

                foreach (var member in interfaceNode.Members)
                {
                    if (member.Kind() == SyntaxKind.MethodDeclaration)
                    {
                        var method = (MethodDeclarationSyntax)member;
                        var parameters = method.ParameterList.Parameters;
                        var functionName = $"{interfaceNode.Identifier.ToString().Substring(1)}_{method.Identifier.ToString()}";

                        var functionNameOriginal = functionName;
                        var currentIndex = 0;

                        while (functionNameList.Contains(functionName))
                        {
                            functionName = functionNameOriginal + $"_{++currentIndex}";
                        }

                        functionNameList.Add(functionName);

                        stringBuilder.Append("typedef ");
                        stringBuilder.Append($"{MapCSharpTypeToC(method.ReturnType.ToString())} ");
                        stringBuilder.Append($"(*{functionName}Ptr)(void* context");
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

                            else if (parameter.Type!.ToString().Contains("ReadOnlySpan<"))
                            {
                                var index = parameter.Type!.ToString().IndexOf("<");
                                var parameterType = parameter.Type!.ToString().Substring(index).Replace("<", string.Empty).Replace(">", string.Empty);

                                stringBuilder.Append($"{MapCSharpTypeToC(parameterType)}* {parameter.Identifier}, int {parameter.Identifier}Length");
                            }

                            else
                            {
                               stringBuilder.Append($"{MapCSharpTypeToC(parameter.Type.ToString())} {parameter.Identifier}");
                            }

                            currentParameterIndex++;
                        }

                        stringBuilder.Append(");");
                        stringBuilder.AppendLine();
                    }
                }

                stringBuilder.AppendLine("");
                stringBuilder.AppendLine($"struct {interfaceNode.Identifier.ToString().Substring(1)}");
                stringBuilder.AppendLine("{");
                stringBuilder.AppendLine($"    void* Context;");

                functionNameList = new List<string>();

                foreach(var member in interfaceNode.Members)
                {
                    if (member.Kind() == SyntaxKind.MethodDeclaration)
                    {
                        var method = (MethodDeclarationSyntax)member;
                        var parameters = method.ParameterList.Parameters;
                        var functionName = $"{interfaceNode.Identifier.ToString().Substring(1)}_{method.Identifier.ToString()}";

                        var functionNameOriginal = functionName;
                        var currentIndex = 0;

                        while (functionNameList.Contains(functionName))
                        {
                            functionName = functionNameOriginal + $"_{++currentIndex}";
                        }

                        functionNameList.Add(functionName);

                        stringBuilder.AppendLine($"    {functionName}Ptr {functionName};");
                    }
                }

                stringBuilder.AppendLine("};");
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

            else if (typeName == "string" || typeName == "string?")
            {
                result = "char*";
            }

            if (result.EndsWith('?'))
            {
                result = $"Nullable{typeName}".TrimEnd('?');
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
                "void", "bool", "byte", "short", "ushort", "int", "uint", "float", "double", "char", "string", "string?"
            };

            return builtInTypes.Contains(typeName);
        }
    }
}