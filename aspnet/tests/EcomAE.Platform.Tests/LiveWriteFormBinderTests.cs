using EcomAE.Platform.Migration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LiveWriteFormBinderTests
{
    [Fact]
    public void Form_content_wants_html_and_safe_return_url()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["confirmWrites"] = "true",
            ["returnUrl"] = "/storefront/cart-app",
            ["id"] = "42",
            ["countNeed"] = "3.5",
        });

        Assert.True(LiveWriteFormBinder.WantsHtml(context));
        Assert.True(LiveWriteFormBinder.Flag(context.Request.Form, "confirmWrites"));
        Assert.Equal(42, LiveWriteFormBinder.Long(context.Request.Form, "id"));
        Assert.Equal(3.5m, LiveWriteFormBinder.Dec(context.Request.Form, "countNeed"));
        Assert.Equal("/storefront/cart-app", LiveWriteFormBinder.ReturnUrl(context, "/fallback"));
    }

    [Fact]
    public void External_return_url_is_rejected()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["returnUrl"] = "https://evil.example/phish",
        });

        Assert.Equal("/erp/payroll-app", LiveWriteFormBinder.ReturnUrl(context, "/erp/payroll-app"));
    }

    [Fact]
    public void Complete_redirects_form_posts_with_ok_or_err()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["returnUrl"] = "/cp/credit-limits-app",
        });

        var ok = LiveWriteFormBinder.Complete(context, "/cp/credit-limits-app", true, "Credit limit saved.", new { ok = true });
        var redirect = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.HttpResults.RedirectHttpResult>(ok);
        Assert.Contains("/cp/credit-limits-app?ok=", redirect.Url, StringComparison.Ordinal);
        Assert.Contains("Credit%20limit%20saved.", redirect.Url, StringComparison.Ordinal);
    }
}
