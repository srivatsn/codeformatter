﻿using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.DotNet.CodeFormatting.Analyzers
{
    [Export(typeof(DiagnosticAnalyzer))]
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExplicitThisAnalyzer : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor rule = new DiagnosticDescriptor("DNS0001",
                                                                             "Don't use explicit 'this' for private fields",
                                                                             "Don't use explicit 'this' for private fields",
                                                                             "Style",
                                                                             DiagnosticSeverity.Warning,
                                                                             true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(rule); }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(syntaxContext => 
            {
                var node = syntaxContext.Node as MemberAccessExpressionSyntax;

                if (node != null)
                {
                    if (node.Expression != null &&
                        node.Expression.CSharpKind() == SyntaxKind.ThisExpression &&
                        IsPrivateField(node, syntaxContext.SemanticModel, syntaxContext.CancellationToken))
                    {
                        syntaxContext.ReportDiagnostic(Diagnostic.Create(rule, node.GetLocation()));
                    }
                }
            }, SyntaxKind.SimpleMemberAccessExpression);
        }

        private bool IsPrivateField(MemberAccessExpressionSyntax memberSyntax, SemanticModel model, CancellationToken token)
        {
            var symbolInfo = model.GetSymbolInfo(memberSyntax, token);
            if (symbolInfo.Symbol != null && symbolInfo.Symbol.Kind == SymbolKind.Field)
            {
                var field = (IFieldSymbol)symbolInfo.Symbol;
                return field.DeclaredAccessibility == Accessibility.Private;
            }

            return false;
        }
    }
}
