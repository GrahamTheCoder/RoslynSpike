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
            return CodeAction.Create("Help extract a field", c => DeclareField(document, typeDecl, c));
        }

        private async Task<Document> DeclareField(Document document, ExpressionSyntax expression, CancellationToken cancellationToken)
        {
            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var expressionTypeInfo = semanticModel.GetTypeInfo(expression);
            
            INamedTypeSymbol classTypeSymbol = semanticModel.GetEnclosingSymbol(expression.SpanStart).ContainingType;
            IFieldSymbol newField = CodeGenerationSymbolFactory.CreateFieldSymbol(new List<AttributeData>(), new Accessibility(), new SymbolModifiers(), expressionTypeInfo.Type, "myNewField", initializer: expression);
            return await CodeGenerator.AddFieldDeclarationAsync(originalSolution, classTypeSymbol, newField).ConfigureAwait(false);
        }
    }
}