#!/usr/bin/env python3
"""Apply Wave 22 CMS/platform leftover digests (codegen helper — not part of runtime)."""
from __future__ import annotations

import json
import re
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NOW = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

# stem, Pascal, php path, tables note, collection key, summary fields, row fields, kpis labels, omit note
DIGESTS = [
    {
        "stem": "geo-regions",
        "pascal": "GeoRegions",
        "php": "/CP/shop/geo/nodes",
        "tables": "shop_geo + shop_offices_geo_map",
        "collection": "nodes",
        "summary_fields": ["nodeCount", "level1Count", "level2Count", "mappedOfficeCount"],
        "sql_stats": """
        SELECT
            (SELECT COUNT(*) FROM `shop_geo`) AS node_count,
            (SELECT COUNT(*) FROM `shop_geo` WHERE IFNULL(`level`,0)=1) AS level1_count,
            (SELECT COUNT(*) FROM `shop_geo` WHERE IFNULL(`level`,0)=2) AS level2_count,
            (SELECT COUNT(DISTINCT `office_id`) FROM `shop_offices_geo_map`) AS mapped_office_count
        """,
        "sql_rows": """
        SELECT `id`, IFNULL(`level`,0) AS level, IFNULL(`parent`,0) AS parent,
               IFNULL(`order`,0) AS sort_order, IFNULL(`count`,0) AS child_count,
               IFNULL(`value`,0) AS value_lang_id
        FROM `shop_geo`
        ORDER BY `level` ASC, `order` ASC, `id` ASC
        LIMIT @limit
        """,
        "row_ctor": [
            ("Id", "id", "Int64"),
            ("Level", "level", "Int32"),
            ("Parent", "parent", "Int64"),
            ("SortOrder", "sort_order", "Int32"),
            ("ChildCount", "child_count", "Int32"),
            ("ValueLangId", "value_lang_id", "Int64"),
        ],
        "kpi_labels": ["Nodes", "Level 1", "Level 2", "Mapped offices"],
        "omit": "raw lang string bodies; value stored as lang id",
        "title": "Geo / regions",
        "hero": "Read-only geo tree KPIs from shop_geo. Display names stay on PHP (lang ids).",
        "cols": [("Id", "Id"), ("Level", "Level"), ("Parent", "Parent"), ("Order", "SortOrder"), ("Children", "ChildCount"), ("Lang id", "ValueLangId")],
        "resilient_counts": [
            ("nodeCount", "SELECT COUNT(*) FROM `shop_geo`"),
            ("level1Count", "SELECT COUNT(*) FROM `shop_geo` WHERE IFNULL(`level`,0)=1"),
            ("level2Count", "SELECT COUNT(*) FROM `shop_geo` WHERE IFNULL(`level`,0)=2"),
            ("mappedOfficeCount", "SELECT COUNT(DISTINCT `office_id`) FROM `shop_offices_geo_map`"),
        ],
        "matcher_cp": ["geo-regions"],
        "matcher_bos": [],
    },
    {
        "stem": "product-filters",
        "pascal": "ProductFilters",
        "php": "/CP/shop/filter",
        "tables": "shop_docpart_filter",
        "collection": "filters",
        "summary_fields": ["filterCount", "withStorageScope", "withPriceBand", "withTimeBand"],
        "sql_stats": """
        SELECT
            COUNT(*) AS filter_count,
            SUM(CASE WHEN IFNULL(`list_storages`,'') NOT IN ('','[]','null') THEN 1 ELSE 0 END) AS with_storage_scope,
            SUM(CASE WHEN IFNULL(`min_price`,0)>0 OR IFNULL(`max_price`,0)>0 THEN 1 ELSE 0 END) AS with_price_band,
            SUM(CASE WHEN IFNULL(`min_time`,0)>0 OR IFNULL(`max_time`,0)>0 THEN 1 ELSE 0 END) AS with_time_band
        FROM `shop_docpart_filter`
        """,
        "sql_rows": """
        SELECT `id`, IFNULL(`manufacturer`,'') AS manufacturer, IFNULL(`article`,'') AS article,
               IFNULL(`name`,'') AS name,
               IFNULL(`min_price`,0) AS min_price, IFNULL(`max_price`,0) AS max_price,
               IFNULL(`min_time`,0) AS min_time, IFNULL(`max_time`,0) AS max_time
        FROM `shop_docpart_filter`
        ORDER BY `id` DESC
        LIMIT @limit
        """,
        "row_ctor": [
            ("Id", "id", "Int64"),
            ("Manufacturer", "manufacturer", "String"),
            ("Article", "article", "String"),
            ("Name", "name", "String"),
            ("MinPrice", "min_price", "Decimal"),
            ("MaxPrice", "max_price", "Decimal"),
            ("MinTime", "min_time", "Int32"),
            ("MaxTime", "max_time", "Int32"),
        ],
        "kpi_labels": ["Filters", "Storage scope", "Price band", "Time band"],
        "omit": "list_storages JSON",
        "title": "Product filters",
        "hero": "Read-only shop_docpart_filter KPIs. PHP ajax_operations.php write path is broken — ASP.NET stays read-only.",
        "cols": [("Id", "Id"), ("Manufacturer", "Manufacturer"), ("Article", "Article"), ("Name", "Name"), ("Min price", "MinPrice"), ("Max price", "MaxPrice")],
        "matcher_cp": ["product-filters"],
        "matcher_bos": [],
    },
    {
        "stem": "search-tabs",
        "pascal": "SearchTabs",
        "php": "/CP/shop/taby-poiska",
        "tables": "shop_docpart_search_tabs",
        "collection": "tabs",
        "summary_fields": ["tabCount", "enabledCount", "disabledCount", "maxOrder"],
        "sql_stats": """
        SELECT
            COUNT(*) AS tab_count,
            SUM(CASE WHEN IFNULL(`enabled`,0)=1 THEN 1 ELSE 0 END) AS enabled_count,
            SUM(CASE WHEN IFNULL(`enabled`,0)=0 THEN 1 ELSE 0 END) AS disabled_count,
            IFNULL(MAX(`order`),0) AS max_order
        FROM `shop_docpart_search_tabs`
        """,
        "sql_rows": """
        SELECT `id`, IFNULL(`caption`,'') AS caption, IFNULL(`order`,0) AS sort_order,
               IFNULL(`enabled`,0) AS enabled
        FROM `shop_docpart_search_tabs`
        ORDER BY `order` ASC, `id` ASC
        LIMIT @limit
        """,
        "row_ctor": [
            ("Id", "id", "Int64"),
            ("Caption", "caption", "String"),
            ("SortOrder", "sort_order", "Int32"),
            ("Enabled", "enabled", "Int32"),
        ],
        "kpi_labels": ["Tabs", "Enabled", "Disabled", "Max order"],
        "omit": "parameters_values JSON",
        "title": "Search tabs",
        "hero": "Read-only search tab KPIs. parameters_values stay on PHP.",
        "cols": [("Id", "Id"), ("Caption", "Caption"), ("Order", "SortOrder"), ("Enabled", "Enabled")],
        "matcher_cp": ["search-tabs"],
        "matcher_bos": [],
    },
    {
        "stem": "system-requests",
        "pascal": "SystemRequests",
        "php": "/CP/requests",
        "tables": "users_vin",
        "collection": "requests",
        "summary_fields": ["requestCount", "unviewedCount", "viewedCount", "withUserCount"],
        "sql_stats": """
        SELECT
            COUNT(*) AS request_count,
            SUM(CASE WHEN IFNULL(`viewed`,0)=0 THEN 1 ELSE 0 END) AS unviewed_count,
            SUM(CASE WHEN IFNULL(`viewed`,0)=1 THEN 1 ELSE 0 END) AS viewed_count,
            SUM(CASE WHEN IFNULL(`user_id`,0)>0 THEN 1 ELSE 0 END) AS with_user_count
        FROM `users_vin`
        """,
        "sql_rows": """
        SELECT `id`, IFNULL(`time`,0) AS time_unix, IFNULL(`user_id`,0) AS user_id,
               IFNULL(`viewed`,0) AS viewed
        FROM `users_vin`
        ORDER BY `id` DESC
        LIMIT @limit
        """,
        "row_ctor": [
            ("Id", "id", "Int64"),
            ("TimeUnix", "time_unix", "Int64"),
            ("UserId", "user_id", "Int64"),
            ("Viewed", "viewed", "Int32"),
        ],
        "kpi_labels": ["Requests", "Unviewed", "Viewed", "With user"],
        "omit": "VIN request text body (injection-prone PHP cookie filters not ported)",
        "title": "System requests",
        "hero": "Read-only VIN request queue KPIs with bound params (PHP cookie-filter SQL is unsafe).",
        "cols": [("Id", "Id"), ("Time", "TimeUnix"), ("User", "UserId"), ("Viewed", "Viewed")],
        "matcher_cp": ["system-requests"],
        "matcher_bos": [],
    },
    {
        "stem": "additional-texts",
        "pascal": "AdditionalTexts",
        "php": "/CP/content/dopolnitelnye-teksty",
        "tables": "text_for_url",
        "collection": "texts",
        "summary_fields": ["textCount", "beforeMainCount", "withTitleCount", "withDescriptionCount"],
        "sql_stats": """
        SELECT
            COUNT(*) AS text_count,
            SUM(CASE WHEN IFNULL(`before_main`,0)=1 THEN 1 ELSE 0 END) AS before_main_count,
            SUM(CASE WHEN IFNULL(`title_tag`,'')!='' THEN 1 ELSE 0 END) AS with_title_count,
            SUM(CASE WHEN IFNULL(`description_tag`,'')!='' THEN 1 ELSE 0 END) AS with_description_count
        FROM `text_for_url`
        """,
        "sql_rows": """
        SELECT `id`, IFNULL(`url`,'') AS url, IFNULL(`before_main`,0) AS before_main,
               IFNULL(`title_tag`,'') AS title_tag, IFNULL(`keywords_tag`,'') AS keywords_tag
        FROM `text_for_url`
        ORDER BY `id` DESC
        LIMIT @limit
        """,
        "row_ctor": [
            ("Id", "id", "Int64"),
            ("Url", "url", "String"),
            ("BeforeMain", "before_main", "Int32"),
            ("TitleTag", "title_tag", "String"),
            ("KeywordsTag", "keywords_tag", "String"),
        ],
        "kpi_labels": ["Texts", "Before main", "With title", "With description"],
        "omit": "content HTML + description_tag bodies in rows (title/keywords only)",
        "title": "Additional texts",
        "hero": "Read-only SEO/text-for-URL KPIs. HTML content omitted.",
        "cols": [("Id", "Id"), ("URL", "Url"), ("Before", "BeforeMain"), ("Title", "TitleTag")],
        "matcher_cp": ["additional-texts"],
        "matcher_bos": [],
    },
    {
        "stem": "slider-banners",
        "pascal": "SliderBanners",
        "php": "/CP/content/slider",
        "tables": "slider_images + slider_setings",
        "collection": "images",
        "summary_fields": ["imageCount", "connected", "cntImg", "cntImgNext"],
        "sql_stats": """
        SELECT
            (SELECT COUNT(*) FROM `slider_images`) AS image_count,
            (SELECT IFNULL(`connected`,0) FROM `slider_setings` LIMIT 1) AS connected,
            (SELECT IFNULL(`cnt_img`,0) FROM `slider_setings` LIMIT 1) AS cnt_img,
            (SELECT IFNULL(`cnt_img_next`,0) FROM `slider_setings` LIMIT 1) AS cnt_img_next
        """,
        "sql_rows": """
        SELECT `id`, IFNULL(`orders`,0) AS sort_order, IFNULL(`link`,'') AS link,
               IFNULL(`href`,'') AS href
        FROM `slider_images`
        ORDER BY `orders` ASC, `id` ASC
        LIMIT @limit
        """,
        "row_ctor": [
            ("Id", "id", "Int64"),
            ("SortOrder", "sort_order", "Int32"),
            ("Link", "link", "String"),
            ("Href", "href", "String"),
        ],
        "kpi_labels": ["Images", "Connected", "Visible", "Scroll"],
        "omit": "none critical (paths only)",
        "title": "Slider / banners",
        "hero": "Read-only slider_images + slider_setings KPIs (PHP table typo setings preserved).",
        "cols": [("Id", "Id"), ("Order", "SortOrder"), ("Link", "Link"), ("Href", "Href")],
        "resilient_counts": [
            ("imageCount", "SELECT COUNT(*) FROM `slider_images`"),
            ("connected", "SELECT IFNULL(`connected`,0) FROM `slider_setings` LIMIT 1"),
            ("cntImg", "SELECT IFNULL(`cnt_img`,0) FROM `slider_setings` LIMIT 1"),
            ("cntImgNext", "SELECT IFNULL(`cnt_img_next`,0) FROM `slider_setings` LIMIT 1"),
        ],
        "matcher_cp": ["slider-banners"],
        "matcher_bos": [],
    },
    {
        "stem": "structure-dumps",
        "pascal": "StructureDumps",
        "php": "/CP/content/structure_dumps",
        "tables": "content_structure_dumps",
        "collection": "dumps",
        "summary_fields": ["dumpCount", "totalRecords", "latestTimeCreated", "withFileCount"],
        "sql_stats": """
        SELECT
            COUNT(*) AS dump_count,
            IFNULL(SUM(`records_count`),0) AS total_records,
            IFNULL(MAX(`time_created`),0) AS latest_time_created,
            SUM(CASE WHEN IFNULL(`file_name`,'')!='' THEN 1 ELSE 0 END) AS with_file_count
        FROM `content_structure_dumps`
        """,
        "sql_rows": """
        SELECT `id`, IFNULL(`time_created`,0) AS time_created, IFNULL(`fields_in_dump`,'') AS fields_in_dump,
               IFNULL(`file_name`,'') AS file_name, IFNULL(`records_count`,0) AS records_count
        FROM `content_structure_dumps`
        ORDER BY `id` DESC
        LIMIT @limit
        """,
        "row_ctor": [
            ("Id", "id", "Int64"),
            ("TimeCreated", "time_created", "Int64"),
            ("FieldsInDump", "fields_in_dump", "String"),
            ("FileName", "file_name", "String"),
            ("RecordsCount", "records_count", "Int64"),
        ],
        "kpi_labels": ["Dumps", "Total records", "Latest", "With file"],
        "omit": "dump file bodies",
        "title": "Structure dumps",
        "hero": "Read-only content structure dump metadata. File bodies stay on disk/PHP.",
        "cols": [("Id", "Id"), ("Created", "TimeCreated"), ("Fields", "FieldsInDump"), ("File", "FileName"), ("Records", "RecordsCount")],
        "matcher_cp": ["structure-dumps"],
        "matcher_bos": [],
    },
    {
        "stem": "communications-test",
        "pascal": "CommunicationsTest",
        "php": "/CP/control/communications",
        "tables": "debug_results + sms_api",
        "collection": "channels",
        "summary_fields": ["smsActiveCount", "smsTotalCount", "emailLastStatus", "smsLastStatus"],
        # emailLastStatus/smsLastStatus encoded as 1=ok 0=fail/-1=unknown via COUNT tricks — use string fields in models instead
        "summary_types": ["int", "int", "string", "string"],
        "sql_stats": None,  # resilient only
        "sql_rows": """
        SELECT IFNULL(`name`,'') AS name, IFNULL(`active`,0) AS active,
               IFNULL(`is_selectable`,0) AS is_selectable, IFNULL(`handler`,'') AS handler
        FROM `sms_api`
        ORDER BY `id` ASC
        LIMIT @limit
        """,
        "row_ctor": [
            ("Name", "name", "String"),
            ("Active", "active", "Int32"),
            ("IsSelectable", "is_selectable", "Int32"),
            ("Handler", "handler", "String"),
        ],
        "kpi_labels": ["SMS active", "SMS total", "Email last", "SMS last"],
        "omit": "debug_result blobs + sms parameters_values secrets",
        "title": "Communications test",
        "hero": "Read-only SMS channel + last email/SMS probe status. Secrets and debug blobs omitted.",
        "cols": [("Name", "Name"), ("Active", "Active"), ("Selectable", "IsSelectable"), ("Handler", "Handler")],
        "resilient_counts": [
            ("smsActiveCount", "SELECT COUNT(*) FROM `sms_api` WHERE IFNULL(`active`,0)=1"),
            ("smsTotalCount", "SELECT COUNT(*) FROM `sms_api`"),
        ],
        "status_queries": [
            ("emailLastStatus", "SELECT IFNULL(`status`,'') FROM `debug_results` WHERE `name`='email' ORDER BY `time` DESC LIMIT 1"),
            ("smsLastStatus", "SELECT IFNULL(`status`,'') FROM `debug_results` WHERE `name`='sms' ORDER BY `time` DESC LIMIT 1"),
        ],
        "matcher_cp": ["communications-test"],
        "matcher_bos": [],
    },
    {
        "stem": "languages",
        "pascal": "Languages",
        "php": "/CP/lang",
        "tables": "lang_languages",
        "collection": "languages",
        "summary_fields": ["languageCount", "activeCount", "defaultCount", "inactiveCount"],
        "sql_stats": """
        SELECT
            COUNT(*) AS language_count,
            SUM(CASE WHEN IFNULL(`active`,0)=1 THEN 1 ELSE 0 END) AS active_count,
            SUM(CASE WHEN IFNULL(`is_default`,0)=1 THEN 1 ELSE 0 END) AS default_count,
            SUM(CASE WHEN IFNULL(`active`,0)=0 THEN 1 ELSE 0 END) AS inactive_count
        FROM `lang_languages`
        """,
        "sql_rows": """
        SELECT IFNULL(`lang_code`,'') AS lang_code, IFNULL(`active`,0) AS active,
               IFNULL(`is_default`,0) AS is_default
        FROM `lang_languages`
        ORDER BY `is_default` DESC, `active` DESC, `lang_code` ASC
        LIMIT @limit
        """,
        "row_ctor": [
            ("LangCode", "lang_code", "String"),
            ("Active", "active", "Int32"),
            ("IsDefault", "is_default", "Int32"),
        ],
        "kpi_labels": ["Languages", "Active", "Default", "Inactive"],
        "omit": "translation string bodies",
        "title": "Languages",
        "hero": "Read-only language pack KPIs. Translation bodies stay on PHP.",
        "cols": [("Code", "LangCode"), ("Active", "Active"), ("Default", "IsDefault")],
        "matcher_cp": ["languages"],
        "matcher_bos": [],
    },
    {
        "stem": "plugins-manager",
        "pascal": "PluginsManager",
        "php": "/CP/plugins/plugins_manager",
        "tables": "plugins",
        "collection": "plugins",
        "summary_fields": ["pluginCount", "activatedCount", "frontendCount", "lockedCount"],
        "sql_stats": """
        SELECT
            COUNT(*) AS plugin_count,
            SUM(CASE WHEN IFNULL(`activated`,0)=1 THEN 1 ELSE 0 END) AS activated_count,
            SUM(CASE WHEN IFNULL(`is_frontend`,0)=1 THEN 1 ELSE 0 END) AS frontend_count,
            SUM(CASE WHEN IFNULL(`control_lock`,0)=1 THEN 1 ELSE 0 END) AS locked_count
        FROM `plugins`
        """,
        "sql_rows": """
        SELECT `id`, IFNULL(`caption`,'') AS caption, IFNULL(`order`,0) AS sort_order,
               IFNULL(`activated`,0) AS activated, IFNULL(`is_frontend`,0) AS is_frontend,
               IFNULL(`control_lock`,0) AS control_lock
        FROM `plugins`
        ORDER BY `order` ASC, `id` ASC
        LIMIT @limit
        """,
        "row_ctor": [
            ("Id", "id", "Int64"),
            ("Caption", "caption", "String"),
            ("SortOrder", "sort_order", "Int32"),
            ("Activated", "activated", "Int32"),
            ("IsFrontend", "is_frontend", "Int32"),
            ("ControlLock", "control_lock", "Int32"),
        ],
        "kpi_labels": ["Plugins", "Activated", "Frontend", "Locked"],
        "omit": "data_value JSON + filesystem delete side-effects",
        "title": "Plugins manager",
        "hero": "Read-only plugin inventory. data_value and FS delete stay on PHP.",
        "cols": [("Id", "Id"), ("Caption", "Caption"), ("Order", "SortOrder"), ("Active", "Activated"), ("Frontend", "IsFrontend"), ("Lock", "ControlLock")],
        "matcher_cp": ["plugins-manager"],
        "matcher_bos": [],
    },
    {
        "stem": "templates-manager",
        "pascal": "TemplatesManager",
        "php": "/CP/templates/templates_manager",
        "tables": "templates",
        "collection": "templates",
        "summary_fields": ["templateCount", "frontendCount", "currentFrontendCount", "currentBackendCount"],
        "sql_stats": """
        SELECT
            COUNT(*) AS template_count,
            SUM(CASE WHEN IFNULL(`is_frontend`,0)=1 THEN 1 ELSE 0 END) AS frontend_count,
            SUM(CASE WHEN IFNULL(`is_frontend`,0)=1 AND IFNULL(`current`,0)=1 THEN 1 ELSE 0 END) AS current_frontend_count,
            SUM(CASE WHEN IFNULL(`is_frontend`,0)=0 AND IFNULL(`current`,0)=1 THEN 1 ELSE 0 END) AS current_backend_count
        FROM `templates`
        """,
        "sql_rows": """
        SELECT `id`, IFNULL(`caption`,'') AS caption, IFNULL(`name`,'') AS name,
               IFNULL(`current`,0) AS current_flag, IFNULL(`is_frontend`,0) AS is_frontend,
               IFNULL(`phone_support`,0) AS phone_support, IFNULL(`tablet_support`,0) AS tablet_support
        FROM `templates`
        ORDER BY `is_frontend` DESC, `current` DESC, `id` ASC
        LIMIT @limit
        """,
        "row_ctor": [
            ("Id", "id", "Int64"),
            ("Caption", "caption", "String"),
            ("Name", "name", "String"),
            ("Current", "current_flag", "Int32"),
            ("IsFrontend", "is_frontend", "Int32"),
            ("PhoneSupport", "phone_support", "Int32"),
            ("TabletSupport", "tablet_support", "Int32"),
        ],
        "kpi_labels": ["Templates", "Frontend", "Current FE", "Current BE"],
        "omit": "data_value JSON + FS delete",
        "title": "Templates manager",
        "hero": "Read-only template inventory. data_value and FS delete stay on PHP.",
        "cols": [("Id", "Id"), ("Caption", "Caption"), ("Name", "Name"), ("Current", "Current"), ("Frontend", "IsFrontend")],
        "matcher_cp": ["templates-manager"],
        "matcher_bos": [],
    },
    {
        "stem": "design-tokens",
        "pascal": "DesignTokens",
        "php": "/CP/control/portal/epc_design_tokens",
        "tables": "epc_settings (brand_*)",
        "collection": "tokens",
        "summary_fields": ["tokenCount", "tenantCount", "whiteLabelCount", "updatedRecentCount"],
        "sql_stats": """
        SELECT
            (SELECT COUNT(*) FROM `epc_settings` WHERE `setting_key` LIKE 'brand_%' OR `setting_key`='white_label_login') AS token_count,
            (SELECT COUNT(DISTINCT IFNULL(`site_key`,'')) FROM `epc_settings` WHERE `setting_key` LIKE 'brand_%' OR `setting_key`='white_label_login') AS tenant_count,
            (SELECT COUNT(*) FROM `epc_settings` WHERE `setting_key`='white_label_login' AND IFNULL(`setting_value`,'') NOT IN ('','0','false')) AS white_label_count,
            (SELECT COUNT(*) FROM `epc_settings` WHERE (`setting_key` LIKE 'brand_%' OR `setting_key`='white_label_login') AND `updated_at` >= (UNIX_TIMESTAMP()-86400*30)) AS updated_recent_count
        """,
        "sql_rows": """
        SELECT IFNULL(`site_key`,'') AS site_key, IFNULL(`setting_key`,'') AS setting_key,
               IFNULL(CAST(`updated_at` AS CHAR),'') AS updated_at
        FROM `epc_settings`
        WHERE `setting_key` LIKE 'brand_%' OR `setting_key`='white_label_login'
        ORDER BY `updated_at` DESC, `site_key` ASC, `setting_key` ASC
        LIMIT @limit
        """,
        "row_ctor": [
            ("SiteKey", "site_key", "String"),
            ("SettingKey", "setting_key", "String"),
            ("UpdatedAt", "updated_at", "String"),
        ],
        "kpi_labels": ["Tokens", "Tenants", "White-label", "Updated 30d"],
        "omit": "setting_value (colors/URLs); ASP.NET also tolerates missing site_key via resilient KPIs",
        "title": "Design tokens",
        "hero": "Read-only brand token keys from epc_settings. Values omitted. PHP CP page is missing — digest exposes registry truth.",
        "cols": [("Site", "SiteKey"), ("Key", "SettingKey"), ("Updated", "UpdatedAt")],
        "resilient_counts": [
            ("tokenCount", "SELECT COUNT(*) FROM `epc_settings` WHERE `setting_key` LIKE 'brand_%' OR `setting_key`='white_label_login'"),
            ("tenantCount", "SELECT COUNT(DISTINCT IFNULL(`site_key`,'')) FROM `epc_settings` WHERE `setting_key` LIKE 'brand_%' OR `setting_key`='white_label_login'"),
            ("whiteLabelCount", "SELECT COUNT(*) FROM `epc_settings` WHERE `setting_key`='white_label_login' AND IFNULL(`setting_value`,'') NOT IN ('','0','false')"),
            ("updatedRecentCount", "SELECT COUNT(*) FROM `epc_settings` WHERE (`setting_key` LIKE 'brand_%' OR `setting_key`='white_label_login') AND `updated_at` >= DATE_SUB(NOW(), INTERVAL 30 DAY)"),
        ],
        "matcher_cp": [],
        "matcher_bos": ["design_tokens"],
        "surface_prefix": "cp",  # still under /cp/ for digests; BOS matcher points here
    },
    {
        "stem": "sitemap",
        "pascal": "Sitemap",
        "php": "/CP/content/sitemap",
        "tables": "content + shop_catalogue_categories + shop_catalogue_products",
        "collection": "pages",
        "summary_fields": ["contentUrlCount", "categoryCount", "productCount", "frontendContentCount"],
        "sql_stats": None,
        "sql_rows": """
        SELECT `id`, IFNULL(`alias`,'') AS alias, IFNULL(`value`,0) AS value_lang_id,
               IFNULL(`is_frontend`,0) AS is_frontend, IFNULL(`published_flag`,0) AS published_flag
        FROM `content`
        WHERE IFNULL(`is_frontend`,0)=1
        ORDER BY `id` DESC
        LIMIT @limit
        """,
        "row_ctor": [
            ("Id", "id", "Int64"),
            ("Alias", "alias", "String"),
            ("ValueLangId", "value_lang_id", "Int64"),
            ("IsFrontend", "is_frontend", "Int32"),
            ("PublishedFlag", "published_flag", "Int32"),
        ],
        "kpi_labels": ["Content URLs", "Categories", "Products", "Frontend pages"],
        "omit": "sitemap.xml file artifact (generation remains PHP); content HTML omitted",
        "title": "Sitemap",
        "hero": "Read-only sitemap source readiness counts. XML generation remains PHP.",
        "cols": [("Id", "Id"), ("Alias", "Alias"), ("Frontend", "IsFrontend"), ("Published", "PublishedFlag")],
        "resilient_counts": [
            ("contentUrlCount", "SELECT COUNT(*) FROM `content` WHERE IFNULL(`alias`,'')!=''"),
            ("categoryCount", "SELECT COUNT(*) FROM `shop_catalogue_categories`"),
            ("productCount", "SELECT COUNT(*) FROM `shop_catalogue_products`"),
            ("frontendContentCount", "SELECT COUNT(*) FROM `content` WHERE IFNULL(`is_frontend`,0)=1"),
        ],
        "matcher_cp": ["sitemap"],
        "matcher_bos": [],
    },
]


def camel_to_snake(name: str) -> str:
    s1 = re.sub("(.)([A-Z][a-z]+)", r"\1_\2", name)
    return re.sub("([a-z0-9])([A-Z])", r"\1_\2", s1).lower()


def reader_expr(col: str, typ: str) -> str:
    if typ == "String":
        return f'Convert.ToString(reader["{col}"] is DBNull ? string.Empty : reader["{col}"], CultureInfo.InvariantCulture) ?? string.Empty'
    if typ == "Int32":
        return f'Convert.ToInt32(reader["{col}"] is DBNull ? 0 : reader["{col}"], CultureInfo.InvariantCulture)'
    if typ == "Int64":
        return f'Convert.ToInt64(reader["{col}"] is DBNull ? 0 : reader["{col}"], CultureInfo.InvariantCulture)'
    if typ == "Decimal":
        return f'Convert.ToDecimal(reader["{col}"] is DBNull ? 0 : reader["{col}"], CultureInfo.InvariantCulture)'
    raise ValueError(typ)


def csharp_type(typ: str) -> str:
    return {"String": "string", "Int32": "int", "Int64": "long", "Decimal": "decimal"}[typ]


def summary_csharp_types(d: dict) -> list[str]:
    custom = d.get("summary_types")
    if custom:
        return [{"int": "int", "string": "string"}[t] for t in custom]
    return ["int"] * len(d["summary_fields"])


def append_sql():
    path = ROOT / "aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs"
    text = path.read_text(encoding="utf-8")
    if "SelectCpGeoRegionsStats" in text or "CountCpGeoRegionsNodeCount" in text:
        print("SQL already present")
        return
    chunks = ["\n    // ---- Wave 22 CMS/platform leftover digests ----\n"]
    for d in DIGESTS:
        p = d["pascal"]
        if d.get("sql_stats") and not d.get("resilient_counts"):
            chunks.append(
                f'    /// <summary>Wave 22 {d["stem"]} KPIs ({d["tables"]}).</summary>\n'
                f'    public const string SelectCp{p}Stats = """\n'
                f'{d["sql_stats"].strip()}\n'
                f'        """;\n\n'
            )
        if d.get("resilient_counts"):
            for field, sql in d["resilient_counts"]:
                const = f"CountCp{p}{field[0].upper()+field[1:]}"
                chunks.append(
                    f'    public const string {const} = "{sql.strip()}";\n'
                )
            chunks.append("\n")
        if d.get("status_queries"):
            for field, sql in d["status_queries"]:
                const = f"SelectCp{p}{field[0].upper()+field[1:]}"
                chunks.append(
                    f'    public const string {const} = "{sql.strip()}";\n'
                )
            chunks.append("\n")
        chunks.append(
            f'    /// <summary>Wave 22 {d["stem"]} rows — {d["omit"]}.</summary>\n'
            f'    public const string SelectCp{p}Rows = """\n'
            f'{d["sql_rows"].strip()}\n'
            f'        """;\n\n'
        )
    idx = text.rfind("\n}")
    if idx < 0:
        raise SystemExit("Cannot find class end in LegacySurfaceDashboardSql.cs")
    path.write_text(text[:idx] + "\n" + "".join(chunks) + text[idx:], encoding="utf-8")
    print("SQL appended", len(DIGESTS))


def append_models():
    path = ROOT / "aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardModels.cs"
    text = path.read_text(encoding="utf-8")
    if "CpGeoRegionsSummary" in text:
        print("Models already present")
        return
    chunks = ["\n// ---- Wave 22 CMS/platform leftover digests ----\n"]
    for d in DIGESTS:
        p = d["pascal"]
        types = summary_csharp_types(d)
        sum_args = ",\n    ".join(f"{t} {f[0].upper()+f[1:]}" for t, f in zip(types, d["summary_fields"]))
        chunks.append(
            f"public sealed record Cp{p}Summary(\n    {sum_args},\n    string Source,\n    string Message);\n\n"
        )
        row_args = ",\n    ".join(f"{csharp_type(t)} {name}" for name, _, t in d["row_ctor"])
        chunks.append(
            f"public sealed record Cp{p}RowDigest(\n    {row_args});\n\n"
        )
        coll = d["collection"][0].upper() + d["collection"][1:]
        chunks.append(
            f"public sealed record Cp{p}DigestResult(\n"
            f"    Cp{p}Summary Summary,\n"
            f"    IReadOnlyList<Cp{p}RowDigest> {coll},\n"
            f"    int Count,\n"
            f"    string Source,\n"
            f"    string Message);\n\n"
        )
    path.write_text(text.rstrip() + "\n" + "".join(chunks), encoding="utf-8")
    print("Models appended")


def append_interface():
    path = ROOT / "aspnet/src/EcomAE.Platform/Migration/ISurfaceDashboardSummaryReporter.cs"
    text = path.read_text(encoding="utf-8")
    if "BuildCpGeoRegionsDigestAsync" in text:
        print("Interface already present")
        return
    lines = []
    for d in DIGESTS:
        p = d["pascal"]
        lines.append(
            f"    Task<Cp{p}DigestResult> BuildCp{p}DigestAsync(int limit, CancellationToken cancellationToken = default);"
        )
    text = text.replace(
        "    Task<CpDataMigrationsDigestResult> BuildCpDataMigrationsDigestAsync(int limit, CancellationToken cancellationToken = default);\n}",
        "    Task<CpDataMigrationsDigestResult> BuildCpDataMigrationsDigestAsync(int limit, CancellationToken cancellationToken = default);\n"
        + "\n".join(lines)
        + "\n}",
    )
    path.write_text(text, encoding="utf-8")
    print("Interface appended")


def gen_reporter_method(d: dict) -> str:
    p = d["pascal"]
    types = summary_csharp_types(d)
    empty_vals = []
    for t in types:
        empty_vals.append('""' if t == "string" else "0")
    empty_ctor = ", ".join(empty_vals)
    coll = d["collection"][0].upper() + d["collection"][1:]
    # locals
    locals_decl = []
    for t, f in zip(types, d["summary_fields"]):
        if t == "string":
            locals_decl.append(f'var {f} = "";')
        else:
            locals_decl.append(f"var {f} = 0;")
    locals_block = " ".join(locals_decl)

    stats_block = ""
    if d.get("resilient_counts") or d.get("status_queries"):
        parts = []
        for field, _sql in d.get("resilient_counts") or []:
            const = f"CountCp{p}{field[0].upper()+field[1:]}"
            parts.append(
                f"            {field} = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.{const}, cancellationToken).ConfigureAwait(false);"
            )
        for field, _sql in d.get("status_queries") or []:
            const = f"SelectCp{p}{field[0].upper()+field[1:]}"
            parts.append(
                f"""            try
            {{
                await using var st = connection.CreateCommand();
                st.CommandText = LegacySurfaceDashboardSql.{const};
                var sv = await st.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                {field} = Convert.ToString(sv is DBNull or null ? string.Empty : sv, CultureInfo.InvariantCulture) ?? string.Empty;
            }}
            catch {{ {field} = ""; }}"""
            )
        stats_block = "\n".join(parts)
    else:
        assigns = []
        for f in d["summary_fields"]:
            snake = camel_to_snake(f)
            assigns.append(
                f'                    {f} = Convert.ToInt32(reader["{snake}"] is DBNull ? 0 : reader["{snake}"], CultureInfo.InvariantCulture);'
            )
        stats_block = f"""            await using (var stats = connection.CreateCommand())
            {{
                stats.CommandText = LegacySurfaceDashboardSql.SelectCp{p}Stats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {{
{chr(10).join(assigns)}
                }}
            }}"""

    row_adds = ",\n                        ".join(
        reader_expr(col, typ) for _, col, typ in d["row_ctor"]
    )
    sum_args = ", ".join(d["summary_fields"])

    return f"""
    public async Task<Cp{p}DigestResult> BuildCp{p}DigestAsync(int limit, CancellationToken cancellationToken = default)
    {{
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new Cp{p}Summary({empty_ctor}, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {{
            return new(empty, [], 0, "migration", empty.Message);
        }}

        try
        {{
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            {locals_block}
{stats_block}

            var rows = new List<Cp{p}RowDigest>();
            try
            {{
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCp{p}Rows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {{
                    rows.Add(new Cp{p}RowDigest(
                        {row_adds}));
                }}
            }}
            catch
            {{
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }}

            var summary = new Cp{p}Summary({sum_args}, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }}
        catch (Exception ex)
        {{
            var err = empty with {{ Source = "database-error", Message = ex.Message }};
            return new(err, [], 0, "database-error", ex.Message);
        }}
    }}
"""


def append_reporter():
    path = ROOT / "aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"
    text = path.read_text(encoding="utf-8")
    if "BuildCpGeoRegionsDigestAsync" in text:
        print("Reporter already present")
        return
    methods = "".join(gen_reporter_method(d) for d in DIGESTS)
    # insert before final closing brace of class
    idx = text.rfind("\n}")
    path.write_text(text[:idx] + methods + text[idx:], encoding="utf-8")
    print("Reporter appended")


def append_routes():
    path = ROOT / "aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"
    text = path.read_text(encoding="utf-8")
    if "ControlPanelGeoRegions" in text:
        print("Routes already present")
        return
    chunk = ["\n    // Wave 22 CMS/platform leftovers\n"]
    for d in DIGESTS:
        p = d["pascal"]
        stem = d["stem"]
        chunk.append(f'    public const string ControlPanel{p} = "/cp/{stem}";\n')
        chunk.append(
            f'    /// <summary>CP {d["title"]} Blazor list (JSON digest remains <see cref="ControlPanel{p}"/>).</summary>\n'
        )
        chunk.append(f'    public const string ControlPanel{p}App = "/cp/{stem}-app";\n')
    text = text.replace(
        '    public const string ControlPanelDataMigrationsApp = "/cp/data-migrations-app";\n',
        '    public const string ControlPanelDataMigrationsApp = "/cp/data-migrations-app";\n' + "".join(chunk),
    )
    path.write_text(text, encoding="utf-8")
    print("Routes appended")


def append_module():
    path = ROOT / "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"
    text = path.read_text(encoding="utf-8")
    if "ControlPanelGeoRegions" in text:
        print("Module already present")
        return
    chunks = []
    for d in DIGESTS:
        p = d["pascal"]
        stem = d["stem"]
        coll = d["collection"]
        coll_prop = coll[0].upper() + coll[1:]
        chunks.append(f"""
        endpoints.MapGet(EcomAeRoutes.ControlPanel{p}, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {{
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
            {{
                return Unauthorized("Admin CP capability required for {stem} digest.");
            }}

            var result = await dashboards.BuildCp{p}DigestAsync(limit ?? 200, cancellationToken);
            return Results.Ok(new
            {{
                ok = true,
                surface = "cp",
                summary = result.Summary,
                {coll} = result.{coll_prop},
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only {d['tables']} KPIs + {coll} ({d['omit']}). PHP {d['title']} remains authoritative."
            }});
        }});
""")
    marker = "        foreach (var route in EcomAeRoutes.ControlPanelAliases)"
    text = text.replace(marker, "".join(chunks) + "\n" + marker)
    path.write_text(text, encoding="utf-8")
    print("Module appended")


def append_contracts():
    path = ROOT / "aspnet/src/EcomAE.Platform/Migration/SurfacePayloadContractCatalog.cs"
    text = path.read_text(encoding="utf-8")
    if "/cp/geo-regions" in text:
        print("Contracts already present")
        return
    contract_chunks = []
    func_chunks = []
    for d in DIGESTS:
        stem = d["stem"]
        fields = d["summary_fields"] + ["source", "message"]
        field_lit = ", ".join(f'"{f}"' for f in fields)
        contract_chunks.append(
            f'        Contract("cp", "/cp/{stem}", "{d["tables"]}", "admin-cp",\n'
            f'            ["ok", "surface", "summary", "{d["collection"]}", "count", "source", "message", "session", "note"],\n'
            f'            [{field_lit}],\n'
            f'            ["{d["title"]} KPIs + {d["collection"]}", "{d["omit"]}", "PHP {d["title"]} remains authoritative"],\n'
            f'            "cp/templates/bootstrap_admin/desktop.php"),\n'
        )
        func_chunks.append(
            f'        new("cp", "{stem} Blazor list", "/cp/{stem}-app", "presentation-shell-scaffolded", '
            f'"Read UI over /cp/{stem} digest; {d["omit"]}; PHP {d["title"]} remains authoritative; tenant chrome stays PHP."),\n'
        )
    text = text.replace(
        '        Contract("cp", "/cp/data-migrations", "epc_data_migrations + epc_data_migration_rows", "admin-cp",\n'
        '            ["ok", "surface", "summary", "migrations", "count", "source", "message", "session", "note"],\n'
        '            ["migrationCount", "completedCount", "failedCount", "rowCount", "source", "message"],\n'
        '            ["Data migration KPIs + migrations", "file_path/column_mapping/validation_errors/options/raw_data/mapped_data omitted", "PHP data migration remains authoritative"],\n'
        '            "cp/templates/bootstrap_admin/desktop.php"),\n',
        '        Contract("cp", "/cp/data-migrations", "epc_data_migrations + epc_data_migration_rows", "admin-cp",\n'
        '            ["ok", "surface", "summary", "migrations", "count", "source", "message", "session", "note"],\n'
        '            ["migrationCount", "completedCount", "failedCount", "rowCount", "source", "message"],\n'
        '            ["Data migration KPIs + migrations", "file_path/column_mapping/validation_errors/options/raw_data/mapped_data omitted", "PHP data migration remains authoritative"],\n'
        '            "cp/templates/bootstrap_admin/desktop.php"),\n'
        + "".join(contract_chunks),
    )
    text = text.replace(
        '        new("cp", "data-migrations Blazor list", "/cp/data-migrations-app", "presentation-shell-scaffolded", "Read UI over /cp/data-migrations digest; file_path/mapping/errors/options/raw payloads omitted; PHP data migration remains authoritative; tenant chrome stays PHP."),\n',
        '        new("cp", "data-migrations Blazor list", "/cp/data-migrations-app", "presentation-shell-scaffolded", "Read UI over /cp/data-migrations digest; file_path/mapping/errors/options/raw payloads omitted; PHP data migration remains authoritative; tenant chrome stays PHP."),\n'
        + "".join(func_chunks),
    )
    path.write_text(text, encoding="utf-8")
    print("Contracts appended")


def append_nav_and_links():
    nav = ROOT / "aspnet/src/EcomAE.Platform/Presentation/LegacyChromeNavCatalog.cs"
    text = nav.read_text(encoding="utf-8")
    if "Geo / regions" in text:
        print("Nav already present")
    else:
        lines = []
        for d in DIGESTS:
            lines.append(f'        new("{d["title"]}", "/cp/{d["stem"]}-app"),\n')
        text = text.replace(
            '        new("Data migrations", "/cp/data-migrations-app"),\n',
            '        new("Data migrations", "/cp/data-migrations-app"),\n' + "".join(lines),
        )
        nav.write_text(text, encoding="utf-8")
        print("Nav appended")

    links = ROOT / "aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs"
    text = links.read_text(encoding="utf-8")
    if "/cp/geo-regions-app" in text:
        print("Links already present")
        return
    prev = []
    dig = []
    for d in DIGESTS:
        stem = d["stem"]
        prev.append(
            f'            Link("aspnet-presentation-preview", "CP {d["title"]} read UI", "https://www.ecomae.com/cp/{stem}-app", "aspnet", "/cp/{stem}-app", "Blazor list over /cp/{stem} digest under PhpCpDesktopChrome. {d["omit"]}. PHP {d["title"]} remains authoritative. Tenant chrome stays PHP (same-to-same)."),\n'
        )
        dig.append(
            f'            Link("aspnet-exact-route-shadow-live", "CP {stem} digest", "https://www.ecomae.com/cp/{stem}", "aspnet", "/cp/{stem}", "Wired exact-route nginx shadow (unauth 401 when installed). Part of surface digests 123/123 batch."),\n'
        )
    text = text.replace(
        '            Link("aspnet-presentation-preview", "CP Data migrations read UI", "https://www.ecomae.com/cp/data-migrations-app", "aspnet", "/cp/data-migrations-app", "Blazor list over /cp/data-migrations digest under PhpCpDesktopChrome. file_path/mapping/errors/options/raw payloads omitted. PHP data migration remains authoritative. Tenant chrome stays PHP (same-to-same)."),\n',
        '            Link("aspnet-presentation-preview", "CP Data migrations read UI", "https://www.ecomae.com/cp/data-migrations-app", "aspnet", "/cp/data-migrations-app", "Blazor list over /cp/data-migrations digest under PhpCpDesktopChrome. file_path/mapping/errors/options/raw payloads omitted. PHP data migration remains authoritative. Tenant chrome stays PHP (same-to-same)."),\n'
        + "".join(prev),
    )
    text = text.replace(
        '            Link("aspnet-exact-route-shadow-live", "CP data-migrations digest", "https://www.ecomae.com/cp/data-migrations", "aspnet", "/cp/data-migrations", "Wired exact-route nginx shadow (unauth 401 when installed). Part of surface digests 110/110 batch."),\n',
        '            Link("aspnet-exact-route-shadow-live", "CP data-migrations digest", "https://www.ecomae.com/cp/data-migrations", "aspnet", "/cp/data-migrations", "Wired exact-route nginx shadow (unauth 401 when installed). Part of surface digests 123/123 batch."),\n'
        + "".join(dig),
    )
    text = text.replace(
        "Surface digests: wired 110 (live until operator installs wave-21a portal-settings/data-migrations digest shadows).",
        "Surface digests: wired 123 (live until operator installs wave-22 CMS/platform leftover digest shadows).",
    )
    # also bump older 110 references in portal/data links
    text = text.replace("Part of surface digests 110/110 batch.", "Part of surface digests 123/123 batch.")
    links.write_text(text, encoding="utf-8")
    print("Links appended")


def write_blazor_pages():
    for d in DIGESTS:
        stem = d["stem"]
        p = d["pascal"]
        path = ROOT / f"aspnet/src/EcomAE.Platform/Components/Pages/Cp{p}App.razor"
        if path.exists():
            continue
        kpi_cells = []
        for i, (field, label) in enumerate(zip(d["summary_fields"], d["kpi_labels"])):
            prop = field[0].upper() + field[1:]
            typ = summary_csharp_types(d)[i]
            if typ == "string":
                kpi_cells.append(
                    f'        <div class="epc-w22-kpi"><strong>@(!string.IsNullOrWhiteSpace(_summary.{prop}) ? _summary.{prop} : "—")</strong><span>{label}</span></div>'
                )
            else:
                kpi_cells.append(
                    f'        <div class="epc-w22-kpi"><strong>@_summary.{prop}.ToString(CultureInfo.InvariantCulture)</strong><span>{label}</span></div>'
                )
        ths = "".join(f"<th>{c[0]}</th>\n                    " for c in d["cols"]) + "<th></th>"
        tds = []
        for label, prop in d["cols"]:
            # find type
            typ = "String"
            for name, _, t in d["row_ctor"]:
                if name == prop:
                    typ = t
                    break
            if typ == "String":
                tds.append(f'<td>@(!string.IsNullOrWhiteSpace(row.{prop}) ? row.{prop} : "—")</td>')
            else:
                tds.append(f'<td>@row.{prop}.ToString(CultureInfo.InvariantCulture)</td>')
        tds.append('<td><a href="@_phpTab">Open in PHP</a></td>')
        colspan = len(d["cols"]) + 1
        empty_sum = ", ".join('""' if t == "string" else "0" for t in summary_csharp_types(d))
        content = f"""@page "/cp/{stem}-app"
@layout Layout.PhpChromeLayout
@using System.Globalization
@using EcomAE.Platform.Auth
@using EcomAE.Platform.Migration
@using EcomAE.Platform.Presentation
@inject ISurfaceDashboardSummaryReporter Dashboards
@inject ILegacySessionValidator Sessions
@inject IHttpContextAccessor Http
@inject NavigationManager Nav

<PageTitle>{d["title"]} · Control Panel · eParts Cart</PageTitle>
<PhpChromeStyles Surface="cp" />

<PhpCpDesktopChrome IsAdmin="_isAdmin">
    <style>
        .epc-w22-hero {{
            margin-bottom:1rem; padding:1.1rem 1.2rem; border-radius:.7rem; color:#fff;
            background:linear-gradient(135deg,#0f172a 0%,#1e3a5f 45%,#0ea5e9 100%);
        }}
        .epc-w22-hero h1 {{ margin:.25rem 0 .35rem; font-size:clamp(1.35rem,2.8vw,1.85rem); }}
        .epc-w22-hero p {{ margin:0; color:rgba(255,255,255,.88); max-width:44rem; font-size:.92rem; line-height:1.45; }}
        .epc-w22-cta {{ display:flex; gap:.45rem; flex-wrap:wrap; margin-top:.85rem; }}
        .epc-w22-cta a {{
            display:inline-flex; padding:.5rem .8rem; border-radius:.4rem; font-weight:700; font-size:.82rem;
            text-decoration:none; color:#0f172a; background:#fff;
        }}
        .epc-w22-cta a.ghost {{ background:transparent; border:1px solid rgba(255,255,255,.35); color:#fff; }}
        .epc-w22-kpis {{ display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:.65rem; margin-bottom:1rem; }}
        .epc-w22-kpi {{ background:#fff; border:1px solid #e2e8f0; border-radius:.55rem; padding:.85rem .9rem; }}
        .epc-w22-kpi strong {{ display:block; font-size:1.2rem; word-break:break-word; }}
        .epc-w22-kpi span {{ color:#64748b; font-size:.78rem; }}
        .epc-w22-table-wrap {{ background:#fff; border:1px solid #e2e8f0; border-radius:.55rem; overflow:auto; }}
        .epc-w22-table {{ width:100%; border-collapse:collapse; font-size:.86rem; }}
        .epc-w22-table th, .epc-w22-table td {{ padding:.55rem .7rem; border-bottom:1px solid #f1f5f9; text-align:left; white-space:nowrap; }}
        .epc-w22-table th {{ background:#f8fafc; color:#475569; font-size:.75rem; text-transform:uppercase; letter-spacing:.03em; }}
        .epc-w22-note {{ margin-top:1rem; padding:.75rem .9rem; background:#f8fafc; border:1px solid #cbd5e1; border-radius:.45rem; color:#334155; font-size:.86rem; }}
        @@media (max-width:900px){{ .epc-w22-kpis{{grid-template-columns:1fr 1fr;}} }}
    </style>

    <section class="epc-w22-hero">
        <img src="@LegacyPresentationAssets.BrandMarkUrl" alt="ECOM AE" style="height:28px;background:#fff;border-radius:4px;padding:2px" />
        <h1>{d["title"]}</h1>
        <p>{d["hero"]}</p>
        <div class="epc-w22-cta">
            <a href="@_phpTab">Open PHP</a>
            <a class="ghost" href="@PhpModuleCatalog.HybridWorkspaceHref("/cp/app", _phpTab)">Hybrid workspace</a>
            <a class="ghost" href="/cp/app">Command centre</a>
        </div>
    </section>

    <div class="epc-w22-kpis" aria-label="{d["title"]} stats">
{chr(10).join(kpi_cells)}
    </div>

    <div class="epc-w22-table-wrap">
    <table class="epc-w22-table">
        <thead>
            <tr>
                    {ths}
            </tr>
        </thead>
        <tbody>
            @if (_rows.Count == 0)
            {{
                <tr><td colspan="{colspan}">No rows in digest (@_source). @_message</td></tr>
            }}
            else
            {{
                @foreach (var row in _rows)
                {{
                    <tr>
                            {chr(10).join("                            "+t for t in tds)}
                    </tr>
                }}
            }}
        </tbody>
    </table>
    </div>

    <div class="epc-w22-note">
        Read-only preview · JSON digest <a href="/cp/{stem}?limit=200">/cp/{stem}</a>. PHP {d["title"]} remains authoritative · Not a broad /cp cutover · tenant product chrome stays PHP.
        Source: <code>@_source</code>. Tables: <code>{d["tables"]}</code>. {d["omit"]}.
    </div>
</PhpCpDesktopChrome>

@code {{
    private const string _phpTab = "{d["php"]}";
    private bool _isAdmin;
    private Cp{p}Summary _summary = new({empty_sum}, "n/a", "");
    private IReadOnlyList<Cp{p}RowDigest> _rows = [];
    private string _source = "n/a";
    private string _message = "";

    protected override async Task OnInitializedAsync()
    {{
        var ctx = Http.HttpContext;
        if (ctx is null) return;

        var session = await Sessions.ValidateAsync(ctx, ctx.RequestAborted);
        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("cp"))
        {{
            Nav.NavigateTo("/cp/login", forceLoad: true);
            return;
        }}

        _isAdmin = true;
        var result = await Dashboards.BuildCp{p}DigestAsync(200, ctx.RequestAborted);
        _summary = result.Summary;
        _rows = result.{d["collection"][0].upper()+d["collection"][1:]};
        _source = result.Source;
        _message = result.Message;
    }}
}}
"""
        path.write_text(content, encoding="utf-8")
    print("Blazor pages written")


def patch_scripts_and_deploy():
    n = len(DIGESTS)
    # hybrid TARGETS
    hybrid = ROOT / "scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh"
    text = hybrid.read_text(encoding="utf-8")
    if "cp-geo-regions" not in text:
        lines = []
        for d in DIGESTS:
            lines.append(
                f'    ("cp-{d["stem"]}", "cp", "/cp/{d["stem"]}-app", "/cp/{d["stem"]}", "{d["php"]}", "Cp{d["pascal"]}App", "PhpCpDesktopChrome", "admin"),\n'
            )
        text = text.replace(
            '    ("cp-data-migrations", "cp", "/cp/data-migrations-app", "/cp/data-migrations", "/CP/shop/finance/erp?area=setup&tab=data_import&epc_erp_shell=1", "CpDataMigrationsApp", "PhpCpDesktopChrome", "admin"),\n',
            '    ("cp-data-migrations", "cp", "/cp/data-migrations-app", "/cp/data-migrations", "/CP/shop/finance/erp?area=setup&tab=data_import&epc_erp_shell=1", "CpDataMigrationsApp", "PhpCpDesktopChrome", "admin"),\n'
            + "".join(lines),
        )
        hybrid.write_text(text, encoding="utf-8")

    # digest dual samples
    dig = ROOT / "scripts/cloudpanel_capture_digest_dual_samples.sh"
    text = dig.read_text(encoding="utf-8")
    if "cp-geo-regions" not in text:
        lines = []
        for d in DIGESTS:
            lines.append(f'  [cp-{d["stem"]}]="/cp/{d["stem"]}?limit=5"\n')
        text = text.replace(
            '  [cp-data-migrations]="/cp/data-migrations?limit=5"\n',
            '  [cp-data-migrations]="/cp/data-migrations?limit=5"\n' + "".join(lines),
        )
        dig.write_text(text, encoding="utf-8")

    # compare_digest_dual_samples.py — summary map + list map
    cmp_path = ROOT / "scripts/compare_digest_dual_samples.py"
    text = cmp_path.read_text(encoding="utf-8")
    if "cp-geo-regions" not in text:
        sum_lines = []
        list_lines = []
        for d in DIGESTS:
            fields = ",".join(d["summary_fields"] + ["source", "message"])
            sum_lines.append(
                f'    "cp-{d["stem"]}": (\n'
                f'        "summary",\n'
                f'        "{fields}",\n'
                f'    ),\n'
            )
            row_fields = [name[0].lower() + name[1:] for name, _, _ in d["row_ctor"]]
            # camelCase json: Id->id already lower first
            json_fields = []
            for name, _, _ in d["row_ctor"]:
                json_fields.append(name[0].lower() + name[1:])
            list_lines.append(
                f'    "cp-{d["stem"]}": (\n'
                f'        "{d["collection"]}",\n'
                f'        {json.dumps(json_fields)},\n'
                f'    ),\n'
            )
        text = text.replace(
            '    "cp-data-migrations": (\n'
            '        "summary",\n'
            '        "migrationCount,completedCount,failedCount,rowCount,source,message",\n'
            '    ),\n',
            '    "cp-data-migrations": (\n'
            '        "summary",\n'
            '        "migrationCount,completedCount,failedCount,rowCount,source,message",\n'
            '    ),\n' + "".join(sum_lines),
        )
        text = text.replace(
            '    "cp-data-migrations": (\n'
            '        "migrations",\n'
            '        ["id", "companyId", "migrationType", "entityType", "fileName", "totalRows", "validRows", "errorRows", "importedRows", "status", "importedByName", "timeCreated", "timeCompleted"],\n'
            '    ),\n',
            '    "cp-data-migrations": (\n'
            '        "migrations",\n'
            '        ["id", "companyId", "migrationType", "entityType", "fileName", "totalRows", "validRows", "errorRows", "importedRows", "status", "importedByName", "timeCreated", "timeCompleted"],\n'
            '    ),\n' + "".join(list_lines),
        )
        cmp_path.write_text(text, encoding="utf-8")

    # matchers
    cap = ROOT / "scripts/cloudpanel_capture_module_function_parity.sh"
    text = cap.read_text(encoding="utf-8")
    if '"geo-regions": "cp-geo-regions"' not in text:
        cp_lines = [
            "\n    # Wave 22 — CMS/platform leftovers + config alias\n",
            '    "configuration": "cp-config-items",\n',
        ]
        for d in DIGESTS:
            for mid in d["matcher_cp"]:
                cp_lines.append(f'    "{mid}": "cp-{d["stem"]}",\n')
        text = text.replace(
            '    "data-migration": "cp-data-migrations",\n'
            '    "erp-guide": "erp-dashboard-summary",\n'
            "}\n",
            '    "data-migration": "cp-data-migrations",\n'
            '    "erp-guide": "erp-dashboard-summary",\n'
            + "".join(cp_lines)
            + "}\n",
        )
        bos_lines = []
        for d in DIGESTS:
            for mid in d["matcher_bos"]:
                bos_lines.append(f'    "{mid}": "cp-{d["stem"]}",\n')
        if bos_lines:
            text = text.replace(
                '    "portal_settings": "cp-portal-settings",\n'
                "}\n",
                '    "portal_settings": "cp-portal-settings",\n'
                + "".join(bos_lines)
                + "}\n",
            )
        cap.write_text(text, encoding="utf-8")

    # nginx digests
    ngx = ROOT / "deploy/aspnet/nginx-surface-digests-shadow-example.conf"
    text = ngx.read_text(encoding="utf-8")
    if "location = /cp/geo-regions" not in text:
        blocks = []
        for d in DIGESTS:
            stem = d["stem"]
            blocks.append(
                f"location = /cp/{stem} {{\n"
                f"    proxy_pass http://127.0.0.1:5100;\n"
                f"    proxy_set_header Host $host;\n"
                f"    proxy_set_header X-Real-IP $remote_addr;\n"
                f"    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;\n"
                f"    proxy_set_header X-EcomAE-Route-Cutover cp-{stem}-shadow-approved;\n"
                f"}}\n"
            )
        text = text.rstrip() + "\n" + "".join(blocks)
        ngx.write_text(text, encoding="utf-8")

    # nginx presentation apps
    ngx_app = ROOT / "deploy/aspnet/nginx-presentation-app-shadow-example.conf"
    text = ngx_app.read_text(encoding="utf-8")
    if "location = /cp/geo-regions-app" not in text:
        blocks = []
        for d in DIGESTS:
            stem = d["stem"]
            blocks.append(
                f"location = /cp/{stem}-app {{\n"
                f"    proxy_pass http://127.0.0.1:5100;\n"
                f"    proxy_set_header Host $host;\n"
                f"    proxy_set_header X-Real-IP $remote_addr;\n"
                f"    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;\n"
                f"    proxy_set_header X-EcomAE-Route-Cutover cp-{stem}-app-read-ui-preview;\n"
                f"}}\n"
            )
        text = text.rstrip() + "\n" + "".join(blocks)
        ngx_app.write_text(text, encoding="utf-8")

    # bump expected counts
    for path, old, new in [
        (ROOT / "scripts/cloudpanel_install_surface_digest_shadows.sh", "expected 110", "expected 123"),
        (ROOT / "scripts/cloudpanel_install_presentation_app_shadows.sh", "expected = 127", "expected = 140"),
        (ROOT / "scripts/generate_all_yarp_design_examples.sh", '"deploy/aspnet/yarp-exact-routes-example.json": 127', '"deploy/aspnet/yarp-exact-routes-example.json": 140'),
        (ROOT / "scripts/generate_all_yarp_design_examples.sh", '"deploy/aspnet/yarp-surface-digests-example.json": 110', '"deploy/aspnet/yarp-surface-digests-example.json": 123'),
    ]:
        t = path.read_text(encoding="utf-8")
        if old in t:
            path.write_text(t.replace(old, new), encoding="utf-8")

    # tests
    tests = ROOT / "aspnet/tests/EcomAE.Platform.Tests/LiveSurfaceLinkReporterTests.cs"
    t = tests.read_text(encoding="utf-8")
    t = t.replace("Assert.Equal(110, report.Links.Count(link =>", "Assert.Equal(123, report.Links.Count(link =>")
    t = t.replace("Assert.Equal(122, report.Links.Count(link => link.HostClass == \"aspnet-presentation-preview\"));",
                  "Assert.Equal(135, report.Links.Count(link => link.HostClass == \"aspnet-presentation-preview\"));")
    if "/cp/geo-regions-app" not in t:
        asserts = []
        for d in DIGESTS[:4]:
            asserts.append(
                f'        Assert.Contains(report.Links, link =>\n'
                f'            link.HostClass == "aspnet-presentation-preview"\n'
                f'            && link.AspNetRouteHint == "/cp/{d["stem"]}-app");\n'
            )
            asserts.append(
                f'        Assert.Contains(report.Links, link =>\n'
                f'            link.HostClass == "aspnet-exact-route-shadow-live"\n'
                f'            && link.AspNetRouteHint == "/cp/{d["stem"]}");\n'
            )
        t = t.replace(
            '        Assert.Contains(report.Links, link =>\n'
            '            link.HostClass == "aspnet-exact-route-shadow-live"\n'
            '            && link.AspNetRouteHint == "/cp/jewellery-fixing");\n',
            '        Assert.Contains(report.Links, link =>\n'
            '            link.HostClass == "aspnet-exact-route-shadow-live"\n'
            '            && link.AspNetRouteHint == "/cp/jewellery-fixing");\n'
            + "".join(asserts),
        )
    tests.write_text(t, encoding="utf-8")

    nav_tests = ROOT / "aspnet/tests/EcomAE.Platform.Tests/LegacyChromeNavCatalogTests.cs"
    t = nav_tests.read_text(encoding="utf-8")
    if "Geo / regions" not in t:
        t = t.replace(
            '        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Portal settings" && item.Href == "/cp/portal-settings-app");\n',
            '        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Portal settings" && item.Href == "/cp/portal-settings-app");\n'
            '        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Geo / regions" && item.Href == "/cp/geo-regions-app");\n'
            '        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Languages" && item.Href == "/cp/languages-app");\n',
        )
        nav_tests.write_text(t, encoding="utf-8")

    # contract validator payloads
    val = ROOT / "aspnet/tests/EcomAE.Platform.Tests/SurfaceDigestContractValidatorTests.cs"
    t = val.read_text(encoding="utf-8")
    if "/cp/geo-regions" not in t:
        lines = []
        for d in DIGESTS:
            p = d["pascal"]
            coll = d["collection"]
            coll_prop = coll[0].upper() + coll[1:]
            lines.append(
                f'            ["/cp/{d["stem"]}"] = new {{ ok = true, surface = "cp", summary = (await reporter.BuildCp{p}DigestAsync(10)).Summary, {coll} = (await reporter.BuildCp{p}DigestAsync(10)).{coll_prop}, count = 0, source = "migration", message = "x", session, note = "contract validation" }},\n'
            )
        t = t.replace(
            '            ["/cp/data-migrations"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpDataMigrationsDigestAsync(10)).Summary, migrations = (await reporter.BuildCpDataMigrationsDigestAsync(10)).Migrations, count = 0, source = "migration", message = "x", session, note = "contract validation" },\n',
            '            ["/cp/data-migrations"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpDataMigrationsDigestAsync(10)).Summary, migrations = (await reporter.BuildCpDataMigrationsDigestAsync(10)).Migrations, count = 0, source = "migration", message = "x", session, note = "contract validation" },\n'
            + "".join(lines),
        )
        val.write_text(t, encoding="utf-8")

    print("Scripts/deploy/tests patched")


def write_stubs_and_goldens():
    # hybrid stubs
    out = ROOT / "docs/migration/evidence/hybrid-ui-dual-samples"
    for d in DIGESTS:
        stem = f"cp-{d['stem']}"
        path = out / f"aspnet-{stem}-hybrid-ui.json"
        if path.exists():
            continue
        doc = {
            "role": "aspnet-hybrid-ui-sample",
            "stem": stem,
            "surface": "cp",
            "appRoute": f"/cp/{d['stem']}-app",
            "digestRoute": f"/cp/{d['stem']}",
            "phpAuthoritativePath": d["php"],
            "blazorMarker": f"Cp{d['pascal']}App",
            "chromeShell": "PhpCpDesktopChrome",
            "authKind": "admin",
            "httpStatus": None,
            "markersFound": [],
            "phpDeeplinkFound": False,
            "phpAuthoritative": True,
            "wwwPreviewOnly": True,
            "tenantChromePhp": True,
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "capturedAt": NOW,
            "baseUrl": "http://127.0.0.1:5100",
            "publicBaseUrl": "https://www.ecomae.com",
            "note": "Contract stub. Re-run on CloudPanel with cookies + ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES=1.",
        }
        path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")

    # golden samples via generate script patch
    gen = ROOT / "scripts/generate_migration_digest_contract_samples.py"
    text = gen.read_text(encoding="utf-8")
    if "cp-geo-regions.json" not in text:
        chunks = []
        for d in DIGESTS:
            # build summary dict with 1s
            types = summary_csharp_types(d)
            summ = {}
            for t, f in zip(types, d["summary_fields"]):
                summ[f] = "" if t == "string" else 1
            # sentinel row
            row = {}
            for name, _, typ in d["row_ctor"]:
                key = name[0].lower() + name[1:]
                if typ == "String":
                    row[key] = "migration"
                elif typ == "Decimal":
                    row[key] = 0.0
                else:
                    row[key] = 1
            chunks.append(
                f'        "cp-{d["stem"]}.json": {{\n'
                f'            **summary("cp", {json.dumps(summ)}),\n'
                f'            "{d["collection"]}": [{json.dumps(row)}],\n'
                f'            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",\n'
                f'            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,\n'
                f'            "note": "migration-mode; {d["collection"]}[] sentinel; {d["omit"]}; PHP {d["title"]} remains authoritative; cutoverAllowed=false",\n'
                f'        }},\n'
            )
        text = text.replace(
            '        "cp-data-migrations.json": {\n',
            "".join(chunks) + '        "cp-data-migrations.json": {\n',
        )
        gen.write_text(text, encoding="utf-8")
    print("Stubs/goldens prepared")


def main():
    append_sql()
    # fix SQL if double-append issue — re-read
    append_models()
    append_interface()
    append_reporter()
    append_routes()
    append_module()
    append_contracts()
    append_nav_and_links()
    write_blazor_pages()
    patch_scripts_and_deploy()
    write_stubs_and_goldens()
    print("Wave 22 apply complete:", len(DIGESTS), "digests")


if __name__ == "__main__":
    main()
