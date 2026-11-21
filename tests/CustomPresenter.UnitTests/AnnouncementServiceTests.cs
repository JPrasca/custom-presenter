using CustomPresenter.Application.Announcements;
using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Announcements;
using CustomPresenter.Domain.Live;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class AnnouncementServiceTests
{
    private sealed class InMemoryAnnouncementRepository : IAnnouncementRepository
    {
        private readonly Dictionary<Guid, Announcement> _store = [];

        public Task CreateAsync(Announcement announcement, CancellationToken cancellationToken = default)
        {
            _store[announcement.Id] = announcement;
            return Task.CompletedTask;
        }

        public Task<Announcement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.GetValueOrDefault(id));

        public Task<IReadOnlyList<Announcement>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Announcement>>(_store.Values.OrderBy(a => a.Title).ToList());

        public Task<bool> UpdateAsync(Announcement announcement, CancellationToken cancellationToken = default)
        {
            if (!_store.ContainsKey(announcement.Id))
            {
                return Task.FromResult(false);
            }

            _store[announcement.Id] = announcement;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.Remove(id));
    }

    [Fact]
    public async Task CreateYListAsyncDividenBloquesDeTextoYPersisten()
    {
        // Arrange
        var repo = new InMemoryAnnouncementRepository();
        var service = new AnnouncementService(repo);
        var template = new AnnouncementBuilder().Build();

        // Act
        var created = await service.CreateAsync(template.Title, template.RawBody);
        var all = await service.ListAllAsync();

        // Assert
        Assert.Single(all);
        Assert.Equal(2, created.Slides.Count);
        Assert.Equal("Reunión de oración el miércoles a las 7:00 PM", created.Slides[0].Text);
        Assert.Equal("Escuelita dominical a las 9:30 AM", created.Slides[1].Text);
    }

    [Fact]
    public async Task ProjectSlideAsyncProyectaBloqueSeleccionadoEnLiveStateManager()
    {
        // Arrange
        var repo = new InMemoryAnnouncementRepository();
        var service = new AnnouncementService(repo);
        var liveManager = new LiveStateManager([]);
        var created = await service.CreateAsync("Bienvenida", "Hola a todos\n\nDios les bendiga");

        // Act
        var state = await service.ProjectSlideAsync(created.Id, slideIndex: 1, liveManager);

        // Assert
        Assert.True(state.IsTextVisible);
        Assert.Equal("Dios les bendiga", state.CurrentSlide?.Text);
        Assert.Equal("Bienvenida", state.CurrentSlide?.Reference);
        Assert.Equal($"announcement:{created.Id}:slide:1", state.CurrentSlide?.Id);
    }

    [Fact]
    public async Task ProjectSlideAsyncConIndiceFueraDeRangoLanzaDomainException()
    {
        // Arrange
        var repo = new InMemoryAnnouncementRepository();
        var service = new AnnouncementService(repo);
        var liveManager = new LiveStateManager([]);
        var created = await service.CreateAsync("Aviso", "Único bloque");

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => service.ProjectSlideAsync(created.Id, slideIndex: 5, liveManager));
    }

    [Fact]
    public async Task CreateConTituloOCuerpoVacioLanzaDomainException()
    {
        // Arrange
        var service = new AnnouncementService(new InMemoryAnnouncementRepository());

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync("  ", "Texto válido"));
        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync("Título", "   \n  "));
    }
}
