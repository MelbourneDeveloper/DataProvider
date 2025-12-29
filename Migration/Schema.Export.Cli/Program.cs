using System.Reflection;
using Migration;

namespace Schema.Export.Cli;

/// <summary>
/// CLI tool to export C# schema definitions to YAML files.
/// Usage: dotnet run -- --assembly path/to/assembly.dll --type Namespace.SchemaClass --output path/to/schema.yaml
/// </summary>
public static class Program
{
    /// <summary>
    /// Entry point - loads assembly, finds schema, exports to YAML.
    /// </summary>
    public static int Main(string[] args)
    {
        var parseResult = ParseArguments(args);

        return parseResult switch
        {
            ParseResult.Success success => ExportSchema(success),
            ParseResult.ParseError error => ShowError(error),
            ParseResult.Help => ShowUsage(),
        };
    }

    private static int ExportSchema(ParseResult.Success args)
    {
        Console.WriteLine($"Schema.Export.Cli - Export C# Schema to YAML");
        Console.WriteLine($"  Assembly: {args.AssemblyPath}");
        Console.WriteLine($"  Type:     {args.TypeName}");
        Console.WriteLine($"  Output:   {args.OutputPath}");
        Console.WriteLine();

        if (!File.Exists(args.AssemblyPath))
        {
            Console.WriteLine($"Error: Assembly not found: {args.AssemblyPath}");
            return 1;
        }

        try
        {
            var assembly = Assembly.LoadFrom(args.AssemblyPath);
            var schemaType = assembly.GetType(args.TypeName);

            if (schemaType is null)
            {
                Console.WriteLine($"Error: Type '{args.TypeName}' not found in assembly");
                return 1;
            }

            // Try to find a static property or method that returns SchemaDefinition
            var schema = GetSchemaDefinition(schemaType);

            if (schema is null)
            {
                Console.WriteLine($"Error: Could not get SchemaDefinition from type '{args.TypeName}'");
                Console.WriteLine("  Expected: static property 'Definition' or static method 'Build()' returning SchemaDefinition");
                return 1;
            }

            // Export to YAML
            var directory = Path.GetDirectoryName(args.OutputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            SchemaYamlSerializer.ToYamlFile(schema, args.OutputPath);
            Console.WriteLine($"Successfully exported schema '{schema.Name}' with {schema.Tables.Count} tables");
            Console.WriteLine($"  Output: {args.OutputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
            return 1;
        }
    }

    private static SchemaDefinition? GetSchemaDefinition(Type schemaType)
    {
        // Try static property named "Definition"
        var definitionProp = schemaType.GetProperty(
            "Definition",
            BindingFlags.Public | BindingFlags.Static);

        if (definitionProp?.GetValue(null) is SchemaDefinition defFromProp)
        {
            return defFromProp;
        }

        // Try static method named "Build"
        var buildMethod = schemaType.GetMethod(
            "Build",
            BindingFlags.Public | BindingFlags.Static,
            Type.EmptyTypes);

        if (buildMethod?.Invoke(null, null) is SchemaDefinition defFromMethod)
        {
            return defFromMethod;
        }

        return null;
    }

    private static int ShowError(ParseResult.ParseError error)
    {
        Console.WriteLine($"Error: {error.Message}");
        Console.WriteLine();
        return ShowUsage();
    }

    private static int ShowUsage()
    {
        Console.WriteLine("Schema.Export.Cli - Export C# Schema to YAML");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project Migration/Schema.Export.Cli/Schema.Export.Cli.csproj -- \\");
        Console.WriteLine("      --assembly path/to/assembly.dll \\");
        Console.WriteLine("      --type Namespace.SchemaClass \\");
        Console.WriteLine("      --output path/to/schema.yaml");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --assembly, -a    Path to compiled assembly containing schema class");
        Console.WriteLine("  --type, -t        Fully qualified type name of schema class");
        Console.WriteLine("  --output, -o      Path to output YAML file");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Export ExampleSchema");
        Console.WriteLine("  dotnet run -- -a bin/Debug/net9.0/DataProvider.Example.dll \\");
        Console.WriteLine("               -t DataProvider.Example.ExampleSchema \\");
        Console.WriteLine("               -o example-schema.yaml");
        Console.WriteLine();
        Console.WriteLine("Schema Class Requirements:");
        Console.WriteLine("  - Static property 'Definition' returning SchemaDefinition, OR");
        Console.WriteLine("  - Static method 'Build()' returning SchemaDefinition");
        return 1;
    }

    private static ParseResult ParseArguments(string[] args)
    {
        string? assemblyPath = null;
        string? typeName = null;
        string? outputPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--assembly" or "-a":
                    if (i + 1 >= args.Length)
                    {
                        return new ParseResult.ParseError("--assembly requires a path argument");
                    }

                    assemblyPath = args[++i];
                    break;

                case "--type" or "-t":
                    if (i + 1 >= args.Length)
                    {
                        return new ParseResult.ParseError("--type requires a type name argument");
                    }

                    typeName = args[++i];
                    break;

                case "--output" or "-o":
                    if (i + 1 >= args.Length)
                    {
                        return new ParseResult.ParseError("--output requires a path argument");
                    }

                    outputPath = args[++i];
                    break;

                case "--help" or "-h":
                    return new ParseResult.Help();

                default:
                    if (arg.StartsWith('-'))
                    {
                        return new ParseResult.ParseError($"Unknown option: {arg}");
                    }

                    break;
            }
        }

        if (string.IsNullOrEmpty(assemblyPath))
        {
            return new ParseResult.ParseError("--assembly is required");
        }

        if (string.IsNullOrEmpty(typeName))
        {
            return new ParseResult.ParseError("--type is required");
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            return new ParseResult.ParseError("--output is required");
        }

        return new ParseResult.Success(assemblyPath, typeName, outputPath);
    }
}

/// <summary>Argument parsing result base.</summary>
public abstract record ParseResult
{
    private ParseResult() { }

    /// <summary>Successfully parsed arguments.</summary>
    public sealed record Success(string AssemblyPath, string TypeName, string OutputPath) : ParseResult;

    /// <summary>Parse error.</summary>
    public sealed record ParseError(string Message) : ParseResult;

    /// <summary>Help requested.</summary>
    public sealed record Help : ParseResult;
}
