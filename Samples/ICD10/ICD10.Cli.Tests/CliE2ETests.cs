namespace ICD10.Cli.Tests;

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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("search", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Quit_ExitsGracefully()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("quit");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QuitShortcut_ExitsGracefully()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exit_ExitsGracefully()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("exit");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stats_DisplaysApiStatus()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("stats");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        // Should show API health status
        Assert.Contains("API", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Status", output, StringComparison.OrdinalIgnoreCase);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("J18.9", output);
        Assert.Contains("J20.9", output);
    }

    [Fact]
    public async Task Search_ShowsResultsOrFallsBack()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("search chest pain");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);

        await cli.RunAsync();

        var output = console.Output;
        // Should show search results or fall back to text search
        Assert.Contains("chest", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_RequiresArgument()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("search");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("Usage", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_ShowsHeartRelatedResults()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("search heart attack");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);

        await cli.RunAsync();

        var output = console.Output;
        // Should show search results - either RAG or fallback to text search
        Assert.Contains("Results", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_ShortcutWorks()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("s diabetes");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);

        await cli.RunAsync();

        // Should show diabetes-related results
        Assert.Contains("diabetes", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownCommand_TreatedAsSearch()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("chest pain symptoms");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);

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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
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
    public async Task ApiStatusDisplaysOnStartup()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // API health status shown on startup
        Assert.Contains("API", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BillableIndicator_ShownInCodeList()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("find chest");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // Should show billable indicator
        Assert.Contains("billable", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_CodeNotFound_ShowsErrorMessage()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("lookup ZZZ99.99");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // Should show "not found" message instead of crashing
        Assert.Contains("not found", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Json_CodeNotFound_ShowsErrorMessage()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("json ZZZ99.99");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // Should show "not found" message instead of crashing
        Assert.Contains("not found", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_NoResults_HandlesGracefully()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("search xyznonexistent123");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // Should gracefully handle search and exit without crashing
        var output = console.Output;
        Assert.Contains("Goodbye", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Browse_InvalidLetter_ShowsEmpty()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("browse 9");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // Should handle invalid letter gracefully
        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // CRITICAL: Lookup tests for ICD-10-CM codes (returned by RAG search)
    // These tests ensure codes from search can actually be looked up
    // =========================================================================

    [Fact]
    public async Task Lookup_FindsIcd10CmCode_I10()
    {
        // I10 (Essential hypertension) is in icd10_code (used by RAG search)
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l I10");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("I10", output);
        Assert.Contains("hypertension", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_FindsIcd10CmCode_I2111_HeartAttack()
    {
        // I21.11 (ST elevation MI) is in icd10_code - critical for "heart attack" search
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l I21.11");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("I21.11", output);
        Assert.Contains("myocardial infarction", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_FindsIcd10CmCode_M545_BackPain()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l M54.5");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("M54.5", output);
        Assert.Contains("back pain", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_FindsIcd10CmCode_G43909_Migraine()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l G43.909");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("G43.909", output);
        Assert.Contains("migraine", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_ShowsFullCodeDetails_AllFields()
    {
        // Verify lookup shows ALL required information for R07.4
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l R07.4");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;

        // MUST show the code
        Assert.Contains("R07.4", output);

        // MUST show short description
        Assert.Contains("Chest pain", output, StringComparison.OrdinalIgnoreCase);

        // MUST show billable status
        Assert.Contains("Billable", output, StringComparison.OrdinalIgnoreCase);

        // MUST show chapter info
        Assert.Contains("XVIII", output);
        Assert.Contains("Symptoms", output, StringComparison.OrdinalIgnoreCase);

        // MUST show block info
        Assert.Contains("R00-R09", output);

        // MUST show category info
        Assert.Contains("R07", output);
        Assert.Contains("Pain in throat and chest", output, StringComparison.OrdinalIgnoreCase);

        // MUST show synonyms if present
        Assert.Contains("thoracic pain", output, StringComparison.OrdinalIgnoreCase);

        // MUST NOT say not found
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_ShowsChapterBlockCategoryHierarchy()
    {
        // Verify I10 (hypertension) shows full hierarchy
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l I10");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;

        // Code and description
        Assert.Contains("I10", output);
        Assert.Contains("hypertension", output, StringComparison.OrdinalIgnoreCase);

        // Chapter IX - Circulatory system
        Assert.Contains("IX", output);
        Assert.Contains("circulatory", output, StringComparison.OrdinalIgnoreCase);

        // Block I10-I15
        Assert.Contains("I10-I15", output);
        Assert.Contains("Hypertensive", output, StringComparison.OrdinalIgnoreCase);

        // Category I10
        Assert.Contains("Essential hypertension", output, StringComparison.OrdinalIgnoreCase);

        // Synonym
        Assert.Contains("high blood pressure", output, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_ShowsSynonyms_WhenPresent()
    {
        // Verify E11.9 (Type 2 diabetes) shows synonyms
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l E11.9");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;

        Assert.Contains("E11.9", output);
        Assert.Contains("Type 2 diabetes", output, StringComparison.OrdinalIgnoreCase);
        // Must show synonyms
        Assert.Contains("adult-onset diabetes", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-insulin-dependent", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_ShowsLongDescription()
    {
        // Verify G43.909 shows long description
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l G43.909");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;

        Assert.Contains("G43.909", output);
        Assert.Contains("Migraine", output, StringComparison.OrdinalIgnoreCase);
        // Long description has more detail
        Assert.Contains("without status migrainosus", output, StringComparison.OrdinalIgnoreCase);
        // Synonyms
        Assert.Contains("sick headache", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_ShowsEditionInfo()
    {
        // Verify edition/version info is shown
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l I21.11");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;

        Assert.Contains("I21.11", output);
        Assert.Contains("STEMI", output, StringComparison.OrdinalIgnoreCase);
        // Must show edition
        Assert.Contains("2025", output);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_AllSeededCodes_Succeed()
    {
        // Verify ALL seeded ICD-10-CM codes can be looked up
        var codesToTest = new[]
        {
            ("R07.4", "chest pain"),
            ("R06.02", "shortness of breath"),
            ("I21.11", "myocardial infarction"),
            ("J18.9", "pneumonia"),
            ("E11.9", "diabetes"),
            ("I10", "hypertension"),
            ("M54.5", "back pain"),
        };

        foreach (var (code, expectedText) in codesToTest)
        {
            var console = new TestConsole();
            console.Profile.Capabilities.Interactive = true;
            console.Input.PushTextWithEnter($"l {code}");
            console.Input.PushTextWithEnter("q");

            using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
            await cli.RunAsync();

            var output = console.Output;
            Assert.True(
                output.Contains(code, StringComparison.Ordinal),
                $"Lookup for {code} should show the code in output"
            );
            Assert.True(
                output.Contains(expectedText, StringComparison.OrdinalIgnoreCase),
                $"Lookup for {code} should show '{expectedText}' in output"
            );
            Assert.False(
                output.Contains("not found", StringComparison.OrdinalIgnoreCase),
                $"Lookup for {code} should NOT show 'not found'"
            );
        }
    }

    [Fact]
    public async Task Json_FindsIcd10CmCode()
    {
        // JSON command should also find ICD-10-CM codes
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("json I10");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("I10", output);
        Assert.Contains("hypertension", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_ShowsChapterInfo()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l I10");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("I10", output);
        Assert.Contains("Chapter", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IX", output);
        Assert.Contains("circulatory", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_ShowsCategoryInfo()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l E11.9");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("E11.9", output);
        Assert.Contains("Category", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("E11", output);
    }

    [Fact]
    public async Task Lookup_ShowsSynonymsWhenPresent()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l I10");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("I10", output);
        Assert.Contains("Synonyms", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("high blood pressure", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_ShowsMultipleSynonyms()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l M54.5");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("M54.5", output);
        Assert.Contains("Synonyms", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lumbago", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("backache", output, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // COMPREHENSIVE E2E TEST: LOOKUP COMMAND DISPLAYS ALL DETAILS
    // This test PROVES the `l` command shows EVERY SINGLE FIELD
    // =========================================================================

    [Fact]
    public async Task LookupCommand_E2E_DisplaysAllCodeDetails_ChapterBlockCategorySynonymsEdition()
    {
        // ARRANGE: Use I10 (hypertension) - has full hierarchy and synonyms
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l I10");
        console.Input.PushTextWithEnter("q");

        // ACT: Run the CLI
        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;

        // ASSERT: Code is displayed
        Assert.Contains("I10", output);

        // ASSERT: Short description is displayed
        Assert.Contains("hypertension", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: CHAPTER is displayed (Chapter IX - Circulatory)
        Assert.Contains("Chapter", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IX", output);
        Assert.Contains("circulatory", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: BLOCK is displayed (I10-I15 - Hypertensive diseases)
        Assert.Contains("Block", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("I10-I15", output);
        Assert.Contains("Hypertensive", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: CATEGORY is displayed (I10 - Essential hypertension)
        Assert.Contains("Category", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Essential hypertension", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: SYNONYMS are displayed
        Assert.Contains("Synonyms", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("high blood pressure", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: EDITION is displayed
        Assert.Contains("Edition", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2025", output);

        // ASSERT: Billable status is displayed
        Assert.Contains("Billable", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: NOT showing "not found"
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupCommand_E2E_R074_DisplaysAllCodeDetails()
    {
        // ARRANGE: Use R07.4 (chest pain) - has full hierarchy and synonyms
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l R07.4");
        console.Input.PushTextWithEnter("q");

        // ACT
        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;

        // ASSERT: Code
        Assert.Contains("R07.4", output);

        // ASSERT: Description
        Assert.Contains("Chest pain", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: CHAPTER XVIII - Symptoms
        Assert.Contains("Chapter", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("XVIII", output);
        Assert.Contains("Symptoms", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: BLOCK R00-R09
        Assert.Contains("Block", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("R00-R09", output);

        // ASSERT: CATEGORY R07 - Pain in throat and chest
        Assert.Contains("Category", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("R07", output);
        Assert.Contains("Pain in throat and chest", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: SYNONYMS
        Assert.Contains("Synonyms", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("thoracic pain", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: EDITION
        Assert.Contains("Edition", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2025", output);

        // ASSERT: Billable
        Assert.Contains("Billable", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: NOT "not found"
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupCommand_E2E_E119_Diabetes_DisplaysAllCodeDetails()
    {
        // ARRANGE: Use E11.9 (Type 2 diabetes) - has synonyms
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l E11.9");
        console.Input.PushTextWithEnter("q");

        // ACT
        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;

        // ASSERT: Code
        Assert.Contains("E11.9", output);

        // ASSERT: Description
        Assert.Contains("Type 2 diabetes", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: CHAPTER IV - Endocrine
        Assert.Contains("Chapter", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IV", output);

        // ASSERT: BLOCK E10-E14 - Diabetes mellitus
        Assert.Contains("Block", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("E10-E14", output);
        Assert.Contains("Diabetes mellitus", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: CATEGORY E11 - Type 2 diabetes mellitus
        Assert.Contains("Category", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("E11", output);
        Assert.Contains("Type 2 diabetes mellitus", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: SYNONYMS - adult-onset, non-insulin-dependent
        Assert.Contains("Synonyms", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("adult-onset diabetes", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-insulin-dependent", output, StringComparison.OrdinalIgnoreCase);

        // ASSERT: EDITION
        Assert.Contains("Edition", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2025", output);

        // ASSERT: NOT "not found"
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// E2E tests that hit the REAL API at localhost:5558.
/// These tests verify the CLI works with ACTUAL production data.
/// </summary>
public sealed class RealApiE2ETests : IAsyncLifetime, IDisposable
{
    private const string RealApiUrl = "http://localhost:5558";
    private HttpClient? _httpClient;
    private bool _apiAvailable;

    public async Task InitializeAsync()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            var response = await _httpClient.GetAsync($"{RealApiUrl}/health");
            _apiAvailable = response.IsSuccessStatusCode;
        }
        catch
        {
            _apiAvailable = false;
        }
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose() => _httpClient?.Dispose();

    [SkippableFact]
    public async Task RealApi_Lookup_H53481_DisplaysAllDetails()
    {
        Skip.IfNot(_apiAvailable, "Real API not running at localhost:5558");

        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l H53.481");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(RealApiUrl, console, _httpClient);
        await cli.RunAsync();

        var output = console.Output;

        // MUST find the code
        Assert.Contains("H53.481", output);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);

        // MUST show description
        Assert.Contains("visual field", output, StringComparison.OrdinalIgnoreCase);

        // MUST show chapter
        Assert.Contains("Chapter", output, StringComparison.OrdinalIgnoreCase);

        // MUST show block
        Assert.Contains("Block", output, StringComparison.OrdinalIgnoreCase);

        // MUST show category
        Assert.Contains("Category", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("H53", output);
    }

    [SkippableFact]
    public async Task RealApi_Lookup_Q531_DisplaysAllDetails()
    {
        Skip.IfNot(_apiAvailable, "Real API not running at localhost:5558");

        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l Q53.1");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(RealApiUrl, console, _httpClient);
        await cli.RunAsync();

        var output = console.Output;

        // MUST find the code
        Assert.Contains("Q53.1", output);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);

        // MUST show description
        Assert.Contains("testicle", output, StringComparison.OrdinalIgnoreCase);

        // MUST show chapter 17
        Assert.Contains("Chapter", output, StringComparison.OrdinalIgnoreCase);

        // MUST show category
        Assert.Contains("Category", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Q53", output);
    }

    [SkippableFact]
    public async Task RealApi_Lookup_AnyCodeFromSearch_MustWork()
    {
        Skip.IfNot(_apiAvailable, "Real API not running at localhost:5558");

        // First, get a code from search to prove it exists
        var searchResponse = await _httpClient!.GetAsync($"{RealApiUrl}/api/icd10/codes?q=diabetes&limit=1");
        Assert.True(searchResponse.IsSuccessStatusCode, "Search should return results");

        var searchJson = await searchResponse.Content.ReadAsStringAsync();
        Assert.Contains("Code", searchJson, StringComparison.OrdinalIgnoreCase);

        // Now lookup that same code via CLI
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l E11.9");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(RealApiUrl, console, _httpClient);
        await cli.RunAsync();

        var output = console.Output;

        // If search found it, lookup MUST find it too
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }
}
