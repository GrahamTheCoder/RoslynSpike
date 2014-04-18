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
            var typeDecl = await GetCurrentNode<ExpressionSyntax>(document, textSpan, cancellationToken);
            return typeDecl == null ? null :  new[] { GetAction(document, typeDecl) };
        }

        private async Task<T> GetCurrentNode<T>(Document document, TextSpan textSpan, CancellationToken cancellationToken) where T : class
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return root.FindNode(textSpan) as T;
        }

        private CodeAction GetAction(Document document, ExpressionSyntax typeDecl)
        {
            return CodeAction.Create("Extract variable", c => DeclareField(document, typeDecl, c));
        }

        private async Task<Document> DeclareField(Document document, ExpressionSyntax expression, CancellationToken cancellationToken)
        {
            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var declaringClass = expression.Parent.FirstAncestorOrSelf<ClassDeclarationSyntax>();

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            INamedTypeSymbol classTypeSymbol = semanticModel.GetDeclaredSymbol(declaringClass, cancellationToken);
            IFieldSymbol newField = CodeGenerationSymbolFactory.CreateFieldSymbol(new List<AttributeData>(), new Accessibility(), new SymbolModifiers(), classTypeSymbol, "myNewField", initializer: expression);
            return await CodeGenerator.AddFieldDeclarationAsync(originalSolution, classTypeSymbol, newField).ConfigureAwait(false);
        }
    }
}