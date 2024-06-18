# Playwright Project

## Introduction

Playwright is an end-to-end testing framework designed to test modern web applications. It supports all modern rendering engines including Chromium, WebKit, and Firefox, and can run tests on Windows, Linux, and macOS. This project demonstrates how to set up and run Playwright tests using .NET.

## System Requirements

- .NET 8 or higher
- Windows 10+, Windows Server 2016+, or Windows Subsystem for Linux (WSL)
- macOS 12 Monterey, macOS 13 Ventura, or macOS 14 Sonoma
- Debian 11, Debian 12, Ubuntu 20.04 or Ubuntu 22.04

## Installation

1. **Create a new project:**
    ```bash
    dotnet new nunit -n PlaywrightTests
    cd PlaywrightTests
    ```

2. **Install the necessary Playwright dependencies:**
    ```bash
    dotnet add package Microsoft.Playwright.NUnit
    ```

3. **Build the project:**
    ```bash
    dotnet build
    ```

4. **Install required browsers:**
    ```bash
    pwsh bin/Debug/net8.0/playwright.ps1 install
    ```
    If `pwsh` is not available, you will need to install PowerShell.

## Adding Example Tests

Edit the `UnitTest1.cs` file to include the following example tests:

```csharp
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace PlaywrightTests
{
    [Parallelizable(ParallelScope.Self)]
    [TestFixture]
    public class ExampleTest : PageTest
    {
        [Test]
        public async Task HasTitle()
        {
            await Page.GotoAsync("https://playwright.dev");
            await Expect(Page).ToHaveTitleAsync(new Regex("Playwright"));
        }

        [Test]
        public async Task GetStartedLink()
        {
            await Page.GotoAsync("https://playwright.dev");
            await Page.GetByRole(AriaRole.Link, new() { Name = "Get started" }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Installation" })).ToBeVisibleAsync();
        }
    }
}
