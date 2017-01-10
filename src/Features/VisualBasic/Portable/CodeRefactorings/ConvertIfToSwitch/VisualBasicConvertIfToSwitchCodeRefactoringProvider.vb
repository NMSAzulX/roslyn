﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 9.0.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis.CodeRefactorings
Imports Microsoft.CodeAnalysis.CodeRefactorings.ConvertIfToSwitch
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.LanguageServices

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeRefactorings.ConvertIfToSwitch
    <ExportCodeRefactoringProvider(LanguageNames.VisualBasic, Name:=NameOf(VisualBasicConvertIfToSwitchCodeRefactoringProvider)), [Shared]>
    Partial Friend NotInheritable Class VisualBasicConvertIfToSwitchCodeRefactoringProvider
        Inherits AbstractConvertIfToSwitchCodeRefactoringProvider

        Protected Overrides Function CreateAnalyzer(syntaxFacts As ISyntaxFactsService, semanticModel As SemanticModel) As IAnalyzer
            Return New VisualBasicAnalyzer(syntaxFacts, semanticModel)
        End Function

        Private NotInheritable Class VisualBasicAnalyzer
            Inherits Analyzer(Of SyntaxList(Of StatementSyntax), ExecutableStatementSyntax, ExpressionSyntax)

            Public Sub New(syntaxFacts As ISyntaxFactsService, semanticModel As SemanticModel)
                MyBase.New(syntaxFacts, semanticModel)
            End Sub

            Protected Overrides ReadOnly Property Title As String
                Get
                    Return VBFeaturesResources.Convert_If_to_Select_Case
                End Get
            End Property

            Protected Overrides Function GetSwitchSectionBody(statement As SyntaxList(Of StatementSyntax)) As IEnumerable(Of SyntaxNode)
                Return statement
            End Function

            Protected Overrides Function CreatePatternFromExpression(operand As ExpressionSyntax) As IPattern
                Select Case operand.Kind
                    Case SyntaxKind.EqualsExpression,
                         SyntaxKind.GreaterThanOrEqualExpression,
                         SyntaxKind.GreaterThanExpression,
                         SyntaxKind.LessThanExpression,
                         SyntaxKind.LessThanOrEqualExpression,
                         SyntaxKind.NotEqualsExpression
                        ' Look for the form "x = 5" where x is equivalent to the switch expression.
                        ' This will turn into a simple case clause e.g. "Case 5". For other comparison
                        ' operators, we will use the form "Case Is > 5" et cetera.
                        Dim node = DirectCast(operand, BinaryExpressionSyntax)
                        Dim constant As ExpressionSyntax = Nothing
                        Dim expression As ExpressionSyntax = Nothing

                        If Not TryDetermineConstant(node.Right, node.Left, constant, expression) Then
                            Return Nothing
                        End If

                        If Not SetInitialOrIsEquivalentToSwitchExpression(expression) Then
                            Return Nothing
                        End If

                        If operand.Kind = SyntaxKind.EqualsExpression Then
                            Return New Pattern.Constant(constant)
                        End If

                        Return New Pattern.Comparison(constant, inverted:=constant Is node.Left, operatorTokenKind:=node.OperatorToken.Kind)

                    Case SyntaxKind.AndAlsoExpression,
                         SyntaxKind.AndExpression
                        ' Look for the from "x >= 1 AndAlso x <= 9" where x is equivalent to the switch expression.
                        ' This will turn into a range case clause e.g. "Case 1 To 10"
                        Dim node = DirectCast(operand, BinaryExpressionSyntax)
                        Dim left = node.Left.WalkDownParentheses
                        Dim right = node.Right.WalkDownParentheses

                        If Not IsRangeComparisonOperator(left) OrElse Not IsRangeComparisonOperator(right) Then
                            Return Nothing
                        End If

                        Dim leftComparison = DirectCast(left, BinaryExpressionSyntax)
                        Dim rightComparison = DirectCast(right, BinaryExpressionSyntax)
                        Dim leftConstant As ExpressionSyntax = Nothing
                        Dim rightConstant As ExpressionSyntax = Nothing
                        Dim leftExpression As ExpressionSyntax = Nothing
                        Dim rightExpression As ExpressionSyntax = Nothing

                        If Not TryDetermineConstant(leftComparison.Right, leftComparison.Left,
                                                    leftConstant, leftExpression) Then
                            Return Nothing
                        End If

                        If Not TryDetermineConstant(rightComparison.Right, rightComparison.Left,
                                                    rightConstant, rightExpression) Then
                            Return Nothing
                        End If

                        If Not _syntaxFacts.AreEquivalent(leftExpression, rightExpression) Then
                            Return Nothing
                        End If

                        Dim leftIsLowerBound = IsLowerBound(leftExpression, leftComparison)
                        Dim rightIsLowerBound = IsLowerBound(rightExpression, rightComparison)
                        If leftIsLowerBound = rightIsLowerBound Then
                            Return Nothing
                        End If

                        If Not SetInitialOrIsEquivalentToSwitchExpression(leftExpression) Then
                            Return Nothing
                        End If

                        Dim rangeBounds = If(leftIsLowerBound, (rightConstant, leftConstant), (leftConstant, rightConstant))
                        Return New Pattern.Range(rangeBounds)

                    Case Else
                        Return Nothing

                End Select
            End Function

            Private Shared Function IsLowerBound(expression As ExpressionSyntax, node As BinaryExpressionSyntax) As Boolean
                Return If(node.IsKind(SyntaxKind.LessThanOrEqualExpression), expression Is node.Left, expression Is node.Right)
            End Function

            Private Shared Function IsRangeComparisonOperator(node As SyntaxNode) As Boolean
                Select Case node.Kind
                    Case SyntaxKind.LessThanOrEqualExpression,
                         SyntaxKind.GreaterThanOrEqualExpression
                        Return True
                    Case Else
                        Return False
                End Select
            End Function

            Protected Overrides Iterator Function GetLogicalOrExpressionOperands(node As ExpressionSyntax) As IEnumerable(Of ExpressionSyntax)
                node = node.WalkDownParentheses
                While node.IsKind(SyntaxKind.OrElseExpression)
                    Dim binaryExpression = DirectCast(node, BinaryExpressionSyntax)
                    Yield binaryExpression.Right.WalkDownParentheses
                    node = binaryExpression.Left.WalkDownParentheses
                End While

                Yield node
            End Function

            Protected Overrides Function CanConvertIfToSwitch(ifStatement As ExecutableStatementSyntax) As Boolean
                Return TypeOf ifStatement Is SingleLineIfStatementSyntax OrElse TypeOf ifStatement Is MultiLineIfBlockSyntax
            End Function

            Protected Overrides Iterator Function GetIfElseStatementChain(node As ExecutableStatementSyntax) _
                As IEnumerable(Of (SyntaxList(Of StatementSyntax), ExpressionSyntax))
                Do
                    Dim elseBody As SyntaxList(Of StatementSyntax) ?
                    Dim singleLineIf = TryCast(node, SingleLineIfStatementSyntax)
                    If singleLineIf IsNot Nothing Then
                        Yield (singleLineIf.Statements, singleLineIf.Condition)
                        elseBody = singleLineIf.ElseClause?.Statements
                    Else
                        Dim multiLineIf = DirectCast(node, MultiLineIfBlockSyntax)
                        Yield (multiLineIf.Statements, multiLineIf.IfStatement.Condition)
                        For Each item In multiLineIf.ElseIfBlocks
                            Yield (item.Statements, item.ElseIfStatement.Condition)
                        Next
                        elseBody = multiLineIf.ElseBlock?.Statements
                    End If

                    If elseBody Is Nothing AndAlso
                        Not AnalyzeControlFlow(node.GetStatements)?.EndPointIsReachable = True Then
                        Dim nextStatement = TryCast(singleLineIf.GetNextNonEmptyStatement, ExecutableStatementSyntax)
                        If CanConvertIfToSwitch(nextStatement) Then
                            node = nextStatement
                            _numberOfSubsequentIfStatementsToRemove += 1
                            Continue Do
                        End If
                    Else
                        Yield (elseBody.GetValueOrDefault(), Nothing)
                    End If
                    Exit Do
                Loop
            End Function

            Private Function AnalyzeControlFlow(statements As SyntaxList(Of StatementSyntax)) As ControlFlowAnalysis
                Return If(statements.IsEmpty, Nothing, _semanticModel.AnalyzeControlFlow(statements.First(), statements.Last()))
            End Function
        End Class
    End Class
End Namespace