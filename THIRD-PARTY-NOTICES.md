# Third-Party Notices

Scratchdeck Hybrid uses the third-party components listed below. In this document, **shipped** means that a component is included in, or compiled into, the framework-dependent application publish. **Development/test-only** means that the component is restored for building or testing and is absent from the published application.

Complete, verbatim license and notice texts are stored in the [`licenses/`](licenses/) directory.

## Components shipped with the application

### AvalonEdit 6.3.1.120

- **Component:** AvalonEdit (`ICSharpCode.AvalonEdit.dll`)
- **Copyright:** The upstream license identifies AvalonEdit Contributors. NuGet package metadata additionally states `2000-2025 AlphaSierraPapa for the SharpDevelop Team`.
- **License:** MIT
- **Project:** [AvalonEdit homepage](http://www.avalonedit.net/) and [source repository](https://github.com/icsharpcode/AvalonEdit)
- **Use in Scratchdeck:** Provides the text editor, document model, and syntax-highlighting infrastructure.
- **Distribution:** Shipped with published builds, including AvalonEdit's embedded resources.
- **License text:** [`licenses/AvalonEdit-LICENSE.txt`](licenses/AvalonEdit-LICENSE.txt)

### Nord color palette

- **Component:** Nord
- **Copyright:** Copyright (c) 2016-present Sven Greb `<development@svengreb.de>`
- **License:** MIT
- **Project:** [Nord homepage](https://www.nordtheme.com/) and [source repository](https://github.com/nordtheme/nord)
- **Use in Scratchdeck:** The built-in Nord Dark app and code themes adapt Nord palette colors. The palette values are present in the XAML theme and the compiled fallback theme catalog.
- **Distribution:** Shipped as part of Scratchdeck's compiled application resources and code; no separate Nord asset files are distributed.
- **License text:** [`licenses/Nord-LICENSE.txt`](licenses/Nord-LICENSE.txt)

## Development and test dependencies

The following components are used by the test project. They are not included in the framework-dependent Scratchdeck application publish.

### Microsoft Test Platform 18.0.1

The Microsoft Test Platform package family is licensed and maintained together:

| Component | Dependency kind | Use in Scratchdeck |
| --- | --- | --- |
| `Microsoft.NET.Test.Sdk` 18.0.1 | Direct test dependency | Connects the test project to `dotnet test` and test hosts. |
| `Microsoft.CodeCoverage` 18.0.1 | Transitive test dependency | Supplies Test Platform code-coverage infrastructure. |
| `Microsoft.TestPlatform.ObjectModel` 18.0.1 | Transitive test dependency | Supplies the Test Platform object model. |
| `Microsoft.TestPlatform.TestHost` 18.0.1 | Transitive test dependency | Hosts and executes the test assemblies. |

- **Copyright:** Copyright (c) Microsoft Corporation
- **License:** MIT
- **Project:** [Microsoft VSTest repository](https://github.com/microsoft/vstest)
- **Distribution:** Development/test-only. These packages may be copied to test output, but they are not shipped in the application publish.
- **License text:** [`licenses/Microsoft-vstest-LICENSE.txt`](licenses/Microsoft-vstest-LICENSE.txt)
- **Package notices:** [`licenses/Microsoft.CodeCoverage-ThirdPartyNotices.txt`](licenses/Microsoft.CodeCoverage-ThirdPartyNotices.txt) and [`licenses/Microsoft.TestPlatform.TestHost-ThirdPartyNotices.txt`](licenses/Microsoft.TestPlatform.TestHost-ThirdPartyNotices.txt)

The package notices above identify these components embedded in Microsoft test tooling:

#### Mono.Cecil 0.11.3

- **Copyright:** Copyright (c) 2008-2015 Jb Evain; Copyright (c) 2008-2011 Novell, Inc.
- **License:** MIT
- **Project:** [Mono.Cecil repository](https://github.com/jbevain/cecil)
- **Use in Scratchdeck:** Embedded within Microsoft.CodeCoverage and Microsoft.TestPlatform.TestHost tooling.
- **Distribution:** Development/test-only; absent from the application publish.
- **License text:** Preserved verbatim in both Microsoft package notice files linked above.

#### NuGet.Client 6.8.0.117

- **Copyright:** Copyright (c) .NET Foundation and Contributors
- **License:** Apache License 2.0
- **Project:** [NuGet.Client repository](https://github.com/NuGet/NuGet.Client)
- **Use in Scratchdeck:** Embedded within Microsoft.TestPlatform.TestHost tooling.
- **Distribution:** Development/test-only; absent from the application publish.
- **License text:** [`licenses/Apache-2.0.txt`](licenses/Apache-2.0.txt), with the package attribution preserved in [`licenses/Microsoft.TestPlatform.TestHost-ThirdPartyNotices.txt`](licenses/Microsoft.TestPlatform.TestHost-ThirdPartyNotices.txt)

### Newtonsoft.Json 13.0.3

- **Component:** Newtonsoft.Json
- **Copyright:** James Newton-King. The included license states `Copyright (c) 2007 James Newton-King`; NuGet package metadata states `Copyright © James Newton-King 2008`.
- **License:** MIT
- **Project:** [Newtonsoft.Json homepage](https://www.newtonsoft.com/json) and [source repository](https://github.com/JamesNK/Newtonsoft.Json)
- **Use in Scratchdeck:** A transitive JSON dependency of Microsoft.TestPlatform.TestHost. It is also identified in TestHost's bundled third-party notice.
- **Distribution:** Development/test-only; absent from the application publish.
- **License text:** [`licenses/Newtonsoft.Json-LICENSE.txt`](licenses/Newtonsoft.Json-LICENSE.txt), with TestHost's package copy covered by [`licenses/Microsoft.TestPlatform.TestHost-ThirdPartyNotices.txt`](licenses/Microsoft.TestPlatform.TestHost-ThirdPartyNotices.txt)

### xUnit.net framework packages

| Component | Version | Dependency kind | Use in Scratchdeck |
| --- | --- | --- | --- |
| `xunit` | 2.9.3 | Direct test dependency | Test framework meta-package. |
| `xunit.assert` | 2.9.3 | Transitive test dependency | Test assertions. |
| `xunit.core` | 2.9.3 | Transitive test dependency | Core test framework. |
| `xunit.extensibility.core` | 2.9.3 | Transitive test dependency | Core extensibility support. |
| `xunit.extensibility.execution` | 2.9.3 | Transitive test dependency | Test discovery and execution support. |
| `xunit.abstractions` | 2.0.3 | Transitive test dependency | Shared test abstractions. |

- **Copyright:** The 2.9.3 packages identify Copyright (C) .NET Foundation. The `xunit.abstractions` assembly identifies Copyright (C) Outercurve Foundation.
- **License:** Apache License 2.0. The older `xunit.abstractions` package does not contain an SPDX license expression or local license file; its authoritative NuGet `licenseUrl` points to the xUnit Apache License 2.0 license and attribution file reproduced here.
- **Project:** [xUnit.net repository](https://github.com/xunit/xunit)
- **Distribution:** Development/test-only; absent from the application publish.
- **License and attribution text:** [`licenses/xUnit-LICENSE.txt`](licenses/xUnit-LICENSE.txt) and the complete [`licenses/Apache-2.0.txt`](licenses/Apache-2.0.txt)

The xUnit license file also preserves MIT notices for imported .NET Foundation code.

### xunit.runner.visualstudio 3.1.5

- **Component:** xUnit.net Visual Studio runner
- **Copyright:** Copyright (C) .NET Foundation
- **License:** Apache License 2.0
- **Project:** [xUnit.net Visual Studio runner repository](https://github.com/xunit/visualstudio.xunit)
- **Use in Scratchdeck:** Provides the Visual Studio and Microsoft Test Platform adapter for the test project. It is a private test asset.
- **Distribution:** Development/test-only; absent from the application publish.
- **License and attribution text:** [`licenses/xUnit-VisualStudio-LICENSE.txt`](licenses/xUnit-VisualStudio-LICENSE.txt) and the complete [`licenses/Apache-2.0.txt`](licenses/Apache-2.0.txt)

The runner license file also preserves MIT notices for imported .NET Foundation code.

### xunit.analyzers 1.18.0

- **Component:** xUnit.net Analyzers
- **Copyright:** Copyright (C) .NET Foundation
- **License:** Apache License 2.0
- **Project:** [xUnit.net Analyzers repository](https://github.com/xunit/xunit.analyzers)
- **Use in Scratchdeck:** Provides compile-time diagnostics and fixes for the test project.
- **Distribution:** Development/test-only and compile-time-only; absent from the application publish.
- **License and attribution text:** [`licenses/xUnit-Analyzers-LICENSE.txt`](licenses/xUnit-Analyzers-LICENSE.txt) and the complete [`licenses/Apache-2.0.txt`](licenses/Apache-2.0.txt)

## Audit scope and exclusions

The solution package graph was audited with:

```powershell
dotnet list Scratchdeck.sln package --include-transitive
```

The framework-dependent application dependency manifest and publish directory contain AvalonEdit as the only non-framework package binary. The full .NET runtime is not intentionally redistributed, and normal .NET or Windows framework assemblies are not listed above as custom NuGet dependencies.

The repository and published output were also checked for fonts, icons, images, syntax definitions, copied controls or styles, external resource links, and source files carrying copyright or license headers:

- No font binaries are bundled. The configured font-family names resolve against fonts installed on the user's Windows system.
- The Scratchdeck application icon is generated by the repository's own icon-generation script. UI symbols and paths are defined locally; no third-party icon pack was found.
- The documentation images are screenshots of Scratchdeck and are not included in the application publish.
- No standalone third-party syntax-highlighting definition files are bundled. Scratchdeck constructs its definitions locally and uses AvalonEdit's highlighting APIs and embedded resources.
- No separately identifiable copied XAML control or style was found. Nord is the only externally identifiable palette adaptation and is disclosed above.
- No additional source copyright, license, attribution, or copied-code headers were found.
