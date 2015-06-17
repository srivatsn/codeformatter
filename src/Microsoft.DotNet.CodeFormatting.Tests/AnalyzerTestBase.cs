using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using System.Threading;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public abstract class AnalyzerTestBase : CodeFormattingTestBase
    {
        private IFormattingEngine _engine;

        protected AnalyzerTestBase(IFormattingEngine engine)
        {
            _engine = engine;
        }

        protected override async Task<Solution> Format(Solution solution, bool runFormatter)
        {
            Workspace workspace = solution.Workspace;
            await _engine.FormatSolutionAsync(solution, default(CancellationToken)).ConfigureAwait(false);
            return workspace.CurrentSolution;
        }
    }
}
