﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#pragma warning disable CA1305

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.PublicApiAnalyzers.UnitTests
{
    public class AnnotatePublicApiAnalyzerTests
    {
        #region Utilities
        private async Task VerifyCSharpAsync(string source, string shippedApiText, string unshippedApiText, params DiagnosticResult[] expected)
        {
            var test = new CSharpCodeFixTest<DeclarePublicApiAnalyzer, AnnotatePublicApiFix, XUnitVerifier>
            {
                TestState =
                {
                    Sources = { source },
                    AdditionalFiles = { },
                },
            };

            if (shippedApiText != null)
            {
                test.TestState.AdditionalFiles.Add((DeclarePublicApiAnalyzer.ShippedFileName, shippedApiText));
            }

            if (unshippedApiText != null)
            {
                test.TestState.AdditionalFiles.Add((DeclarePublicApiAnalyzer.UnshippedFileName, unshippedApiText));
            }

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }

        private async Task VerifyCSharpAdditionalFileFixAsync(string source, string oldShippedApiText, string oldUnshippedApiText, string newShippedApiText, string newUnshippedApiText)
        {
            await VerifyAdditionalFileFixAsync(source, oldShippedApiText, oldUnshippedApiText, newShippedApiText, newUnshippedApiText);
        }

        private async Task VerifyAdditionalFileFixAsync(string source, string oldShippedApiText, string oldUnshippedApiText, string newShippedApiText, string newUnshippedApiText)
        {
            var test = new CSharpCodeFixTest<DeclarePublicApiAnalyzer, AnnotatePublicApiFix, XUnitVerifier>();

            test.TestState.Sources.Add(source);
            test.TestState.AdditionalFiles.Add((DeclarePublicApiAnalyzer.ShippedFileName, oldShippedApiText));
            test.TestState.AdditionalFiles.Add((DeclarePublicApiAnalyzer.UnshippedFileName, oldUnshippedApiText));

            test.FixedState.AdditionalFiles.Add((DeclarePublicApiAnalyzer.ShippedFileName, newShippedApiText));
            test.FixedState.AdditionalFiles.Add((DeclarePublicApiAnalyzer.UnshippedFileName, newUnshippedApiText));

            await test.RunAsync();
        }
        #endregion

        #region Fix tests

        [Fact, WorkItem(4040, "https://github.com/dotnet/roslyn-analyzers/issues/4040")]
        public async Task NoObliviousWhenUnannotatedClassConstraintAsync()
        {
            var source = @"
#nullable enable
public class C<T> where T : class
{
}
";

            var shippedText = @"";
            var unshippedText = @"#nullable enable
C<T>.C() -> void
C<T>
";

            await VerifyCSharpAsync(source, shippedText, unshippedText);
        }

        [Fact, WorkItem(4040, "https://github.com/dotnet/roslyn-analyzers/issues/4040")]
        public async Task NoObliviousWhenAnnotatedClassConstraintAsync()
        {
            var source = @"
#nullable enable
public class C<T> where T : class?
{
}
";

            var shippedText = @"";
            var unshippedText = @"#nullable enable
C<T>.C() -> void
C<T>
";

            await VerifyCSharpAsync(source, shippedText, unshippedText);
        }

        [Fact]
        public async Task NoObliviousWhenAnnotatedClassConstraintMultipleFiles()
        {
            var source = @"
#nullable enable
public class C<T> where T : class?
{
}
";

            var shippedText = @"#nullable enable";
            var unshippedText1 = @"#nullable enable
C<T>.C() -> void
";
            var unshippedText2 = @"#nullable enable
C<T>
";

            var test = new CSharpCodeFixTest<DeclarePublicApiAnalyzer, AnnotatePublicApiFix, XUnitVerifier>
            {
                TestState =
                {
                    Sources = { source },
                    AdditionalFiles =
                    {
                        (DeclarePublicApiAnalyzer.ShippedFileName, shippedText),
                        (DeclarePublicApiAnalyzer.UnshippedFileName, unshippedText1),
                        (DeclarePublicApiAnalyzer.UnshippedFileNamePrefix + "test" + DeclarePublicApiAnalyzer.Extension, unshippedText2),
                    },
                },
            };

            await test.RunAsync();
        }

        [Fact, WorkItem(4040, "https://github.com/dotnet/roslyn-analyzers/issues/4040")]
        public async Task ObliviousWhenObliviousClassConstraintAsync()
        {
            var source = @"
#nullable enable
public class {|RS0041:C|}<T> // oblivious
#nullable disable
    where T : class
{
}
";

            var shippedText = @"";
            var unshippedText = @"#nullable enable
C<T>.C() -> void
~C<T>
";

            await VerifyCSharpAsync(source, shippedText, unshippedText);
        }

        [Fact, WorkItem(4040, "https://github.com/dotnet/roslyn-analyzers/issues/4040")]
        public async Task NoObliviousWhenUnannotatedReferenceTypeConstraintAsync()
        {
            var source = @"
#nullable enable
public class D { }
public class C<T> where T : D
{
}
";

            var shippedText = @"";
            var unshippedText = @"#nullable enable
C<T>.C() -> void
C<T>
D
D.D() -> void
";

            await VerifyCSharpAsync(source, shippedText, unshippedText);
        }

        [Fact, WorkItem(4040, "https://github.com/dotnet/roslyn-analyzers/issues/4040")]
        public async Task NoObliviousWhenAnnotatedReferenceTypeConstraintAsync()
        {
            var source = @"
#nullable enable
public class D { }
public class C<T> where T : D?
{
}
";

            var shippedText = @"";
            var unshippedText = @"#nullable enable
C<T>.C() -> void
C<T>
D
D.D() -> void
";

            await VerifyCSharpAsync(source, shippedText, unshippedText);
        }

        [Fact, WorkItem(4040, "https://github.com/dotnet/roslyn-analyzers/issues/4040")]
        public async Task ObliviousWhenObliviousReferenceTypeConstraintAsync()
        {
            var source = @"
#nullable enable
public class D { }

public class {|RS0041:C|}<T> // oblivious
#nullable disable
    where T : D
{
}
";

            var shippedText = @"";
            var unshippedText = @"#nullable enable
C<T>.C() -> void
~C<T>
D
D.D() -> void
";

            await VerifyCSharpAsync(source, shippedText, unshippedText);
        }

        [Fact]
        public async Task DoNotAnnotateMemberInUnannotatedUnshippedAPI_NullableAsync()
        {
            var source = @"
#nullable enable
public class C
{
    public string? {|RS0037:Field|};
}
";

            var shippedText = @"";
            var unshippedText = @"C
C.C() -> void
C.Field -> string";

            await VerifyCSharpAsync(source, shippedText, unshippedText);
        }

        [Fact]
        public async Task DoNotAnnotateMemberInUnannotatedUnshippedAPI_NonNullableAsync()
        {
            var source = @"
#nullable enable
public class C
{
    public string {|RS0037:Field2|};
}
";

            var shippedText = @"";
            var unshippedText = @"C
C.C() -> void
C.Field2 -> string";

            await VerifyCSharpAsync(source, shippedText, unshippedText);
        }

        [Fact]
        public async Task DoNotAnnotateMemberInUnannotatedShippedAPIAsync()
        {
            var source = @"
#nullable enable
public class C
{
    public string? {|RS0037:Field|};
    public string {|RS0037:Field2|};
}
";

            var shippedText = @"C
C.C() -> void
C.Field -> string
C.Field2 -> string";
            var unshippedText = @"";

            await VerifyCSharpAsync(source, shippedText, unshippedText);
        }

        [Fact]
        public async Task AnnotatedMemberInAnnotatedShippedAPIAsync()
        {
            var source = @"
#nullable enable
public class C
{
    public string? OldField;
    public string? {|RS0036:Field|};
    public string {|RS0036:Field2|};
}
";

            var shippedText = @"#nullable enable
C
C.C() -> void
C.OldField -> string?
C.Field -> string
C.Field2 -> string";

            var unshippedText = @"";

            var fixedShippedText = @"#nullable enable
C
C.C() -> void
C.OldField -> string?
C.Field -> string?
C.Field2 -> string!";

            await VerifyCSharpAdditionalFileFixAsync(source, shippedText, unshippedText, fixedShippedText, newUnshippedApiText: unshippedText);
        }

        [Fact]
        public async Task AnnotatedMemberInAnnotatedUnshippedAPI_EnabledViaUnshippedAsync()
        {
            var source = @"
#nullable enable
public class C
{
    public string? OldField;
    public string? {|RS0036:Field|};
    public string {|RS0036:Field2|};
}
";

            var unshippedText = @"#nullable enable
C
C.C() -> void
C.OldField -> string?
C.Field -> string
C.Field2 -> string";

            var shippedText = @"";

            var fixedUnshippedText = @"#nullable enable
C
C.C() -> void
C.OldField -> string?
C.Field -> string?
C.Field2 -> string!";

            await VerifyCSharpAdditionalFileFixAsync(source, shippedText, unshippedText, shippedText, fixedUnshippedText);
        }

        [Fact]
        public async Task AnnotatedMemberInAnnotatedUnshippedAPI_EnabledViaMultipleUnshippedAsync()
        {
            var source = @"
#nullable enable
public class C
{
    public string? OldField;
    public string? {|RS0036:Field|};
    public string {|RS0036:Field2|};
}
";

            var unshippedText = @"#nullable enable
C
C.C() -> void
C.OldField -> string?
C.Field -> string
C.Field2 -> string";

            var shippedText = @"";

            var fixedUnshippedText = @"#nullable enable
C
C.C() -> void
C.OldField -> string?
C.Field -> string?
C.Field2 -> string!";

            var test = new CSharpCodeFixTest<DeclarePublicApiAnalyzer, AnnotatePublicApiFix, XUnitVerifier>();

            test.TestState.Sources.Add(source);

            test.TestState.AdditionalFiles.Add((DeclarePublicApiAnalyzer.ShippedFileName, shippedText));
            test.TestState.AdditionalFiles.Add((DeclarePublicApiAnalyzer.UnshippedFileName, string.Empty));
            test.TestState.AdditionalFiles.Add((DeclarePublicApiAnalyzer.UnshippedFileNamePrefix + "test" + DeclarePublicApiAnalyzer.Extension, unshippedText));

            test.FixedState.AdditionalFiles.Add((DeclarePublicApiAnalyzer.ShippedFileName, shippedText));
            test.FixedState.AdditionalFiles.Add((DeclarePublicApiAnalyzer.UnshippedFileName, string.Empty));
            test.FixedState.AdditionalFiles.Add((DeclarePublicApiAnalyzer.UnshippedFileNamePrefix + "test" + DeclarePublicApiAnalyzer.Extension, fixedUnshippedText));

            await test.RunAsync();
        }

        [Fact]
        public async Task AnnotatedMemberInAnnotatedUnshippedAPI_EnabledViaShippedAsync()
        {
            var source = @"
#nullable enable
public class C
{
    public string? OldField;
    public string? {|RS0036:Field|};
    public string {|RS0036:Field2|};
}
";

            var shippedText = @"#nullable enable";
            var unshippedText = @"C
C.C() -> void
C.OldField -> string?
C.Field -> string
C.Field2 -> string";

            var fixedUnshippedText = @"C
C.C() -> void
C.OldField -> string?
C.Field -> string?
C.Field2 -> string!";

            await VerifyCSharpAdditionalFileFixAsync(source, shippedText, unshippedText, newShippedApiText: shippedText, fixedUnshippedText);
        }

        [Fact]
        public async Task AnnotatedMemberInAnnotatedUnshippedAPI_EnabledViaBothAsync()
        {
            var source = @"
#nullable enable
public class C
{
    public string? OldField;
    public string? {|RS0036:Field|};
    public string {|RS0036:Field2|};
}
";

            var shippedText = @"#nullable enable";
            var unshippedText = @"#nullable enable
C
C.C() -> void
C.OldField -> string?
C.Field -> string
C.Field2 -> string";

            var fixedUnshippedText = @"#nullable enable
C
C.C() -> void
C.OldField -> string?
C.Field -> string?
C.Field2 -> string!";

            await VerifyCSharpAdditionalFileFixAsync(source, shippedText, unshippedText, newShippedApiText: shippedText, fixedUnshippedText);
        }

        [Fact]
        public async Task TestAddAndRemoveMembers_CSharp_Fix_WithAddedNullability_WithoutObliviousAsync()
        {
            var source = @"
#nullable enable
public class C
{
    public string? {|RS0036:ChangedField|};
}
";
            var shippedText = $@"#nullable enable";
            var unshippedText = @"C
C.C() -> void
C.ChangedField -> string";
            var fixedUnshippedText = @"C
C.C() -> void
C.ChangedField -> string?";
            await VerifyCSharpAdditionalFileFixAsync(source, shippedText, unshippedText, newShippedApiText: shippedText, fixedUnshippedText);
        }

        [Fact]
        public async Task LegacyAPIShouldBeAnnotatedWithObliviousMarkerAsync()
        {
            var source = @"
public class C
{
    public string {|RS0036:{|RS0041:Field|}|}; // oblivious
}
";
            var shippedText = $@"#nullable enable";
            var unshippedText = @"C
C.C() -> void
C.Field -> string";
            var fixedUnshippedText = @"C
C.C() -> void
~C.Field -> string";
            await VerifyCSharpAdditionalFileFixAsync(source, shippedText, unshippedText, newShippedApiText: shippedText, fixedUnshippedText);
        }

        [Fact]
        public async Task LegacyAPIShouldBeAnnotatedWithObliviousMarker_ShippedFileAsync()
        {
            var source = @"
public class C
{
    public string {|RS0036:{|RS0041:Field|}|}; // oblivious
}
";
            var shippedText = $@"#nullable enable
C
C.C() -> void
C.Field -> string";
            var unshippedText = @"";
            var fixedShippedText = $@"#nullable enable
C
C.C() -> void
~C.Field -> string";
            await VerifyCSharpAdditionalFileFixAsync(source, shippedText, unshippedText, fixedShippedText, newUnshippedApiText: unshippedText);
        }

        [Fact]
        public async Task LegacyAPIWithObliviousMarkerGetsAnnotatedAsNullableAsync()
        {
            var source = @"
#nullable enable
public class C
{
    public string? {|RS0036:Field|};
}
";
            var shippedText = $@"#nullable enable
C
C.C() -> void
~C.Field -> string";
            var unshippedText = @"";
            var fixedShippedText = $@"#nullable enable
C
C.C() -> void
C.Field -> string?";
            await VerifyCSharpAdditionalFileFixAsync(source, shippedText, unshippedText, fixedShippedText, newUnshippedApiText: unshippedText);
        }

        [Fact]
        public async Task LegacyAPIWithObliviousMarkerGetsAnnotatedAsNotNullableAsync()
        {
            var source = @"
#nullable enable
public class C
{
    public string {|RS0036:Field|};
}
";
            var shippedText = $@"#nullable enable
C
C.C() -> void
~C.Field -> string";
            var unshippedText = @"";
            var fixedShippedText = $@"#nullable enable
C
C.C() -> void
C.Field -> string!";
            await VerifyCSharpAdditionalFileFixAsync(source, shippedText, unshippedText, fixedShippedText, newUnshippedApiText: unshippedText);
        }

        #endregion
    }
}
