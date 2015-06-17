using Xunit;
using System.Collections.Immutable;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    /// <summary>
    /// A test which runs all rules on a given piece of code 
    /// </summary>
    public sealed class CombinationTest : AnalyzerTestBase
    {
        private static FormattingEngineImplementation s_formattingEngine;

        static CombinationTest()
        {
            s_formattingEngine = (FormattingEngineImplementation)FormattingEngine.Create(ImmutableArray<string>.Empty);
        }

        public CombinationTest() : base(s_formattingEngine)
        {
            s_formattingEngine.CopyrightHeader = ImmutableArray.Create("", "// header");
            s_formattingEngine.FormatLogger = new EmptyFormatLogger();
            s_formattingEngine.PreprocessorConfigurations = ImmutableArray<string[]>.Empty;
        }

        [Fact(Skip = "NYI")]
        public void FieldUse()
        {
            var text = @"
class C {
    int field;

    void M() {
        N(this.field);
    }
}";

            var expected = @"
// header

internal class C
{
    private int _field;

    private void M()
    {
        N(_field);
    }
}";

            Verify(text, expected, runFormatter: false);
        }

        [Fact(Skip = "NYI")]
        public void FieldAssignment()
        {

            var text = @"
class C {
    int field;

    void M() {
        this.field = 42;
    }
}";

            var expected = @"
// header

internal class C
{
    private int _field;

    private void M()
    {
        _field = 42;
    }
}";

            Verify(text, expected, runFormatter: false);
        }

        [Fact(Skip = "NYI")]
        public void PreprocessorSymbolNotDefined()
        {
            var text = @"
class C
{
#if DOG
    void M() { } 
#endif
}";

            var expected = @"
// header

internal class C
{
#if DOG
    void M() { } 
#endif
}";

            Verify(text, expected, runFormatter: false);
        }

        [Fact(Skip = "NYI")]
        public void PreprocessorSymbolDefined()
        {
            var text = @"
internal class C
{
#if DOG
    internal void M() {
} 
#endif
}";

            var expected = @"
// header

internal class C
{
#if DOG
    internal void M()
    {
    }
#endif
}";

            s_formattingEngine.PreprocessorConfigurations = ImmutableArray.CreateRange(new[] { new[] { "DOG" } });
            Verify(text, expected, runFormatter: false);
        }

        [Fact(Skip = "NYI")]
        public void TableCode()
        {
            var text = @"
class C
{
    void G() { 

    }

#if !DOTNET_FORMATTER
    void M() {
}
#endif
}";

            var expected = @"
// header

internal class C
{
    private void G()
    {
    }

#if !DOTNET_FORMATTER
    void M() {
}
#endif
}";

            Verify(text, expected, runFormatter: false);
        }

        /// <summary>
        /// Make sure the code which deals with additional configurations respects the
        /// table exception.
        /// </summary>
        [Fact(Skip = "NYI")]
        public void TableCodeAndAdditionalConfiguration()
        {
            var text = @"
class C
{
#if TEST
    void G(){
    }
#endif

#if !DOTNET_FORMATTER
    void M() {
}
#endif
}";

            var expected = @"
// header

internal class C
{
#if TEST
    void G()
    {
    }
#endif

#if !DOTNET_FORMATTER
    void M() {
}
#endif
}";

            s_formattingEngine.PreprocessorConfigurations = ImmutableArray.CreateRange(new[] { new[] { "TEST" } });
            Verify(text, expected, runFormatter: false);
        }
    }
}
