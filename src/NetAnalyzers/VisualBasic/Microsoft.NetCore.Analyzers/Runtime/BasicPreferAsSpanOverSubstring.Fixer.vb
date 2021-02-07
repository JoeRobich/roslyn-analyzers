﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Linq
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <ExportCodeFixProvider(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicPreferAsSpanOverSubstringFixer : Inherits PreferAsSpanOverSubstringFixer

        Private Protected Overrides Function ReplaceInvocationMethodName(memberInvocation As SyntaxNode, newName As String) As SyntaxNode

            Dim cast = DirectCast(memberInvocation, InvocationExpressionSyntax)
            Dim memberAccessSyntax = DirectCast(cast.Expression, MemberAccessExpressionSyntax)
            Dim newNameSyntax = SyntaxFactory.IdentifierName(newName)
            Return cast.ReplaceNode(memberAccessSyntax.Name, newNameSyntax)
        End Function

        Private Protected Overrides Function ReplaceNamedArgumentName(invocation As SyntaxNode, oldName As String, newName As String) As SyntaxNode

            Dim cast = DirectCast(invocation, InvocationExpressionSyntax)
            Dim argumentToReplace = cast.ArgumentList.Arguments.FirstOrDefault(
                Function(x)
                    If Not x.IsNamed Then Return False
                    Dim simpleArgumentSyntax = TryCast(x, SimpleArgumentSyntax)
                    If simpleArgumentSyntax Is Nothing Then Return False
                    Return simpleArgumentSyntax.NameColonEquals.Name.Identifier.ValueText = oldName
                End Function)
            If argumentToReplace Is Nothing Then Return cast

            Dim oldNameSyntax = DirectCast(argumentToReplace, SimpleArgumentSyntax).NameColonEquals.Name
            Dim newNameSyntax = SyntaxFactory.IdentifierName(newName)
            Return cast.ReplaceNode(oldNameSyntax, newNameSyntax)
        End Function
    End Class
End Namespace
