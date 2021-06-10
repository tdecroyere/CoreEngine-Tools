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
    public class CppOutput
    {
        public CppOutput(string path, string content)
        {
            this.Path = path;
            this.Content = content;
        }

        public string Path { get; }
        public string Content { get; }
    }

    public static class CppCodeGenerator
    {
        private static List<string> enumTypes = new List<string>();

        public static IList<CppOutput> GenerateInteropCode(CompilationUnitSyntax compilationUnit, IDictionary<string, string> implementationTypes)
        {
            if (compilationUnit == null)
            {
                return new List<CppOutput>();
            }

            if (implementationTypes == null)
            {
                return new List<CppOutput>();
            }

            var result = new List<CppOutput>();
            var enums = compilationUnit.DescendantNodes().OfType<EnumDeclarationSyntax>();
            
            foreach (var enumNode in enums)
            {
                enumTypes.Add(enumNode.Identifier.ToString());
            }

            var interfaces = compilationUnit.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
            
            foreach (var interfaceNode in interfaces)
            {
                if (!implementationTypes.ContainsKey(interfaceNode.Identifier.ToString()))
                {
                    continue;
                }

                var implementationTypeArray = implementationTypes[interfaceNode.Identifier.ToString()].Split(',');

                foreach (var implementationType in implementationTypeArray)
                {
                    var content = GenerateInteropClass(interfaceNode, implementationType);
                    result.Add(new CppOutput(implementationType + "Interop.h", content));
                }
            }

            return result;
        }

        private static string GenerateInteropClass(InterfaceDeclarationSyntax interfaceNode, string implementationType)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("#pragma once");

            stringBuilder.AppendLine($"#include \"../{implementationType}.h\"");
            stringBuilder.AppendLine();

            foreach (var member in interfaceNode.Members)
            {
                if (member.Kind() == SyntaxKind.MethodDeclaration)
                {
                    var hasStringReturn = false;
                    var method = (MethodDeclarationSyntax)member;
                    var parameters = method.ParameterList.Parameters;
                    var functionName = method.Identifier.ToString();
                    var cppReturnType = MapCSharpTypeToC(method.ReturnType.ToString());

                    if (method.ReturnType.ToString() == "string")
                    {
                        hasStringReturn = true;
                        stringBuilder.Append($"void ");
                    }

                    else if (method.ReturnType.ToString() == "IntPtr")
                    {
                        stringBuilder.Append($"void* ");
                    }

                    else
                    {
                        stringBuilder.Append($"{cppReturnType} ");
                    }

                    stringBuilder.Append($"{implementationType}{functionName}Interop(void* context");
                    var currentParameterIndex = 1;

                    foreach (var parameter in parameters)
                    {
                        if (currentParameterIndex > 0)
                        {
                            stringBuilder.Append(", ");
                        }

                        if (parameter.Type!.ToString().Contains("ReadOnlySpan<"))
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

                    if (hasStringReturn)
                    {
                        stringBuilder.Append(", char* output");
                    }

                    stringBuilder.AppendLine(")");
                    stringBuilder.AppendLine("{");
                    stringBuilder.AppendLine($"    auto contextObject = ({implementationType}*)context;");
                    stringBuilder.Append("    ");

                    if (method.ReturnType.ToString() != "void" && !hasStringReturn)
                    {
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

                        if (parameter.Type!.ToString().Contains("ReadOnlySpan<"))
                        {
                            stringBuilder.Append($"{parameter.Identifier}, {parameter.Identifier}Length");
                        }

                        else
                        {
                            stringBuilder.Append($"{parameter.Identifier}");
                        }

                        currentParameterIndex++;
                    }

                    if (hasStringReturn)
                    {
                        if (currentParameterIndex > 0)
                        {
                            stringBuilder.Append(", ");
                        }

                        stringBuilder.Append("output");
                    }

                    stringBuilder.AppendLine(");");
                    stringBuilder.AppendLine("}");
                    stringBuilder.AppendLine();
                }
            }

            stringBuilder.AppendLine($"void Init{implementationType}(const {implementationType}* context, {interfaceNode.Identifier.ToString().Substring(1)}* service)");
            stringBuilder.AppendLine("{");
            stringBuilder.AppendLine("    service->Context = (void*)context;");

            foreach (var member in interfaceNode.Members)
            {
                if (member.Kind() == SyntaxKind.MethodDeclaration)
                {
                    var method = (MethodDeclarationSyntax)member;
                    var parameters = method.ParameterList.Parameters;
                    var functionName = method.Identifier.ToString();

                    stringBuilder.AppendLine($"    service->{interfaceNode.Identifier.ToString().Substring(1)}_{method.Identifier} = {implementationType}{functionName}Interop;");
                }
            }

            stringBuilder.AppendLine("}");

            return stringBuilder.ToString();
        }

        private static string MapCSharpTypeToC(string typeName)
        {
            var result = typeName;

            if (typeName == "uint")
            {
                result = "unsigned int";
            }

            else if (typeName == "ulong")
            {
                result = "unsigned long";
            }

            else if (typeName == "bool")
            {
                result = "int";
            }

            else if (typeName == "byte")
            {
                result = "void";
            }

            else if (typeName == "IntPtr")
            {
                result = "void*";
            }

            else if (typeName == "string" || typeName == "string?")
            {
                result = "char*";
            }

            else if (typeName.Last() == '?')
            {
                return $"Nullable{typeName[0..^1]}";
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
                "void", "bool", "byte", "short", "IntPtr", "ushort", "int", "uint", "ulong", "float", "double", "char", "string", "string?"
            };

            return builtInTypes.Contains(typeName);
        }
    }
}