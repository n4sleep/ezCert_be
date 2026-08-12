namespace EzCert.Processor.Infrastructure.Postgres;

// Guest device identity (AD-2). No User table; a UUID cookie identifies the device.
public class GuestDevice
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
