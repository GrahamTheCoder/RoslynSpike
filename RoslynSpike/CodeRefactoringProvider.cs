using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CodeGeneration;

namespace CodeRefactoring1
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class CodeRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = "CodeRefactoring1";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            var syntaxNode = await GetCurrentNode<ExpressionSyntax>(document, textSpan, cancellationToken);
            return !syntaxNode.Any() ? null :  new[] { ExtractFieldAction(document, syntaxNode.First())};
        }

        private async Task<IEnumerable<T>> GetCurrentNode<T>(Document document, TextSpan textSpan, CancellationToken cancellationToken) where T : class
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return root.FindNode(textSpan).DescendantNodesAndSelf().OfType<T>();
        }

        private CodeAction ExtractFieldAction(Document document, ExpressionSyntax expression)
        {
            var fieldName = GetNewFieldName(expression.GetText().ToString());
            return CodeAction.Create("Extract field " + fieldName, c => ExtractField(fieldName, expression, document, c));
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
            var containingType = semanticModel.GetEnclosingSymbol(expression.SpanStart).ContainingType;
            return containingType;
        }

        private IFieldSymbol CreateFieldFromExpression(string fieldName, ExpressionSyntax expression, SemanticModel semanticModel)
        {
            var expressionTypeInfo = semanticModel.GetTypeInfo(expression);
            IFieldSymbol newField = CodeGenerationSymbolFactory.CreateFieldSymbol(new List<AttributeData>(), Accessibility.Private, new SymbolModifiers(), expressionTypeInfo.Type, fieldName, initializer: expression);
            return newField;
        }

        private static string GetNewFieldName(string expressionText)
        {
            var expressionTextName = expressionText.ToLower().Where(char.IsLetter).ToArray();
            return expressionTextName.Any() ? new string(expressionTextName) : "newField";
        }
    }
}