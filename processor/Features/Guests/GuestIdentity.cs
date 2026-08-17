using EzCert.Processor.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Processor.Features.Guests;

// Guest device identity (AD-2): issues an HttpOnly guest_device_id cookie on
// first request and exposes the current device id via HttpContext.Items.
public static class GuestIdentity
{
    public const string CookieName = "guest_device_id";

    public static string GetOrCreateDeviceId(HttpContext ctx, bool crossSite = false)
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
                // Cross-site topology (AD-15): SPA on CloudFront, API on App
                // Runner. Lax would not be sent on cross-site fetch() calls,
                // so production uses None+Secure. Local dev is same-origin
                // through the Vite proxy, where Lax works (and Secure would
                // break plain http).
                SameSite = crossSite ? SameSiteMode.None : SameSiteMode.Lax,
                Secure = crossSite,
                MaxAge = TimeSpan.FromDays(90),
            });
        }
        ctx.Items["deviceId"] = id;
        return id;
    }
}

public static class GuestMiddlewareExtensions
{
    public static IApplicationBuilder UseGuestIdentity(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IHostEnvironment>();
        // Production SPA (CloudFront) and API (App Runner) are different
        // sites, so the guest cookie must survive cross-site requests.
        var crossSite = !env.IsDevelopment();
        return app.Use(async (ctx, next) =>
        {
            // Health paths stay DB-free: a Postgres outage must not turn the
            // health check into a 500 (that restarts the service on App Runner).
            if (ctx.Request.Path.StartsWithSegments("/health") ||
                ctx.Request.Path.StartsWithSegments("/api/health"))
            {
                await next();
                return;
            }
            var db = ctx.RequestServices.GetRequiredService<EzCertDbContext>();
            var id = GuestIdentity.GetOrCreateDeviceId(ctx, crossSite);

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
}
