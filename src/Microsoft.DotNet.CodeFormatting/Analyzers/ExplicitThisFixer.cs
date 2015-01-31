using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace Microsoft.DotNet.CodeFormatting.Analyzers
{
    public class ExplicitThisFixer : CodeFixProvider
    {
        public override async Task ComputeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
            var memberAccessNode = root.FindNode(context.Span);

            Debug.Assert(memberAccessNode is MemberAccessExpressionSyntax);

            var newDocument = context.Document.WithSyntaxRoot(root.ReplaceNode(root, memberAccessNode.WithAdditionalAnnotations(Simplifier.Annotation)));
            context.RegisterFix(CodeAction.Create("Remove 'this' qualifier", newDocument), context.Diagnostics.First());
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override ImmutableArray<string> GetFixableDiagnosticIds()
        {
            return ImmutableArray.Create("DNS0001");
        }
    }
}
