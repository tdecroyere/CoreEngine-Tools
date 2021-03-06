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
    public static class SwiftCodeGenerator
    {
        public static string GenerateProtocolCode(CompilationUnitSyntax compilationUnit)
        {
            if (compilationUnit == null)
            {
                return string.Empty;
            }
            
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("import CoreEngineCommonInterop");
            stringBuilder.AppendLine();

            var interfaces = compilationUnit.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
            
            foreach (var interfaceNode in interfaces)
            {
                stringBuilder.AppendLine($"public protocol {interfaceNode.Identifier.ToString().Substring(1)}Protocol {{");

                foreach (var member in interfaceNode.Members)
                {
                    if (member.Kind() == SyntaxKind.MethodDeclaration)
                    {
                        var method = (MethodDeclarationSyntax)member;
                        var parameters = method.ParameterList.Parameters;
                        var functionName = char.ToLowerInvariant(method.Identifier.ToString()[0]) + method.Identifier.ToString().Substring(1);

                        stringBuilder.Append($"    func {functionName}(");
                        var currentParameterIndex = 0;

                        foreach (var parameter in parameters)
                        {
                            if (currentParameterIndex > 0)
                            {
                                stringBuilder.Append(", ");
                            }

                            if (parameter.Type!.ToString() == "ReadOnlySpan<byte>")
                            {
                                stringBuilder.Append($"_ {parameter.Identifier}: UnsafeMutableRawPointer, _ {parameter.Identifier}Length: Int");
                            }

                            else if (parameter.Type!.ToString().Contains("ReadOnlySpan<"))
                            {
                                var index = parameter.Type!.ToString().IndexOf("<");
                                var parameterType = parameter.Type!.ToString().Substring(index).Replace("<", string.Empty).Replace(">", string.Empty);

                                stringBuilder.Append($"_ {parameter.Identifier}: [{MapCSharpTypeToSwift(parameterType, true)}]");
                            }

                            else
                            {
                               stringBuilder.Append($"_ {parameter.Identifier}: {MapCSharpTypeToSwift(parameter.Type.ToString())}");
                            }

                            currentParameterIndex++;
                        }

                        if (method.ReturnType.ToString() == "string" || method.ReturnType.ToString() == "string?")
                        {
                            if (currentParameterIndex > 0)
                            {
                                stringBuilder.Append(", ");
                            }

                            stringBuilder.Append($"_ output: UnsafeMutablePointer<Int8>?");
                        }

                        stringBuilder.Append(")");
                       
                        if (method.ReturnType.ToString() != "void" && method.ReturnType.ToString() != "string" && method.ReturnType.ToString() != "string?")
                        {
                            stringBuilder.Append($" -> {MapCSharpTypeToSwift(method.ReturnType.ToString())}");
                        }

                        stringBuilder.AppendLine();
                    }
                }
            }
            
            stringBuilder.AppendLine("}");
            return stringBuilder.ToString();
        }

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
            stringBuilder.AppendLine("import Foundation");
            stringBuilder.AppendLine("import CoreEngineCommonInterop");
            stringBuilder.AppendLine();

            var interfaces = compilationUnit.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
            
            foreach (var interfaceNode in interfaces)
            {
                var implementationType = interfaceNode.Identifier.ToString();

                if (implementationTypes.ContainsKey(interfaceNode.Identifier.ToString()))
                {
                    implementationType = implementationTypes[interfaceNode.Identifier.ToString()];
                }

                var functionNameList = new List<string>();

                foreach (var member in interfaceNode.Members)
                {
                    if (member.Kind() == SyntaxKind.MethodDeclaration)
                    {
                        var method = (MethodDeclarationSyntax)member;
                        var parameters = method.ParameterList.Parameters;
                        var functionNameContext = char.ToLowerInvariant(method.Identifier.ToString()[0]) + method.Identifier.ToString().Substring(1);
                        var functionName = $"{interfaceNode.Identifier.ToString().Substring(1)}_{char.ToLowerInvariant(method.Identifier.ToString()[0]) + method.Identifier.ToString().Substring(1)}";

                        var functionNameOriginal = functionName;
                        var currentIndex = 0;

                        while (functionNameList.Contains(functionName))
                        {
                            functionName = functionNameOriginal + $"_{++currentIndex}";
                        }

                        functionNameList.Add(functionName);

                        var swiftReturnType = MapCSharpTypeToSwift(method.ReturnType.ToString(), true);

                        stringBuilder.Append($"func {functionName}Interop(context: UnsafeMutableRawPointer?");
                        var currentParameterIndex = 1;

                        foreach (var parameter in parameters)
                        {
                            if (currentParameterIndex > 0)
                            {
                                stringBuilder.Append(", ");
                            }

                            if (parameter.Type!.ToString() == "ReadOnlySpan<byte>")
                            {
                                stringBuilder.Append($"_ {parameter.Identifier}: UnsafeMutableRawPointer?, _ {parameter.Identifier}Length: Int32");
                            }

                            else if (parameter.Type!.ToString().Contains("ReadOnlySpan<"))
                            {
                                var index = parameter.Type!.ToString().IndexOf("<");
                                var parameterType = parameter.Type!.ToString().Substring(index).Replace("<", string.Empty).Replace(">", string.Empty);

                                stringBuilder.Append($"_ {parameter.Identifier}: UnsafeMutablePointer<{MapCSharpTypeToSwift(parameterType, true)}>?, _ {parameter.Identifier}Length: Int32");
                            }

                            else
                            {
                               stringBuilder.Append($"_ {parameter.Identifier}: {MapCSharpTypeToSwift(parameter.Type.ToString(), true)}");
                            }

                            currentParameterIndex++;
                        }

                        if (method.ReturnType.ToString() == "string" || method.ReturnType.ToString() == "string?")
                        {
                            stringBuilder.Append($", _ output: UnsafeMutablePointer<Int8>?");
                        }

                        stringBuilder.Append(")");
                       
                        if (method.ReturnType.ToString() != "void" && method.ReturnType.ToString() != "string" && method.ReturnType.ToString() != "string?")
                        {
                            stringBuilder.Append($" -> {swiftReturnType}");
                        }

                        stringBuilder.AppendLine(" {");
                        stringBuilder.AppendLine($"    let contextObject = Unmanaged<{implementationType}>.fromOpaque(context!).takeUnretainedValue()");
                        stringBuilder.Append("    ");

                        if (method.ReturnType.ToString() != "void" && method.ReturnType.ToString() != "string" && method.ReturnType.ToString() != "string?") 
                        {
                            stringBuilder.Append("return ");

                            if (IsCastingNeededForSwiftType(swiftReturnType))
                            {
                                stringBuilder.Append($"{swiftReturnType}(");

                                if (method.ReturnType.ToString() == "string")
                                {
                                    stringBuilder.Append("strdup(");
                                }
                            }
                        }

                        stringBuilder.Append($"contextObject.{functionNameContext}(");

                        currentParameterIndex = 0;

                        foreach (var parameter in parameters)
                        {
                            var swiftParameterTypeInterop = MapCSharpTypeToSwift(parameter.Type!.ToString(), true);
                            var swiftParameterType = MapCSharpTypeToSwift(parameter.Type!.ToString(), false);

                            if (currentParameterIndex > 0)
                            {
                                stringBuilder.Append(", ");
                            }

                            if (parameter.Type!.ToString() == "ReadOnlySpan<byte>")
                            {
                                stringBuilder.Append($"{parameter.Identifier}!, Int({parameter.Identifier}Length)");
                            }

                            else if (parameter.Type!.ToString().Contains("ReadOnlySpan<"))
                            {
                                stringBuilder.Append($"Array(UnsafeBufferPointer(start: {parameter.Identifier}, count: Int({parameter.Identifier}Length)))");
                            }

                            else
                            {
                                if (IsCastingNeededForSwiftType(swiftParameterTypeInterop) && swiftParameterType == "String?")
                                {
                                    stringBuilder.Append($"({parameter.Identifier} != nil) ? String(cString: ");
                                } 

                                else if (IsCastingNeededForSwiftType(swiftParameterTypeInterop) && swiftParameterType == "String")
                                {
                                    stringBuilder.Append($"String(cString: ");
                                } 
                                
                                else if (IsCastingNeededForSwiftType(swiftParameterTypeInterop))
                                {
                                    stringBuilder.Append($"{swiftParameterType}(");
                                }
                                
                                stringBuilder.Append($"{parameter.Identifier}");

                                if (IsCastingNeededForSwiftType(swiftParameterTypeInterop) && swiftParameterType == "String?")
                                {
                                    stringBuilder.Append($"!) : nil");
                                }

                                else if (IsCastingNeededForSwiftType(swiftParameterTypeInterop) && swiftParameterType == "String")
                                {
                                    stringBuilder.Append($"!)");
                                }

                                else if (IsCastingNeededForSwiftType(swiftParameterTypeInterop) && swiftParameterType == "Bool")
                                {
                                    stringBuilder.Append($" == 1)");
                                }

                                else if (IsCastingNeededForSwiftType(swiftParameterTypeInterop))
                                {
                                    stringBuilder.Append($")");
                                }
                            }

                            currentParameterIndex++;
                        }

                        if (method.ReturnType.ToString() == "string" || method.ReturnType.ToString() == "string?")
                        {
                            if (currentParameterIndex > 0)
                            {
                                stringBuilder.Append(", ");
                            }

                            stringBuilder.Append($"output");
                        }

                        if (method.ReturnType.ToString() != "void" && method.ReturnType.ToString() != "string" && method.ReturnType.ToString() != "string?" && IsCastingNeededForSwiftType(swiftReturnType))
                        {
                            stringBuilder.Append(")");
                            
                            if (method.ReturnType.ToString() == "string")
                            {
                                // TODO: We have a memory leak here, don't forget to call free
                                stringBuilder.Append(")");
                            }
                            
                            else if (method.ReturnType.ToString() == "bool")
                            {
                                stringBuilder.Append(" ? 1 : 0");
                            }
                        }

                        stringBuilder.AppendLine(")");
                        stringBuilder.AppendLine("}");
                        stringBuilder.AppendLine();
                    }
                }

                stringBuilder.AppendLine($"func init{interfaceNode.Identifier.ToString().Substring(1)}(_ context: {implementationType}, _ service: inout {interfaceNode.Identifier.ToString().Substring(1)}) {{");
                stringBuilder.AppendLine("    service.Context = Unmanaged.passUnretained(context).toOpaque()");
                
                functionNameList = new List<string>();

                foreach (var member in interfaceNode.Members)
                {
                    if (member.Kind() == SyntaxKind.MethodDeclaration)
                    {
                        var method = (MethodDeclarationSyntax)member;
                        var functionNameStruct = $"{interfaceNode.Identifier.ToString().Substring(1)}_{method.Identifier.ToString()}";
                        var functionName = $"{interfaceNode.Identifier.ToString().Substring(1)}_{char.ToLowerInvariant(method.Identifier.ToString()[0]) + method.Identifier.ToString().Substring(1)}";

                        var functionNameOriginal = functionName;
                        var functionNameStructOriginal = functionNameStruct;
                        var currentIndex = 0;

                        while (functionNameList.Contains(functionName))
                        {
                            ++currentIndex;
                            functionName = functionNameOriginal + $"_{currentIndex}";
                            functionNameStruct = functionNameStructOriginal + $"_{currentIndex}";
                        }

                        functionNameList.Add(functionName);

                        stringBuilder.AppendLine($"    service.{functionNameStruct} = {functionName}Interop");
                    }
                }

                stringBuilder.AppendLine("}");
            }
            
            return stringBuilder.ToString();
        }

        private static string MapCSharpTypeToSwift(string typeName, bool isInteropCode = false)
        {
            if (typeName == "int")
            {
                return "Int" + (isInteropCode ? "32" : string.Empty);
            }

            else if (typeName == "IntPtr")
            {
                return "UnsafeMutableRawPointer?";
            }

            else if (typeName == "uint")
            {
                return "UInt" + (isInteropCode ? "32" : string.Empty);
            }

            else if (typeName == "ulong")
            {
                return "UInt";
            }

            else if (typeName == "float")
            {
                return "Float";
            }

            else if (typeName == "bool")
            {
                return isInteropCode ? "Int32" : "Bool";
            }

            else if (typeName == "string")
            {
                return isInteropCode ? "UnsafeMutablePointer<Int8>?" : "String";
            }

            else if (typeName == "string?")
            {
                return isInteropCode ? "UnsafeMutablePointer<Int8>?" : "String?";
            }

            else if (typeName.Last() == '?')
            {
                return $"Nullable{typeName[0..^1]}";
            }

            return typeName;
        }

        private static bool IsCastingNeededForSwiftType(string typeName)
        {
            return typeName == "Int32" || typeName == "UInt32" || typeName == "UnsafeMutablePointer<Int8>?" || typeName == "UnsafeMutablePointer<Int8>?";
        }
    }
}