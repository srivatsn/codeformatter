using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace Microsoft.DotNet.CodeFormatting.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class ExplicitThisFixer : CodeFixProvider
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
            var memberAccessNode = root.FindNode(context.Span);

            Debug.Assert(memberAccessNode is MemberAccessExpressionSyntax);

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove 'this' qualifier",
                    c => RemoveThisQualifier(context.Document, root, memberAccessNode)),
                context.Diagnostics.First());
        }

        private Task<Document> RemoveThisQualifier(Document document, SyntaxNode root, SyntaxNode memberAccessNode)
        {
            return Task.FromResult(
                document.WithSyntaxRoot(root.ReplaceNode(memberAccessNode, memberAccessNode.WithAdditionalAnnotations(Simplifier.Annotation))));
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create("DNS0001");
    }
}
