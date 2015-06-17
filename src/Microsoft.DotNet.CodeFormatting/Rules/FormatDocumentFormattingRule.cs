// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [LocalSemanticRuleOrder(LocalSemanticRuleOrder.IsFormattedFormattingRule)]
    internal sealed class FormatDocumentFormattingRule : ILocalSemanticFormattingRule
    {
        private readonly Options _options;

        [ImportingConstructor]
        internal FormatDocumentFormattingRule(Options options)
        {
            _options = options;
        }

        public async Task<SyntaxNode> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            document = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);

            if (!_options.PreprocessorConfigurations.IsDefaultOrEmpty)
            {
                var project = document.Project;
                var parseOptions = (CSharpParseOptions)document.Project.ParseOptions;
                foreach (var configuration in _options.PreprocessorConfigurations)
                {
                    var list = new List<string>(configuration.Length + 1);
                    list.AddRange(configuration);

                    var newParseOptions = parseOptions.WithPreprocessorSymbols(list);
                    document = project.WithParseOptions(newParseOptions).GetDocument(document.Id);
                    document = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
                }
            }

            return await document.GetSyntaxRootAsync(cancellationToken);
        }
    }
}