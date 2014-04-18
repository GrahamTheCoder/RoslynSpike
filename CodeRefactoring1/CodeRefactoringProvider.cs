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

        private CodeAction GetAction(Document document, ExpressionSyntax typeDecl)
        {
            return CodeAction.Create("Help extract a field", c => ExtractField(GetNewFieldName(), typeDecl, document, c));
        }

        private async Task<Document> ExtractField(string fieldName, ExpressionSyntax expression, Document document, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var newField = CreateFieldFromExpression(fieldName, expression, semanticModel);

            var originalSolution = document.Project.Solution;
            INamedTypeSymbol classTypeSymbol = GetClassTypeSymbol(expression, semanticModel);
            return await CodeGenerator.AddFieldDeclarationAsync(originalSolution, classTypeSymbol, newField).ConfigureAwait(false);
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

        private static string GetNewFieldName()
        {
            return "myNewField";
        }
    }
}