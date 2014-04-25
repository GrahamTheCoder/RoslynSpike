using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.FindSymbols.Finders;
using Microsoft.CodeAnalysis.CSharp.CodeGeneration;
using System;

namespace CodeRefactoring1
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class CodeRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = "CodeRefactoring1";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            var syntaxNode = await GetCurrentNode<ExpressionSyntax>(document, textSpan, cancellationToken);
            if (syntaxNode == null) return null;
            return GetRefactoringActions(document, syntaxNode);
        }

        private IEnumerable<CodeAction> GetRefactoringActions(Document document, ExpressionSyntax syntaxNode)
        {
            yield return ExtractFieldAction(document, syntaxNode);
            if (GetEnclosingMethod(syntaxNode) != null) yield return ExtractParameterAction(document, syntaxNode);
        }

        private async Task<T> GetCurrentNode<T>(Document document, TextSpan textSpan, CancellationToken cancellationToken) where T : class
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return root.FindNode(textSpan) as T;
        }

        private CodeAction ExtractFieldAction(Document document, ExpressionSyntax expression)
        {
            return CodeAction.Create("Extract field", c => ExtractField(GetValidIdentifierFromExpression(expression.GetText().ToString()), expression, document, c));
        }

        private async Task<Document> ExtractField(string fieldName, ExpressionSyntax expression, Document document, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var documentWithReplacement = ReplaceExpressionWithText(fieldName, expression, document, cancellationToken, semanticModel);
            return await WithFieldDeclarationAsync(fieldName, expression, cancellationToken, semanticModel, documentWithReplacement);
        }

        private async Task<Document> WithFieldDeclarationAsync(string fieldName, ExpressionSyntax expression,
            CancellationToken cancellationToken, SemanticModel semanticModel, Document documentWithReplacement)
        {
            var newField = CreateFieldFromExpression(fieldName, expression, semanticModel);
            INamedTypeSymbol classTypeSymbol = GetClassTypeSymbol(expression, semanticModel);
            var addFieldTask = CodeGenerator.AddFieldDeclarationAsync(documentWithReplacement.Project.Solution, classTypeSymbol, newField,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return await addFieldTask;
        }

        private IFieldSymbol CreateFieldFromExpression(string fieldName, ExpressionSyntax expression, SemanticModel semanticModel)
        {
            var expressionTypeInfo = semanticModel.GetTypeInfo(expression);
            IFieldSymbol newField = CodeGenerationSymbolFactory.CreateFieldSymbol(new List<AttributeData>(), Accessibility.Private, new SymbolModifiers(), expressionTypeInfo.Type, fieldName, initializer: expression);
            return newField;
        }

        private CodeAction ExtractParameterAction(Document document, ExpressionSyntax expression)
        {
            return CodeAction.Create("Extract parameter", c => ExtractParameter(GetValidIdentifierFromExpression(expression.GetText().ToString()), expression, document, c));
        }
        
        private async Task<Document> ExtractParameter(string newParameterName, ExpressionSyntax expression, Document document, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var documentWithReplacement = ReplaceExpressionWithText(newParameterName, expression, document, cancellationToken, semanticModel);
            return await WithParameterDeclarationAsync(newParameterName, expression, cancellationToken, semanticModel, documentWithReplacement);
        }

        private async Task<Document> WithParameterDeclarationAsync(string newParameterName, ExpressionSyntax expression, CancellationToken cancellationToken, SemanticModel semanticModel, Document document)
        {
            var containingMethod = GetEnclosingMethod(expression);
            var doc2 = await GetDocumentWithUpdatedCallers(cancellationToken, semanticModel, document, containingMethod);
            var withAddedParameter = GetReplacementMethodDeclaration(containingMethod, newParameterName, expression, semanticModel, document);
            var expressionReplaces = new ExpressionReplacer(semanticModel.SyntaxTree, document, semanticModel);
            var withReplacement = expressionReplaces.WithReplacementNode(expression, withAddedParameter);
            return document.WithSyntaxRoot(withReplacement);
        }

        private static async Task<Document> GetDocumentWithUpdatedCallers(CancellationToken cancellationToken, SemanticModel semanticModel,
            Document document, MethodDeclarationSyntax containingMethod)
        {
            var references = await GetReferencesToMethod(semanticModel, document, cancellationToken, containingMethod);
            foreach (var callReference in references.Select(s => s.CallingSymbol))
            {
                    foreach (var location in callReference.Locations)
                    {
                        var position = location.SourceSpan;
                        var root = await location.SourceTree.GetRootAsync(cancellationToken);
                        var methodCallNode = root.FindNode(position);
                        var methodCallWithAddedParameter = methodCallNode; //TODO: Update parameters passed to include the extracted expression
                        root.ReplaceNode(methodCallNode, methodCallWithAddedParameter);
                    }

            }
            return document;
        }

        private MethodDeclarationSyntax GetReplacementMethodDeclaration(MethodDeclarationSyntax containingMethod, string newParameterName, ExpressionSyntax expression, SemanticModel semanticModel, Document document)
        {
            var expressionTypeInfo = semanticModel.GetTypeInfo(expression);
            var parameterSymbol = CodeGenerationSymbolFactory.CreateParameterSymbol(new List<AttributeData>(), RefKind.None,
                ShouldUseParamsForExtractedExpression(expression), expressionTypeInfo.Type, newParameterName);
            var withAddedParameter = CodeGenerator.AddParameterDeclarations(containingMethod, new[] {parameterSymbol},
                document.Project.Solution.Workspace);
            return withAddedParameter;
        }

        private static async Task<IEnumerable<SymbolCallerInfo>> GetReferencesToMethod(SemanticModel semanticModel, Document document,
            CancellationToken cancellationToken, MethodDeclarationSyntax containingMethod)
        {
            var methodSymbol = (IMethodSymbol) semanticModel.GetSymbolInfo(containingMethod).Symbol;

            return await
                    SymbolFinder.FindCallersAsync(methodSymbol, document.Project.Solution, cancellationToken);


        }

        private bool ShouldUseParamsForExtractedExpression(ExpressionSyntax expression)
        {
            return false;
        }

        private MethodDeclarationSyntax GetEnclosingMethod(ExpressionSyntax expression)
        {
            var containingMethods = expression.AncestorsAndSelf().OfType<MethodDeclarationSyntax>();
            return containingMethods.SingleOrDefault();
        }


        private static Document ReplaceExpressionWithText(string replacementCSharp, ExpressionSyntax expression, Document document,
            CancellationToken cancellationToken, SemanticModel semanticModel)
        {
            var expressionReplaces = new ExpressionReplacer(semanticModel.SyntaxTree, document, semanticModel);
            var withReplacement = expressionReplaces.WithReplacementNode(expression, replacementCSharp);
            return document.WithSyntaxRoot(withReplacement);
        }

        private static INamedTypeSymbol GetClassTypeSymbol(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            return semanticModel.GetEnclosingSymbol(expression.SpanStart).ContainingType;
        }

        private static string GetValidIdentifierFromExpression(string expressionText)
        {
            var expressionTextName = new String(expressionText.ToLower().Where(char.IsLetter).ToArray());
            return expressionTextName.Any() ? expressionTextName : "newField";
        }
    }
}