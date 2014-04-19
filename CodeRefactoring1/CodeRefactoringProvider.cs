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
            return syntaxNode == null ? null :  new[] { GetAction(document, syntaxNode) };
        }

        private async Task<T> GetCurrentNode<T>(Document document, TextSpan textSpan, CancellationToken cancellationToken) where T : class
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return root.FindNode(textSpan) as T;
        }

        private CodeAction GetAction(Document document, ExpressionSyntax expression)
        {
            return CodeAction.Create("Extract field", c => ExtractField(GetNewFieldName(expression.GetText().ToString()), expression, document, c));
        }

        private async Task<Document> ExtractField(string fieldName, ExpressionSyntax expression, Document document, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var newField = CreateFieldFromExpression(fieldName, expression, semanticModel);

            var originalSolution = document.Project.Solution;
            INamedTypeSymbol classTypeSymbol = GetClassTypeSymbol(expression, semanticModel);
            var expressionReplaces = new ExpressionReplacer(semanticModel.SyntaxTree, document, semanticModel);
            var withReplacement = expressionReplaces.WithReplacementNode(expression, fieldName, cancellationToken);
            var documentWithReplacement = document.WithSyntaxRoot(withReplacement);
            return await CodeGenerator.AddFieldDeclarationAsync(documentWithReplacement.Project.Solution, classTypeSymbol, newField).ConfigureAwait(false);
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

    public class ExpressionReplacer
    {
        private SyntaxTree syntaxTree;
        private Document document;
        private SemanticModel semanticModel;

        public ExpressionReplacer(SyntaxTree syntaxTree, Document document, SemanticModel semanticModel)
        {
            this.syntaxTree = syntaxTree;
            this.document = document;
            this.semanticModel = semanticModel;
        }

        public SyntaxNode WithReplacementNode(ExpressionSyntax binaryExpression, string replaceWith, CancellationToken cancellationToken)
        {
            return syntaxTree.GetRoot().ReplaceNode(binaryExpression, GetNewNode(replaceWith));
        }

        private SyntaxNode GetNewNode(string replaceWith)
        {
            return SyntaxFactory.ParseExpression(replaceWith);
        }
    }
}