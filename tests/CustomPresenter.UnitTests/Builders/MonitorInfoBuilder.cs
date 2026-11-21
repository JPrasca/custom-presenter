using CustomPresenter.Domain.Display;

namespace CustomPresenter.UnitTests.Builders;

internal sealed class MonitorInfoBuilder
{
    private string _id = "mon-1";
    private string _name = "Monitor Principal";
    private int _x;
    private int _y;
    private int _width = 1920;
    private int _height = 1080;
    private bool _isPrimary = true;

    public MonitorInfoBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public MonitorInfoBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public MonitorInfoBuilder WithPosition(int x, int y)
    {
        _x = x;
        _y = y;
        return this;
    }

    public MonitorInfoBuilder WithResolution(int width, int height)
    {
        _width = width;
        _height = height;
        return this;
    }

    public MonitorInfoBuilder AsSecondary()
    {
        _isPrimary = false;
        return this;
    }

    public MonitorInfo Build() => new(_id, _name, _x, _y, _width, _height, _isPrimary);
}
