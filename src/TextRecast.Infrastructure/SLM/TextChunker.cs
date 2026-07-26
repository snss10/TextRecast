namespace TextRecast.Infrastructure.SLM;

internal static class TextChunker
{
    internal sealed record Chunk(string Text, string Separator);

    public static IReadOnlyList<Chunk> Split(string text, int maximumCharacters)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumCharacters, 64);

        var chunks = new List<Chunk>();
        var start = 0;
        while (start < text.Length)
        {
            while (start < text.Length && char.IsWhiteSpace(text[start]))
            {
                start++;
            }

            if (start >= text.Length)
            {
                break;
            }

            var split = FindSplit(text, start, maximumCharacters);
            var contentEnd = split;
            while (contentEnd > start && char.IsWhiteSpace(text[contentEnd - 1]))
            {
                contentEnd--;
            }

            var separatorEnd = split;
            while (separatorEnd < text.Length && char.IsWhiteSpace(text[separatorEnd]))
            {
                separatorEnd++;
            }

            var separator = text[contentEnd..separatorEnd];
            if (separator.Length == 0 && separatorEnd < text.Length)
            {
                separator = " ";
            }

            chunks.Add(new Chunk(text[start..contentEnd], separator));
            start = separatorEnd;
        }

        return chunks;
    }

    private static int FindSplit(string text, int start, int maximumCharacters)
    {
        var hardEnd = Math.Min(start + maximumCharacters, text.Length);
        if (hardEnd == text.Length)
        {
            return hardEnd;
        }

        if (char.IsHighSurrogate(text[hardEnd - 1]) && char.IsLowSurrogate(text[hardEnd]))
        {
            hardEnd--;
        }

        var minimumBoundary = start + maximumCharacters / 2;
        var whitespaceBoundary = -1;
        for (var index = hardEnd - 1; index >= minimumBoundary; index--)
        {
            if (text[index] == '\n')
            {
                return index + 1;
            }

            if (index + 1 < text.Length &&
                text[index] is '.' or '!' or '?' or ';' &&
                char.IsWhiteSpace(text[index + 1]))
            {
                return index + 1;
            }

            if (whitespaceBoundary < 0 && char.IsWhiteSpace(text[index]))
            {
                whitespaceBoundary = index;
            }
        }

        return whitespaceBoundary > start ? whitespaceBoundary : hardEnd;
    }
}
