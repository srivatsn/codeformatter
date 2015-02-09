// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using System.Reflection;

namespace Microsoft.DotNet.CodeFormatting
{
    [Export(typeof(IFormattingEngine))]
    internal sealed class FormattingEngineImplementation : IFormattingEngine
    {
        internal const string TablePreprocessorSymbolName = "DOTNET_FORMATTER";

        private readonly Options _options;
        private readonly IEnumerable<IFormattingFilter> _filters;
        private readonly IEnumerable<DiagnosticAnalyzer> _analyzers;
        private readonly IEnumerable<CodeFixProvider> _fixers;
        private readonly Stopwatch _watch = new Stopwatch();
        private readonly ImmutableDictionary<string, CodeFixProvider> _fixerMap;

        public ImmutableArray<string> CopyrightHeader
        {
            get { return _options.CopyrightHeader; }
            set { _options.CopyrightHeader = value; }
        }

        public ImmutableArray<string[]> PreprocessorConfigurations
        {
            get { return _options.PreprocessorConfigurations; }
            set { _options.PreprocessorConfigurations = value; }
        }

        public ImmutableArray<string> FileNames
        {
            get { return _options.FileNames; }
            set { _options.FileNames = value; }
        }

        public IFormatLogger FormatLogger
        {
            get { return _options.FormatLogger; }
            set { _options.FormatLogger = value; }
        }

        [ImportingConstructor]
        public FormattingEngineImplementation(
            Options options,
            [ImportMany] IEnumerable<IFormattingFilter> filters,
            [ImportMany] IEnumerable<DiagnosticAnalyzer> analyzers,
            [ImportMany] IEnumerable<CodeFixProvider> fixers)
        {
            _options = options;
            _filters = filters;
            _analyzers = analyzers;
            _fixers = fixers;

            var fixerMap = ImmutableDictionary.CreateBuilder<string, CodeFixProvider>();

            foreach (var fixer in _fixers)
            {
                var supportedDiagnosticIds = fixer.GetFixableDiagnosticIds();

                foreach (var id in supportedDiagnosticIds)
                {
                    fixerMap.Add(id, fixer);
                }
            }
            _fixerMap = fixerMap.ToImmutable();
        }

        public async Task FormatSolutionAsync(Solution solution, CancellationToken cancellationToken)
        {
            foreach (var project in solution.Projects)
            {
                await FormatProjectAsync(project, cancellationToken);
            }
        }

        private FixAllContext CreateFixAllContext(
            Project project,
            CodeFixProvider codeFixProvider,
            FixAllScope scope,
            string codeActionId,
            IEnumerable<string> diagnosticIds,
            Func<Project, Document, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> getDocumentDiagnosticsAsync,
            CancellationToken cancellationToken)
        {
            var ctor = typeof(FixAllContext).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];

            return (FixAllContext) ctor.Invoke(new object[] { project.Documents.First(), codeFixProvider, scope, codeActionId, diagnosticIds, getDocumentDiagnosticsAsync, cancellationToken});
        }

        public async Task FormatProjectAsync(Project project, CancellationToken cancellationToken)
        {
            var watch = new Stopwatch();
            watch.Start();

            var solution = AddTablePreprocessorSymbol(project.Solution);
            project = solution.GetProject(project.Id);
            var diagnostics = await GetDiagnostics(project, cancellationToken);

            var batchFixer = WellKnownFixAllProviders.BatchFixer;

            var context = CreateFixAllContext(project,
                                              new UberCodeFixer(_fixerMap),
                                              FixAllScope.Project, 
                                              null, 
                                              diagnostics.Select(d=>d.Id),
                                              (projcet, doc, dids, ct) =>
                                              {
                                                  return Task.FromResult(diagnostics.Where(d => d.Location.SourceTree.FilePath == doc.FilePath));
                                              },
                                              cancellationToken);
            var fix = await batchFixer.GetFixAsync(context);
            foreach (var operation in await fix.GetOperationsAsync(cancellationToken))
            {
                operation.Apply(project.Solution.Workspace, cancellationToken);
            }

            watch.Stop();
            FormatLogger.WriteLine("Total time {0}", watch.Elapsed);
        }

        class UberCodeFixer : CodeFixProvider
        {
            private ImmutableDictionary<string, CodeFixProvider> _fixerMap;
            
            public UberCodeFixer(ImmutableDictionary<string, CodeFixProvider> fixerMap)
            {
                _fixerMap = fixerMap;
            }

            public override async Task ComputeFixesAsync(CodeFixContext context)
            {
                foreach (var diagnostic in context.Diagnostics)
                {
                    var fixer = _fixerMap[diagnostic.Id];
                    await fixer.ComputeFixesAsync(new CodeFixContext(context.Document, diagnostic, (a, d) => context.RegisterFix(a, d), context.CancellationToken));
                }
            }

            public override FixAllProvider GetFixAllProvider()
            {
                return null;
            }

            public override ImmutableArray<string> GetFixableDiagnosticIds()
            {
                return ImmutableArray<string>.Empty;
            }
        }

        private async Task<ImmutableArray<Diagnostic>> GetDiagnostics(Project project, CancellationToken cancellationToken)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            var driver = AnalyzerDriver.Create(compilation, _analyzers.ToImmutableArray(), null, out compilation, cancellationToken);
            compilation.GetDiagnostics(cancellationToken);
            return await driver.GetDiagnosticsAsync();
        }

        internal Solution AddTablePreprocessorSymbol(Solution solution)
        {
            var projectIds = solution.ProjectIds;
            foreach (var projectId in projectIds)
            {
                var project = solution.GetProject(projectId);
                var parseOptions = project.ParseOptions as CSharpParseOptions;
                if (parseOptions != null)
                {
                    var list = new List<string>();
                    list.AddRange(parseOptions.PreprocessorSymbolNames);
                    list.Add(TablePreprocessorSymbolName);
                    parseOptions = parseOptions.WithPreprocessorSymbols(list);
                    solution = project.WithParseOptions(parseOptions).Solution;
                }
            }

            return solution;
        }

        private bool ShouldBeProcessed(Document document)
        {
            foreach (var filter in _filters)
            {
                var shouldBeProcessed = filter.ShouldBeProcessed(document);
                if (!shouldBeProcessed)
                    return false;
            }

            return true;
        }

        private void StartDocument()
        {
            _watch.Restart();
        }

        private void EndDocument(Document document)
        {
            _watch.Stop();
            FormatLogger.WriteLine("    {0} {1} seconds", document.Name, _watch.Elapsed.TotalSeconds);
        }
    }
}
