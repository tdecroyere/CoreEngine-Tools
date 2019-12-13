using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

            var interfaces = compilationUnit.DescendantNodes().OfType<InterfaceDeclarationSyntax>();

            var generatedCompilationUnit = SyntaxFactory.CompilationUnit();
            generatedCompilationUnit = generatedCompilationUnit.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")));
            generatedCompilationUnit = generatedCompilationUnit.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Numerics")));

            var generatedNamespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("CoreEngine.HostServices.Interop"));
            var delegateNameList = new List<string>();

            foreach (var interfaceNode in interfaces)
            {
                var generatedStruct = SyntaxFactory.StructDeclaration(interfaceNode.Identifier.Text.Substring(1))
                                                   .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                                                   .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(interfaceNode.Identifier.Text)));

                // Generate context field
                var generatedProperty = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName("IntPtr"), SyntaxFactory.ParseToken("context"))
                                                     .WithAccessorList(SyntaxFactory.AccessorList(new SyntaxList<AccessorDeclarationSyntax>(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.ParseToken(";")))))
                                                     .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

                generatedStruct = generatedStruct.AddMembers(generatedProperty);

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

                        delegateNameList.Add(delegateTypeName);

                        var delegateVariableName = char.ToLowerInvariant(delegateTypeName[0]) + delegateTypeName.Substring(1);

                        // Generate delegate
                        var delegateParameters = new List<ParameterSyntax>();
                        delegateParameters.Insert(0, SyntaxFactory.Parameter(SyntaxFactory.ParseToken("context"))
                                                                                   .WithType(SyntaxFactory.ParseTypeName("IntPtr")));

                        foreach (var parameter in parameters)
                        {
                            if (parameter.Type!.ToString() == "ReadOnlySpan<byte>")
                            {
                                var bytePointerParameter = parameter.WithType(SyntaxFactory.ParseTypeName("byte*"));
                                delegateParameters.Add(bytePointerParameter);
                                delegateParameters.Add(SyntaxFactory.Parameter(SyntaxFactory.ParseToken($"{parameter.Identifier}Length")).WithType(SyntaxFactory.ParseTypeName("int")));
                            }

                            else if (parameter.Type!.ToString().Contains("ReadOnlySpan<"))
                            {
                                var index = parameter.Type!.ToString().IndexOf("<");
                                var parameterType = parameter.Type!.ToString().Substring(index).Replace("<", string.Empty).Replace(">", string.Empty);

                                var bytePointerParameter = parameter.WithType(SyntaxFactory.ParseTypeName($"{parameterType}*"));

                                delegateParameters.Add(bytePointerParameter);
                                delegateParameters.Add(SyntaxFactory.Parameter(SyntaxFactory.ParseToken($"{parameter.Identifier}Length")).WithType(SyntaxFactory.ParseTypeName("int")));
                            }

                            else
                            {
                                delegateParameters.Add(parameter);
                            }
                        }

                        var generatedDelegate = SyntaxFactory.DelegateDeclaration(method.ReturnType, delegateTypeName)
                                                             .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.UnsafeKeyword))
                                                             .AddParameterListParameters(delegateParameters.ToArray());

                        generatedNamespace = generatedNamespace.AddMembers(generatedDelegate);

                        // Generate struct field
                        generatedProperty = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(delegateTypeName), SyntaxFactory.ParseToken(delegateVariableName))
                                                         .WithAccessorList(SyntaxFactory.AccessorList(new SyntaxList<AccessorDeclarationSyntax>(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.ParseToken(";")))))
                                                         .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

                        generatedStruct = generatedStruct.AddMembers(generatedProperty);

                        // Generate struct method
                        var argumentList = new List<ArgumentSyntax>();
                        argumentList.Add(SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.ThisExpression(), SyntaxFactory.IdentifierName("context"))));
                        
                        var currentParameterIndex = 1;

                        foreach (var parameter in parameters)
                        {
                            if (parameter.Type!.ToString() == "ReadOnlySpan<byte>")
                            {
                                argumentList.Add(SyntaxFactory.Argument(SyntaxFactory.ParseExpression($"{parameter.Identifier.Text}Pinned")));
                                argumentList.Insert(++currentParameterIndex, SyntaxFactory.Argument(SyntaxFactory.ParseExpression($"{parameter.Identifier.Text}.Length")));
                            }

                            else if (parameter.Type!.ToString().Contains("ReadOnlySpan<"))
                            {
                                argumentList.Add(SyntaxFactory.Argument(SyntaxFactory.ParseExpression($"{parameter.Identifier.Text}Pinned")));
                                argumentList.Insert(++currentParameterIndex, SyntaxFactory.Argument(SyntaxFactory.ParseExpression($"{parameter.Identifier.Text}.Length")));
                            }

                            else
                            {
                                argumentList.Add(SyntaxFactory.Argument(SyntaxFactory.ParseExpression(parameter.Identifier.Text)));
                            }

                            currentParameterIndex++;
                        }

                        var generatedArgumentList = SyntaxFactory.SeparatedList(argumentList);

                        var accessExpression = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.ThisExpression(), SyntaxFactory.IdentifierName(delegateVariableName));
                        var invocationExpression = SyntaxFactory.InvocationExpression(accessExpression)
                                                                .WithArgumentList(SyntaxFactory.ArgumentList(generatedArgumentList));

                        var methodBody = SyntaxFactory.ExpressionStatement(invocationExpression) as StatementSyntax;

                        if (method.ReturnType.ToString() != "void")
                        {
                            methodBody = SyntaxFactory.ReturnStatement(invocationExpression);
                        }

                        var variablesToPin = parameters.Where(item => item.Type!.ToString().Contains("ReadOnlySpan<"));

                        foreach (var variableToPin in variablesToPin)
                        {
                            var variableType = "byte*";

                            if (variableToPin.Type!.ToString() != "ReadOnlySpan<byte>")
                            {
                                var index = variableToPin.Type!.ToString().IndexOf("<");
                                variableType = variableToPin.Type!.ToString().Substring(index).Replace("<", string.Empty).Replace(">", string.Empty) + "*";
                            }

                            var pinnedVariableDeclaration = SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(variableType))
                                                                         .AddVariables(SyntaxFactory.VariableDeclarator($"{variableToPin.Identifier.Text}Pinned")
                                                                                                    .WithInitializer(SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(variableToPin.Identifier.Text))));
                            
                            methodBody = SyntaxFactory.FixedStatement(pinnedVariableDeclaration, methodBody);
                        }

                        ElseClauseSyntax? elseClauseSyntax = SyntaxFactory.ElseClause(SyntaxFactory.ParseStatement($"return default({method.ReturnType});"));
                        
                        if (method.ReturnType.ToString() == "void")
                        {
                            elseClauseSyntax = null;
                        }

                        methodBody = SyntaxFactory.IfStatement(SyntaxFactory.ParseExpression($"this.context != null && this.{delegateVariableName} != null"), methodBody, elseClauseSyntax);

                        var generatedMethod = SyntaxFactory.MethodDeclaration(method.ReturnType, method.Identifier)
                                                           .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.UnsafeKeyword))
                                                           .AddParameterListParameters(parameters.ToArray())
                                                           .WithBody(SyntaxFactory.Block(methodBody));

                        generatedStruct = generatedStruct.AddMembers(generatedMethod);
                    }
                }
             
                generatedNamespace = generatedNamespace.AddMembers(generatedStruct);
            }

            generatedCompilationUnit = generatedCompilationUnit.AddMembers(generatedNamespace);
            
            var output = generatedCompilationUnit
                .NormalizeWhitespace()
                .ToFullString();

            return output;
        }
    }
}