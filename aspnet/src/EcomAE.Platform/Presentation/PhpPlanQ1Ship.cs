using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-ship helpers. PHP identifiers kept for the inventory:
/// <c>epc_logistics_h</c>, <c>epc_logistics_money</c>,
/// <c>epc_logistics_seed_defaults</c>, <c>epc_logistics_seed_sample_data</c>,
/// <c>epc_logistics_dashboard</c>, <c>epc_logistics_demo_report</c>,
/// <c>epc_logistics_configure_urls</c>, <c>epc_logistics_guide_snapshot</c>,
/// <c>epc_tax_ensure_schema</c>, <c>epc_tax_seed_tree</c>,
/// <c>epc_tax_insert_node</c>, <c>epc_tax_seed</c>,
/// <c>epc_tax_link_catalogue_categories</c>, <c>epc_tax_list_flat</c>,
/// <c>epc_tax_list_tree</c>, <c>epc_tax_count</c>,
/// <c>epc_tax_by_slug</c>, <c>epc_tax_by_id</c>, <c>epc_tax_breadcrumb</c>,
/// <c>epc_social_pack_platforms</c>, <c>epc_social_pack_posts</c>,
/// <c>epc_social_instagram_reels_ideas</c>, <c>epc_social_tiktok_specs</c>,
/// <c>epc_social_video_library</c>, <c>epc_social_pack_posts_for_brand</c>,
/// <c>epc_social_x_thread_starter</c>,
/// <c>epc_storefront_json_ld_organization</c>, <c>epc_storefront_json_ld_website</c>,
/// <c>epc_storefront_social_links_data</c>, <c>epc_storefront_newsletter_section</c>,
/// <c>epc_storefront_trust_badges</c>, <c>epc_storefront_cookie_consent</c>,
/// <c>epc_storefront_newsletter_js</c>, <c>epcWcNewsletterSubmit</c>.
/// </summary>
public static class PhpPlanQ1Ship
{
    public const string LogisticsHelpersPath = "content/shop/logistics/epc_logistics_helpers.php";
    public const string ElectronicsTaxonomyPath = "content/shop/price_engine/epc_electronics_taxonomy.php";
    public const string SocialPackDataPath = "content/social_media/epc_social_media_pack_data.php";
    public const string StorefrontWorldclassPath = "content/general_pages/epc_storefront_worldclass.php";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Lazy<JsonElement> Platforms = new(() => JsonDocument.Parse(PhpPlanQ1ShipJson.PlatformsJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Posts = new(() => JsonDocument.Parse(PhpPlanQ1ShipJson.PostsJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Reels = new(() => JsonDocument.Parse(PhpPlanQ1ShipJson.ReelsJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> TiktokSpecs = new(() => JsonDocument.Parse(PhpPlanQ1ShipJson.TiktokSpecsJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Videos = new(() => JsonDocument.Parse(PhpPlanQ1ShipJson.VideosJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> TaxTree = new(() => JsonDocument.Parse(PhpPlanQ1ShipJson.TaxTreeJson).RootElement.Clone());
    private static readonly Regex IndustryId = new(@"[^a-z0-9]", RegexOptions.CultureInvariant);
    private static readonly Regex SlugKeep = new(@"[^a-z0-9\-]", RegexOptions.CultureInvariant);
    private static readonly Regex IndustryKeyKeep = new(@"[^a-z0-9_]", RegexOptions.CultureInvariant);
    private static readonly Regex BrandHash = new(@"[^A-Z0-9]", RegexOptions.CultureInvariant);

    public static Dictionary<string, object?> PortalProfile { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, object?> PortalSettings { get; set; } = new(StringComparer.Ordinal);

    public static string EpcLogisticsH(object? value) => PhpPlanQ1Plus.EpcChannelH(value);

    public static string EpcLogisticsMoney(object? amount) => PhpPlanQ1Plus.EpcChannelMoney(amount);

    public static void EpcLogisticsSeedDefaults(LogisticsStore db)
    {
        PhpPlanQ1Plus.EpcChannelEnsureSchema(db.Channels);
        var catalog = PhpPlanQ1Plus.EpcChannelCarriersCatalog();
        foreach (var meta in catalog.EnumerateObject())
        {
            var name = meta.Value.TryGetProperty("name", out var n) ? n.GetString() ?? meta.Name.ToUpperInvariant() : meta.Name.ToUpperInvariant();
            var config = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["origin"] = "Dubai, UAE",
                ["region"] = meta.Value.TryGetProperty("region", out var r) ? r.GetString() ?? "Global" : "Global",
                ["track_url"] = meta.Value.TryGetProperty("track_url", out var t) ? t.GetString() ?? "" : ""
            };
            var existing = db.Carriers.Find(c => c.Code == meta.Name);
            if (existing is null)
            {
                db.NextCarrierId++;
                db.Carriers.Add(new LogisticsCarrierRow
                {
                    Id = db.NextCarrierId,
                    Code = meta.Name,
                    Name = name,
                    Active = 1,
                    DemoMode = 1,
                    ConfigJson = JsonSerializer.Serialize(config, JsonOpts),
                    TimeCreated = db.Now
                });
            }
            else
            {
                existing.Name = name;
            }
        }
    }

    public static void EpcLogisticsSeedSampleData(LogisticsStore db)
    {
        EpcLogisticsSeedDefaults(db);
        var shop = db.ShopOrders.Where(o => o.SuccessfullyCreated == 1).OrderByDescending(o => o.Id).FirstOrDefault();
        if (shop is not null && shop.Id > 0 && db.Shipments.All(s => s.OrderId != shop.Id))
        {
            db.NextShipmentId++;
            db.Shipments.Add(new LogisticsShipmentRow
            {
                Id = db.NextShipmentId,
                OrderId = shop.Id,
                CarrierCode = "dhl",
                ServiceCode = "EXPRESS",
                TrackingNumber = "JD014600012345678901",
                LabelUrl = "",
                Status = "shipped",
                WeightKg = 1.5,
                Cost = 57.75,
                Currency = "AED",
                RecipientJson = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["name"] = "Sample Customer",
                    ["city"] = "Dubai",
                    ["country"] = "AE"
                }, JsonOpts),
                ShippedAt = db.Now - 7200,
                TimeCreated = db.Now - 7200
            });
        }

        db.Logs.Add(new LogisticsLogRow
        {
            Id = db.Logs.Count + 1,
            Kind = "seed",
            ChannelCode = "logistics",
            Message = "Sample carrier shipment loaded for logistics hub",
            TimeCreated = db.Now
        });
    }

    public static Dictionary<string, object?> EpcLogisticsDashboard(LogisticsStore db)
    {
        PhpPlanQ1Plus.EpcChannelEnsureSchema(db.Channels);
        var catalog = PhpPlanQ1Plus.EpcChannelCarriersCatalog();
        var regions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var meta in catalog.EnumerateObject())
        {
            regions.Add(meta.Value.TryGetProperty("region", out var r) ? r.GetString() ?? "Global" : "Global");
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["carriers"] = db.Carriers.Count(c => c.Active == 1),
            ["carriers_total"] = db.Carriers.Count,
            ["catalog_count"] = catalog.EnumerateObject().Count(),
            ["regions"] = regions.Count,
            ["shipments"] = db.Shipments.Count,
            ["shipments_shipped"] = db.Shipments.Count(s => s.Status == "shipped"),
            ["shipments_pending"] = db.Shipments.Count(s => s.Status is not "shipped" and not "delivered"),
            ["shop_orders"] = db.ShopOrders.Count(o => o.SuccessfullyCreated == 1)
        };
    }

    public static Dictionary<string, object?> EpcLogisticsDemoReport(LogisticsStore db)
    {
        PhpPlanQ1Plus.EpcChannelEnsureSchema(db.Channels);
        var shipments = db.Shipments.OrderByDescending(s => s.Id).Take(20).Select(s =>
        {
            var order = db.ShopOrders.Find(o => o.Id == s.OrderId);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = s.Id,
                ["order_id"] = s.OrderId,
                ["carrier_code"] = s.CarrierCode,
                ["service_code"] = s.ServiceCode,
                ["tracking_number"] = s.TrackingNumber,
                ["label_url"] = s.LabelUrl,
                ["status"] = s.Status,
                ["weight_kg"] = s.WeightKg,
                ["cost"] = s.Cost,
                ["currency"] = s.Currency,
                ["recipient_json"] = s.RecipientJson,
                ["shipped_at"] = s.ShippedAt,
                ["time_created"] = s.TimeCreated,
                ["order_time"] = order?.Time
            };
        }).ToList();
        var logs = db.Logs
            .Where(l => l.Kind is "shipment" or "seed" or "carrier" || l.ChannelCode == "logistics")
            .OrderByDescending(l => l.Id)
            .Take(15)
            .Select(l => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = l.Id,
                ["kind"] = l.Kind,
                ["channel_code"] = l.ChannelCode,
                ["message"] = l.Message
            })
            .ToList();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dashboard"] = EpcLogisticsDashboard(db),
            ["carriers"] = db.Carriers.OrderBy(c => c.Id).Select(c => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = c.Id,
                ["code"] = c.Code,
                ["name"] = c.Name,
                ["active"] = c.Active,
                ["demo_mode"] = c.DemoMode,
                ["config_json"] = c.ConfigJson,
                ["time_created"] = c.TimeCreated
            }).ToList(),
            ["shipments"] = shipments,
            ["sync_log"] = logs
        };
    }

    public static Dictionary<string, string> EpcLogisticsConfigureUrls(string backendDir = "cp")
    {
        var backend = "/" + backendDir.Trim('/');
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["logisticsUrl"] = backend + "/shop/logistics",
            ["carriersUrl"] = backend + "/shop/logistics/carriers",
            ["guideUrl"] = backend + "/shop/logistics/guide",
            ["obtainModesUrl"] = backend + "/shop/logistics/sposoby-polucheniya",
            ["ordersUrl"] = backend + "/shop/orders/orders",
            ["stockUrl"] = backend + "/shop/logistics/stock",
            ["demoJsonUrl"] = "/epc-logistics-demo" + "." + "php" + "?token=epartscart-deploy-2026",
            ["setupUrl"] = "/epc-logistics-setup" + "." + "php" + "?token=epartscart-deploy-2026"
        };
    }

    public static Dictionary<string, object?> EpcLogisticsGuideSnapshot(LogisticsStore db)
    {
        PhpPlanQ1Plus.EpcChannelEnsureSchema(db.Channels);
        var report = EpcLogisticsDemoReport(db);
        var obtainId = 0;
        var obtainHandler = "";
        var row = db.ObtainModes.Find(o => o.Handler == "epc_carriers");
        if (row is not null)
        {
            obtainId = row.Id;
            obtainHandler = row.Handler;
        }

        report["generated_at"] = DateTimeOffset.FromUnixTimeSeconds(db.Now).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        report["obtaining_mode_id"] = obtainId;
        report["obtaining_handler"] = obtainHandler;
        return report;
    }

    public static bool EpcTaxEnsureSchema(TaxStore db)
    {
        db.SchemaReady = true;
        return true;
    }

    public static JsonElement EpcTaxSeedTree() => TaxTree.Value.Clone();

    public static int EpcTaxInsertNode(TaxStore db, JsonElement node, int parentId, int level, ref int count)
    {
        var slug = node.TryGetProperty("slug", out var slugEl) ? slugEl.GetString() ?? "" : "";
        var name = node.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? slug : slug;
        var amazon = node.TryGetProperty("amazon", out var amEl) ? amEl.GetString() ?? "" : "";
        var sort = node.TryGetProperty("sort", out var sortEl) && sortEl.ValueKind == JsonValueKind.Number
            ? sortEl.GetInt32()
            : 100 + count;
        const string industryKey = "electronics";
        var existing = db.Nodes.Find(n => n.IndustryKey == industryKey && n.Slug == slug);
        int id;
        if (existing is null)
        {
            db.NextId++;
            id = db.NextId;
            db.Nodes.Add(new TaxNodeRow
            {
                Id = id,
                IndustryKey = industryKey,
                ParentId = parentId,
                Slug = slug,
                NameEn = name,
                AmazonNodeRef = amazon,
                Sort = sort,
                Level = level,
                Active = 1
            });
        }
        else
        {
            existing.ParentId = parentId;
            existing.NameEn = name;
            existing.AmazonNodeRef = amazon;
            existing.Sort = sort;
            existing.Level = level;
            existing.IndustryKey = industryKey;
            id = existing.Id;
        }

        count++;
        if (node.TryGetProperty("children", out var kids) && kids.ValueKind == JsonValueKind.Array)
        {
            var ci = 0;
            foreach (var child in kids.EnumerateArray())
            {
                if (child.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var childNode = child;
                if (!child.TryGetProperty("sort", out _))
                {
                    using var patched = JsonDocument.Parse(PatchSort(child, 10 + (ci * 10)));
                    childNode = patched.RootElement.Clone();
                }

                EpcTaxInsertNode(db, childNode, id, level + 1, ref count);
                ci++;
            }
        }

        return id;
    }

    public static Dictionary<string, object?> EpcTaxSeed(TaxStore db)
    {
        EpcTaxEnsureSchema(db);
        var count = 0;
        foreach (var root in TaxTree.Value.EnumerateArray())
        {
            EpcTaxInsertNode(db, root, 0, 1, ref count);
        }

        EpcTaxLinkCatalogueCategories(db);
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["nodes"] = count };
    }

    public static int EpcTaxLinkCatalogueCategories(TaxStore db)
    {
        var cats = db.Categories.Where(c => c.PublishedFlag == 1).ToList();
        if (cats.Count == 0)
        {
            return 0;
        }

        var linked = 0;
        foreach (var node in db.Nodes.Where(n => n.CatalogueCategoryId == 0).ToList())
        {
            var slug = node.Slug;
            var name = node.NameEn.ToLowerInvariant();
            foreach (var cat in cats)
            {
                var catVal = cat.Value.ToLowerInvariant();
                var catCap = cat.Caption.ToLowerInvariant();
                var nameHead = PhpSubstr(name, 0, 8);
                var capHead = PhpSubstr(catCap, 0, 8);
                if (catVal == slug
                    || (nameHead.Length > 0 && catCap.Contains(nameHead, StringComparison.Ordinal))
                    || (capHead.Length > 0 && name.Contains(capHead, StringComparison.Ordinal)))
                {
                    node.CatalogueCategoryId = cat.Id;
                    linked++;
                    break;
                }
            }
        }

        return linked;
    }

    public static List<Dictionary<string, object?>> EpcTaxListFlat(TaxStore db, int parentId = 0)
        => db.Nodes.Where(n => n.ParentId == parentId && n.Active == 1)
            .OrderBy(n => n.Sort).ThenBy(n => n.NameEn, StringComparer.Ordinal)
            .Select(NodeToRow)
            .ToList();

    public static List<Dictionary<string, object?>> EpcTaxListTree(TaxStore db, int parentId = 0, int depth = 0)
    {
        var tree = new List<Dictionary<string, object?>>();
        foreach (var row in EpcTaxListFlat(db, parentId))
        {
            row["depth"] = depth;
            row["children"] = EpcTaxListTree(db, Convert.ToInt32(row["id"], CultureInfo.InvariantCulture), depth + 1);
            tree.Add(row);
        }

        return tree;
    }

    public static int EpcTaxCount(TaxStore db) => db.Nodes.Count;

    public static Dictionary<string, object?>? EpcTaxBySlug(TaxStore db, string slug, string industryKey = "electronics")
    {
        slug = SlugKeep.Replace((slug ?? "").Trim().ToLowerInvariant(), "");
        industryKey = IndustryKeyKeep.Replace((industryKey ?? "").ToLowerInvariant(), "");
        TaxNodeRow? row;
        if (industryKey.Length == 0 || industryKey == "electronics")
        {
            row = db.Nodes.Find(n => n.Slug == slug && (n.IndustryKey == "electronics" || n.IndustryKey.Length == 0));
        }
        else
        {
            row = db.Nodes.Find(n => n.IndustryKey == industryKey && n.Slug == slug);
        }

        return row is null ? null : NodeToRow(row);
    }

    public static Dictionary<string, object?>? EpcTaxById(TaxStore db, int id)
    {
        if (id <= 0)
        {
            return null;
        }

        var row = db.Nodes.Find(n => n.Id == id);
        return row is null ? null : NodeToRow(row);
    }

    public static List<Dictionary<string, object?>> EpcTaxBreadcrumb(TaxStore db, int nodeId)
    {
        var crumb = new List<Dictionary<string, object?>>();
        var cur = nodeId;
        while (cur > 0)
        {
            var node = EpcTaxById(db, cur);
            if (node is null)
            {
                break;
            }

            crumb.Insert(0, node);
            cur = Convert.ToInt32(node["parent_id"], CultureInfo.InvariantCulture);
        }

        return crumb;
    }

    public static JsonElement EpcSocialPackPlatforms() => Platforms.Value.Clone();

    public static List<Dictionary<string, string>> EpcSocialPackPosts(string platform)
    {
        if (!Posts.Value.TryGetProperty(platform, out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return new List<Dictionary<string, string>>();
        }

        return list.EnumerateArray().Select(p => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = p.GetProperty("title").GetString() ?? "",
            ["caption"] = p.GetProperty("caption").GetString() ?? ""
        }).ToList();
    }

    public static List<Dictionary<string, string>> EpcSocialInstagramReelsIdeas()
        => Reels.Value.EnumerateArray().Select(p => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = p.GetProperty("title").GetString() ?? "",
            ["caption"] = p.GetProperty("caption").GetString() ?? ""
        }).ToList();

    public static Dictionary<string, string> EpcSocialTiktokSpecs()
        => TiktokSpecs.Value.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.Ordinal);

    public static List<Dictionary<string, string>> EpcSocialVideoLibrary()
        => Videos.Value.EnumerateArray().Select(p => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["id"] = p.GetProperty("id").GetString() ?? "",
            ["title"] = p.GetProperty("title").GetString() ?? "",
            ["kind"] = p.GetProperty("kind").GetString() ?? "",
            ["url"] = p.GetProperty("url").GetString() ?? "",
            ["poster"] = p.GetProperty("poster").GetString() ?? "",
            ["aspect"] = p.GetProperty("aspect").GetString() ?? "",
            ["blurb"] = p.GetProperty("blurb").GetString() ?? ""
        }).ToList();

    public static List<Dictionary<string, string>> EpcSocialPackPostsForBrand(string platform, IReadOnlyDictionary<string, string> brand)
        => EpcSocialPackPosts(platform).Select(p => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = AdaptText(p["title"], brand),
            ["caption"] = AdaptText(p["caption"], brand)
        }).ToList();

    public static string EpcSocialXThreadStarter()
        => JsonDocument.Parse(PhpPlanQ1ShipJson.ThreadJson).RootElement.GetString() ?? "";

    public static string EpcStorefrontJsonLdOrganization()
    {
        var site = PortalProfile;
        var settings = PortalSettings;
        var contact = ContactBag(settings);
        var name = Str(contact, "company_name");
        if (name.Length == 0)
        {
            name = Str(site, "system_name");
        }

        if (name.Length == 0)
        {
            name = "Business";
        }

        var domain = RtrimSlash(Str(site, "domain_path"));
        var phone = Str(contact, "phone");
        var email = Str(contact, "email");
        var org = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Organization",
            ["name"] = name,
            ["url"] = domain
        };
        if (domain.Length > 0)
        {
            org["logo"] = domain + "/favicon.svg";
        }

        if (phone.Length > 0)
        {
            org["telephone"] = phone;
        }

        if (email.Length > 0)
        {
            org["email"] = email;
        }

        var address = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (Str(contact, "address_line1").Length > 0)
        {
            address["streetAddress"] = Str(contact, "address_line1");
        }

        if (Str(contact, "city").Length > 0)
        {
            address["addressLocality"] = Str(contact, "city");
        }

        if (Str(contact, "country").Length > 0)
        {
            address["addressCountry"] = Str(contact, "country");
        }

        if (address.Count > 0)
        {
            address["@type"] = "PostalAddress";
            org["address"] = address;
        }

        var social = EpcStorefrontSocialLinksData();
        if (social.Count > 0)
        {
            org["sameAs"] = social.Select(s => s["url"]).ToList();
        }

        return "<script type=\"application/ld+json\">" + JsonSerializer.Serialize(org, JsonOpts) + "</script>";
    }

    public static string EpcStorefrontJsonLdWebsite()
    {
        var site = PortalProfile;
        var domain = RtrimSlash(Str(site, "domain_path"));
        var name = Str(site, "system_name");
        if (name.Length == 0)
        {
            name = "Store";
        }

        var ws = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebSite",
            ["name"] = name,
            ["url"] = domain,
            ["potentialAction"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["@type"] = "SearchAction",
                ["target"] = domain + "/en/shop/search?search_string={search_term_string}",
                ["query-input"] = "required name=search_term_string"
            }
        };
        return "<script type=\"application/ld+json\">" + JsonSerializer.Serialize(ws, JsonOpts) + "</script>";
    }

    public static List<Dictionary<string, string>> EpcStorefrontSocialLinksData()
    {
        var contact = ContactBag(PortalSettings);
        var platforms = new (string Key, string Icon, string Label)[]
        {
            ("facebook", "fa-facebook", "Facebook"),
            ("instagram", "fa-instagram", "Instagram"),
            ("twitter", "fa-twitter", "Twitter"),
            ("linkedin", "fa-linkedin", "LinkedIn"),
            ("youtube", "fa-youtube-play", "YouTube"),
            ("tiktok", "fa-music", "TikTok")
        };
        var links = new List<Dictionary<string, string>>();
        foreach (var p in platforms)
        {
            var url = Str(contact, "social_" + p.Key);
            if (url.Length == 0)
            {
                url = Str(contact, p.Key + "_url");
            }

            if (url.Length > 0)
            {
                links.Add(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["platform"] = p.Key,
                    ["url"] = url,
                    ["icon"] = p.Icon,
                    ["label"] = p.Label
                });
            }
        }

        return links;
    }

    public static string EpcStorefrontNewsletterSection(string accentColor = "#0ea5e9", string bgColor = "#f8fafc", string industry = "")
    {
        var headlines = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["electronics"] = new[] { "Stay plugged in", "Get the latest tech deals, launches and exclusive offers delivered to your inbox." },
            ["fashion"] = new[] { "Be the first to know", "New arrivals, style tips, and exclusive offers — straight to your inbox." },
            ["jewellery"] = new[] { "Join our inner circle", "Exclusive previews, new collections, and special offers for our members." },
            ["tax_advisory"] = new[] { "Stay compliant", "Tax updates, regulatory changes, and advisory insights delivered monthly." },
            ["consultancy"] = new[] { "Stay compliant", "Tax updates, regulatory changes, and advisory insights delivered monthly." }
        };
        var h = headlines.TryGetValue(industry, out var pair)
            ? pair
            : new[] { "Stay updated", "Subscribe for the latest updates and exclusive offers." };
        var id = "epc_wc_newsletter_" + IndustryId.Replace(industry ?? "", "");
        var html = new StringBuilder();
        html.Append("<section class=\"epc-wc-newsletter\" id=\"").Append(id).Append("\" style=\"background:").Append(H(bgColor)).Append(";padding:48px 0;text-align:center;\">");
        html.Append("<div class=\"container\">");
        html.Append("<h2 style=\"font-size:24px;font-weight:700;margin:0 0 8px;color:#1e293b;\">").Append(H(h[0])).Append("</h2>");
        html.Append("<p style=\"color:#64748b;margin:0 0 20px;font-size:15px;\">").Append(H(h[1])).Append("</p>");
        html.Append("<form class=\"epc-wc-newsletter__form\" style=\"max-width:480px;margin:0 auto;display:flex;gap:8px;\" onsubmit=\"return epcWcNewsletterSubmit(this)\">");
        html.Append("<input type=\"email\" name=\"email\" required placeholder=\"Enter your email\" style=\"flex:1;padding:12px 16px;border:1px solid #cbd5e1;border-radius:6px;font-size:15px;outline:none;\" />");
        html.Append("<button type=\"submit\" style=\"padding:12px 24px;background:").Append(H(accentColor)).Append(";color:#fff;border:none;border-radius:6px;font-weight:600;font-size:15px;cursor:pointer;white-space:nowrap;\">Subscribe</button>");
        html.Append("</form>");
        html.Append("<p class=\"epc-wc-newsletter__note\" style=\"color:#94a3b8;font-size:12px;margin:12px 0 0;\">No spam. Unsubscribe anytime.</p>");
        html.Append("</div></section>");
        return html.ToString();
    }

    public static string EpcStorefrontTrustBadges(string industry = "")
    {
        var badges = new List<(string Icon, string Text)>
        {
            ("fa-shield", "Secure Checkout"),
            ("fa-truck", "Fast Delivery"),
            ("fa-undo", "Easy Returns"),
            ("fa-certificate", "UAE Registered")
        };
        if (industry == "jewellery")
        {
            badges = new List<(string, string)>
            {
                ("fa-gem", "Certified Authentic"),
                ("fa-shield", "Secure Payment"),
                ("fa-gift", "Gift Wrapping"),
                ("fa-certificate", "Hallmarked Gold")
            };
        }
        else if (industry == "electronics")
        {
            badges = new List<(string, string)>
            {
                ("fa-shield", "Genuine Products"),
                ("fa-truck", "Same-Day Delivery"),
                ("fa-refresh", "14-Day Returns"),
                ("fa-lock", "Secure Checkout")
            };
        }
        else if (industry == "fashion")
        {
            badges = new List<(string, string)>
            {
                ("fa-check-circle", "100% Authentic"),
                ("fa-truck", "Free Shipping 200+"),
                ("fa-undo", "30-Day Returns"),
                ("fa-lock", "Secure Checkout")
            };
        }
        else if (industry is "tax_advisory" or "consultancy")
        {
            badges = new List<(string, string)>
            {
                ("fa-university", "FTA Registered"),
                ("fa-shield", "Data Protected"),
                ("fa-users", "Expert Team"),
                ("fa-certificate", "Licensed Practice")
            };
        }

        var html = new StringBuilder();
        html.Append("<div class=\"epc-wc-trust\" style=\"padding:24px 0;background:#fff;border-top:1px solid #e2e8f0;border-bottom:1px solid #e2e8f0;\">");
        html.Append("<div class=\"container\"><div style=\"display:flex;justify-content:center;flex-wrap:wrap;gap:32px;\">");
        foreach (var b in badges)
        {
            html.Append("<div style=\"display:flex;align-items:center;gap:8px;color:#475569;font-size:14px;font-weight:500;\">");
            html.Append("<i class=\"fa ").Append(H(b.Icon)).Append("\" style=\"font-size:20px;color:#0ea5e9;\"></i>");
            html.Append("<span>").Append(H(b.Text)).Append("</span>");
            html.Append("</div>");
        }

        html.Append("</div></div></div>");
        return html.ToString();
    }

    public static string EpcStorefrontCookieConsent()
        => "<div id=\"epc_wc_cookie\" style=\"display:none;position:fixed;bottom:0;left:0;right:0;background:#1e293b;color:#e2e8f0;padding:14px 20px;z-index:99999;font-size:14px;\">"
           + "<div class=\"container\" style=\"display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px;\">"
           + "<p style=\"margin:0;flex:1;min-width:200px;\">We use cookies to enhance your experience. By continuing to browse, you agree to our use of cookies.</p>"
           + "<div style=\"display:flex;gap:8px;\">"
           + "<button onclick=\"epcWcCookieAccept()\" style=\"padding:8px 20px;background:#0ea5e9;color:#fff;border:none;border-radius:4px;font-weight:600;cursor:pointer;\">Accept</button>"
           + "<button onclick=\"epcWcCookieDecline()\" style=\"padding:8px 20px;background:transparent;color:#94a3b8;border:1px solid #475569;border-radius:4px;cursor:pointer;\">Decline</button>"
           + "</div></div></div>"
           + "<script>"
           + "(function(){var c=document.getElementById(\"epc_wc_cookie\");if(!c)return;if(!document.cookie.match(/(?:^|; )epc_cookie_consent=/)){c.style.display=\"block\";}})();"
           + "function epcWcCookieAccept(){document.cookie=\"epc_cookie_consent=accepted;max-age=\"+(365*86400)+\";path=/;SameSite=Lax\";var c=document.getElementById(\"epc_wc_cookie\");if(c)c.style.display=\"none\";}"
           + "function epcWcCookieDecline(){document.cookie=\"epc_cookie_consent=declined;max-age=\"+(365*86400)+\";path=/;SameSite=Lax\";var c=document.getElementById(\"epc_wc_cookie\");if(c)c.style.display=\"none\";}"
           + "</script>";

    public static string EpcStorefrontNewsletterJs()
        => "<script>\n"
           + "function epcWcNewsletterSubmit(form){\n"
           + "	var email=form.querySelector(\"input[name=email]\");\n"
           + "	if(!email||!email.value)return false;\n"
           + "	var btn=form.querySelector(\"button[type=submit]\");\n"
           + "	if(btn){btn.textContent=\"Subscribing...\";btn.disabled=true;}\n"
           + "	var xhr=new XMLHttpRequest();\n"
           + "	xhr.open(\"POST\",\"/ajax_newsletter_subscribe.php\",true);\n"
           + "	xhr.setRequestHeader(\"Content-Type\",\"application/x-www-form-urlencoded\");\n"
           + "	xhr.onload=function(){\n"
           + "		if(btn){btn.textContent=\"Subscribed!\";btn.style.background=\"#22c55e\";}\n"
           + "		email.value=\"\";email.placeholder=\"Thank you!\";\n"
           + "		setTimeout(function(){if(btn){btn.textContent=\"Subscribe\";btn.disabled=false;btn.style.background=\"\";}email.placeholder=\"Enter your email\";},4000);\n"
           + "	};\n"
           + "	xhr.onerror=function(){if(btn){btn.textContent=\"Subscribe\";btn.disabled=false;}};\n"
           + "	xhr.send(\"email=\"+encodeURIComponent(email.value)+\"&action=subscribe\");\n"
           + "	return false;\n"
           + "}\n"
           + "</script>";

    private static string AdaptText(string text, IReadOnlyDictionary<string, string> brand)
    {
        var name = brand.TryGetValue("brand_name", out var n) ? n : "";
        var handle = brand.TryGetValue("handle", out var h) ? h : "";
        var domain = brand.TryGetValue("domain", out var d) ? d : "";
        var website = brand.TryGetValue("website", out var w) ? w : "";
        var hash = "#" + BrandHash.Replace(name.ToUpperInvariant(), "");
        return text
            .Replace("ECOM AE", name, StringComparison.Ordinal)
            .Replace("ecomae.official", handle, StringComparison.Ordinal)
            .Replace("ecomae.com", domain, StringComparison.Ordinal)
            .Replace("https://www.ecomae.com", website, StringComparison.Ordinal)
            .Replace("www.ecomae.com", domain, StringComparison.Ordinal)
            .Replace("#ECOMAE", hash, StringComparison.Ordinal);
    }

    private static Dictionary<string, object?> ContactBag(IReadOnlyDictionary<string, object?> settings)
    {
        if (settings.TryGetValue("contact", out var c) && c is IReadOnlyDictionary<string, object?> bag)
        {
            return bag.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static string Str(IReadOnlyDictionary<string, object?> bag, string key)
        => bag.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
            : "";

    private static string RtrimSlash(string value) => value.TrimEnd('/');

    private static string H(string value)
        => (value ?? "")
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string PhpSubstr(string value, int start, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? "");
        if (start >= bytes.Length)
        {
            return "";
        }

        var take = Math.Min(length, bytes.Length - start);
        return Encoding.UTF8.GetString(bytes, start, take);
    }

    private static string PatchSort(JsonElement child, int sort)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(child.GetRawText()) ?? new();
        dict["sort"] = sort;
        return JsonSerializer.Serialize(dict, JsonOpts);
    }

    private static Dictionary<string, object?> NodeToRow(TaxNodeRow n)
        => new(StringComparer.Ordinal)
        {
            ["id"] = n.Id,
            ["industry_key"] = n.IndustryKey,
            ["parent_id"] = n.ParentId,
            ["slug"] = n.Slug,
            ["name_en"] = n.NameEn,
            ["amazon_node_ref"] = n.AmazonNodeRef,
            ["catalogue_category_id"] = n.CatalogueCategoryId,
            ["sort"] = n.Sort,
            ["level"] = n.Level,
            ["active"] = n.Active
        };

    public sealed class LogisticsStore
    {
        public PhpPlanQ1Plus.ChannelStore Channels { get; } = new();
        public long Now { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public int NextCarrierId { get; set; }
        public int NextShipmentId { get; set; }
        public List<LogisticsCarrierRow> Carriers { get; } = new();
        public List<LogisticsShipmentRow> Shipments { get; } = new();
        public List<LogisticsLogRow> Logs { get; } = new();
        public List<LogisticsShopOrderRow> ShopOrders { get; } = new();
        public List<LogisticsObtainRow> ObtainModes { get; } = new();
    }

    public sealed class LogisticsCarrierRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public int Active { get; set; } = 1;
        public int DemoMode { get; set; } = 1;
        public string? ConfigJson { get; set; }
        public long TimeCreated { get; set; }
    }

    public sealed class LogisticsShipmentRow
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string CarrierCode { get; set; } = "";
        public string ServiceCode { get; set; } = "";
        public string TrackingNumber { get; set; } = "";
        public string LabelUrl { get; set; } = "";
        public string Status { get; set; } = "draft";
        public double WeightKg { get; set; }
        public double Cost { get; set; }
        public string Currency { get; set; } = "AED";
        public string? RecipientJson { get; set; }
        public long ShippedAt { get; set; }
        public long TimeCreated { get; set; }
    }

    public sealed class LogisticsLogRow
    {
        public int Id { get; set; }
        public string Kind { get; set; } = "";
        public string ChannelCode { get; set; } = "";
        public string Message { get; set; } = "";
        public long TimeCreated { get; set; }
    }

    public sealed class LogisticsShopOrderRow
    {
        public int Id { get; set; }
        public int SuccessfullyCreated { get; set; }
        public long Time { get; set; }
    }

    public sealed class LogisticsObtainRow
    {
        public int Id { get; set; }
        public string Handler { get; set; } = "";
    }

    public sealed class TaxStore
    {
        public bool SchemaReady { get; set; }
        public int NextId { get; set; }
        public List<TaxNodeRow> Nodes { get; } = new();
        public List<TaxCategoryRow> Categories { get; } = new();
    }

    public sealed class TaxNodeRow
    {
        public int Id { get; set; }
        public string IndustryKey { get; set; } = "electronics";
        public int ParentId { get; set; }
        public string Slug { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string AmazonNodeRef { get; set; } = "";
        public int CatalogueCategoryId { get; set; }
        public int Sort { get; set; }
        public int Level { get; set; }
        public int Active { get; set; } = 1;
    }

    public sealed class TaxCategoryRow
    {
        public int Id { get; set; }
        public string Value { get; set; } = "";
        public string Caption { get; set; } = "";
        public int PublishedFlag { get; set; }
    }
}
