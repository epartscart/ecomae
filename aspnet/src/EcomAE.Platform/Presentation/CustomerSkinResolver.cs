namespace EcomAE.Platform.Presentation;

/// <summary>
/// Resolves the active customer-package skin for the current request: the first
/// contributor whose tenant matches AND whose package is activated.
/// </summary>
public interface ICustomerSkinResolver
{
    Task<ICustomerSkinContributor?> ResolveAsync(HttpContext? context, CancellationToken cancellationToken = default);
}

public sealed class CustomerSkinResolver : ICustomerSkinResolver
{
    private readonly IEnumerable<ICustomerSkinContributor> _contributors;

    public CustomerSkinResolver(IEnumerable<ICustomerSkinContributor> contributors)
        => _contributors = contributors;

    public async Task<ICustomerSkinContributor?> ResolveAsync(HttpContext? context, CancellationToken cancellationToken = default)
    {
        foreach (var contributor in _contributors)
        {
            try
            {
                if (await contributor.AppliesToAsync(context, cancellationToken).ConfigureAwait(false))
                {
                    return contributor;
                }
            }
            catch
            {
                // A faulted contributor never blocks the storefront — fail-closed.
            }
        }

        return null;
    }
}
