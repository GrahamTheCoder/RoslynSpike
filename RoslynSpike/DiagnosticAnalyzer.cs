using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Diagnostic1
{
    // TODO: Consider implementing other interfaces that implement IDiagnosticAnalyzer instead of or in addition to ISymbolAnalyzer

    [DiagnosticAnalyzer]
    //[ExportDiagnosticAnalyzer(DiagnosticId, LanguageNames.CSharp)]
    public class DiagnosticAnalyzer : ISymbolAnalyzer
    {
        internal const string DiagnosticId = "Diagnoser";
        internal const string Description = "Class name does not match file name";
        internal const string MessageFormat = "Type name '{0}' does not match filename '{1}'";
        internal const string Category = "Naming";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, DiagnosticSeverity.Warning, true);

        public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public ImmutableArray<SymbolKind> SymbolKindsOfInterest { get { return ImmutableArray.Create(SymbolKind.NamedType); } }
        
        public void AnalyzeSymbol(ISymbol symbol, Compilation compilation, Action<Diagnostic> addDiagnostic, AnalyzerOptions options, CancellationToken cancellationToken)
        {
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find

            var namedTypeSymbol = (INamedTypeSymbol)symbol;

            // Find just those named type symbols with names containing lowercase letters.
            var symbolLocations = symbol.Locations.Where(s => s.IsInSource);
            if (symbolLocations.Count() > 1) return;

            var locationFilename = Path.GetFileNameWithoutExtension(symbolLocations.Single().GetLineSpan().Path);
            if (namedTypeSymbol.Name != locationFilename)
            {
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name, locationFilename);

                addDiagnostic(diagnostic);
            }
        }
    }
}
