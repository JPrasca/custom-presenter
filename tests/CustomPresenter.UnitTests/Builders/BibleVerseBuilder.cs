using CustomPresenter.Domain.Bibles;

namespace CustomPresenter.UnitTests.Builders;

internal sealed class BibleVerseBuilder
{
    private Guid _versionId = Guid.NewGuid();
    private int _bookNumber = 19;
    private string _bookName = "Salmos";
    private int _chapter = 23;
    private int _verse = 1;
    private string _text = "Jehová es mi pastor; nada me faltará.";

    public BibleVerseBuilder WithVersionId(Guid versionId)
    {
        _versionId = versionId;
        return this;
    }

    public BibleVerseBuilder WithBook(int bookNumber, string bookName)
    {
        _bookNumber = bookNumber;
        _bookName = bookName;
        return this;
    }

    public BibleVerseBuilder WithReference(int chapter, int verse)
    {
        _chapter = chapter;
        _verse = verse;
        return this;
    }

    public BibleVerseBuilder WithText(string text)
    {
        _text = text;
        return this;
    }

    public BibleVerse Build() => new(_versionId, _bookNumber, _bookName, _chapter, _verse, _text);
}
