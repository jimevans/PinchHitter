# PinchHitter
A basic in-memory web server designed for testing .NET applications

![Unit tests](https://github.com/jimevans/PinchHitter/actions/workflows/dotnet.yml/badge.svg)
[![Coverage Status](https://coveralls.io/repos/github/jimevans/PinchHitter/badge.svg?branch=main&kill_cache=1)](https://coveralls.io/github/jimevans/PinchHitter?branch=main)

This repository contains a class library implementing an in-memory simple web server
capable of serving HTTP content as well as acting as a server for WebSocket traffic.
The project uses a`System.Net.Sockets.TcpListener` to avoid the necessity of registering
URL prefixes on Windows, which using `System.Net.Sockets.HttpListener` requires, and which
needs administrative access. It is provided as an alternative to 
[Kestrel](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/?view=aspnetcore-6.0),
as Kestral is tuned for use with ASP.NET Core, and including it into a test project
brings in many additional dependencies, even when the project being tested is not an
ASP.NET project.

This project is not intended to be a fully-featured, production-ready web server. It does
not support many of the features of a modern web server.

## Getting Started
The library is built using .NET 6. There are no plans at present to support earlier versions of .NET.
Future versions of .NET will be supported when [a new LTS version](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) is released. The next LTS version of .NET is scheduled to be .NET 8, released in November, 2023.

To build the library, after cloning the repository, execute the following in a terminal window
in the root of your clone:

    dotnet build

To run the project unit tests, execute the following in a terminal window:

    dotnet test

## Development
There are three projects in this repository:
* src/PinchHitter/PinchHitter.csproj - The main library source code
* src/PinchHitter.Client/PinchHitter.Client.csproj - A console application used as a "playground"
for practice using the library. Changes to this project are not canonical at this time, and this
project should not be viewed as having desirable coding practices.
* test/PinchHitter.Tests/PinchHitter.Tests.csproj - The unit tests for the main library

[Visual Studio Code](https://code.visualstudio.com/) is the preferred IDE for development of this library.
It can be used across multiple operating systems, and there should be nothing platform-specific in the
library or its unit tests that would require platform-specific code. For working with C# code, we recommend
using the [C# for Visual Studio Code plugin](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp).

The project uses [NUnit](https://nunit.org/) for its unit tests.

The project has enabled Roslyn analyzers to help with code quality, and uses the
[StyleCop analyzers](https://www.nuget.org/packages/StyleCop.Analyzers) to enforce a consistent code style.
PRs should contain no warnings from any of the analyzers. Use of warning suppression in the source code
is mostly prohibited, and will only be allowed on a very strictly reviewed case-by-case basis.

The project uses [GitHub Actions](https://github.com/jimevans/PinchHitter/actions) for continuous
integration (CI). Code coverage statistics are generated and gathered by
[Coverlet](https://www.nuget.org/packages/coverlet.collector/), and uploaded to
[coveralls.io](https://coveralls.io/github/jimevans/PinchHitter?branch=main). PRs for which
the code coverage drops from the current percentage on the `main` branch will need to be carefully
reviewed.

Some useful plugins in your Visual Studio Code environment for this project are:
* [.NET Core Test Explorer](https://marketplace.visualstudio.com/items?itemName=formulahendry.dotnet-test-explorer):
This plugin allows one to execute any or all of the unit tests from within the IDE. By changing the settings
of the plugin to add `/p:CollectCoverage=true /p:CoverletOutputFormat=lcov /p:CoverletOutput=../coverage/lcov` to
the test arguments, code coverage data can be collected locally when the tests are executed using the explorer.
* [Coverage Gutters](https://marketplace.visualstudio.com/items?itemName=ryanluker.vscode-coverage-gutters):
This plugin allows visualization of code coverage directly within the IDE.