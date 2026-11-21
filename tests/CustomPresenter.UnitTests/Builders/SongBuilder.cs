using CustomPresenter.Domain.Songs;

namespace CustomPresenter.UnitTests.Builders;

internal sealed class SongBuilder
{
    private string _title = "Cuán Grande Es Él";
    private string? _author = "Carl Boberg";
    private string? _musicalKey = "G";
    private string _rawLyrics = "Señor mi Dios, al contemplar los cielos\nEl firmamento y las estrellas mil\n\nMi corazón entona la canción\nCuán grande es Él, cuán grande es Él";

    public SongBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public SongBuilder WithAuthor(string? author)
    {
        _author = author;
        return this;
    }

    public SongBuilder WithMusicalKey(string? musicalKey)
    {
        _musicalKey = musicalKey;
        return this;
    }

    public SongBuilder WithRawLyrics(string rawLyrics)
    {
        _rawLyrics = rawLyrics;
        return this;
    }

    public Song Build() => Song.Create(_title, _author, _musicalKey, _rawLyrics);
}
