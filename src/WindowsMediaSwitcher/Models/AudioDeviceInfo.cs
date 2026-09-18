namespace WindowsMediaSwitcher.Models;

public sealed class AudioDeviceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; init; } = true;
}
