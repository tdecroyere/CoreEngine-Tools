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
    public static class CSharpCodeGenerator
    {
        public static string GenerateCode(CompilationUnitSyntax compilationUnit)
        {
            if (compilationUnit == null)
            {
                return string.Empty;
            }

            var useFunctionPointers = true;

            var interfaces = compilationUnit.DescendantNodes().OfType<InterfaceDeclarationSyntax>();

            var nullableTypes = new List<string>();

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("using System;");
            stringBuilder.AppendLine("using System.Buffers;");
            stringBuilder.AppendLine("using System.Numerics;");

            // TODO: Find a way to get external types so we can generate interop code

            // foreach (var interfaceNode in interfaces)
            // {
            //     foreach (var member in interfaceNode.Members)
            //     {
            //         if (member.Kind() == SyntaxKind.MethodDeclaration)
            //         {
            //             var method = (MethodDeclarationSyntax)member;
            //             var parameters = method.ParameterList.Parameters;
                        
            //             for (var i = 0; i < parameters.Count; i++)
            //             {
            //                 var parameter = parameters[i];

            //                 compilationUnit.
            //             }
            //         }
            //     }
            // }

            stringBuilder.AppendLine();

            stringBuilder.AppendLine("namespace CoreEngine.HostServices.Interop");
            stringBuilder.AppendLine("{");

            var delegateNameList = new List<string>();
            var functionPointerTypes = new List<string>();

            foreach (var interfaceNode in interfaces)
            {
                // Generate delegate declarations
                foreach (var member in interfaceNode.Members)
                {
                    if (member.Kind() == SyntaxKind.MethodDeclaration)
                    {
                        var functionPointerStringBuilder = new StringBuilder();
                        bool hasStringReturn = false;

                        var method = (MethodDeclarationSyntax)member;
                        var parameters = method.ParameterList.Parameters;
                        var delegateTypeName = $"{interfaceNode.Identifier.ToString().Substring(1)}_{method.Identifier}Delegate";

                        var currentIndex = 0;
                        var delegateTypeNameOriginal = delegateTypeName;

                        while (delegateNameList.Contains(delegateTypeName))
                        {
                            delegateTypeName = delegateTypeNameOriginal + $"_{++currentIndex}";
                        }

                        delegateNameList.Add(delegateTypeName);

                        var delegateVariableName = char.ToLowerInvariant(delegateTypeName[0]) + delegateTypeName.Substring(1);
                        var returnType = method.ReturnType.ToString();

                        if (!useFunctionPointers)
                        {
                            // Generate delegate
                            IndentCode(stringBuilder, 1);
                        }
                        
                        if (!useFunctionPointers)
                        {
                            stringBuilder.Append("internal unsafe delegate ");
                        }

                        if (returnType == "string")
                        {
                            hasStringReturn = true;
                            returnType = "void";
                        }

                        else if (returnType.Last() == '?')
                        {
                            nullableTypes.Add(returnType[0..^1]);
                            returnType = $"Nullable{returnType[0..^1]}";
                        }

                        if (!useFunctionPointers)
                        {
                            stringBuilder.Append(returnType);
                            stringBuilder.Append($" {delegateTypeName}(IntPtr context");
                        }

                        // functionPointerStringBuilder.Append("delegate* unmanaged[Cdecl, SuppressGCTransition]<IntPtr, ");
                        functionPointerStringBuilder.Append("delegate* unmanaged[Cdecl]<IntPtr, ");

                        for (var i = 0; i < parameters.Count; i++)
                        {
                            var parameter = parameters[i];

                            if (!useFunctionPointers)
                            {
                                stringBuilder.Append(", ");
                            }

                            if (parameter.Type!.ToString().Contains("ReadOnlySpan<"))
                            {
                                var index = parameter.Type!.ToString().IndexOf("<");
                                var parameterType = parameter.Type!.ToString().Substring(index).Replace("<", string.Empty).Replace(">", string.Empty);
                                
                                if (!useFunctionPointers)
                                {
                                    stringBuilder.Append($"{parameterType}* {parameter.Identifier}, int {parameter.Identifier}Length");
                                }

                                functionPointerStringBuilder.Append($"{parameterType}*, int, ");
                            }

                            else
                            {
                                if (!useFunctionPointers)
                                {
                                    stringBuilder.Append($"{parameter.Type} {parameter.Identifier}");
                                }

                                functionPointerStringBuilder.Append($"{parameter.Type}, ");
                            }
                        }

                        if (hasStringReturn)
                        {
                            if (!useFunctionPointers)
                            {
                                stringBuilder.Append(", ");
                                stringBuilder.Append($"byte* output");
                            }

                            functionPointerStringBuilder.Append($"byte*, ");
                        }

                        if (!useFunctionPointers)
                        {
                            stringBuilder.AppendLine(");");
                        }

                        functionPointerStringBuilder.Append($"{returnType}>");
                        functionPointerTypes.Add(functionPointerStringBuilder.ToString());
                    }
                }

                if (!useFunctionPointers)
                {
                    // Generate struct
                    stringBuilder.AppendLine();
                }

                IndentCode(stringBuilder, 1);
                stringBuilder.AppendLine($"public unsafe struct {interfaceNode.Identifier.Text.Substring(1)} : {interfaceNode.Identifier}");
                
                IndentCode(stringBuilder, 1);
                stringBuilder.AppendLine("{");

                IndentCode(stringBuilder, 2);
                stringBuilder.AppendLine("private IntPtr context { get; }");
            
                delegateNameList = new List<string>();
                var memberIndex = 0;

                foreach (var member in interfaceNode.Members)
                {
                    if (member.Kind() == SyntaxKind.MethodDeclaration)
                    {
                        var method = (MethodDeclarationSyntax)member;
                        var parameters = method.ParameterList.Parameters;
                        var delegateTypeName = $"{interfaceNode.Identifier.ToString().Substring(1)}_{method.Identifier}Delegate";

                        var delegateTypeNameOriginal = delegateTypeName;
                        var currentIndex = 0;

                        while (delegateNameList.Contains(delegateTypeName))
                        {
                            delegateTypeName = delegateTypeNameOriginal + $"_{++currentIndex}";
                        }

                        var delegateVariableName = char.ToLowerInvariant(delegateTypeName[0]) + delegateTypeName.Substring(1);
                        var functionPointerType = functionPointerTypes[memberIndex];

                        // Generate struct field
                        stringBuilder.AppendLine();

                        IndentCode(stringBuilder, 2);

                        if (!useFunctionPointers)
                        {
                            stringBuilder.AppendLine($"private {delegateTypeName} {delegateVariableName} {{ get; }}");
                        }

                        else
                        {
                            stringBuilder.AppendLine($"private {functionPointerType} {delegateVariableName} {{ get; }}");
                        }

                        // Generate struct method
                        IndentCode(stringBuilder, 2);
                        stringBuilder.Append($"public unsafe {method.ReturnType} {method.Identifier}(");

                        for (var i = 0; i < parameters.Count; i++)
                        {
                            var parameter = parameters[i];

                            if (i > 0)
                            {
                                stringBuilder.Append(", ");
                            }

                            stringBuilder.Append($"{parameter.Type} {parameter.Identifier}");
                        }

                        stringBuilder.AppendLine(")");

                        IndentCode(stringBuilder, 2);
                        stringBuilder.AppendLine("{");

                        var argumentList = new List<string>()
                        {
                            "this.context"
                        };
                        
                        var currentParameterIndex = 1;

                        foreach (var parameter in parameters)
                        {
                            if (parameter.Type!.ToString().Contains("ReadOnlySpan<"))
                            {
                                argumentList.Add($"{parameter.Identifier.Text}Pinned");
                                argumentList.Insert(++currentParameterIndex, $"{parameter.Identifier.Text}.Length");
                            }

                            else
                            {
                                argumentList.Add(parameter.Identifier.Text);
                            }

                            currentParameterIndex++;
                        }

                        var generatedArgumentList = string.Join(", ", argumentList.ToArray());

                        var currentIndentationLevel = 3;

                        if (method.ReturnType.ToString() == "string")
                        {
                            IndentCode(stringBuilder, currentIndentationLevel);
                            stringBuilder.AppendLine($"var output = ArrayPool<byte>.Shared.Rent(255);");
                        }

                        IndentCode(stringBuilder, currentIndentationLevel++);
                        stringBuilder.AppendLine($"if (this.{delegateVariableName} != null)");
                        IndentCode(stringBuilder, currentIndentationLevel - 1);
                        stringBuilder.AppendLine("{");

                        var variablesToPin = parameters.Where(item => item.Type!.ToString().Contains("ReadOnlySpan<"));

                        foreach (var variableToPin in variablesToPin)
                        {
                            var index = variableToPin.Type!.ToString().IndexOf("<");
                            var variableType = variableToPin.Type!.ToString().Substring(index).Replace("<", string.Empty).Replace(">", string.Empty);

                            IndentCode(stringBuilder, currentIndentationLevel++);
                            stringBuilder.AppendLine($"fixed ({variableType}* {variableToPin.Identifier.Text}Pinned = {variableToPin.Identifier.Text})");
                        }

                        if (method.ReturnType.ToString() != "void" && method.ReturnType.ToString() != "string")
                        {
                            if (nullableTypes.Contains(method.ReturnType.ToString()[0..^1]))
                            {
                                IndentCode(stringBuilder, currentIndentationLevel - 1);
                                stringBuilder.AppendLine("{");

                                IndentCode(stringBuilder, currentIndentationLevel);
                                stringBuilder.Append($"var returnedValue = ");
                            }

                            else
                            {
                                IndentCode(stringBuilder, currentIndentationLevel);
                                stringBuilder.Append("return ");
                            }
                        }

                        else if (method.ReturnType.ToString() == "string")
                        {
                            IndentCode(stringBuilder, currentIndentationLevel++);
                            stringBuilder.AppendLine($"fixed (byte* outputPinned = output)");

                            generatedArgumentList += ", outputPinned";
                        }

                        else
                        {
                            IndentCode(stringBuilder, currentIndentationLevel);
                        }

                        stringBuilder.Append($"this.{delegateVariableName}({generatedArgumentList});");
                        stringBuilder.AppendLine();
                        
                        if (method.ReturnType.ToString() != "void" && nullableTypes.Contains(method.ReturnType.ToString()[0..^1]))
                        {
                            IndentCode(stringBuilder, currentIndentationLevel);
                            stringBuilder.AppendLine("if (returnedValue.HasValue) return returnedValue.Value;");

                            IndentCode(stringBuilder, currentIndentationLevel - 1);
                            stringBuilder.AppendLine("}");
                        }

                        else if (method.ReturnType.ToString() == "string")
                        {
                            IndentCode(stringBuilder, currentIndentationLevel);
                            // stringBuilder.AppendLine($"var result = System.Text.Encoding.Unicode.GetString(output).TrimEnd('\0');");
                            stringBuilder.AppendLine($"var result = System.Text.Encoding.UTF8.GetString(output).TrimEnd('\0');");

                            IndentCode(stringBuilder, currentIndentationLevel);
                            stringBuilder.AppendLine($"ArrayPool<byte>.Shared.Return(output);");

                            IndentCode(stringBuilder, currentIndentationLevel);
                            stringBuilder.AppendLine($"return result;");
                        }

                        IndentCode(stringBuilder, currentIndentationLevel - 1);
                        stringBuilder.AppendLine("}");

                        if (method.ReturnType.ToString() != "void")
                        {
                            stringBuilder.AppendLine();

                            if (method.ReturnType.ToString() == "string")
                            {
                                IndentCode(stringBuilder, 3);
                                stringBuilder.AppendLine($"return string.Empty;");
                            }

                            else
                            {
                                IndentCode(stringBuilder, 3);
                                stringBuilder.AppendLine($"return default({method.ReturnType});");
                            }
                        }

                        IndentCode(stringBuilder, 2);
                        stringBuilder.AppendLine("}");
                        
                        memberIndex++;
                    }
                }

                IndentCode(stringBuilder, 1);
                stringBuilder.AppendLine("}");
            }

            foreach (var nullableType in nullableTypes)
            {
                stringBuilder.AppendLine();

                IndentCode(stringBuilder, 1);
                stringBuilder.AppendLine($"public struct Nullable{nullableType}");
                
                IndentCode(stringBuilder, 1);
                stringBuilder.AppendLine("{");

                IndentCode(stringBuilder, 2);
                stringBuilder.AppendLine("public bool HasValue { get; }");

                IndentCode(stringBuilder, 2);
                stringBuilder.AppendLine($"public {nullableType} Value {{ get; }}");

                IndentCode(stringBuilder, 1);
                stringBuilder.AppendLine("}");
            }

            stringBuilder.AppendLine("}");
            return stringBuilder.ToString();
        }

        private static void IndentCode(StringBuilder stringBuilder, int level)
        {
            for (var i = 0; i < level; i++)
            {
                stringBuilder.Append("    ");
            }
        }
    }
}