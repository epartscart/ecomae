using System.Text.RegularExpressions;

namespace EcomAE.Platform.Storefront;

/// <summary>The address rules of PHP <c>epc_countries_address_meta()</c>.</summary>
public sealed record EpcCountryAddressMeta(string StateLabel, string PostalLabel, bool PostalRequired, bool UseEmirateSelect, IReadOnlyList<string> Emirates);

/// <summary>PHP <c>content/users/epc_countries.php</c>: the ISO 3166-1 country names, dial codes and address rules of the storefront forms.</summary>
public static class EpcCountries
{
    private static readonly (string Code, string Name)[] Alpha2 =
    [
        ("AF", "Afghanistan"),
        ("AL", "Albania"),
        ("DZ", "Algeria"),
        ("AS", "American Samoa"),
        ("AD", "Andorra"),
        ("AO", "Angola"),
        ("AI", "Anguilla"),
        ("AQ", "Antarctica"),
        ("AG", "Antigua and Barbuda"),
        ("AN", "Netherlands Antilles"),
        ("AR", "Argentina"),
        ("AM", "Armenia"),
        ("AW", "Aruba"),
        ("AU", "Australia"),
        ("AT", "Austria"),
        ("AZ", "Azerbaijan"),
        ("BS", "Bahamas"),
        ("BH", "Bahrain"),
        ("BD", "Bangladesh"),
        ("BB", "Barbados"),
        ("BY", "Belarus"),
        ("BE", "Belgium"),
        ("BZ", "Belize"),
        ("BJ", "Benin"),
        ("BM", "Bermuda"),
        ("BT", "Bhutan"),
        ("BO", "Bolivia"),
        ("BA", "Bosnia and Herzegovina"),
        ("BW", "Botswana"),
        ("BR", "Brazil"),
        ("IO", "British Indian Ocean Territory"),
        ("VG", "British Virgin Islands"),
        ("BN", "Brunei"),
        ("BG", "Bulgaria"),
        ("BF", "Burkina Faso"),
        ("BI", "Burundi"),
        ("KH", "Cambodia"),
        ("CM", "Cameroon"),
        ("CA", "Canada"),
        ("CV", "Cape Verde"),
        ("KY", "Cayman Islands"),
        ("CF", "Central African Republic (CAR/RCA)"),
        ("TD", "Chad"),
        ("CL", "Chile"),
        ("CN", "China"),
        ("CX", "Christmas Island"),
        ("CC", "Cocos Islands (Keeling)"),
        ("CO", "Colombia"),
        ("KM", "Comoros"),
        ("CG", "Congo"),
        ("CK", "Cook Islands‎‎"),
        ("CR", "Costa Rica"),
        ("HR", "Croatia"),
        ("CU", "Cuba"),
        ("CW", "Curaçao"),
        ("CY", "Cyprus"),
        ("CZ", "Czech Republic"),
        ("CI", "Côte d'Ivoire"),
        ("DK", "Denmark"),
        ("DJ", "Djibouti"),
        ("DM", "Dominica"),
        ("DO", "Dominican Republic"),
        ("CD", "DR Congo (RDC)"),
        ("EG", "Egypt"),
        ("SV", "El Salvador"),
        ("GQ", "Equatorial Guinea"),
        ("ER", "Eritrea"),
        ("EE", "Estonia"),
        ("ET", "Ethiopia"),
        ("FK", "Falkland Islands"),
        ("FO", "Faroe Islands"),
        ("FJ", "Fidji Islands"),
        ("FI", "Finland"),
        ("FR", "France"),
        ("GF", "French Guiana"),
        ("PF", "French Polynesia"),
        ("TF", "French Southern and Antarctic Lands"),
        ("GA", "Gabon"),
        ("GM", "Gambia"),
        ("GE", "Georgia"),
        ("DE", "Germany"),
        ("GH", "Ghana"),
        ("GI", "Gibraltar"),
        ("GR", "Greece"),
        ("GL", "Greenland"),
        ("GD", "Grenade"),
        ("GU", "Guam"),
        ("GT", "Guatemala"),
        ("GG", "Guernsey"),
        ("GN", "Guinea"),
        ("GW", "Guinea-Bissao"),
        ("HT", "Haiti"),
        ("HM", "Heard Island and McDonald Islands"),
        ("HN", "Honduras"),
        ("HK", "Hong Kong"),
        ("HU", "Hungary"),
        ("IS", "Iceland"),
        ("IN", "India"),
        ("ID", "Indonesia"),
        ("IR", "Iran"),
        ("IQ", "Iraq"),
        ("IE", "Ireland"),
        ("IM", "Isle of Man"),
        ("IL", "Israel"),
        ("IT", "Italy"),
        ("JM", "Jamaica"),
        ("JP", "Japan"),
        ("JE", "Jersey"),
        ("JO", "Jordan"),
        ("KZ", "Kazakhstan"),
        ("KE", "Kenya"),
        ("KG", "Kirghizistan"),
        ("KI", "Kiribati"),
        ("XK", "Kosovo"),
        ("KW", "Koweït"),
        ("LA", "Laos"),
        ("LV", "Latvia"),
        ("LB", "Lebanon"),
        ("LS", "Lesotho"),
        ("LR", "Liberia"),
        ("LY", "Libya"),
        ("LI", "Liechtenstein"),
        ("LT", "Lithuania"),
        ("LU", "Luxembourg"),
        ("MO", "Macao"),
        ("MG", "Madagascar"),
        ("MW", "Malawi"),
        ("MY", "Malaysia"),
        ("MV", "Maldives"),
        ("ML", "Mali"),
        ("MT", "Malta"),
        ("MH", "Marshall Islands"),
        ("MR", "Mauritania"),
        ("MU", "Mauritius"),
        ("YT", "Mayotte"),
        ("MX", "Mexico"),
        ("FM", "Micronesia"),
        ("MD", "Moldova"),
        ("MC", "Monaco"),
        ("MN", "Mongolia"),
        ("ME", "Montenegro"),
        ("MS", "Montserrat"),
        ("MA", "Morocco"),
        ("MZ", "Mozambique"),
        ("MM", "Myanmar"),
        ("NA", "Namibia"),
        ("NR", "Nauru"),
        ("NL", "Netherlands"),
        ("NC", "New Caledonia"),
        ("NZ", "New Zealand"),
        ("NI", "Nicaragua"),
        ("NE", "Niger"),
        ("NG", "Nigeria"),
        ("NU", "Niue"),
        ("NF", "Norfolk Island"),
        ("KP", "North Corea"),
        ("MK", "North Macedonia"),
        ("MP", "Northern Mariana Islands"),
        ("NO", "Norway"),
        ("NP", "Nepal"),
        ("OM", "Oman"),
        ("PK", "Pakistan"),
        ("PW", "Palau"),
        ("PS", "Palestine"),
        ("PA", "Panama"),
        ("PG", "Papua New Guinea"),
        ("PY", "Paraguay"),
        ("PE", "Peru"),
        ("PH", "Philippines"),
        ("PN", "Pitcairn Islands"),
        ("PL", "Poland"),
        ("PT", "Portugal"),
        ("PR", "Puerto Rico"),
        ("QA", "Qatar"),
        ("EC", "Republic of Ecuador"),
        ("RO", "Romania"),
        ("RU", "Russia"),
        ("RW", "Rwanda"),
        ("SH", "Saint Helena"),
        ("KN", "Saint Kitts and Nevis"),
        ("LC", "Saint Lucia"),
        ("PM", "Saint Pierre and Miquelon"),
        ("ST", "Saint Thomas and Prince"),
        ("VC", "Saint Vincent and the Grenadines"),
        ("BL", "Saint Barthélemy"),
        ("MF", "Saint-Martin"),
        ("WS", "Samoa"),
        ("SM", "San Marino "),
        ("SA", "Saudi Arabia"),
        ("SN", "Senegal"),
        ("RS", "Serbia"),
        ("SC", "Seychelles"),
        ("SL", "Sierra Leone"),
        ("SG", "Singapore"),
        ("SX", "Sint Maarten"),
        ("SK", "Slovakia"),
        ("SI", "Slovenia"),
        ("SB", "Solomon Islands"),
        ("SO", "Somalia"),
        ("ZA", "South Africa"),
        ("KR", "South Corea"),
        ("GS", "South Georgia and the South Sandwich Islands "),
        ("ES", "Spain"),
        ("LK", "Sri Lanka"),
        ("SD", "Sudan"),
        ("SR", "Suriname"),
        ("SJ", "Svalbard and Jan Mayen"),
        ("SZ", "Swaziland / Eswatini"),
        ("SE", "Sweden"),
        ("CH", "Switzerland"),
        ("SY", "Syria"),
        ("TW", "Taiwan"),
        ("TJ", "Tajikistan"),
        ("TZ", "Tanzania"),
        ("TH", "Thailand"),
        ("TL", "Timor-Leste"),
        ("TG", "Togo"),
        ("TK", "Tokelau"),
        ("TO", "Tonga"),
        ("TT", "Trinidad and Tobago"),
        ("TN", "Tunisia"),
        ("TR", "Turkey"),
        ("TM", "Turkmenistan"),
        ("TC", "Turks and Caicos Islands"),
        ("TV", "Tuvalu"),
        ("UG", "Uganda"),
        ("UA", "Ukraine"),
        ("AE", "United Arab Emirates"),
        ("GB", "United Kingdom"),
        ("US", "United States"),
        ("UM", "United States Minor Outlying Islands"),
        ("UY", "Uruguay"),
        ("UZ", "Uzbekistan"),
        ("VU", "Vanuatu"),
        ("VA", "Vatican City"),
        ("VE", "Venezuela"),
        ("VN", "Vietnam"),
        ("VI", "Virgin Islands of the United States"),
        ("WF", "Wallis and Futuna"),
        ("EH", "Western Sahara"),
        ("YE", "Yemen"),
        ("ZM", "Zambia"),
        ("ZW", "Zimbabwe"),
        ("AX", "Åland Islands"),
    ];

    private static readonly string[] Gulf = ["AE", "SA", "QA", "KW", "BH", "OM"];

    /// <summary>PHP <c>epc_countries_iso3166_alpha2()</c>, in its order.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Iso3166Alpha2 { get; } = Alpha2.Select(c => KeyValuePair.Create(c.Code, c.Name)).ToArray();

    /// <summary>PHP <c>epc_countries_registration_options()</c>: the Gulf states first, then the rest in list order.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> RegistrationOptions { get; } =
        Gulf.Select(code => Alpha2.First(c => c.Code == code))
            .Concat(Alpha2.Where(c => !Gulf.Contains(c.Code)))
            .Select(c => KeyValuePair.Create(c.Code, c.Name))
            .ToArray();

    /// <summary>PHP <c>epc_countries_dial_codes()</c>, without the plus sign.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> DialCodes { get; } =
    [
        new("AE", "971"), new("SA", "966"), new("QA", "974"), new("KW", "965"), new("BH", "973"), new("OM", "968"),
        new("PK", "92"), new("IN", "91"), new("GB", "44"), new("US", "1"), new("EG", "20"), new("JO", "962"),
        new("LB", "961"), new("TR", "90"), new("DE", "49"), new("FR", "33"), new("CN", "86"),
    ];

    /// <summary>PHP <c>epc_countries_uae_emirates()</c>.</summary>
    public static IReadOnlyList<string> UaeEmirates { get; } = ["Dubai", "Abu Dhabi", "Sharjah", "Ajman", "Umm Al Quwain", "Ras Al Khaimah", "Fujairah"];

    private static string Code2(string countryCode)
    {
        var letters = Regex.Replace(countryCode, "[^A-Za-z]", string.Empty);
        return (letters.Length > 2 ? letters[..2] : letters).ToUpperInvariant();
    }

    /// <summary>PHP <c>epc_countries_dial_prefix()</c>: "+971" for AE, empty when unknown.</summary>
    public static string DialPrefix(string countryCode)
    {
        var code = Code2(countryCode);
        var hit = DialCodes.FirstOrDefault(d => d.Key == code);
        return hit.Key is null ? string.Empty : "+" + hit.Value;
    }

    /// <summary>PHP <c>epc_countries_address_meta()</c>.</summary>
    public static EpcCountryAddressMeta AddressMeta(string countryCode) => Code2(countryCode) switch
    {
        "AE" => new("Emirate", "Postal / ZIP code (optional in UAE)", false, true, UaeEmirates),
        "US" => new("State", "ZIP code", true, false, []),
        "GB" => new("County", "Postcode", true, false, []),
        _ => new("State / Province / Region", "Postal / ZIP code", false, false, []),
    };

    /// <summary>PHP <c>epc_countries_normalize_code()</c>: a two-letter code upper-cased, else the code of a name matched like <c>strcasecmp()</c>, else empty.</summary>
    public static string NormalizeCode(string value)
    {
        value = value.Trim(' ', '\t', '\n', '\r', '\0', '\x0B');
        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (Regex.IsMatch(value, "^[A-Za-z]{2}$"))
        {
            return value.ToUpperInvariant();
        }

        foreach (var (code, name) in Alpha2)
        {
            if (AsciiCaseEquals(name, value))
            {
                return code;
            }
        }

        return string.Empty;
    }

    private static bool AsciiCaseEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            var x = a[i] is >= 'A' and <= 'Z' ? (char)(a[i] + 32) : a[i];
            var y = b[i] is >= 'A' and <= 'Z' ? (char)(b[i] + 32) : b[i];
            if (x != y)
            {
                return false;
            }
        }

        return true;
    }
}
