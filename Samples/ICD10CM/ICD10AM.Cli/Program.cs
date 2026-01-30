using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Spectre.Console;

var dbPath = args.Length > 0 ? args[0] : FindDatabase();
if (dbPath is null)
{
    AnsiConsole.MarkupLine(
        "[red]Database not found![/] Pass path as argument or set DbPath env var."
    );
    return 1;
}

using var app = new Icd10Cli(dbPath, AnsiConsole.Console);
await app.RunAsync();
return 0;

static string? FindDatabase()
{
    var envPath = Environment.GetEnvironmentVariable("DbPath");
    if (envPath is not null && File.Exists(envPath))
        return envPath;

    var candidates = new[]
    {
        "../icd10cm.db",
        "../../icd10cm.db",
        "../../../icd10cm.db",
        "./icd10cm.db",
    };

    return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
}

/// <summary>
/// ICD-10-CM code record from database.
/// </summary>
sealed record Icd10Code(
    string Id,
    string Code,
    string ShortDescription,
    string LongDescription,
    bool Billable
);

/// <summary>
/// RAG search result with similarity score.
/// </summary>
sealed record SearchResult(
    string Code,
    string ShortDescription,
    string LongDescription,
    double Similarity
);

/// <summary>
/// Embedding service response.
/// </summary>
sealed record EmbedResponse(ImmutableArray<float> Embedding);

/// <summary>
/// Beautiful TUI for ICD-10-CM code lookup.
/// </summary>
sealed class Icd10Cli : IDisposable
{
    readonly SqliteConnection _db;
    readonly HttpClient _http;
    readonly List<string> _history = [];
    readonly string _embeddingUrl;
    readonly IAnsiConsole _console;
    readonly bool _ownsHttpClient;

    /// <summary>
    /// Creates CLI with default HttpClient.
    /// </summary>
    public Icd10Cli(string dbPath, IAnsiConsole console)
        : this(dbPath, console, new HttpClient(), ownsHttpClient: true) { }

    /// <summary>
    /// Creates CLI with injected HttpClient for testing.
    /// </summary>
    public Icd10Cli(string dbPath, IAnsiConsole console, HttpClient httpClient, bool ownsHttpClient = false)
    {
        _db = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        _db.Open();
        _http = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _console = console;
        _embeddingUrl =
            Environment.GetEnvironmentVariable("EMBEDDING_URL") ?? "http://localhost:8000";
    }

    public async Task RunAsync()
    {
        RenderHeader();
        RenderStats();

        while (true)
        {
            AnsiConsole.WriteLine();
            var input = AnsiConsole.Prompt(new TextPrompt<string>("[cyan]icd10>[/]").AllowEmpty());

            if (string.IsNullOrWhiteSpace(input))
                continue;

            _history.Add(input);
            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? parts[1] : "";

            switch (cmd)
            {
                case "q":
                case "quit":
                case "exit":
                    RenderGoodbye();
                    return;

                case "?":
                case "h":
                case "help":
                    RenderHelp();
                    break;

                case "s":
                case "search":
                    if (string.IsNullOrWhiteSpace(arg))
                        AnsiConsole.MarkupLine("[yellow]Usage:[/] search <query>");
                    else
                        await SearchAsync(arg);
                    break;

                case "l":
                case "lookup":
                    if (string.IsNullOrWhiteSpace(arg))
                        AnsiConsole.MarkupLine("[yellow]Usage:[/] lookup <code>");
                    else
                        Lookup(arg);
                    break;

                case "f":
                case "find":
                    if (string.IsNullOrWhiteSpace(arg))
                        AnsiConsole.MarkupLine("[yellow]Usage:[/] find <text>");
                    else
                        Find(arg);
                    break;

                case "b":
                case "browse":
                    Browse(arg);
                    break;

                case "stats":
                    RenderStats();
                    break;

                case "history":
                    RenderHistory();
                    break;

                case "clear":
                    AnsiConsole.Clear();
                    RenderHeader();
                    break;

                default:
                    await SearchAsync(input);
                    break;
            }
        }
    }

    void RenderHeader()
    {
        var header = new FigletText("ICD-10-CM").Centered().Color(Color.Cyan1);

        AnsiConsole.Write(header);

        var panel = new Panel(
            "[grey]Medical Diagnosis Code Explorer[/]\n"
                + "[dim]Type [cyan]help[/] for commands, or just start typing to search[/]"
        )
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
    }

    void RenderHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan]Command[/]").Width(20))
            .AddColumn(new TableColumn("[cyan]Description[/]"));

        table.AddRow(
            "[green]search[/] [dim]<query>[/]",
            "Semantic RAG search (requires embeddings)"
        );
        table.AddRow("[green]find[/] [dim]<text>[/]", "Text search in descriptions");
        table.AddRow("[green]lookup[/] [dim]<code>[/]", "Direct code lookup (e.g., R07.9)");
        table.AddRow("[green]browse[/] [dim][[letter]][/]", "Browse codes by first letter");
        table.AddRow("[green]stats[/]", "Show database statistics");
        table.AddRow("[green]history[/]", "Show command history");
        table.AddRow("[green]clear[/]", "Clear screen");
        table.AddRow("[green]help[/]", "Show this help");
        table.AddRow("[green]quit[/]", "Exit the application");
        table.AddEmptyRow();
        table.AddRow("[dim]<anything else>[/]", "[dim]Treated as search query[/]");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine(
            "\n[dim]Shortcuts: s=search, f=find, l=lookup, b=browse, h=help, q=quit[/]"
        );
    }

    void RenderStats()
    {
        var codeCount = ExecuteScalar<long>("SELECT COUNT(*) FROM icd10cm_code");
        var embeddingCount = ExecuteScalar<long>("SELECT COUNT(*) FROM icd10cm_code_embedding");
        var billableCount = ExecuteScalar<long>(
            "SELECT COUNT(*) FROM icd10cm_code WHERE Billable = 1"
        );

        var grid = new Grid().AddColumn().AddColumn().AddColumn();

        grid.AddRow(
            new Panel($"[bold cyan]{codeCount:N0}[/]\n[dim]Total Codes[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Cyan1),
            new Panel($"[bold green]{embeddingCount:N0}[/]\n[dim]Embeddings[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(embeddingCount > 0 ? Color.Green : Color.Yellow),
            new Panel($"[bold blue]{billableCount:N0}[/]\n[dim]Billable[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Blue)
        );

        AnsiConsole.Write(grid);

        if (embeddingCount == 0)
        {
            AnsiConsole.MarkupLine(
                "[yellow]RAG search unavailable - no embeddings. Use [cyan]find[/] for text search.[/]"
            );
        }
    }

    void RenderHistory()
    {
        if (_history.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No history yet.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[dim]#[/]").RightAligned())
            .AddColumn("[cyan]Command[/]");

        for (var i = 0; i < _history.Count; i++)
        {
            table.AddRow($"[dim]{i + 1}[/]", _history[i].EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }

    async Task SearchAsync(string query)
    {
        var embeddingCount = ExecuteScalar<long>("SELECT COUNT(*) FROM icd10cm_code_embedding");
        if (embeddingCount == 0)
        {
            AnsiConsole.MarkupLine(
                "[yellow]No embeddings in database. Using text search instead...[/]"
            );
            Find(query);
            return;
        }

        ImmutableArray<float> queryEmbedding;
        try
        {
            queryEmbedding = await AnsiConsole
                .Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync(
                    "Generating embedding...",
                    async _ =>
                    {
                        var response = await _http.PostAsJsonAsync(
                            $"{_embeddingUrl}/embed",
                            new { text = query }
                        );

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(
                                $"Embedding service error: {response.StatusCode}"
                            );
                        }

                        var result = await response.Content.ReadFromJsonAsync<EmbedResponse>();
                        return result?.Embedding
                            ?? throw new InvalidOperationException("Empty embedding response");
                    }
                );
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[red]Embedding service unavailable:[/] {ex.Message.EscapeMarkup()}"
            );
            AnsiConsole.MarkupLine("[yellow]Falling back to text search...[/]");
            Find(query);
            return;
        }

        var results = AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .Start("Searching...", _ => ComputeSimilarity(queryEmbedding, limit: 15));

        RenderSearchResults(query, results);
    }

    ImmutableArray<SearchResult> ComputeSimilarity(ImmutableArray<float> queryEmbedding, int limit)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            SELECT c.Code, c.ShortDescription, c.LongDescription, e.Embedding
            FROM icd10cm_code c
            INNER JOIN icd10cm_code_embedding e ON c.Id = e.CodeId
            """;

        var results = new List<(SearchResult Result, double Score)>();
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var code = reader.GetString(0);
            var shortDesc = reader.GetString(1);
            var longDesc = reader.GetString(2);
            var embeddingJson = reader.GetString(3);

            var embedding = JsonSerializer.Deserialize<float[]>(embeddingJson);
            if (embedding is null)
                continue;

            var similarity = CosineSimilarity(queryEmbedding, embedding);
            results.Add((new SearchResult(code, shortDesc, longDesc, similarity), similarity));
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(limit)
            .Select(r => r.Result)
            .ToImmutableArray();
    }

    static double CosineSimilarity(ImmutableArray<float> a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        double dot = 0,
            magA = 0,
            magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(magA) * Math.Sqrt(magB);
        return magnitude > 0 ? dot / magnitude : 0;
    }

    void RenderSearchResults(string query, ImmutableArray<SearchResult> results)
    {
        AnsiConsole.MarkupLine($"\n[dim]RAG search for:[/] [cyan]{query.EscapeMarkup()}[/]");

        if (results.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No results found.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn(new TableColumn("[cyan]Score[/]").Width(7).RightAligned())
            .AddColumn(new TableColumn("[cyan]Code[/]").Width(10))
            .AddColumn(new TableColumn("[cyan]Description[/]"));

        foreach (var r in results)
        {
            var scoreColor = r.Similarity switch
            {
                > 0.8 => "green",
                > 0.6 => "yellow",
                _ => "dim",
            };

            table.AddRow(
                $"[{scoreColor}]{r.Similarity:P0}[/]",
                $"[bold]{r.Code.EscapeMarkup()}[/]",
                Truncate(r.ShortDescription, 60).EscapeMarkup()
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]{results.Length} results[/]");
    }

    void Find(string text)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Code, ShortDescription, LongDescription, Billable
            FROM icd10cm_code
            WHERE Code LIKE @search
               OR ShortDescription LIKE @search
               OR LongDescription LIKE @search
            ORDER BY
                CASE WHEN Code LIKE @exact THEN 0 ELSE 1 END,
                Code
            LIMIT 20
            """;
        cmd.Parameters.AddWithValue("@search", $"%{text}%");
        cmd.Parameters.AddWithValue("@exact", text);

        var codes = ReadCodes(cmd);
        RenderCodeList($"Text search: {text}", codes);
    }

    void Lookup(string code)
    {
        var normalized = code.ToUpperInvariant().Replace(".", "");
        if (normalized.Length > 3)
        {
            normalized = normalized[..3] + "." + normalized[3..];
        }

        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Code, ShortDescription, LongDescription, Billable
            FROM icd10cm_code
            WHERE Code = @code OR Code = @normalized OR Code LIKE @prefix
            ORDER BY Code
            LIMIT 20
            """;
        cmd.Parameters.AddWithValue("@code", code.ToUpperInvariant());
        cmd.Parameters.AddWithValue("@normalized", normalized);
        cmd.Parameters.AddWithValue("@prefix", $"{code.ToUpperInvariant()}%");

        var codes = ReadCodes(cmd);

        if (codes.Length == 1)
        {
            RenderCodeDetail(codes[0]);
        }
        else
        {
            RenderCodeList($"Lookup: {code}", codes);
        }
    }

    void Browse(string letter)
    {
        var prefix = string.IsNullOrEmpty(letter) ? "" : letter.ToUpperInvariant()[..1];

        if (string.IsNullOrEmpty(prefix))
        {
            RenderChapterOverview();
            return;
        }

        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Code, ShortDescription, LongDescription, Billable
            FROM icd10cm_code
            WHERE Code LIKE @prefix
            ORDER BY Code
            LIMIT 50
            """;
        cmd.Parameters.AddWithValue("@prefix", $"{prefix}%");

        var codes = ReadCodes(cmd);
        RenderCodeList($"Codes starting with {prefix}", codes);
    }

    void RenderChapterOverview()
    {
        var chapters = new[]
        {
            ("A-B", "Infectious and parasitic diseases", Color.Red),
            ("C-D", "Neoplasms / Blood diseases", Color.Maroon),
            ("E", "Endocrine, nutritional, metabolic", Color.Orange1),
            ("F", "Mental and behavioral disorders", Color.Yellow),
            ("G", "Nervous system diseases", Color.Green),
            ("H", "Eye and ear diseases", Color.Cyan1),
            ("I", "Circulatory system diseases", Color.Blue),
            ("J", "Respiratory system diseases", Color.Purple),
            ("K", "Digestive system diseases", Color.Fuchsia),
            ("L", "Skin diseases", Color.Pink1),
            ("M", "Musculoskeletal diseases", Color.Salmon1),
            ("N", "Genitourinary diseases", Color.Aqua),
            ("O", "Pregnancy and childbirth", Color.LightPink1),
            ("P", "Perinatal conditions", Color.LightYellow3),
            ("Q", "Congenital malformations", Color.LightGreen),
            ("R", "Symptoms and signs", Color.Grey),
            ("S-T", "Injury and poisoning", Color.DarkOrange),
            ("V-Y", "External causes", Color.DarkRed),
            ("Z", "Health status factors", Color.SteelBlue),
        };

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan]Range[/]").Width(8))
            .AddColumn(new TableColumn("[cyan]Chapter[/]"))
            .AddColumn(new TableColumn("[cyan]Count[/]").RightAligned());

        foreach (var (range, desc, color) in chapters)
        {
            var letters = range.Contains('-')
                ? string.Join(
                    ",",
                    Enumerable.Range(range[0], range[2] - range[0] + 1).Select(c => $"'{(char)c}%'")
                )
                : $"'{range}%'";

            var count = ExecuteScalar<long>(
                $"SELECT COUNT(*) FROM icd10cm_code WHERE {string.Join(" OR ", range.Replace("-", "").Select(c => $"Code LIKE '{c}%'"))}"
            );

            table.AddRow(
                $"[{color.ToMarkup()}]{range}[/]",
                desc.EscapeMarkup(),
                $"[dim]{count:N0}[/]"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("\n[dim]Type [cyan]browse <letter>[/] to explore a chapter[/]");
    }

    void RenderCodeList(string title, ImmutableArray<Icd10Code> codes)
    {
        AnsiConsole.MarkupLine($"\n[dim]{title.EscapeMarkup()}[/]");

        if (codes.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No codes found.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan]Code[/]").Width(10))
            .AddColumn(new TableColumn("[cyan]Description[/]"))
            .AddColumn(new TableColumn("[cyan]$[/]").Width(3).Centered());

        foreach (var code in codes)
        {
            table.AddRow(
                $"[bold]{code.Code.EscapeMarkup()}[/]",
                Truncate(code.ShortDescription, 55).EscapeMarkup(),
                code.Billable ? "[green]\u2713[/]" : "[dim]-[/]"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]{codes.Length} codes ([green]\u2713[/] = billable)[/]");
    }

    void RenderCodeDetail(Icd10Code code)
    {
        var panel = new Panel(
            new Rows(
                new Markup($"[bold cyan]{code.Code.EscapeMarkup()}[/]"),
                new Rule().RuleStyle("grey"),
                new Markup($"[bold]{code.ShortDescription.EscapeMarkup()}[/]"),
                new Text(""),
                new Markup($"[dim]{code.LongDescription.EscapeMarkup()}[/]"),
                new Text(""),
                new Markup(
                    code.Billable
                        ? "[green]\u2713 Billable[/]"
                        : "[yellow]Not directly billable (category code)[/]"
                )
            )
        )
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Header("[cyan] Code Detail [/]")
            .Padding(1, 1);

        AnsiConsole.Write(panel);
    }

    void RenderGoodbye()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[cyan]Goodbye![/]").RuleStyle("grey"));
    }

    ImmutableArray<Icd10Code> ReadCodes(SqliteCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        var codes = new List<Icd10Code>();

        while (reader.Read())
        {
            codes.Add(
                new Icd10Code(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4) == 1
                )
            );
        }

        return codes.ToImmutableArray();
    }

    T ExecuteScalar<T>(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        return (T)Convert.ChangeType(result!, typeof(T));
    }

    static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";

    public void Dispose()
    {
        _db.Dispose();
        _http.Dispose();
    }
}
