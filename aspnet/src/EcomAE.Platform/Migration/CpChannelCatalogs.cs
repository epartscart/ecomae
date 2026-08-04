namespace EcomAE.Platform.Migration;

/// <summary>
/// Same-to-same CP channel catalogs (mirrors PHP <c>epc_channel_carriers_catalog</c> /
/// <c>epc_channel_marketplaces_catalog</c>). config_json / secrets omitted.
/// </summary>
public static class CpChannelCatalogs
{
    private static readonly Dictionary<string, CpCarrierCatalogMeta> Carriers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dhl"] = new("DHL Express", "Global", "Express worldwide via Dubai & European hubs"),
        ["fedex"] = new("FedEx", "Global", "International Priority & Economy from UAE"),
        ["ups"] = new("UPS", "Global", "Worldwide Saver & freight for B2B parts"),
        ["tnt"] = new("TNT Express", "Global", "Europe & global express (FedEx network)"),
        ["aramex"] = new("Aramex", "MENA", "MENA parcel express & domestic UAE"),
        ["smsa"] = new("SMSA Express", "MENA", "Saudi & GCC last-mile express"),
        ["naqel"] = new("Naqel Express", "MENA", "KSA / GCC e-commerce & B2B delivery"),
        ["emirates_post"] = new("Emirates Post", "MENA", "UAE national post & international parcels"),
        ["imile"] = new("iMile", "MENA", "Middle East & Asia cross-border parcels"),
        ["dpd"] = new("DPD", "Europe", "European road & Predict delivery"),
        ["gls"] = new("GLS", "Europe", "EuroBusinessParcel across EU"),
        ["postnl"] = new("PostNL", "Europe", "Netherlands & EU parcels"),
        ["royal_mail"] = new("Royal Mail", "Europe", "UK Tracked & International Signed"),
        ["chronopost"] = new("Chronopost", "Europe", "France express & Europe Chrono"),
        ["usps"] = new("USPS", "Americas", "US Priority Mail International"),
        ["canada_post"] = new("Canada Post", "Americas", "Xpresspost International"),
        ["sf_express"] = new("SF Express", "Asia", "China & Asia Pacific express"),
        ["jt_express"] = new("J&T Express", "Asia", "SEA & Middle East e-commerce parcels"),
        ["yamato"] = new("Yamato Transport", "Asia", "Japan TA-Q-BIN & international"),
        ["bluedart"] = new("Blue Dart", "Asia", "India domestic & export express"),
    };

    private static readonly Dictionary<string, CpMarketplaceCatalogMeta> Marketplaces = new(StringComparer.OrdinalIgnoreCase)
    {
        ["amazon"] = new("Amazon.ae", "Amazon", "MENA", "SP-API", "Amazon UAE — SP-API listings & FBA/FBM"),
        ["ebay"] = new("eBay US / Motors", "eBay", "Americas", "Sell API", "eBay Sell API — US + Motors inventory"),
        ["amazon_com"] = new("Amazon.com", "Amazon", "Americas", "SP-API", "Amazon US — SP-API North America"),
        ["amazon_ca"] = new("Amazon.ca", "Amazon", "Americas", "SP-API", "Amazon Canada"),
        ["amazon_mx"] = new("Amazon.com.mx", "Amazon", "Americas", "SP-API", "Amazon Mexico"),
        ["amazon_br"] = new("Amazon.com.br", "Amazon", "Americas", "SP-API", "Amazon Brazil"),
        ["amazon_uk"] = new("Amazon.co.uk", "Amazon", "Europe", "SP-API", "Amazon United Kingdom"),
        ["amazon_de"] = new("Amazon.de", "Amazon", "Europe", "SP-API", "Amazon Germany"),
        ["amazon_fr"] = new("Amazon.fr", "Amazon", "Europe", "SP-API", "Amazon France"),
        ["amazon_it"] = new("Amazon.it", "Amazon", "Europe", "SP-API", "Amazon Italy"),
        ["amazon_es"] = new("Amazon.es", "Amazon", "Europe", "SP-API", "Amazon Spain"),
        ["amazon_nl"] = new("Amazon.nl", "Amazon", "Europe", "SP-API", "Amazon Netherlands"),
        ["amazon_sa"] = new("Amazon.sa", "Amazon", "MENA", "SP-API", "Amazon Saudi Arabia"),
        ["amazon_eg"] = new("Amazon.eg", "Amazon", "MENA", "SP-API", "Amazon Egypt"),
        ["amazon_in"] = new("Amazon.in", "Amazon", "Asia", "SP-API", "Amazon India"),
        ["amazon_au"] = new("Amazon.com.au", "Amazon", "Asia", "SP-API", "Amazon Australia"),
        ["amazon_jp"] = new("Amazon.co.jp", "Amazon", "Asia", "SP-API", "Amazon Japan"),
        ["amazon_ae"] = new("Amazon.ae", "Amazon", "MENA", "SP-API", "Amazon UAE — SP-API listings & FBA/FBM"),
        ["ebay_uk"] = new("eBay UK", "eBay", "Europe", "Sell API", "eBay.co.uk Sell API"),
        ["ebay_de"] = new("eBay Germany", "eBay", "Europe", "Sell API", "eBay.de Sell API"),
        ["ebay_au"] = new("eBay Australia", "eBay", "Asia", "Sell API", "eBay.com.au Sell API"),
        ["ebay_ca"] = new("eBay Canada", "eBay", "Americas", "Sell API", "eBay.ca Sell API"),
        ["ebay_fr"] = new("eBay France", "eBay", "Europe", "Sell API", "eBay.fr Sell API"),
        ["noon"] = new("noon UAE", "noon", "MENA", "noon Partner", "noon.com UAE — catalogue & fulfilment"),
        ["noon_sa"] = new("noon KSA", "noon", "MENA", "noon Partner", "noon.com Saudi Arabia"),
        ["noon_eg"] = new("noon Egypt", "noon", "MENA", "noon Partner", "noon.com Egypt"),
        ["dubizzle"] = new("dubizzle", "Classifieds", "MENA", "Partner API", "UAE classifieds & auto parts listings"),
        ["salla"] = new("Salla", "Commerce", "MENA", "Salla API", "Saudi commerce platform for brands"),
        ["jumia"] = new("Jumia", "Commerce", "Africa", "Seller Center", "Pan-African marketplace"),
        ["daraz_pk"] = new("Daraz Pakistan", "Commerce", "Asia", "Daraz Open", "Daraz / Alibaba Pakistan"),
        ["flipkart"] = new("Flipkart", "Commerce", "Asia", "Seller API", "India Flipkart Seller Hub"),
        ["allegro"] = new("Allegro", "Commerce", "Europe", "Allegro REST", "Poland Allegro REST API"),
        ["mercadolibre"] = new("Mercado Libre", "Commerce", "Americas", "MELI API", "LATAM Mercado Libre"),
        ["walmart"] = new("Walmart Marketplace", "Commerce", "Americas", "Marketplace API", "Walmart US Marketplace API"),
        ["etsy"] = new("Etsy", "Commerce", "Global", "Open API v3", "Etsy Open API v3 listings"),
        ["shopify"] = new("Shopify Channel", "Commerce", "Global", "Admin API", "Push catalogue to any Shopify storefront"),
    };

    public static bool TryGetCarrier(string code, out CpCarrierCatalogMeta meta) =>
        Carriers.TryGetValue(code ?? "", out meta!);

    public static bool TryGetMarketplace(string code, out CpMarketplaceCatalogMeta meta) =>
        Marketplaces.TryGetValue(code ?? "", out meta!);
}

public sealed record CpCarrierCatalogMeta(string Name, string Region, string Blurb);
public sealed record CpMarketplaceCatalogMeta(string Name, string Family, string Region, string Api, string Blurb);
