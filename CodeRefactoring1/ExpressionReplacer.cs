using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeRefactoring1
{
    public class ExpressionReplacer
    {
        private readonly SyntaxTree syntaxTree;
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