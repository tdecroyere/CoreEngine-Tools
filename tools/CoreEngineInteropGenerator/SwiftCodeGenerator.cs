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

                            else
                            {
                               stringBuilder.Append($"_ {parameter.Identifier}: {MapCSharpTypeToSwift(parameter.Type.ToString())}");
                            }

                            currentParameterIndex++;
                        }

                        stringBuilder.Append(")");
                       
                        if (method.ReturnType.ToString() != "void")
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

                foreach (var member in interfaceNode.Members)
                {
                    if (member.Kind() == SyntaxKind.MethodDeclaration)
                    {
                        var method = (MethodDeclarationSyntax)member;
                        var parameters = method.ParameterList.Parameters;
                        var functionName = char.ToLowerInvariant(method.Identifier.ToString()[0]) + method.Identifier.ToString().Substring(1);
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

                            else
                            {
                               stringBuilder.Append($"_ {parameter.Identifier}: {MapCSharpTypeToSwift(parameter.Type.ToString(), true)}");
                            }

                            currentParameterIndex++;
                        }

                        stringBuilder.Append(")");
                       
                        if (method.ReturnType.ToString() != "void")
                        {
                            stringBuilder.Append($" -> {swiftReturnType}");
                        }

                        stringBuilder.AppendLine(" {");
                        stringBuilder.AppendLine($"    let contextObject = Unmanaged<{implementationType}>.fromOpaque(context!).takeUnretainedValue()");
                        stringBuilder.Append("    ");

                        if (method.ReturnType.ToString() != "void") {
                            
                            stringBuilder.Append("return ");

                            if (IsCastingNeededForSwiftType(swiftReturnType))
                            {
                                stringBuilder.Append($"{swiftReturnType}(");
                            }
                        }

                        stringBuilder.Append($"contextObject.{functionName}(");

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

                            else
                            {
                                if (IsCastingNeededForSwiftType(swiftParameterTypeInterop))
                                {
                                    stringBuilder.Append($"{swiftParameterType}(");
                                }
                                
                                stringBuilder.Append($"{parameter.Identifier}");

                                if (IsCastingNeededForSwiftType(swiftParameterTypeInterop))
                                {
                                    stringBuilder.Append($")");
                                }
                            }

                            currentParameterIndex++;
                        }

                        if (method.ReturnType.ToString() != "void" && IsCastingNeededForSwiftType(swiftReturnType))
                        {
                            stringBuilder.Append(")");
                        }

                        stringBuilder.AppendLine(")");
                        stringBuilder.AppendLine("}");
                        stringBuilder.AppendLine();
                    }
                }

                stringBuilder.AppendLine($"func init{interfaceNode.Identifier.ToString().Substring(1)}(_ context: {implementationType}, _ service: inout {interfaceNode.Identifier.ToString().Substring(1)}) {{");
                stringBuilder.AppendLine("    service.Context = Unmanaged.passUnretained(context).toOpaque()");
                
                foreach (var member in interfaceNode.Members)
                {
                    if (member.Kind() == SyntaxKind.MethodDeclaration)
                    {
                        var method = (MethodDeclarationSyntax)member;
                        var parameters = method.ParameterList.Parameters;
                        var functionName = char.ToLowerInvariant(method.Identifier.ToString()[0]) + method.Identifier.ToString().Substring(1);

                        stringBuilder.AppendLine($"    service.{method.Identifier} = {functionName}Interop");
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

            else if (typeName == "uint")
            {
                return "UInt" + (isInteropCode ? "32" : string.Empty);
            }

            else if (typeName == "float")
            {
                return "Float";
            }

            return typeName;
        }

        private static bool IsCastingNeededForSwiftType(string typeName)
        {
            return (typeName == "Int32" || typeName == "UInt32");
        }
    }
}