using EzCert.Processor.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Processor.Features.Guests;

// Guest device identity (AD-2): issues an HttpOnly guest_device_id cookie on
// first request and exposes the current device id via HttpContext.Items.
public static class GuestIdentity
{
    public const string CookieName = "guest_device_id";

    public static string GetOrCreateDeviceId(HttpContext ctx)
    {
        if (ctx.Items.TryGetValue("deviceId", out var cached) && cached is string c)
            return c;

        var id = ctx.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("N");
            ctx.Response.Cookies.Append(CookieName, id, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = ctx.Request.IsHttps,
                MaxAge = TimeSpan.FromDays(90),
            });
        }
        ctx.Items["deviceId"] = id;
        return id;
    }
}

public static class GuestMiddlewareExtensions
{
    public static IApplicationBuilder UseGuestIdentity(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var db = ctx.RequestServices.GetRequiredService<EzCertDbContext>();
        var id = GuestIdentity.GetOrCreateDeviceId(ctx);

        var known = await db.GuestDevices.FirstOrDefaultAsync(g => g.DeviceId == id);
        if (known is null)
        {
            db.GuestDevices.Add(new GuestDevice { DeviceId = id });
        }
        else
        {
            known.LastSeenAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        await next();
    });
}
