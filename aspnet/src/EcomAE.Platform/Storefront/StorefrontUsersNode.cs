namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/users/users_functions.php</c>: the /users node. Guests get the shared login form; signed-in
/// users get the profile link with the visitor's language prefix. PHP eats the newline after <c>?&gt;</c>.
/// Verified against PHP 8.3 by <c>Fixtures/StorefrontFragments/golden.json</c>.
/// </summary>
public static class StorefrontUsersNode
{
    public static string Render(long userId, string? langHref, string? lang)
    {
        if (userId == 0)
        {
            var target = (lang ?? string.Empty) + "/users";
            return "\n\n\t<div class=\"panel panel-primary\">\n\t[[LOGIN_FORM users_node_page " + target + "]]\t</div>\n    ";
        }

        var prefix = langHref ?? string.Empty;
        return "\n\n    \n    <div class=\"cat-item\">\n    \t<a href=\"" + prefix
            + "/users/profile\">\n    \t\t{4668}    \t</a>\n    </div>\n    \n    ";
    }
}
