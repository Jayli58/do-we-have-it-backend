using System.Text.RegularExpressions;

namespace DoWeHaveItApp.Infrastructure;

public sealed class Tokenizer
{
    private static readonly Regex NonWordRegex = new("[^a-zA-Z0-9]+", RegexOptions.Compiled);

    // "Coffee Maker" => "coffee" & "maker" in tokens
    public IReadOnlyList<string> Tokenize(params string?[] values)
    {
        var tokens = new List<string>();

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalized = NonWordRegex.Replace(value.ToLowerInvariant().Trim(), " ");
            foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }
}
