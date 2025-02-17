﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Roslyn.Diagnostics.Analyzers.TestExportsShouldNotBeDiscoverable,
    Roslyn.Diagnostics.Analyzers.TestExportsShouldNotBeDiscoverableCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Roslyn.Diagnostics.Analyzers.TestExportsShouldNotBeDiscoverable,
    Roslyn.Diagnostics.Analyzers.TestExportsShouldNotBeDiscoverableCodeFixProvider>;

namespace Roslyn.Diagnostics.Analyzers.UnitTests
{
    public class TestExportsShouldNotBeDiscoverableTests
    {
        [Theory]
        [InlineData("System.Composition")]
        [InlineData("System.ComponentModel.Composition")]
        public async Task Discoverable_CSharpAsync(string mefNamespace)
        {
            var source = $@"
using {mefNamespace};

[Export]
class C {{ }}
";
            var fixedSource = $@"
using {mefNamespace};

[Export]
[PartNotDiscoverable]
class C {{ }}
";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    AdditionalReferences = { AdditionalMetadataReferences.SystemCompositionReference, AdditionalMetadataReferences.SystemComponentModelCompositionReference },
                    ExpectedDiagnostics = { VerifyCS.Diagnostic().WithSpan(4, 2, 4, 8).WithArguments("C") },
                },
                FixedState =
                {
                    Sources = { fixedSource },
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("System.Composition")]
        [InlineData("System.ComponentModel.Composition")]
        public async Task DiscoverableAddImport_CSharpAsync(string mefNamespace)
        {
            var source = $@"
[{mefNamespace}.Export]
class C {{ }}
";
            var fixedSource = $@"
using {mefNamespace};

[{mefNamespace}.Export]
[PartNotDiscoverable]
class C {{ }}
";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    AdditionalReferences = { AdditionalMetadataReferences.SystemCompositionReference, AdditionalMetadataReferences.SystemComponentModelCompositionReference },
                    ExpectedDiagnostics = { VerifyCS.Diagnostic().WithSpan(2, 2, 2, mefNamespace.Length + 9).WithArguments("C") },
                },
                FixedState =
                {
                    Sources = { fixedSource },
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("System.Composition")]
        [InlineData("System.ComponentModel.Composition")]
        public async Task NotDiscoverable_CSharpAsync(string mefNamespace)
        {
            var source = $@"
using {mefNamespace};

[Export]
[PartNotDiscoverable]
class C {{ }}
";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    AdditionalReferences = { AdditionalMetadataReferences.SystemCompositionReference, AdditionalMetadataReferences.SystemComponentModelCompositionReference },
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("System.Composition")]
        [InlineData("System.ComponentModel.Composition")]
        public async Task Discoverable_VisualBasicAsync(string mefNamespace)
        {
            var source = $@"
Imports {mefNamespace}

<Export>
Class C
End Class
";
            var fixedSource = $@"
Imports {mefNamespace}

<Export>
<PartNotDiscoverable>
Class C
End Class
";

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { source },
                    AdditionalReferences = { AdditionalMetadataReferences.SystemCompositionReference, AdditionalMetadataReferences.SystemComponentModelCompositionReference },
                    ExpectedDiagnostics = { VerifyVB.Diagnostic().WithSpan(4, 2, 4, 8).WithArguments("C") },
                },
                FixedState =
                {
                    Sources = { fixedSource },
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("System.Composition")]
        [InlineData("System.ComponentModel.Composition")]
        public async Task DiscoverableAddImport_VisualBasicAsync(string mefNamespace)
        {
            var source = $@"
<{mefNamespace}.Export>
Class C
End Class
";
            var fixedSource = $@"
Imports {mefNamespace}

<{mefNamespace}.Export>
<PartNotDiscoverable>
Class C
End Class
";

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { source },
                    AdditionalReferences = { AdditionalMetadataReferences.SystemCompositionReference, AdditionalMetadataReferences.SystemComponentModelCompositionReference },
                    ExpectedDiagnostics = { VerifyVB.Diagnostic().WithSpan(2, 2, 2, mefNamespace.Length + 9).WithArguments("C") },
                },
                FixedState =
                {
                    Sources = { fixedSource },
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("System.Composition")]
        [InlineData("System.ComponentModel.Composition")]
        public async Task NotDiscoverable_VisualBasicAsync(string mefNamespace)
        {
            var source = $@"
Imports {mefNamespace}

<Export>
<PartNotDiscoverable>
Class C
End Class
";

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { source },
                    AdditionalReferences = { AdditionalMetadataReferences.SystemCompositionReference, AdditionalMetadataReferences.SystemComponentModelCompositionReference },
                },
            }.RunAsync();
        }
    }
}
