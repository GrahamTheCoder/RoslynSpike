using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CodeGeneration;
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
            return syntaxNode == null ? null :  new[] { ExtractFieldAction(document, syntaxNode)};
        }

        private async Task<T> GetCurrentNode<T>(Document document, TextSpan textSpan, CancellationToken cancellationToken) where T : class
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return root.FindNode(textSpan) as T;
        }

        private CodeAction ExtractFieldAction(Document document, ExpressionSyntax expression)
        {
            return CodeAction.Create("Extract field", c => ExtractField(GetNewFieldName(expression.GetText().ToString()), expression, document, c));
        }

        private async Task<Document> ExtractField(string fieldName, ExpressionSyntax expression, Document document, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var documentWithReplacement = ReplaceExpressionWithText(fieldName, expression, document, cancellationToken, semanticModel);

            var newField = CreateFieldFromExpression(fieldName, expression, semanticModel);
            INamedTypeSymbol classTypeSymbol = GetClassTypeSymbol(expression, semanticModel);
            return await CodeGenerator.AddFieldDeclarationAsync(documentWithReplacement.Project.Solution, classTypeSymbol, newField, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private static Document ReplaceExpressionWithText(string replacementCSharp, ExpressionSyntax expression, Document document,
            CancellationToken cancellationToken, SemanticModel semanticModel)
        {
            var expressionReplaces = new ExpressionReplacer(semanticModel.SyntaxTree, document, semanticModel);
            var withReplacement = expressionReplaces.WithReplacementNode(expression, replacementCSharp, cancellationToken);
            var documentWithReplacement = document.WithSyntaxRoot(withReplacement);
            return documentWithReplacement;
        }

        private static INamedTypeSymbol GetClassTypeSymbol(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            return semanticModel.GetEnclosingSymbol(expression.SpanStart).ContainingType;
        }

        private IFieldSymbol CreateFieldFromExpression(string fieldName, ExpressionSyntax expression, SemanticModel semanticModel)
        {
            var expressionTypeInfo = semanticModel.GetTypeInfo(expression);
            IFieldSymbol newField = CodeGenerationSymbolFactory.CreateFieldSymbol(new List<AttributeData>(), Accessibility.Private, new SymbolModifiers(), expressionTypeInfo.Type, fieldName, initializer: expression);
            return newField;
        }

        private static string GetNewFieldName(string expressionText)
        {
            var expressionTextName = new String(expressionText.ToLower().Where(char.IsLetter).ToArray());
            return expressionTextName.Any() ? expressionTextName : "newField";
        }
    }
}