using CustomPresenter.Domain.Announcements;

namespace CustomPresenter.UnitTests.Builders;

internal sealed class AnnouncementBuilder
{
    private string _title = "Anuncios de la Semana";
    private string _rawBody = "Reunión de oración el miércoles a las 7:00 PM\n\nEscuelita dominical a las 9:30 AM";

    public AnnouncementBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public AnnouncementBuilder WithRawBody(string rawBody)
    {
        _rawBody = rawBody;
        return this;
    }

    public Announcement Build() => Announcement.Create(_title, _rawBody);
}
