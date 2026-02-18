using System.Text.RegularExpressions;

namespace DoWeHaveItApp.Infrastructure;

public sealed class Tokenizer
{
    private static readonly Regex NonWordRegex = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled);

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
                // if token contains cjk (Chinese, Japanese, Korean), add each character as a token
                if (ContainsCjk(token))
                {
                    foreach (var character in token)
                    {
                        if (IsCjk(character))
                        {
                            tokens.Add(character.ToString());
                        }
                    }
                }
            }
        }

        return tokens;
    }

    private static bool ContainsCjk(string token)
        => token.Any(IsCjk);

    private static bool IsCjk(char character)
    {
        return character is >= '\u4E00' and <= '\u9FFF'
            || character is >= '\u3400' and <= '\u4DBF'
            || character is >= '\uF900' and <= '\uFAFF'
            || character is >= '\u3040' and <= '\u30FF'
            || character is >= '\uAC00' and <= '\uD7AF';
    }
}
