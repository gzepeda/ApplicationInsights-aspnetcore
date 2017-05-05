Microsoft Application Insights for ASP.NET Core applications
=============================================================

This repository has a code for [Application Insights monitoring](http://azure.microsoft.com/en-us/services/application-insights/) of [ASP.NET Core](https://github.com/aspnet/home) applications. Read about contribution policies on Application Insights Home [repository](https://github.com/microsoft/ApplicationInsights-home)

Recent updates
--------------
**Microsoft.ApplicationInsights.AspNet** was renamed to **Microsoft.ApplicationInsights.AspNetCore**. We have updated the SDK to use the stable 1.0.0 release of the .NET Core CLI runtime environment. Please note that this version is not compatible with RC1 bits of DNX environment. Furthermore, metrics stream is enabled by default in .NET Framework of ASP.NET Core.

Getting Started
---------------

[Application Insights monitoring](http://azure.microsoft.com/en-us/services/application-insights/) is a service that allows you to collect monitoring and diagnostics information about your application. The [getting started](https://github.com/Microsoft/ApplicationInsights-aspnet5/wiki/Getting-Started) guide shows how you can onboard your ASP.NET Core web application to use the Application Insights SDK.

Application Insights collects a lot of information out-of-the-box such as requests, exceptions, and usage. It also allows you to configure additional data collection.  The [configure](https://github.com/Microsoft/ApplicationInsights-aspnet5/wiki/Configure) guide demonstrates the most common tasks you may want to do.


Repository structure
--------------------

```
root\
    ApplicationInsights.AspNetCore.sln - Main Solution

    src\
        ApplicationInsights.AspNetCore - Application Insights package

    test\
        ApplicationInsights.AspNetCore.Tests - Unit tests
        FunctionalTestUtils - test utilities for functional tests
        MVCFramework45.FunctionalTests - functional tests for MVC application
        WebApiShimFw46.FunctionalTests - functional tests for Web API application
        PerfTest - performance test
```

Developing
----------

**Note: The current version (Microsoft.ApplicationInsights.AspNetCore: 1.0.0) is no longer compatible with DNX runtime and ASP.NET 5 RC1 bits. Please visit [Migration to ASP.NET Core](https://docs.asp.net/en/latest/migration/index.html) to upgrade the application to ASP.NET Core 1.0.0.**

## Pre-requisites
- [Visual Studio 2015 Update 3](https://www.visualstudio.com/en-us/downloads/visual-studio-2015-downloads-vs.aspx).
- [Visual Studio 2015 CLI Tools](https://go.microsoft.com/fwlink/?LinkId=817245).
- [.NET Core CLI](https://www.microsoft.com/net/download).
- [Node.js](https://nodejs.org/download).
- [Git](http://git-scm.com/download).
- Source Code.

```
git clone https://github.com/Microsoft/ApplicationInsights-aspnetcore.git
```

## Building
From Visual Studio 2015
```
devenv ApplicationInsights.AspNetCore.sln
```

From Visual Studio 2015 Developer Command Prompt: Navigate to the source project folder and use the following commands to build the project:

```
dotnet restore &REM Restores the dependency packages
dotnet build &REM Builds the project
```
- If you get NPM package restore errors, make sure Node and NPM are added to PATH.
- If you get Bower package restore errors, make sure Git is added to PATH.
- If you get dotnet package restore errors, make sure [.NET Core CLI is installed](https://github.com/dotnet/cli/blob/rel/1.0.0/Documentation/cli-installation-scenarios.md) and the nuget feeds are up to date.
- In case of **.NET Core** applications, if you run into restore errors with respect to application insights dependency, please add ```"dnxcore50"``` and ```"portable-net45+win8" ``` to the imports list (if it does not exist), under ```frameworks``` section of ```project.json```, as described below. Please visit [Migrating from DNX](http://dotnet.github.io/docs/core-concepts/dnx-migration.html) for more details.
``` json
{
    "frameworks": {
        "netcoreapp1.0": { 
            "imports": ["dnxcore50", "portable-net45+win8"]
        }
    }
}
```

## Branches
- We follow the [Git Flow](http://nvie.com/posts/a-successful-git-branching-model) model.
- [master](https://github.com/Microsoft/ApplicationInsights-aspnetcore/tree/master) has the _latest_ version released on [NuGet.org](https://www.nuget.org/packages/Microsoft.ApplicationInsights.AspNetCore).
- [develop](https://github.com/Microsoft/ApplicationInsights-aspnetcore/tree/develop) has the code for the _next_ release.

Running and writing tests
-------------------------
There are two sets of tests unit tests and functional tests. Please use unit tests for all features testing. The purpose of functional tests is just end-to-end validation of functionality on sample applications.


*Functional tests*
Functional tests are regular web applications with unit tests integrated into them. Application can be compiled as a regular web application as well as set of tests. Typical functional tests will do the following:

1. Host the current project in In-Proc server.
2. Initialize application insights telemetry channel.
3. Initiate request to self hosted web application using HttpClient.
4. Check data received in telemetry channel.

The following are modifications made to a regular web application to make it work this way:

Add dependencies to project.json:


```
"FunctionalTestUtils": "1.0.0-*",
"dotnet.test.xunit": "1.0.0-*",
"xunit": "2.1.0"
```

and test command:

```
"test": "xunit"
```

Add this initialization logic to Startup.cs:

```
services.AddFunctionalTestTelemetryChannel();
```

*Running Tests*
You can run unit tests using Visual Studio.

You can run unit tests using .NET CLI from command line. The prerequisite to this is that you should make sure you have the latest version of .NET CLI. You can check the available runtime using the following command:
```
dotnet --version
```

If you are seeing that ```dotnet``` is not available (or defined), install .NET CLI: [.NET Core + CLI tools (SDK)](https://github.com/dotnet/cli).

After that you can open a developer command prompt, navigate to each test folder and run:
```
dotnet restore &REM Restores the dependency packages
dotnet build &REM Builds the test project
dotnet test &REM Runs the tests within the test project
```

You can also run all tests using the following Powershell from root directory.

```
powershell .\RunTestsCore.ps1
```

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
