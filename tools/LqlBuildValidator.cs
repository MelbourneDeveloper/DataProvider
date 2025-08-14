using System;
using System.IO;
using System.Text.RegularExpressions;
using Lql;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

/// <summary>
/// MSBuild task that validates LQL queries at build time
/// This will cause BUILD FAILURES for invalid LQL, achieving compile-time validation
/// </summary>
public class LqlBuildValidator : Microsoft.Build.Utilities.Task
{
    [Required]
    public ITaskItem[] SourceFiles { get; set; } = Array.Empty<ITaskItem>();

    private readonly Regex lqlPattern = new Regex(
        @"""([^""]*\|>[^""]*)\""",
        RegexOptions.Compiled | RegexOptions.Multiline
    );

    public override bool Execute()
    {
        bool success = true;
        int totalQueries = 0;
        int invalidQueries = 0;

        Log.LogMessage(MessageImportance.Normal, "🔍 Starting BUILD-TIME LQL validation...");

        foreach (var sourceFile in SourceFiles)
        {
            var filePath = sourceFile.ItemSpec;
            if (!File.Exists(filePath))
                continue;

            var content = File.ReadAllText(filePath);
            var matches = lqlPattern.Matches(content);

            foreach (Match match in matches)
            {
                var lqlQuery = match.Groups[1].Value;
                totalQueries++;

                Log.LogMessage(MessageImportance.Low, $"Validating LQL: {lqlQuery}");

                try
                {
                    // Use the C# LQL library to validate
                    var converter = new LqlStatementConverter();
                    var result = converter.ConvertLqlToSql(lqlQuery);

                    if (!result.Success)
                    {
                        invalidQueries++;
                        success = false;

                        // This causes a BUILD ERROR with detailed information
                        Log.LogError(
                            subcategory: "LQL",
                            errorCode: "LQL001",
                            helpKeyword: "InvalidLqlSyntax",
                            file: filePath,
                            lineNumber: GetLineNumber(content, match.Index),
                            columnNumber: GetColumnNumber(content, match.Index),
                            endLineNumber: 0,
                            endColumnNumber: 0,
                            message: $"❌ INVALID LQL SYNTAX: {result.ErrorMessage} in query: {lqlQuery}"
                        );
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.Low, $"✅ Valid LQL: {lqlQuery}");
                    }
                }
                catch (Exception ex)
                {
                    invalidQueries++;
                    success = false;

                    Log.LogError(
                        subcategory: "LQL",
                        errorCode: "LQL002",
                        helpKeyword: "LqlValidationError",
                        file: filePath,
                        lineNumber: GetLineNumber(content, match.Index),
                        columnNumber: GetColumnNumber(content, match.Index),
                        endLineNumber: 0,
                        endColumnNumber: 0,
                        message: $"❌ LQL VALIDATION ERROR: {ex.Message} in query: {lqlQuery}"
                    );
                }
            }
        }

        if (totalQueries > 0)
        {
            if (success)
            {
                Log.LogMessage(
                    MessageImportance.Normal,
                    $"✅ BUILD-TIME LQL VALIDATION PASSED: {totalQueries} queries validated successfully"
                );
            }
            else
            {
                Log.LogError(
                    $"❌ BUILD-TIME LQL VALIDATION FAILED: {invalidQueries} out of {totalQueries} queries are invalid"
                );
            }
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, "No LQL queries found to validate");
        }

        return success;
    }

    private int GetLineNumber(string content, int index)
    {
        return content.Substring(0, index).Split('\n').Length;
    }

    private int GetColumnNumber(string content, int index)
    {
        var lastNewLine = content.LastIndexOf('\n', index);
        return index - lastNewLine;
    }
}
