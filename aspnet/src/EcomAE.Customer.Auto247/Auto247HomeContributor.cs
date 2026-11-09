using EcomAE.Platform.Middleware;
using EcomAE.Platform.Packages;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EcomAE.Customer.Auto247;

/// <summary>
/// Auto247 customer-package storefront home: the premium navy/blue/white design
/// (top info bar, logo nav, hero + multi-type search, quick-access cards,
/// benefits strip, services, request band, footer). All links/forms point at the
/// existing storefront routes — no new backend surface. Rendered only for the
/// auto247 tenant while <c>customer.auto247</c> is installed+enabled (fail-closed).
/// </summary>
public sealed class Auto247HomeContributor : IStorefrontHomeContributor
{
    public string SiteKey => Auto247Package.OwnerSiteKey;

    public string PackageId => Auto247Package.PackageId;

    public async Task<bool> AppliesToAsync(HttpContext? context, CancellationToken cancellationToken = default)
    {
        if (context?.Items[TenantResolutionMiddleware.HttpContextItemKey]
            is not TenantContext { SiteKey: Auto247Package.OwnerSiteKey })
        {
            return false;
        }

        var gate = context.RequestServices?.GetService<IPackageGate>();
        if (gate is null)
        {
            return false;
        }

        try
        {
            return await gate.IsEnabledAsync(Auto247Package.OwnerSiteKey, Auto247Package.PackageId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public Task<string> RenderHomeHtmlAsync(HttpContext? context, CancellationToken cancellationToken = default)
    {
        var lang = context is null ? "/en" : StorefrontPhpHomeLinks.LangHref(context);
        var parts = StorefrontPhpHomeLinks.Parts(lang);
        var umapi = StorefrontPhpHomeLinks.UmapiCatalog(lang);
        var brands = StorefrontPhpHomeLinks.AvailableBrands(lang);
        var seller = StorefrontPhpHomeLinks.SellerRequest(lang);

        var html = $$"""
<link rel="stylesheet" href="/platform-assets/epc-storefront-home-auto247.css?v=20261119a">
<div class="a247-home">
  <div class="a247-topbar">
    <div class="a247-wrap">
      <div>
        <span>&#9906; Dubai, United Arab Emirates</span><span class="a247-sep">|</span>
        <span>&#9990; +971-56-767-011</span><span class="a247-sep">|</span>
        <span>Mon&ndash;Sat, 9:00 AM &ndash; 6:00 PM</span>
      </div>
      <div>
        <a href="/storefront/garage-manager-app">Garage Manager</a><span class="a247-sep">|</span>
        <a href="/erp">ERP Login</a><span class="a247-sep">|</span>
        <a href="/cp">Control Panel</a><span class="a247-sep">|</span>
        <a href="/storefront/login-app">Login</a>
      </div>
    </div>
  </div>

  <header class="a247-nav">
    <div class="a247-wrap">
      <a href="/"><img src="/platform-assets/auto247-logo.png" alt="AUTO247"></a>
      <nav class="a247-links">
        <a href="/">Home</a>
        <a href="{{parts}}">Products</a>
        <a href="#a247-services">Services</a>
        <a href="{{umapi}}">Electronic Catalog</a>
        <a href="{{seller}}">Contact Us</a>
      </nav>
      <div class="a247-actions">
        <select id="a247_currency" aria-label="Display currency"
                style="border:1px solid #d7e3f2;border-radius:8px;padding:8px 10px;font:600 .82rem system-ui;color:#172B45;background:#F3F7FC"
                onchange="var v=this.value;document.cookie='epc_currency='+v+';max-age=31536000;path=/;secure;samesite=lax';document.cookie='epc_currency_host=;max-age=0;path=/';location.reload();">
          {{CurrencyOptions()}}
        </select>
        <a class="a247-btn a247-btn--navy" href="{{seller}}">Request a Call Back</a>
        <a class="a247-btn a247-btn--wa" href="https://wa.me/97156767011" target="_blank" rel="noopener">WhatsApp</a>
        <a class="a247-btn a247-btn--ghost" href="{{lang}}/shop/bulk-upload">Upload Parts List</a>
      </div>
    </div>
  </header>

  <section class="a247-hero">
    <div class="a247-wrap">
      <div>
        <h1>Find genuine and aftermarket spare parts faster, cleaner, and with confidence.</h1>
        <p>Buy and sell automotive and automotive parts across the GCC &mdash; search by part number, VIN, brand, engine or vehicle.</p>
        <form class="a247-search" id="a247_search" action="{{lang}}/shop/search" method="GET" role="search"
              onsubmit="var t=document.getElementById('a247_stype').value,i=this.querySelector('input');if(t==='vin'){this.action='/storefront/vin-app';i.name='vin';}else{i.name='search_string';}return true;">
          <select id="a247_stype" aria-label="Search by">
            <option value="part">Part Number</option>
            <option value="vin">VIN</option>
            <option value="brand">Brand</option>
            <option value="engine">Engine</option>
            <option value="vehicle">Vehicle</option>
          </select>
          <input type="text" name="search_string" placeholder="Enter part number, VIN, brand or vehicle&hellip;" autocomplete="off">
          <button type="submit">Search</button>
        </form>
      </div>
      <div class="a247-heroart" aria-hidden="true">
        <svg viewBox="0 0 520 260" xmlns="http://www.w3.org/2000/svg">
          <path d="M40 190 C60 150 110 120 170 112 C230 70 330 66 400 100 C440 112 470 140 480 170 L490 200 C490 214 480 222 466 222 L54 222 C40 222 30 212 32 198 Z" fill="#0e5dc9"/>
          <path d="M150 116 C200 82 300 78 372 104 L360 122 L165 128 Z" fill="#9fd0ff" opacity=".85"/>
          <circle cx="140" cy="220" r="34" fill="#041f42"/><circle cx="140" cy="220" r="16" fill="#22B8F0"/>
          <circle cx="392" cy="220" r="34" fill="#041f42"/><circle cx="392" cy="220" r="16" fill="#22B8F0"/>
          <rect x="20" y="196" width="480" height="10" rx="5" fill="#041f42"/>
          <circle cx="470" cy="100" r="26" fill="none" stroke="#22B8F0" stroke-width="7"/>
          <circle cx="470" cy="100" r="9" fill="#22B8F0"/>
          <circle cx="60" cy="60" r="18" fill="none" stroke="#087CF0" stroke-width="6"/>
        </svg>
      </div>
    </div>
    <div class="a247-wrap a247-quick">
      <a class="a247-card" href="{{parts}}"><span class="a247-ico">&#128269;</span><h3>Search by Part Number</h3><p>OEM and aftermarket numbers, instant match.</p></a>
      <a class="a247-card" href="{{brands}}"><span class="a247-ico">&#127942;</span><h3>Trusted Brands</h3><p>Genuine brands and verified suppliers.</p></a>
      <a class="a247-card" href="{{umapi}}"><span class="a247-ico">&#128214;</span><h3>Electronic Catalog</h3><p>Full illustrated electronic parts catalog.</p></a>
      <a class="a247-card" href="/storefront/vin-app"><span class="a247-ico">&#128663;</span><h3>VIN Request</h3><p>Decode your VIN for exact-fit parts.</p></a>
    </div>
  </section>

  <div class="a247-strip">
    <div class="a247-wrap">
      <span>&#10003; Genuine Parts Guaranteed</span>
      <span>&#128666; Fast Delivery</span>
      <span>&#128222; Expert Support</span>
      <span>&#9881; Complete Automotive Solutions</span>
    </div>
  </div>

  <section class="a247-sec" id="a247-services">
    <div class="a247-wrap">
      <h2>Our Services</h2>
      <p class="a247-sub">Everything you need to buy and sell automotive parts &mdash; one account, all GCC domains.</p>
      <div class="a247-grid3">
        <div class="a247-svc"><h3>Part &amp; Vehicle Search</h3><p>Search by part number, brand, engine or vehicle across the full catalog.</p><a href="{{parts}}">Search parts &rarr;</a></div>
        <div class="a247-svc"><h3>VIN Decoding</h3><p>Decode any VIN to pinpoint exact-fit genuine and aftermarket parts.</p><a href="/storefront/vin-app">Decode a VIN &rarr;</a></div>
        <div class="a247-svc"><h3>Electronic Parts Catalog</h3><p>Browse the illustrated e-catalog and original manufacturer catalog.</p><a href="{{umapi}}">Open catalog &rarr;</a></div>
        <div class="a247-svc"><h3>Excel Bulk Upload</h3><p>Upload a parts list in Excel and get availability and pricing in bulk.</p><a href="{{lang}}/shop/bulk-upload">Upload a list &rarr;</a></div>
        <div class="a247-svc"><h3>Vehicle Parts Intelligence</h3><p>AI-assisted parts expert for fitment questions and alternatives.</p><a href="/storefront/ai-parts-expert-app">Ask the AI &rarr;</a></div>
        <div class="a247-svc"><h3>Garage Manager</h3><p>Manage vehicles, quotes and orders for workshops and fleets.</p><a href="/storefront/garage-manager-app">Open Garage Manager &rarr;</a></div>
      </div>
    </div>
  </section>

  <section class="a247-sec" style="padding-top:0">
    <div class="a247-wrap">
      <div class="a247-cta">
        <div>
          <h2>Didn't Find the Part You Need?</h2>
          <p>Send your request to our sellers &mdash; we source genuine and aftermarket parts worldwide.</p>
        </div>
        <a class="a247-btn a247-btn--blue" style="font-size:1rem;padding:13px 26px" href="{{seller}}">Send a Request to Seller</a>
      </div>
    </div>
  </section>

  <footer class="a247-foot">
    <div class="a247-wrap">
      <div>
        <img src="/platform-assets/auto247-logo.png" alt="AUTO247">
        <p>Buy and sell automotive and automotive parts across the GCC &mdash; UAE, Saudi Arabia, Qatar, Kuwait, Bahrain, Oman and Pakistan.</p>
      </div>
      <div>
        <h4>Shop</h4>
        <ul>
          <li><a href="{{parts}}">Search Parts</a></li>
          <li><a href="{{brands}}">Brands</a></li>
          <li><a href="{{umapi}}">Electronic Catalog</a></li>
          <li><a href="/en/original-catalog">Original Catalog</a></li>
          <li><a href="{{lang}}/shop/bulk-upload">Bulk Upload</a></li>
        </ul>
      </div>
      <div>
        <h4>Services</h4>
        <ul>
          <li><a href="/storefront/vin-app">VIN Request</a></li>
          <li><a href="/storefront/ai-parts-expert-app">Parts Intelligence AI</a></li>
          <li><a href="/storefront/garage-manager-app">Garage Manager</a></li>
          <li><a href="{{seller}}">Request to Seller</a></li>
        </ul>
      </div>
      <div>
        <h4>Contact</h4>
        <ul>
          <li>Dubai, United Arab Emirates</li>
          <li><a href="tel:+97156767011">+971-56-767-011</a></li>
          <li>Mon&ndash;Sat, 9:00 AM &ndash; 6:00 PM</li>
          <li><a href="https://wa.me/97156767011">WhatsApp</a></li>
        </ul>
      </div>
      <div class="a247-copy">&copy; AUTO247 &mdash; automotive &amp; automotive parts. All rights reserved.</div>
    </div>
  </footer>
</div>
<script>
  (function(){
    var m=document.cookie.match(/(?:^|;\s*)epc_currency=([^;]*)/);
    var s=document.getElementById('a247_currency');
    if(m&&s){s.value=decodeURIComponent(m[1]);}
    var st=document.getElementById('a247_stype');
    st.addEventListener('change',function(){
      document.querySelector('#a247_search input').placeholder =
        this.value==='vin'?'Enter 17-digit VIN&hellip;':'Enter part number, brand or vehicle&hellip;';
    });
  })();
</script>
""";

        return Task.FromResult(html);
    }

    private static string CurrencyOptions()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var iso in EcomAE.Platform.Storefront.EpcCurrency.SupportedIsoCodes)
        {
            var currency = EcomAE.Platform.Storefront.EpcCurrency.DefaultForIso(iso);
            sb.Append("<option value=\"").Append(iso).Append("\">")
                .Append(currency?.IsoName ?? iso).Append("</option>");
        }

        return sb.ToString();
    }
}
