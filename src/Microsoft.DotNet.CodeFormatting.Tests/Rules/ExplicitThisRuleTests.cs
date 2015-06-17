﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
    public sealed class ExplicitThisRuleTests : LocalSemanticRuleTestBase
    {
        internal override ILocalSemanticFormattingRule Rule
        {
            get { return new Rules.ExplicitThisRule(); }
        }

        [Fact]
        public void TestFieldUse()
        {
            var text = @"
class C1
{
    int _field1;
    string _field2;
    internal string field3;

    void Use(int i) { } 

    void M()
    {
        Use(_field1);
        Use(_field2);
        Use(field3);
        Use(this._field1);
        Use(this._field2);
        Use(this.field3);
    }
}
";

            var expected = @"
class C1
{
    int _field1;
    string _field2;
    internal string field3;

    void Use(int i) { } 

    void M()
    {
        Use(_field1);
        Use(_field2);
        Use(field3);
        Use(_field1);
        Use(_field2);
        Use(this.field3);
    }
}
";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestFieldAssignment()
        {
            var text = @"
class C1
{
    int _field1;
    string _field2;
    internal string field3;

    void M()
    {
        this._field1 = 0;
        this._field2 = null;
        this.field3 = null;
    }
}
";

            var expected = @"
class C1
{
    int _field1;
    string _field2;
    internal string field3;

    void M()
    {
        _field1 = 0;
        _field2 = null;
        this.field3 = null;
    }
}
";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestFieldAssignmentWithTrivia()
        {
            var text = @"
class C1
{
    int _field;

    void M()
    {
        this. /* comment1 */ _field /* comment 2 */ = 0;
        // before comment
        this._field = 42;
        // after comment
    }
}
";

            var expected = @"
class C1
{
    int _field;

    void M()
    {
         /* comment1 */ _field /* comment 2 */ = 0;
        // before comment
        _field = 42;
        // after comment
    }
}
";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestFieldBadName()
        {
            var text = @"
class C1
{
    int _field;

    void M()
    {
        // Not a valid field access, can't reliably remove this.
        this.field1 = 0;
    }
}
";

            var expected = @"
class C1
{
    int _field;

    void M()
    {
        // Not a valid field access, can't reliably remove this.
        this.field1 = 0;
    }
}
";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestFieldNameSameAsParameterName()
        {
            var text = @"
class C1
{
    int field;

    public C1(int field)
    {
        // Can't remove this because it changes the semantics (results in
        // self-assignment).
        this.field = field;
    }
}
";

            var expected = @"
class C1
{
    int field;

    public C1(int field)
    {
        // Can't remove this because it changes the semantics (results in
        // self-assignment).
        this.field = field;
    }
}
";
            Verify(text, expected, runFormatter: false);
        }

        [Fact]
        public void TestFieldNameSameAsLocalName()
        {
            var text = @"
class C1
{
    int field;

    public void M()
    {
        int field = 2;

        // Can't remove this because it changes the semantics (modifies local
        // instead of field).
        ++this.field;
    }
}
";

            var expected = @"
class C1
{
    int field;

    public void M()
    {
        int field = 2;

        // Can't remove this because it changes the semantics (modifies local
        // instead of field).
        ++this.field;
    }
}
";
            Verify(text, expected, runFormatter: false);
        }
    }
}
