// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.DotNet.CodeFormatting
{
    [Export(typeof(IFormattingEngine))]
    internal sealed class FormattingEngineImplementation : IFormattingEngine
    {
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
                var supportedDiagnosticIds = fixer.FixableDiagnosticIds;

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

        public async Task FormatProjectAsync(Project project, CancellationToken cancellationToken)
        {
            var watch = new Stopwatch();
            watch.Start();

            var diagnostics = await GetDiagnostics(project, cancellationToken);

            var batchFixer = WellKnownFixAllProviders.BatchFixer;

            var context = new FixAllContext(
                project.Documents.First(),
                new UberCodeFixer(_fixerMap),
                FixAllScope.Project, 
                null, 
                diagnostics.Select(d=>d.Id),
                new FormattingEngineDiagnosticProvider(project, diagnostics),
                cancellationToken);

            var fix = await batchFixer.GetFixAsync(context);
            if (fix != null)
            {
                foreach (var operation in await fix.GetOperationsAsync(cancellationToken))
                {
                    operation.Apply(project.Solution.Workspace, cancellationToken);
                }
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

            public override async Task RegisterCodeFixesAsync(CodeFixContext context)
            {
                foreach (var diagnostic in context.Diagnostics)
                {
                    var fixer = _fixerMap[diagnostic.Id];
                    await fixer.RegisterCodeFixesAsync(new CodeFixContext(context.Document, diagnostic, (a, d) => context.RegisterCodeFix(a, d), context.CancellationToken));
                }
            }

            public override FixAllProvider GetFixAllProvider()
            {
                return null;
            }

            public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray<string>.Empty;
        }

        private async Task<ImmutableArray<Diagnostic>> GetDiagnostics(Project project, CancellationToken cancellationToken)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            var compilationWithAnalyzers = compilation.WithAnalyzers(_analyzers.ToImmutableArray());
            return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
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

        private class FormattingEngineDiagnosticProvider : FixAllContext.DiagnosticProvider
        {
            private readonly Project _project;
            private List<Diagnostic> _allDiagnostics;

            public FormattingEngineDiagnosticProvider(Project project, IEnumerable<Diagnostic> diagnostics)
            {
                _project = project;
                _allDiagnostics = new List<Diagnostic>(diagnostics);
            }

            public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                if (project == _project)
                {
                    return Task.FromResult(_allDiagnostics.Where(d => true));
                }

                return Task.FromResult(Enumerable.Empty<Diagnostic>());
            }

            public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
            {
                return Task.FromResult(_allDiagnostics.Where(d => d.Location.SourceTree.FilePath == document.FilePath));
            }

            public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                return Task.FromResult(_allDiagnostics.Where(d => d.Location == Location.None));
            }
        }
    }
}
