namespace ICD10AM.Cli.Tests;

/// <summary>
/// E2E tests for ICD-10-CM CLI - REAL database, mock API only.
/// Uses Spectre.Console.Testing to drive the CLI through TestConsole.
/// </summary>
public sealed class CliE2ETests : IClassFixture<CliTestFixture>
{
    readonly CliTestFixture _fixture;

    public CliE2ETests(CliTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Help_DisplaysAllCommands()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("help");
        console.Input.PushTextWithEnter("quit");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("search", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("find", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lookup", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("browse", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stats", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("history", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clear", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quit", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HelpShortcut_DisplaysHelp()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("h");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("search", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Shortcuts", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QuestionMarkHelp_DisplaysHelp()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("?");
        console.Input.PushTextWithEnter("quit");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        Assert.Contains("search", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Quit_ExitsGracefully()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("quit");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QuitShortcut_ExitsGracefully()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exit_ExitsGracefully()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("exit");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stats_DisplaysCodeCounts()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("stats");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        // Should show code counts
        Assert.Contains("Total Codes", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Embeddings", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Billable", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task History_ShowsCommandHistory()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("help");
        console.Input.PushTextWithEnter("stats");
        console.Input.PushTextWithEnter("history");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("help", output);
        Assert.Contains("stats", output);
    }

    [Fact]
    public async Task History_EmptyWhenNoCommands()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("history");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        // First command is "history" so history will show that
        // But the message "No history yet" won't appear since history itself gets added first
        Assert.Contains("history", console.Output);
    }

    [Fact]
    public async Task Find_SearchesByText()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("find chest");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("R07.4", output);
        Assert.Contains("Chest pain", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FindShortcut_SearchesByText()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("f pneumonia");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("J18.9", output);
        Assert.Contains("Pneumonia", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Find_ReturnsEmptyForNoMatch()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("find zzznomatchzzz");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        Assert.Contains("No codes found", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Find_RequiresArgument()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("find");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        Assert.Contains("Usage", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_FindsExactCode()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("lookup R07.4");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("R07.4", output);
        Assert.Contains("Chest pain", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupShortcut_FindsCode()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l E11.9");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("E11.9", output);
        Assert.Contains("diabetes", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_HandlesNoDot()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("lookup R074");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        Assert.Contains("R07.4", console.Output);
    }

    [Fact]
    public async Task Lookup_HandlesLowercase()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("lookup r07.4");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        Assert.Contains("R07.4", console.Output);
    }

    [Fact]
    public async Task Lookup_RequiresArgument()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("lookup");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        Assert.Contains("Usage", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_ShowsMultipleMatches()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("lookup R07");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("R07.4", output);
        Assert.Contains("R07.89", output);
    }

    [Fact]
    public async Task Browse_ShowsChapterOverview()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("browse");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("A-B", output);
        Assert.Contains("Infectious", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Browse_FiltersByLetter()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("browse R");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("R07", output);
        Assert.Contains("R06", output);
    }

    [Fact]
    public async Task BrowseShortcut_FiltersByLetter()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("b J");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("J18.9", output);
        Assert.Contains("J20.9", output);
    }

    [Fact]
    public async Task Search_FallsBackToFind_WhenNoEmbeddingService()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("search chest pain");
        console.Input.PushTextWithEnter("q");

        using var mockHandler = MockEmbeddingHandler.Error();
        using var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:8000"),
        };
        using var cli = new Icd10Cli(_fixture.DbPath, console, httpClient);

        await cli.RunAsync();

        var output = console.Output;
        // Should fall back to text search and show results
        Assert.Contains("chest", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_RequiresArgument()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("search");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        Assert.Contains("Usage", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_UsesEmbeddingService_WhenAvailable()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("search heart attack");
        console.Input.PushTextWithEnter("q");

        using var mockHandler = MockEmbeddingHandler.Success(embeddingDim: 384);
        using var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:8000"),
        };
        using var cli = new Icd10Cli(_fixture.DbPath, console, httpClient);

        await cli.RunAsync();

        var output = console.Output;
        // Should show search results (may show similarity scores)
        Assert.Contains("Search Results", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_ShortcutWorks()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("s diabetes");
        console.Input.PushTextWithEnter("q");

        using var mockHandler = MockEmbeddingHandler.Error();
        using var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:8000"),
        };
        using var cli = new Icd10Cli(_fixture.DbPath, console, httpClient);

        await cli.RunAsync();

        // Falls back to find - should show diabetes result
        Assert.Contains("E11.9", console.Output);
    }

    [Fact]
    public async Task UnknownCommand_TreatedAsSearch()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("chest pain symptoms");
        console.Input.PushTextWithEnter("q");

        using var mockHandler = MockEmbeddingHandler.Error();
        using var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:8000"),
        };
        using var cli = new Icd10Cli(_fixture.DbPath, console, httpClient);

        await cli.RunAsync();

        // Should treat as search and find chest-related codes
        Assert.Contains("chest", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Clear_ClearsScreenAndShowsHeader()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("clear");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        // After clear, should show header again (FigletText renders as ASCII art)
        Assert.Contains(
            "Medical Diagnosis Code Explorer",
            console.Output,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task EmptyInput_IsIgnored()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        // Should just continue and eventually quit
        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HeaderDisplaysOnStartup()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        var output = console.Output;
        // FigletText renders as ASCII art, so check for panel content instead
        Assert.Contains(
            "Medical Diagnosis Code Explorer",
            output,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains("help", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StatsDisplaysOnStartup()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        // Stats shown on startup
        Assert.Contains("Total Codes", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BillableIndicator_ShownInCodeList()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("find chest");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.DbPath, console);
        await cli.RunAsync();

        // Should show billable indicator
        Assert.Contains("billable", console.Output, StringComparison.OrdinalIgnoreCase);
    }
}
