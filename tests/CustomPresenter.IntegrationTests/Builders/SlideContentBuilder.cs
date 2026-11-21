using CustomPresenter.Domain.Live;

namespace CustomPresenter.IntegrationTests.Builders;

internal sealed class SlideContentBuilder
{
    private string _id = "int-1";
    private string _title = "Salmo 23";
    private string _text = "El Señor es mi pastor";
    private string? _reference = "Salmos 23:1";

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
