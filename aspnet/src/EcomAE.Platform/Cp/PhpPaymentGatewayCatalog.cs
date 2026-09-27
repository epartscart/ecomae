namespace EcomAE.Platform.Cp;

/// <summary>One credential/option widget of a gateway (<c>parameters</c> JSON of <c>shop_payment_systems</c>).</summary>
public sealed record PhpPaymentParameter(string Name, string Type, string Caption);

/// <summary>
/// A modern gateway definition of PHP <c>epc_payment_gateway_defs()</c>: region, target countries,
/// credential widgets and the demo values PHP seeds into <c>parameters_values</c>.
/// </summary>
public sealed record PhpPaymentGatewayDefinition(
    string Handler,
    string Title,
    string Description,
    string Region,
    IReadOnlyList<string> Countries,
    IReadOnlyList<PhpPaymentParameter> Parameters,
    IReadOnlyDictionary<string, string> Demo);

/// <summary>
/// C# twin of <c>content/shop/payments/epc_payment_helpers.php</c>: the gateway catalogue,
/// the region labels and the label/region lookups the CP hub renders with.
/// Titles and descriptions are the English strings PHP seeds through <c>epc_payment_lang_seed</c>.
/// </summary>
public static class PhpPaymentGatewayCatalog
{
    public const string RegionGcc = "gcc";
    public const string RegionPakistan = "pakistan";
    public const string RegionCrypto = "crypto";
    public const string RegionInternational = "international";
    public const string RegionLegacy = "legacy";

    /// <summary>PHP <c>epc_payment_region_labels()</c>, in the order the dashboard renders them.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> RegionLabels { get; } =
    [
        new(RegionGcc, "GCC & MENA"),
        new(RegionPakistan, "Pakistan"),
        new(RegionCrypto, "Cryptocurrency"),
        new(RegionInternational, "International"),
        new(RegionLegacy, "Legacy (CIS)")
    ];

    public static IReadOnlyDictionary<string, PhpPaymentGatewayDefinition> Definitions { get; } = Build();

    /// <summary>The <c>lang_text_strings</c> key PHP stores in <c>shop_payment_systems.name</c>.</summary>
    public static string NameKey(string handler) => "epc_pay_" + handler;

    /// <summary>The <c>lang_text_strings</c> key PHP stores in <c>shop_payment_systems.description</c>.</summary>
    public static string DescriptionKey(string handler) => "epc_pay_" + handler + "_desc";

    public static string RegionLabel(string region)
    {
        foreach (var pair in RegionLabels)
        {
            if (string.Equals(pair.Key, region, StringComparison.Ordinal))
            {
                return pair.Value;
            }
        }

        return region;
    }

    /// <summary>PHP <c>epc_payment_handler_region</c> — handlers outside the catalogue are the legacy CIS rails.</summary>
    public static string Region(string? handler)
        => handler is not null && Definitions.TryGetValue(handler, out var def) ? def.Region : RegionLegacy;

    public static bool IsModern(string? handler)
        => handler is not null && Definitions.ContainsKey(handler);

    /// <summary>PHP <c>epc_payment_handler_title</c>: catalogue title, else the humanised handler.</summary>
    public static string Title(string? handler)
    {
        if (string.IsNullOrEmpty(handler))
        {
            return string.Empty;
        }

        if (Definitions.TryGetValue(handler, out var def))
        {
            return def.Title;
        }

        var spaced = handler.Replace('_', ' ');
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    private static PhpPaymentParameter[] Base(string currency) =>
    [
        new("demo_mode", "checkbox", "Demo mode (simulated checkout — no live charge)"),
        new("currency", "text", "Currency (" + currency + ")")
    ];

    private static PhpPaymentParameter[] With(string currency, params PhpPaymentParameter[] extra)
        => [.. Base(currency), .. extra];

    private static Dictionary<string, string> Demo(string currency, params (string Key, string Value)[] pairs)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["demo_mode"] = "1",
            ["currency"] = currency
        };
        foreach (var (key, value) in pairs)
        {
            map[key] = value;
        }

        return map;
    }

    private static Dictionary<string, PhpPaymentGatewayDefinition> Build()
    {
        var defs = new List<PhpPaymentGatewayDefinition>
        {
            new(
                "stripe",
                "Stripe",
                "Cards, Apple Pay, Google Pay via Stripe — UAE & global.",
                RegionInternational,
                ["AE", "SA", "PK", "GLOBAL"],
                With(
                    "AED",
                    new("publishable_key", "text", "Publishable key"),
                    new("secret_key", "password", "Secret key"),
                    new("webhook_secret", "password", "Webhook signing secret")),
                Demo(
                    "AED",
                    ("publishable_key", "pk_test_epc_dummy_publishable_key"),
                    ("secret_key", "sk_test_epc_dummy_secret_key"),
                    ("webhook_secret", "whsec_epc_dummy_webhook"))),
            new(
                "telr",
                "Telr",
                "UAE aggregator — Visa, MC, Apple Pay, local methods.",
                RegionGcc,
                ["AE", "SA", "BH", "OM", "QA", "KW"],
                With(
                    "AED",
                    new("store_id", "text", "Store ID"),
                    new("auth_key", "password", "Auth key")),
                Demo("AED", ("store_id", "EPC-DEMO-TELR"), ("auth_key", "telr_dummy_auth_key"))),
            new(
                "paytabs",
                "PayTabs",
                "MENA cards, wallets, Apple Pay, invoicing.",
                RegionGcc,
                ["AE", "SA", "BH", "OM", "QA", "KW", "EG", "JO"],
                With(
                    "AED",
                    new("profile_id", "text", "Profile ID"),
                    new("server_key", "password", "Server key"),
                    new("client_key", "text", "Client key")),
                Demo(
                    "AED",
                    ("profile_id", "EPC-DEMO-PAYTABS"),
                    ("server_key", "paytabs_dummy_server"),
                    ("client_key", "paytabs_dummy_client"))),
            new(
                "twocheckout",
                "2Checkout (Verifone)",
                "Global checkout — 200+ countries.",
                RegionInternational,
                ["GLOBAL"],
                With(
                    "USD",
                    new("merchant_code", "text", "Merchant code"),
                    new("secret_key", "password", "Secret key")),
                Demo("USD", ("merchant_code", "EPC2CO"), ("secret_key", "2co_dummy_secret"))),
            new(
                "ccavenue",
                "CCAvenue",
                "Cards, net banking, bank transfer.",
                RegionInternational,
                ["AE", "IN", "GLOBAL"],
                With(
                    "AED",
                    new("merchant_id", "text", "Merchant ID"),
                    new("access_code", "password", "Access code"),
                    new("working_key", "password", "Working key")),
                Demo(
                    "AED",
                    ("merchant_id", "EPCCCA"),
                    ("access_code", "cca_dummy_access"),
                    ("working_key", "cca_dummy_working"))),
            new(
                "amazon_ps",
                "Amazon Payment Services",
                "Amazon Payment Services for UAE/KSA merchants.",
                RegionGcc,
                ["AE", "SA"],
                With(
                    "AED",
                    new("merchant_id", "text", "Merchant ID"),
                    new("access_code", "password", "Access code"),
                    new("sha_request_phrase", "password", "SHA request phrase")),
                Demo(
                    "AED",
                    ("merchant_id", "EPC-AMZPS"),
                    ("access_code", "amzps_dummy_access"),
                    ("sha_request_phrase", "amzps_dummy_sha"))),
            new(
                "cashu",
                "CashU",
                "Digital wallet — MENA region.",
                RegionGcc,
                ["AE", "SA", "EG"],
                With(
                    "AED",
                    new("merchant_id", "text", "Merchant ID"),
                    new("encryption_key", "password", "Encryption key")),
                Demo("AED", ("merchant_id", "EPC-CASHU"), ("encryption_key", "cashu_dummy_key"))),
            new(
                "cybersource",
                "CyberSource",
                "Visa infrastructure — enterprise fraud tools.",
                RegionInternational,
                ["AE", "GLOBAL"],
                With(
                    "AED",
                    new("merchant_id", "text", "Merchant ID"),
                    new("api_key", "password", "API key"),
                    new("shared_secret", "password", "Shared secret")),
                Demo(
                    "AED",
                    ("merchant_id", "epc_cybersource"),
                    ("api_key", "cybs_dummy_api"),
                    ("shared_secret", "cybs_dummy_secret"))),
            new(
                "razorpay",
                "Razorpay",
                "Cards, BNPL, net banking.",
                RegionInternational,
                ["AE", "IN"],
                With(
                    "AED",
                    new("key_id", "text", "Key ID"),
                    new("key_secret", "password", "Key secret")),
                Demo("AED", ("key_id", "rzp_test_epc_dummy"), ("key_secret", "razorpay_dummy_secret"))),
            new(
                "paypal",
                "PayPal",
                "PayPal checkout — export & guest buyers.",
                RegionInternational,
                ["GLOBAL"],
                With(
                    "USD",
                    new("client_id", "text", "Client ID"),
                    new("client_secret", "password", "Client secret"),
                    new("sandbox", "checkbox", "Sandbox mode")),
                Demo(
                    "USD",
                    ("client_id", "paypal_dummy_client_id"),
                    ("client_secret", "paypal_dummy_secret"),
                    ("sandbox", "1"))),
            new(
                "skrill",
                "Skrill",
                "Digital wallet — multi-currency.",
                RegionInternational,
                ["GLOBAL"],
                With(
                    "USD",
                    new("merchant_email", "text", "Merchant email"),
                    new("secret_word", "password", "Secret word")),
                Demo("USD", ("merchant_email", "payments@epartscart.demo"), ("secret_word", "skrill_dummy_secret"))),
            new(
                "payoneer",
                "Payoneer",
                "Cross-border collections.",
                RegionInternational,
                ["GLOBAL"],
                With(
                    "USD",
                    new("program_id", "text", "Program ID"),
                    new("api_token", "password", "API token")),
                Demo("USD", ("program_id", "EPC-PAYONEER"), ("api_token", "payoneer_dummy_token"))),
            new(
                "authorize_net",
                "Authorize.net",
                "US gateway — cards.",
                RegionInternational,
                ["US", "GLOBAL"],
                With(
                    "USD",
                    new("api_login_id", "text", "API login ID"),
                    new("transaction_key", "password", "Transaction key")),
                Demo("USD", ("api_login_id", "EPC-AUTHNET"), ("transaction_key", "authnet_dummy_key"))),
            new(
                "adyen",
                "Adyen",
                "Unified online payments at scale.",
                RegionInternational,
                ["AE", "GLOBAL"],
                With(
                    "AED",
                    new("merchant_account", "text", "Merchant account"),
                    new("api_key", "password", "API key"),
                    new("client_key", "text", "Client key")),
                Demo(
                    "AED",
                    ("merchant_account", "EpartscartECOM"),
                    ("api_key", "adyen_dummy_api"),
                    ("client_key", "adyen_dummy_client"))),
            new(
                "tabby",
                "Tabby",
                "Pay-in-4 BNPL — UAE, KSA, Kuwait, Bahrain.",
                RegionGcc,
                ["AE", "SA", "KW", "BH"],
                With(
                    "AED",
                    new("public_key", "text", "Public key"),
                    new("secret_key", "password", "Secret key"),
                    new("merchant_code", "text", "Merchant code")),
                Demo(
                    "AED",
                    ("public_key", "pk_test_tabby_epc"),
                    ("secret_key", "sk_test_tabby_epc"),
                    ("merchant_code", "EPC-TABBY"))),
            new(
                "tamara",
                "Tamara",
                "Sharia-compliant BNPL — UAE, KSA, Kuwait.",
                RegionGcc,
                ["AE", "SA", "KW"],
                With(
                    "AED",
                    new("api_token", "password", "API token"),
                    new("notification_token", "password", "Notification token"),
                    new("public_key", "text", "Public key")),
                Demo(
                    "AED",
                    ("api_token", "tamara_dummy_api"),
                    ("notification_token", "tamara_dummy_notify"),
                    ("public_key", "tamara_dummy_public"))),
            new(
                "myfatoorah",
                "MyFatoorah",
                "GCC multi-country gateway — KNET, MADA, cards, Apple Pay.",
                RegionGcc,
                ["KW", "SA", "BH", "AE", "OM", "QA", "EG"],
                With(
                    "AED",
                    new("api_key", "password", "API token / key"),
                    new("api_url", "text", "API base URL (sandbox or live)")),
                Demo("AED", ("api_key", "myfatoorah_dummy_token"), ("api_url", "https://apitest.myfatoorah.com"))),
            new(
                "tap",
                "Tap Payments",
                "GCC cards, Apple Pay, Google Pay, benefit.",
                RegionGcc,
                ["AE", "SA", "KW", "BH", "OM", "QA", "EG"],
                With(
                    "AED",
                    new("secret_key", "password", "Secret key"),
                    new("public_key", "text", "Public key")),
                Demo("AED", ("secret_key", "sk_test_tap_epc"), ("public_key", "pk_test_tap_epc"))),
            new(
                "hyperpay",
                "HyperPay",
                "OPPWA / HyperPay — strong in KSA (MADA) & UAE.",
                RegionGcc,
                ["SA", "AE", "EG"],
                With(
                    "SAR",
                    new("entity_id", "text", "Entity ID"),
                    new("access_token", "password", "Access token"),
                    new("api_url", "text", "OPPWA API URL")),
                Demo(
                    "SAR",
                    ("entity_id", "8ac7a4c7epc_entity"),
                    ("access_token", "hyperpay_dummy_token"),
                    ("api_url", "https://eu-test.oppwa.com"))),
            new(
                "checkout_com",
                "Checkout.com",
                "Enterprise cards & wallets — UAE DIFC ready.",
                RegionGcc,
                ["AE", "SA", "GLOBAL"],
                With(
                    "AED",
                    new("public_key", "text", "Public key"),
                    new("secret_key", "password", "Secret key"),
                    new("processing_channel", "text", "Processing channel ID")),
                Demo(
                    "AED",
                    ("public_key", "pk_sbox_checkout_epc"),
                    ("secret_key", "sk_sbox_checkout_epc"),
                    ("processing_channel", "pc_epc_demo"))),
            new(
                "network_intl",
                "Network International (N-Genius)",
                "N-Genius Online — major UAE acquirer.",
                RegionGcc,
                ["AE", "SA", "EG"],
                With(
                    "AED",
                    new("outlet_ref", "text", "Outlet reference"),
                    new("api_key", "password", "API key"),
                    new("api_url", "text", "N-Genius API base URL")),
                Demo(
                    "AED",
                    ("outlet_ref", "epc-outlet-demo"),
                    ("api_key", "network_intl_dummy_key"),
                    ("api_url", "https://api-gateway.sandbox.ngenius-payments.com"))),
            new(
                "jazzcash",
                "JazzCash",
                "Pakistan mobile wallet & card checkout (PKR).",
                RegionPakistan,
                ["PK"],
                With(
                    "PKR",
                    new("merchant_id", "text", "Merchant ID"),
                    new("password", "password", "Password"),
                    new("integrity_salt", "password", "Integrity salt"),
                    new("return_url", "text", "Return URL (optional override)")),
                Demo(
                    "PKR",
                    ("merchant_id", "MC12345"),
                    ("password", "jazzcash_dummy_pass"),
                    ("integrity_salt", "jazzcash_dummy_salt"),
                    ("return_url", ""))),
            new(
                "easypaisa",
                "Easypaisa",
                "Pakistan Easypaisa wallet & OTC (PKR).",
                RegionPakistan,
                ["PK"],
                With(
                    "PKR",
                    new("store_id", "text", "Store ID"),
                    new("account_num", "text", "Account number"),
                    new("hash_key", "password", "Hash key")),
                Demo(
                    "PKR",
                    ("store_id", "EPC-EP-STORE"),
                    ("account_num", "03001234567"),
                    ("hash_key", "easypaisa_dummy_hash"))),
            new(
                "nowpayments",
                "Crypto (NOWPayments)",
                "Pay with USDT, BTC, ETH and 100+ coins via NOWPayments.",
                RegionCrypto,
                ["GLOBAL", "AE", "SA", "PK"],
                With(
                    "USD",
                    new("api_key", "password", "NOWPayments API key"),
                    new("ipn_secret", "password", "IPN secret"),
                    new("allowed_coins", "text", "Allowed coins (comma: usdttrc20,btc,eth,usdtbsc)"),
                    new("sandbox", "checkbox", "Sandbox / test API host")),
                Demo(
                    "USD",
                    ("api_key", "NOWPAYMENTS_DUMMY_API_KEY"),
                    ("ipn_secret", "nowpayments_dummy_ipn"),
                    ("allowed_coins", "usdttrc20,btc,eth,ltc"),
                    ("sandbox", "1")))
        };

        var map = new Dictionary<string, PhpPaymentGatewayDefinition>(StringComparer.Ordinal);
        foreach (var def in defs)
        {
            map[def.Handler] = def;
        }

        return map;
    }
}
