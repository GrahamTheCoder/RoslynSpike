using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace Diagnostic1
{
    [ExportCodeFixProvider(DiagnosticAnalyzer.DiagnosticId, LanguageNames.CSharp)]
    internal class CodeFixProvider : ICodeFixProvider
    {
        private const bool RoslynBug857331Fixed = true;

        public IEnumerable<string> GetFixableDiagnosticIds()
        {
            return new[] { DiagnosticAnalyzer.DiagnosticId };
        }

        public async Task<IEnumerable<CodeAction>> GetFixesAsync(Document document, TextSpan span, IEnumerable<Diagnostic> diagnostics, CancellationToken cancellationToken)
        {
            return new[] { CodeAction.Create("Move to matching filename", document.Project.RemoveDocument(document.Id).Solution) };
        }

        private async Task<Solution> MoveToMatchingFileAsync(Document document, SyntaxNode syntaxTree, TypeDeclarationSyntax declaration, CancellationToken cancellationToken)
        {
            var otherTypeDeclarationsInFile = syntaxTree.DescendantNodes().Where(originalNode => TypeDeclarationOtherThan(declaration, originalNode)).ToList();
            string newFilePath = GetNewFilePath(document, declaration);
            var newDocumentSyntaxTree = GetNewDocumentSyntaxTree(syntaxTree, otherTypeDeclarationsInFile);
            var newFile = document.Project.AddDocument(newFilePath, newDocumentSyntaxTree.GetText(), document.Folders);
            var solutionWithClassRemoved = GetDocumentWithClassDeclarationRemoved(newFile.Project, document, syntaxTree, declaration, otherTypeDeclarationsInFile);
            
            return document.Project.RemoveDocument(document.Id).Solution;
        }

        private static SyntaxNode GetNewDocumentSyntaxTree(SyntaxNode syntaxTree, List<SyntaxNode> otherTypeDeclarationsInFile)
        {
            return syntaxTree.RemoveNodes(otherTypeDeclarationsInFile, SyntaxRemoveOptions.KeepNoTrivia);
        }

        private static string GetNewFilePath(Document document, TypeDeclarationSyntax declaration)
        {
            var oldFilePath = document.FilePath;
            var oldFileDirectory = Path.GetDirectoryName(document.FilePath);
            var newFilePath = Path.Combine(oldFileDirectory, declaration.Identifier.Text + ".cs");
            return newFilePath;
        }

        private static Solution GetDocumentWithClassDeclarationRemoved(Project project, Document document, SyntaxNode syntaxTree, TypeDeclarationSyntax declaration, IEnumerable<SyntaxNode> otherTypeDeclarationsInFile)
        {
            if (otherTypeDeclarationsInFile.Any() || !RoslynBug857331Fixed)
            {
                var newSyntaxTree = syntaxTree.RemoveNode(declaration, SyntaxRemoveOptions.KeepNoTrivia);
                return document.WithSyntaxRoot(newSyntaxTree).Project.Solution;
            }
            else
            {
                var emptyDocumentId = document.Id;
                if (project.Solution.GetDocument(emptyDocumentId) != null)
                {
                    var projectWithFileRemoved = project.Solution.RemoveDocument(emptyDocumentId);
                    return projectWithFileRemoved;
                }
                return project.Solution;
            }
        }

        private static bool TypeDeclarationOtherThan(TypeDeclarationSyntax declaration, SyntaxNode originalNode)
        {
            return IsTypeDeclaration(originalNode) && originalNode != declaration;
        }

        private static bool IsTypeDeclaration(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.ClassDeclaration) || node.IsKind(SyntaxKind.EnumDeclaration);
        }
    }
}