using TextRecast.Infrastructure.SLM;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class TextChunkerTests
{
    [TestMethod]
    public void SplitPreservesTextAcrossSentenceBoundaries()
    {
        const string input = "First sentence has enough words to matter. Second sentence also has several words.";

        var chunks = TextChunker.Split(input, 64);
        var reconstructed = string.Concat(chunks.Select(chunk => chunk.Text + chunk.Separator)).Trim();

        Assert.HasCount(2, chunks);
        Assert.AreEqual(input, reconstructed);
    }

    [TestMethod]
    public void SplitDoesNotSeparateSurrogatePairAtHardBoundary()
    {
        var input = new string('a', 63) + "😀" + new string('b', 20);

        var chunks = TextChunker.Split(input, 64);

        Assert.IsTrue(chunks.All(chunk =>
            !char.IsHighSurrogate(chunk.Text[^1]) &&
            !char.IsLowSurrogate(chunk.Text[0])));
        Assert.AreEqual(input, string.Concat(chunks.Select(chunk => chunk.Text)));
    }
}
