using CustomPresenter.Domain.Live;

namespace CustomPresenter.UnitTests.Builders;

internal sealed class SlideContentBuilder
{
    private string _id = "slide-1";
    private string _title = "Título de prueba";
    private string _text = "Alabado sea su nombre";
    private string? _reference;

    public SlideContentBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public SlideContentBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public SlideContentBuilder WithText(string text)
    {
        _text = text;
        return this;
    }

    public SlideContentBuilder WithReference(string? reference)
    {
        _reference = reference;
        return this;
    }

    public SlideContent Build() => new(_id, _title, _text, _reference);
}
