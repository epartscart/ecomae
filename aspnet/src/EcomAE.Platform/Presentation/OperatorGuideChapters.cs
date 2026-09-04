namespace EcomAE.Platform.Presentation;

/// <summary>PHP-extracted chapter bodies for <see cref="OperatorGuidesCatalog"/>.</summary>
internal static class OperatorGuideChapters
{
    internal static IReadOnlyList<OperatorGuidesCatalog.Guide> AllGuides() =>
    [
        new(
            "cp-guideline",
            "cp",
            "shop",
            "Control Panel guideline",
            "fa-book",
            "/CP/control/cp-guideline",
            "/cp/guides-app?g=cp-guideline",
            "A simple map of the left sidebar, daily workflows, and Settings. Open any card to jump straight into the task.",
            [
            OperatorGuidesCatalog.Ch("Roles",
                "Tenant CP vs Super CP, and the four sidebar groups.",
                [
                    "epartscart.com — spare parts tenant CP.",
                    "ecomae.com/cp — platform Super CP.",
                    "Sidebar groups: SYSTEM · SHOP · CATALOG · USERS.",
                    "Product brochure is a separate public brochure, not this guideline."
                ]),
            OperatorGuidesCatalog.Ch("Quick start cards",
                "PHP quick-jump cards at the top of the guideline.",
                [
                    "Settings — jump-nav groups plus Frontend / Backend impact chips for storefront values.",
                    "Price profiles — Retail / Wholesale margins, guest %, brand and article rules.",
                    "Price upload — import supplier CSV / multi-vendor Excel into warehouses.",
                    "OMS daily — queue → items → pay → docs → status → messages → done.",
                    "Fulfilment — checkout → e-mails → supplier LPO → staff processing.",
                    "WhatsApp — quotes, cart share, staff order and LPO messages (EN + AR).",
                    "Customers — directory, orders, invoices, advances, returns.",
                    "Trade approvals — approve Retail / Wholesale registrations and currency."
                ]),
            OperatorGuidesCatalog.Ch("Daily workflow 1 — Prices & stock",
                "Follow the arrows left → right. Each step links into the live CP page.",
                [
                    "Step 1 — Upload list (Price upload guide).",
                    "Step 2 — Verify rows (Prices edit preview).",
                    "Step 3 — Set margins (Price management).",
                    "Step 4 — Assign profile (Retail / Wholesale / guest)."
                ]),
            OperatorGuidesCatalog.Ch("Daily workflow 2 — Orders & fulfilment",
                "Shop flow from trade approval to LPO share.",
                [
                    "Step 1 — Approve trade (Customer approvals).",
                    "Step 2 — Customer orders (Search → cart → checkout).",
                    "Step 3 — Process in OMS (Orders console).",
                    "Step 4 — Share / LPO (WhatsApp guide)."
                ]),
            OperatorGuidesCatalog.Ch("Daily workflow 3 — Customers & profiles",
                "Users group — find, approve, assign margin.",
                [
                    "Step 1 — Find customer (Customer management).",
                    "Step 2 — Approve account (Trade approvals).",
                    "Step 3 — Assign margin (Price profiles).",
                    "Step 4 — Storefront prices — customer sees their profile %."
                ]),
            OperatorGuidesCatalog.Ch("Daily workflow 4 — AI Parts Expert",
                "Shop AI agent.",
                [
                    "Step 1 — Enable agent (Settings → agent toggle).",
                    "Step 2 — Customers chat on the storefront AI widget.",
                    "Step 3 — Review chats (AI agent chats)."
                ]),
            OperatorGuidesCatalog.Ch("Daily workflow 5 — System & notifications",
                "SYSTEM group.",
                [
                    "Step 1 — Site Settings (contacts, SMTP, shop).",
                    "Step 2 — Templates (Notification settings).",
                    "Step 3 — Send a test (Communications test)."
                ]),
            OperatorGuidesCatalog.Ch("Settings — Frontend vs Backend",
                "Open Settings. Blue chips mark values that affect the public storefront.",
                [
                    "Use the left jump nav to open a group. Read the blue “Effect on frontend” line before you save.",
                    "Frontend — customers see it. Backend — CP / integrations only.",
                    "Contacts & footer — phone, WhatsApp, offices appear on the public site.",
                    "Online store — currency, rounding, guest checkout, partial payment.",
                    "Article search — results table, filters, async search layout.",
                    "Refunds — customer return requests and withholding text.",
                    "E-mail / updates mailbox — mostly backend; customers only feel SMTP “from” name."
                ]),
            OperatorGuidesCatalog.Ch("Menu map",
                "Every item from the left sidebar, grouped, with a short “what it does”.",
                [
                    "PHP loads control_groups / control_items and shows a hint per URL.",
                    "Hints cover price-management, prices/guide, OMS, fulfilment, WhatsApp, logistics, ERP, document control, and Settings.",
                    "Open depends on admin group. Access-denied items stay hidden unless show_anyway=1.",
                    "Live menu cards need the tenant shop database — without it the map stays empty here."
                ], "PHP page hints are the source of truth for each sidebar URL.")
            ],
            ["/cp/guides-app?g=prices-upload", "/cp/guides-app?g=oms-daily", "/cp/config-items-app"])
,
        new(
            "prices-upload",
            "cp",
            "shop",
            "Price upload — documentation & system status",
            "fa-upload",
            "/CP/shop/prices/guide",
            "/cp/guides-app?g=prices-upload",
            "All ways prices enter the system: CP wizard, pyprices PC, FTP, e-mail (one list per message), URL, cron, multi-vendor, Deploy API, Treelax API, manual grid, and price review.",
            [
            OperatorGuidesCatalog.Ch("Open this guide while logged into the control panel",
                "A public link without a session shows the CP login screen (that is normal).",
                [
                    "Primary URL: /cp/shop/prices/guide (PHP /CP/shop/prices/guide).",
                    "Alternate: /CP/shop/prices?view=guide.",
                    "Back to price lists: /cp/prices-upload-app (PHP /CP/shop/prices)."
                ]),
            OperatorGuidesCatalog.Ch("System health / live snapshot",
                "PHP loads shop_docpart_prices, upload history, cron tasks, and pyprices pending tasks. Health checks that call pyprices/cron over HTTP run via AJAX only — not on initial page load.",
                [
                    "Load modes: 1 Manual, 2 FTP, 3 E-mail, 4 URL.",
                    "Price lists by update source show count and records per load_mode.",
                    "Recent uploads by channel: upload_source grouped from epc_price_upload_history.",
                    "Live numbers need the tenant shop database. Without it, tables stay empty."
                ]),
            OperatorGuidesCatalog.Ch("E-mail imports: one price list per message (not “all lists”)",
                "A message to the price mailbox does not update every price list.",
                [
                    "Each list in E-mail mode has its own rules. Pyprices scans the inbox and, for each configured list, looks for an unread message that matches that list’s sender, subject (if set), and attachment file name.",
                    "Only the matching list is updated; other lists are unchanged.",
                    "Site mailbox (IMAP) comes from config.php: prices_email_username / server / port.",
                    "Supplier checklist: use the mailbox (or an address allowed by sender filter).",
                    "Attach one file per list; the file name must include the substring (e.g. S-UAE.xlsx → only list S-UAE).",
                    "Subject line only matters if “Subject must contain” is set; words like “price list” alone do not route mail to all lists.",
                    "After sending, click the E-mail icon on that row (manual) or wait for cron.",
                    "Confirm Upload history on the row shows a new file with Download.",
                    "Self-sent test mail (same From and To) is often ignored by pyprices. Prefer an external supplier address, or use the deploy API / CP upload for testing."
                ]),
            OperatorGuidesCatalog.Ch("Upload history (download area)",
                "Every bulk import should archive the source file under /content/files/price_upload_history/{price_id}/.",
                [
                    "Price lists → column Update file / history → green download (active/last file) or Upload history modal.",
                    "Active file = latest successful upload for that list.",
                    "If the archived source is missing, download falls back to Export current DB.",
                    "History codes: cp_wizard, pyprices_upload, pyprices_ftp, pyprices_email, pyprices_url, deploy_api."
                ]),
            OperatorGuidesCatalog.Ch("1. CP upload wizard (file from PC — CSV / Excel / archive)",
                "Engine: PHP steps ajax_1 → ajax_6. History: cp_wizard.",
                [
                    "Create or edit the price list — set column numbers, separator, rows to skip, file name substring.",
                    "Open Price lists → green Upload on the row, or /shop/prices/upload?price_id=ID.",
                    "Choose file (CSV, TXT, or archive). Optionally enable clean table before import.",
                    "Wizard runs: prepare temp dir → extract archive → convert Excel → normalize CSV → import to DB → enable keys.",
                    "Check row count on the manager page and Upload history (skipped lines CSV if any).",
                    "Test: 5–10 row CSV with known brand/article/price. Confirm records_count increases and history shows cp_wizard.",
                    "Excel is supported via step 3 (ajax_3_excel_convert.php). Prefer UTF-8 CSV for large files."
                ]),
            OperatorGuidesCatalog.Ch("2. Pyprices — upload file from PC (manager row)",
                "Engine: /pyprices/pyprices-api.php + upload_file.php. History: pyprices_upload.",
                [
                    "Ensure pyprices health checks are OK.",
                    "On Price lists, use the file input on the row (load_mode 1).",
                    "File is staged; a pyprices task runs; external_tasks_account.php polls until done.",
                    "Refresh — last_updated and record count should change.",
                    "Test: small CSV matching file_name_substring. Watch pyprices task log on the row."
                ]),
            OperatorGuidesCatalog.Ch("3. Update from FTP",
                "Engine: pyprices FTP. Set load_mode = FTP on the price list.",
                [
                    "Edit price list → FTP host, user, password, folder, file name substring (and archive substring if zipped).",
                    "Place the correct file on the FTP server.",
                    "Manual test: on manager row, click FTP icon in Manual update column.",
                    "For automatic updates: add a schedule (cron) — see section 6.",
                    "If manual FTP works but schedule fails → configure server cron."
                ]),
            OperatorGuidesCatalog.Ch("4. Update from E-mail (one list per matching message)",
                "Engine: pyprices IMAP. Set load_mode = E-mail (3) on each list that should use mail.",
                [
                    "Configure global mailbox in config.php: prices_email_server, port, encryption, username, password (Gmail: App Password, IMAP SSL 993).",
                    "Edit each price list → E-mail block: sender e-mail, optional subject substring, required file name substring.",
                    "Supplier sends one attachment per list; filename must contain that list’s substring.",
                    "Manual test: E-mail icon in Manual update. Scheduled: add list to cron schedule.",
                    "Check Upload history for a downloadable copy of the imported file."
                ]),
            OperatorGuidesCatalog.Ch("5. Update from URL / link",
                "Engine: pyprices URL or wizard download. Set load_mode = URL and fill the link field.",
                [
                    "Edit price list → paste direct file URL in link.",
                    "Manual test: link icon in Manual update, or open upload wizard (can download from link when no file selected).",
                    "Schedule for automatic pulls if needed."
                ]),
            OperatorGuidesCatalog.Ch("6. Scheduled automatic update (cron)",
                "The pyprices module works correctly when manual updates work. Scheduled updates require a cron job every minute on the server.",
                [
                    "On Price lists, create a schedule for a test list (FTP/email/URL as configured).",
                    "Add hosting cron task (every minute) wget to cron_crutch.php?key=tech_key.",
                    "Alternative: server crontab with PHP CLI running cron_task_executor.php.",
                    "Verify: after 1–2 minutes, last_updated should change without a manual click.",
                    "If manual FTP/email/URL works but schedule does not → cron is not running."
                ]),
            OperatorGuidesCatalog.Ch("7. Multi-vendor price upload (many vendors → auto warehouses)",
                "Excel/CSV with Vendor full + Vendor short; data types inventory/sales/purchase; auto warehouse + list per vendor.",
                [
                    "Open Multi-vendor upload (/cp/shop/prices/multivendor) and download the sample CSV.",
                    "Short code appears on the storefront; full name stays in CP only.",
                    "POST /epc-upload-multivendor-prices.php is the deploy API twin.",
                    "Warehouses are created/linked automatically per vendor."
                ]),
            OperatorGuidesCatalog.Ch("8–11. Deploy API, Treelax API, manual grid, price review",
                "Remaining channels from the PHP accordion.",
                [
                    "Deploy API — POST price_file + tech_key to epc-upload-uae-prices.php. History: deploy_api.",
                    "Treelax API — supplier API pull when configured on the list.",
                    "Manual grid — edit rows on Prices edit; not a bulk ingest.",
                    "Price review — browse imported rows, preview site price per customer profile, edit lines."
                ]),
            OperatorGuidesCatalog.Ch("Test environment checklist",
                "PHP checklist at the bottom of the guide.",
                [
                    "At least one list per load_mode you use (Manual / FTP / E-mail / URL).",
                    "file_name_substring set on every list that uses FTP, e-mail, or PC upload.",
                    "IMAP mailbox configured before e-mail imports.",
                    "Cron every minute if any schedule exists.",
                    "Upload history download works after a successful import."
                ])
            ],
            ["/cp/prices-upload-app", "/cp/prices-edit-app", "/cp/price-lists-app"])
,
        new(
            "oms-daily",
            "cp",
            "shop",
            "OMS daily guide",
            "fa-list-alt",
            "/CP/shop/orders/oms-guide",
            "/cp/guides-app?g=oms-daily",
            "One screen for daily order work. Use this checklist area by area — from opening the day to completing and messaging the customer.",
            [
            OperatorGuidesCatalog.Ch("1 Open the day",
                "Start every shift from SHOP → OMS · Orders (not separate “items” or “statuses” pages).",
                [
                    "Go to /cp/orders (PHP /CP/shop/orders/orders).",
                    "Check the KPI cards: Open orders, Today, Pending ship.",
                    "Use the Open tab (default) so unfinished work is listed first. Use Completed only when you need history.",
                    "Sort by last modified if you want the most recently touched orders on top."
                ], "If the list looks empty but KPIs show open orders, click Open again or clear sticky filters in Advanced filter."),
            OperatorGuidesCatalog.Ch("2 Order list & filters",
                "The left (or full-width) table is your work queue.",
                [
                    "Status pills — quick filter (Placed, Executing, Sent, etc.). Completed is its own tab, not mixed into Open.",
                    "Advanced filter — date range, order #, customer, phone, article, paid flag, shop.",
                    "Click a row — opens that order in the OMS console on the right (same page).",
                    "Ctrl+click — classic full order card (rare; prefer OMS).",
                    "Unread mail icon on a row means the customer sent a message — open that order first."
                ]),
            OperatorGuidesCatalog.Ch("3 OMS console (right pane)",
                "This is the daily workspace. You should not need separate “order items” or “order status” menu pages for routine work.",
                [
                    "Header shows order #, status badge, created/modified times, shop, delivery mode.",
                    "Chips show paid state, payment method, item count.",
                    "Totals strip: Amount · Paid · Balance due · Purchase · Benefit.",
                    "Tabs: Manage (1), Items (2), Fulfillment (3), Customer (4), Payment (5), Documents (6), Timeline (7), Messages (8).",
                    "Stay on the same tab after saves — OMS remembers your active tab.",
                    "Queue keys: j/k (or ↑/↓) move between orders without leaving the console."
                ]),
            OperatorGuidesCatalog.Ch("4 Items & stock",
                "Items tab for the selected order.",
                [
                    "Confirm brand, article, qty, sale price, and purchase cost — margin & USD update live as you type.",
                    "Use Save all lines or Ctrl+S (saves focused line, or all when focus is not on a line).",
                    "Use Refresh on a line to pull real purchase cost from warehouse / APAI details.",
                    "Set all line statuses when every line moves together (e.g. all received).",
                    "Open Fulfillment for multi-supplier confirm → pay → ship → warehouse → pack → deliver.",
                    "If the order is unpaid, you can edit lines and add a line; after payment, price edits lock."
                ], "Price lists & warehouses are managed under Price lists — keep stock/prices correct so OMS lines stay accurate."),
            OperatorGuidesCatalog.Ch("5 Payment",
                "Payment tab (shortcut 5) — Amount due includes goods + customer-paid courier.",
                [
                    "Set Courier fee (ex-VAT) and ship country: UAE adds VAT on courier; outside UAE is zero-rated.",
                    "Record payment (cash / card / transfer / wallet) for the balance due.",
                    "Confirm paid badge updates (Paid / Partial / Not paid).",
                    "Do not mark the order Completed until payment rules for your shop are satisfied (usually fully paid).",
                    "Refunds: use refund actions only when reversing a recorded payment."
                ], "KPI Pending ship = paid but not finished — click it to jump straight into that queue."),
            OperatorGuidesCatalog.Ch("6 Documents & print",
                "Documents tab.",
                [
                    "Print or download invoice / packing / tax docs as configured for your tenant.",
                    "Select specific lines when the print dialog asks for order items.",
                    "Share documents with the customer via Messages or WhatsApp (area 8)."
                ]),
            OperatorGuidesCatalog.Ch("7 Status & timeline",
                "Manage tab + Timeline tab.",
                [
                    "On Manage, choose the next order status (e.g. Executing → Sent to client).",
                    "Apply status — the badge and timeline update immediately.",
                    "Use Timeline to see who changed what (staff / robot / customer).",
                    "Add an internal note when something needs a handoff to the next shift."
                ], "Status names are configured once by an admin. Daily staff only apply them from OMS."),
            OperatorGuidesCatalog.Ch("8 Customer messages / WhatsApp",
                "Messages tab (shortcut 8).",
                [
                    "WhatsApp share buttons are at the top of this tab (customer, sales, supplier LPO).",
                    "Use in-app chat for order-wide notes; use the envelope on a line for item-specific messages.",
                    "Reply clearly: ETA, payment request, pickup ready, or delivery update.",
                    "Full WhatsApp templates (EN/AR): WhatsApp guide."
                ]),
            OperatorGuidesCatalog.Ch("9 Complete or cancel",
                "Finish the sale or stop it.",
                [
                    "Complete — when goods are delivered/picked up and payment is settled. Move order to a finished status (Completed tab).",
                    "Cancel — only when the sale will not proceed; confirm stock release and any refund.",
                    "After complete, the order leaves the Open tab — find it under Completed or All."
                ]),
            OperatorGuidesCatalog.Ch("10 Bulk actions (end of list)",
                "Tick several rows in the order list.",
                [
                    "Set status / viewed flag for the selection, or delete only when you are sure.",
                    "Prefer single-order OMS for payment and messages; use bulk for status sweeps."
                ]),
            OperatorGuidesCatalog.Ch("End-of-day checklist",
                "Close the shift.",
                [
                    "Open KPI is zero or every open order has a next action / note.",
                    "Pending ship (paid, not finished) reviewed.",
                    "Customer messages answered.",
                    "Payments recorded for today’s collections.",
                    "Completed today’s delivered/picked-up orders."
                ])
            ],
            ["/cp/orders", "/cp/guides-app?g=fulfilment", "/cp/guides-app?g=whatsapp"])
,
        new(
            "fulfilment",
            "cp",
            "shop",
            "Order fulfilment process",
            "fa-truck",
            "/CP/shop/orders/guide",
            "/cp/guides-app?g=fulfilment",
            "End-to-end: registration → checkout → automatic e-mails → supplier LPO → CP processing → statuses → payment → delivery → API warehouses → troubleshoot.",
            [
            OperatorGuidesCatalog.Ch("End-to-end flow (overview)",
                "PHP overview list.",
                [
                    "Customer registers (Retail or Wholesale) → wholesale may need CP approval + fixed dealing currency.",
                    "Shop — search, cart, checkout (blocked if trade approval pending).",
                    "Order created — automatic e-mails: manager, customer, and supplier LPO per warehouse.",
                    "Supplier receives LPO e-mail — LPO number = customer order number (shop_orders.id).",
                    "Staff (CP) — open order, confirm payment, update line statuses, message customer, arrange delivery/pickup.",
                    "Complete — order and line statuses set to finished; customer notified if configured on status change."
                ]),
            OperatorGuidesCatalog.Ch("Live status",
                "PHP snapshot: order_stats, storages LPO-ready, pending trade approvals, recent LPO logs. Needs tenant DB.",
                [
                    "Orders today / last 7 days / total.",
                    "Warehouses with LPO e-mail vs total storages.",
                    "Pending trade approvals waiting on Users → Approvals."
                ]),
            OperatorGuidesCatalog.Ch("Checkout notifications",
                "Three automatic e-mail channels on new order.",
                [
                    "Manager notification — shop admin mailbox.",
                    "Customer confirmation — order summary to the buyer.",
                    "Supplier LPO — one e-mail per warehouse that has LPO e-mail configured."
                ]),
            OperatorGuidesCatalog.Ch("Supplier LPO — warehouse e-mail configuration",
                "LPO number equals the customer order number.",
                [
                    "Set LPO e-mail on each warehouse (Logistics → Storages).",
                    "Lines group by warehouse; each warehouse gets its own LPO mail.",
                    "Recent supplier LPO log entries appear on the PHP guide when the shop DB is present."
                ]),
            OperatorGuidesCatalog.Ch("1. Customer registration & trade approval",
                "Retail / Wholesale.",
                [
                    "Customer registers on the shop.",
                    "B2B / wholesale: approve via Users → Customer approvals and assign currency.",
                    "Checkout is blocked while trade approval is pending."
                ]),
            OperatorGuidesCatalog.Ch("2. Browse, cart & checkout",
                "Storefront path.",
                [
                    "Search parts, add to cart, checkout.",
                    "Guest vs registered checkout follows Settings → Online store.",
                    "Delivery method is chosen here (pickup, local courier, or worldwide carriers)."
                ]),
            OperatorGuidesCatalog.Ch("3. Automatic e-mails on new order (3 channels)",
                "Manager, customer, supplier LPO.",
                [
                    "Confirm SMTP via Communications test before go-live.",
                    "Notification templates live under Notification settings.",
                    "If a channel is missing, check warehouse LPO e-mail and admin notify address."
                ]),
            OperatorGuidesCatalog.Ch("4. Supplier LPO — purchase order to supplier",
                "Per warehouse.",
                [
                    "LPO number = shop_orders.id.",
                    "Supplier receives lines for that warehouse only.",
                    "WhatsApp LPO share is an extra manual channel — it does not replace e-mail LPO."
                ]),
            OperatorGuidesCatalog.Ch("5. CP staff — process the order",
                "OMS daily work.",
                [
                    "Open OMS → click the order.",
                    "Confirm payment, update line statuses, message the customer.",
                    "Arrange delivery or pickup from the obtaining-mode / fulfillment tabs."
                ]),
            OperatorGuidesCatalog.Ch("6. Order statuses & line item statuses",
                "Configured once by admin; daily staff apply them.",
                [
                    "Order-level statuses (Placed, Executing, Sent, Completed, …).",
                    "Line item statuses (first 20 shown on the PHP guide from the tenant DB).",
                    "Do not use a separate “Orders statuses” menu for routine work."
                ]),
            OperatorGuidesCatalog.Ch("7. Payment & customer balance",
                "Paid / partial / not paid.",
                [
                    "Record payment on the OMS Payment tab or Customer account operations.",
                    "Balance due includes goods + customer-paid courier.",
                    "Do not complete until shop payment rules are satisfied."
                ]),
            OperatorGuidesCatalog.Ch("8. Delivery, pickup & obtaining modes",
                "Logistics.",
                [
                    "Pickup points, local courier plugins, and epc_carriers worldwide labels.",
                    "Create a label from the order card when the obtaining mode is carriers.",
                    "See Logistics guide for DHL / FedEx / Aramex / UPS."
                ]),
            OperatorGuidesCatalog.Ch("9. API warehouses (SAO / external stock)",
                "External supplier stock.",
                [
                    "SAO warehouses confirm availability after the order is placed.",
                    "Line statuses update when the API warehouse confirms or rejects.",
                    "Keep price lists current so purchase cost on the line is real."
                ]),
            OperatorGuidesCatalog.Ch("10. Troubleshooting & resend",
                "When a notification is missing.",
                [
                    "Resend manager / customer / LPO from the order card when the tenant supports it.",
                    "Check Communications test lab on the PHP guide (tech staff).",
                    "Site performance lab is a separate probe — not required for daily fulfilment."
                ])
            ],
            ["/cp/orders", "/cp/guides-app?g=oms-daily", "/cp/guides-app?g=logistics"])
,
        new(
            "whatsapp",
            "cp",
            "shop",
            "WhatsApp sharing — guide",
            "fa-whatsapp",
            "/CP/shop/orders/whatsapp-guide",
            "/cp/guides-app?g=whatsapp",
            "Phase 1: wa.me buttons with bilingual English + Arabic prefilled text. Phase 2: Cloud API when credentials are set.",
            [
            OperatorGuidesCatalog.Ch("Phase 1 is live",
                "Share buttons open WhatsApp (wa.me) with bilingual EN + AR prefilled text. No WhatsApp Business API required for Phase 1 — staff send messages manually from phone or desktop WhatsApp.",
                [
                    "Default sales number is used by storefront header, parts search, cart, and Share with sales.",
                    "Change it in Configuration → epc_whatsapp_number (Frontend WhatsApp number)."
                ]),
            OperatorGuidesCatalog.Ch("Who shares what",
                "Actor / where / recipient / message.",
                [
                    "Customer — part search, cart, site header → Sales WhatsApp → quote request or cart summary.",
                    "Staff — CP open order → WhatsApp share panel → customer phone → order summary + line items.",
                    "Staff — same panel → Share with sales → Sales WhatsApp → order summary + CP link.",
                    "Staff — same panel → Supplier LPO buttons → supplier contact_phone (or sales if missing) → LPO text grouped by warehouse."
                ]),
            OperatorGuidesCatalog.Ch("Staff workflow (order card)",
                "OMS / order card.",
                [
                    "Open Orders → click an order.",
                    "Scroll to the green WhatsApp share panel (Messages tab on OMS).",
                    "Message customer — only shown when the order or user profile has a phone number.",
                    "Share with sales — internal handoff with order lines and CP URL.",
                    "LPO: [warehouse] — one button per warehouse; uses supplier phone from Warehouses / ERP suppliers when set."
                ]),
            OperatorGuidesCatalog.Ch("Storefront (customer → sales)",
                "Public site.",
                [
                    "Header — WhatsApp chat opens sales line (no prefilled text).",
                    "Part search — green WhatsApp button on each product row → quote request for that part.",
                    "Cart — Share cart on WhatsApp sends up to 15 lines + estimated total."
                ]),
            OperatorGuidesCatalog.Ch("Language",
                "All prefilled share text is bilingual EN then AR in one message (customer can reply in either language).",
                [
                    "Do not split EN and AR into two buttons — PHP builds one bilingual body."
                ]),
            OperatorGuidesCatalog.Ch("Supplier phone for LPO shares",
                "ERP supplier linked to warehouse.",
                [
                    "ERP → link supplier to warehouse (epc_erp_suppliers.storage_id).",
                    "Set contact_phone on that supplier (mobile with country code, e.g. 971501234567).",
                    "If empty, the LPO button still works but targets sales so staff can forward manually."
                ]),
            OperatorGuidesCatalog.Ch("Phase 2 — automated notifications (Cloud API)",
                "Installed. When Meta WhatsApp Cloud API credentials are set and epc_whatsapp_api_enabled = 1, order and status e-mails also trigger WhatsApp via send_notify_dispatch.php.",
                [
                    "Meta Business → WhatsApp → API setup → copy phone_number_id and permanent token.",
                    "Configuration → set token, phone_number_id, enable API (1).",
                    "Customer must have a phone on profile (or guest order phone) — same as SMS path.",
                    "Messages use SMS template text when set, else plain text from e-mail body — bilingual EN+AR when enabled.",
                    "Log table: epc_whatsapp_notify_log (success/fail per send)."
                ]),
            OperatorGuidesCatalog.Ch("FAQ",
                "PHP FAQ.",
                [
                    "“Message customer” is missing — add phone on the order (guest checkout) or in the customer profile.",
                    "Wrong number opens — update epc_whatsapp_number; hard-refresh the storefront.",
                    "CP page shows login — log in first; direct URLs without session show the login form.",
                    "Does this replace e-mail LPO? No. E-mail LPO from order fulfilment still runs; WhatsApp is an extra manual channel."
                ])
            ],
            ["/cp/orders", "/cp/guides-app?g=fulfilment", "/cp/sms-whatsapp-app"]),
        new(
            "logistics",
            "cp",
            "shop",
            "Logistics — step-by-step guide",
            "fa-truck",
            "/CP/shop/logistics/guide",
            "/cp/guides-app?g=logistics",
            "Warehouses, stock, delivery methods, and international carriers (DHL, FedEx, Aramex, UPS) for storefront and imported marketplace orders.",
            [
            OperatorGuidesCatalog.Ch("Delivery & fulfilment for all orders",
                "Logistics covers warehouses, stock, delivery methods, and worldwide carriers for normal storefront orders and imported marketplace orders alike.",
                [
                    "Channels are separate. Amazon/eBay listing sync and order import live under Channels guide — not here.",
                    "Menu: Logistics group in CP sidebar. Obtaining mode: epc_carriers."
                ]),
            OperatorGuidesCatalog.Ch("Live snapshot",
                "PHP dashboard: carrier accounts, shipments shipped/total, shop orders. Needs tenant DB.",
                [
                    "Carrier accounts (DHL, FedEx, Aramex, UPS).",
                    "Shipments shipped / total.",
                    "Shop orders (all sources)."
                ]),
            OperatorGuidesCatalog.Ch("End-to-end flow A — Storefront customer order",
                "Normal checkout.",
                [
                    "Customer adds parts to cart and checks out on the storefront.",
                    "Selects delivery method — pickup, local courier, or worldwide carriers (DHL, FedEx, Aramex, UPS, SMSA, DPD, and more).",
                    "For carriers: enters city, country, address, weight — sees demo or live rates.",
                    "Order appears in CP Orders like any other sale."
                ]),
            OperatorGuidesCatalog.Ch("End-to-end flow B — Create shipping label",
                "CP order card.",
                [
                    "Open a paid order with epc_carriers delivery (or any order needing a label).",
                    "In the obtaining-mode block: pick carrier, weight → Create demo label.",
                    "Tracking and cost save to epc_carrier_shipments; listed on Carriers hub."
                ]),
            OperatorGuidesCatalog.Ch("End-to-end flow C — Warehouses & stock",
                "Logistics hub.",
                [
                    "Logistics hub → warehouses, stock, pickup points.",
                    "Allocate inventory before dispatch; ERP sees fulfilment when the order completes."
                ]),
            OperatorGuidesCatalog.Ch("Step 1 — First-time setup",
                "Registers logistics menu, carriers page, guide, and epc_carriers delivery method.",
                [
                    "Run the logistics setup script on the tenant.",
                    "With sample: append &sample=1."
                ]),
            OperatorGuidesCatalog.Ch("Step 2 — Logistics hub & warehouses",
                "Configure warehouses, pickup points, stock, and local delivery plugins (SDEK, DPD, etc.).",
                [
                    "Open the Logistics hub from the CP sidebar."
                ]),
            OperatorGuidesCatalog.Ch("Step 3 — Enable carrier delivery at checkout",
                "Delivery methods.",
                [
                    "CP → Delivery methods.",
                    "Ensure the Carriers delivery method (epc_carriers) is available — partners are managed under Logistics → Carriers.",
                    "Parameters: Demo mode, Origin city (default Dubai)."
                ]),
            OperatorGuidesCatalog.Ch("Step 4 — Carriers hub",
                "Accounts, shipments, activity log.",
                [
                    "Review carrier accounts, recent shipments, activity log.",
                    "Load sample shipment if tables are empty."
                ]),
            OperatorGuidesCatalog.Ch("Step 5 — Label from order card",
                "Orders → open paid order.",
                [
                    "Carrier block → select DHL/FedEx/Aramex/UPS, weight (kg) → submit.",
                    "Demo tracking format e.g. DHL260518000018123; live APIs when credentials configured."
                ]),
            OperatorGuidesCatalog.Ch("Step 6–7 — Carriers reference & go live",
                "Catalog + live checklist.",
                [
                    "Carrier codes, demo services, and track URLs live in the PHP catalog.",
                    "DHL MyDHL, FedEx Ship, Aramex, UPS OAuth — store credentials in carrier accounts.",
                    "Turn off Demo mode on obtaining mode when live rating/labels work.",
                    "Test one storefront order + one label before production traffic."
                ]),
            OperatorGuidesCatalog.Ch("Database tables & FAQ",
                "PHP reference.",
                [
                    "epc_carrier_accounts — credentials and demo_mode.",
                    "epc_carrier_shipments — labels for any shop order: tracking, cost, status.",
                    "shop_obtaining_modes — delivery methods including epc_carriers.",
                    "shop_orders — website, phone, or imported from channels.",
                    "Does logistics work for normal website orders? Yes.",
                    "Where are Amazon/eBay orders? Import via Channels; after import they are regular shop orders fulfilled here.",
                    "Checkout rates: demo formula or live carrier APIs when configured."
                ])
            ],
            ["/cp/delivery-methods-app", "/cp/carriers-app", "/cp/guides-app?g=channels"]),
        new(
            "payments",
            "cp",
            "shop",
            "Payment gateways — guide",
            "fa-credit-card",
            "/CP/shop/payments/guide",
            "/cp/guides-app?g=payments",
            "GCC, Pakistan, international, and crypto (NOWPayments) gateways. Individual accounts, checkout flow, and going-live checklist.",
            [
            OperatorGuidesCatalog.Ch("Quick start",
                "Demo credentials ship with the hub. Customers pick a method on the order page.",
                [
                    "Open Payment gateways → Seed / refresh gateways.",
                    "Set a default (e.g. Stripe or Telr) and keep Crypto / JazzCash / Tabby enabled for the customer picker.",
                    "Individual accounts: attach merchant keys / connected account ID / payout IBAN to each office or vendor.",
                    "On the storefront order page, choose Pay with → Card / BNPL / JazzCash / Crypto. Funds are attributed to that order’s office/vendor account.",
                    "For crypto live: Configure → Crypto (NOWPayments) → paste API key + IPN secret → turn off Demo mode."
                ]),
            OperatorGuidesCatalog.Ch("Individual accounts (who receives the money)",
                "Direct / Connected / Payout.",
                [
                    "Direct — office/vendor merchant credentials are used for the charge.",
                    "Connected — store connected account ID (e.g. Stripe Connect acct_…).",
                    "Payout — platform collects; settlement ledger shows net due to IBAN for manual/batch payout.",
                    "Multi-vendor orders create settlement rows per vendor storage share."
                ]),
            OperatorGuidesCatalog.Ch("Checkout flow",
                "ajax_create_operation → go_to_pay → IPN.",
                [
                    "Customer selects a payment method and clicks pay.",
                    "ajax_create_operation.php creates a pending shop_users_accounting row (optional pay_handler).",
                    "Browser opens /content/shop/finance/payment_systems/{handler}/go_to_pay.php.",
                    "Webhook / IPN hits notification.php → pay_for_order.php marks the order paid."
                ]),
            OperatorGuidesCatalog.Ch("GCC & MENA",
                "PHP gateway table.",
                [
                    "Telr (telr) — UAE + GCC cards / Apple Pay.",
                    "PayTabs (paytabs) — MENA cards & wallets.",
                    "Tabby (tabby) — BNPL AE/SA/KW/BH.",
                    "Tamara (tamara) — BNPL AE/SA/KW.",
                    "MyFatoorah (myfatoorah) — KNET, MADA, GCC.",
                    "Tap Payments (tap) — GCC cards & wallets.",
                    "HyperPay (hyperpay) — KSA MADA / UAE.",
                    "Checkout.com (checkout_com) — UAE enterprise.",
                    "Network International (network_intl) — N-Genius UAE.",
                    "Amazon Payment Services (amazon_ps) — AE/SA."
                ]),
            OperatorGuidesCatalog.Ch("Pakistan, crypto, international, legacy CIS",
                "Regional handlers.",
                [
                    "JazzCash (jazzcash) — mobile wallet & cards (PKR).",
                    "Easypaisa (easypaisa) — wallet / OTC (PKR).",
                    "Crypto (nowpayments) — demo coin picker or live API key + IPN secret. Coins: USDT TRC20/BEP20, BTC, ETH, LTC.",
                    "International: Stripe, PayPal, Adyen, 2Checkout, Razorpay, Skrill, Payoneer, Authorize.net, CyberSource, CCAvenue.",
                    "Legacy CIS (Tinkoff, YooKassa, Robokassa, etc.) — Legacy tab."
                ]),
            OperatorGuidesCatalog.Ch("Going live checklist",
                "PHP checklist.",
                [
                    "Trade license + merchant account with the acquirer.",
                    "HTTPS on the shop host.",
                    "Paste live keys; disable Demo mode.",
                    "Register webhook / IPN URL.",
                    "For crypto: fund NOWPayments payout wallet and verify IPN secret.",
                    "Run a small live test payment."
                ])
            ],
            ["/cp/payment-gateways-app"]),
        new(
            "channels",
            "cp",
            "shop",
            "Channels — step-by-step guide",
            "fa-plug",
            "/CP/shop/channels/guide",
            "/cp/guides-app?g=channels",
            "Worldwide marketplace channels: Amazon & eBay, noon, Flipkart, Walmart, Mercado Libre. SKU mapping, inventory sync, order import.",
            [
            OperatorGuidesCatalog.Ch("Worldwide marketplace channels",
                "Plug-and-play partners from one hub.",
                [
                    "Logistics is separate. Delivery methods, carriers, warehouses, and fulfilment for all customer orders are in the Logistics guide.",
                    "Menu: Channels group in CP sidebar."
                ]),
            OperatorGuidesCatalog.Ch("Live snapshot",
                "PHP dashboard: marketplace orders, SKU mappings, channel count. Needs tenant DB.",
                [
                    "Marketplace orders (awaiting ship/import).",
                    "SKU mappings (active).",
                    "Marketplace channels / catalog count."
                ]),
            OperatorGuidesCatalog.Ch("Marketplace flow",
                "Map → sync → import → fulfil.",
                [
                    "Map shop SKUs (brand + article) to marketplace SKUs/ASINs.",
                    "Demo sync pushes stock & price to Amazon/eBay (live: SP-API / Sell API).",
                    "Marketplace orders appear in the hub; Demo import links to shop orders.",
                    "After import, fulfil via standard CP Orders + Logistics."
                ]),
            OperatorGuidesCatalog.Ch("Step 1 — Setup",
                "Run channels setup (add &sample=1 for demo data), then open the Channels hub.",
                [
                    "Log in to CP → Channels hub."
                ]),
            OperatorGuidesCatalog.Ch("Step 2 — SKU mapping",
                "Link manufacturer + article to external SKU / ASIN per channel.",
                [
                    "Sync pushes stock_qty and price when live credentials are set."
                ]),
            OperatorGuidesCatalog.Ch("Step 3 — Inventory sync",
                "Demo sync Amazon stock / Demo sync eBay stock on the hub.",
                [
                    "Live: SP-API (Amazon) and Sell API (eBay) with OAuth credentials."
                ]),
            OperatorGuidesCatalog.Ch("Step 4 — Import orders",
                "Pending orders in hub → Demo import.",
                [
                    "Process in Orders; ship via Logistics."
                ]),
            OperatorGuidesCatalog.Ch("Step 5 — Go live",
                "Amazon SP-API + eBay OAuth; disable demo_mode on channels.",
                [
                    "Test one SKU sync and one order import on staging."
                ]),
            OperatorGuidesCatalog.Ch("Database tables",
                "PHP reference.",
                [
                    "epc_marketplace_channels — Amazon/eBay config.",
                    "epc_marketplace_sku_map — catalog ↔ marketplace SKU mapping.",
                    "epc_marketplace_orders — external orders pending import.",
                    "epc_channel_sync_log — sync and import audit trail."
                ])
            ],
            ["/cp/marketplace-channels-app", "/cp/guides-app?g=logistics"]),
        new(
            "procurement",
            "cp",
            "shop",
            "Procurement — step-by-step guide",
            "fa-shopping-cart",
            "/CP/shop/procurement/procurement_guide",
            "/cp/guides-app?g=procurement",
            "Suppliers, purchase bills, payments, and advances. Warehouses hold price lists and stock — not legal supplier data.",
            [
            OperatorGuidesCatalog.Ch("End-to-end procurement flow",
                "Procurement panel handles suppliers, purchase bills, payments, and advances.",
                [
                    "Supplier master — Tab Suppliers: legal name, TRN, country, address, payment terms. Required for UAE input VAT on purchases.",
                    "Price source — parts prices come from warehouse price lists. Link warehouse optionally on supplier profile.",
                    "Purchase bill — Tab Purchase bills: record supplier invoice (ex VAT). VAT added for UAE VAT-registered suppliers.",
                    "Advance payment — Tab Advances: pay supplier before goods/invoice (prepayment).",
                    "Payment — Tab Payments: settle payable balance when invoice is due.",
                    "Fulfillment — Tab Fulfillment + ERP Fulfilment: supplier paid → goods in → deliver to customer.",
                    "GL / VAT — purchases post to ERP payables and UAE VAT input."
                ]),
            OperatorGuidesCatalog.Ch("Warehouse vs supplier",
                "Two different masters.",
                [
                    "Warehouse (storage): price list, stock location, catalog source — Logistics → Storages. Many warehouses possible.",
                    "Supplier (procurement): legal entity, TRN, purchase invoice, payable — this panel → Suppliers. One supplier record per vendor; optional warehouse link."
                ]),
            OperatorGuidesCatalog.Ch("UAE e-invoicing (purchase side)",
                "For B2B purchases, ensure supplier TRN and address are complete on the supplier profile.",
                [
                    "Seller-side e-invoices for your sales are in ERP → E-Invoicing."
                ]),
            OperatorGuidesCatalog.Ch("Live snapshot",
                "PHP dashboard: suppliers (with TRN), purchase bills, payable AED, advances AED. Needs tenant DB.",
                [
                    "Without the shop database the counts stay zero."
                ])
            ],
            ["/cp/purchase-requests-app", "/erp/payables-app"]),
        new(
            "customer-mgmt",
            "cp",
            "shop",
            "Customer management — guide",
            "fa-users",
            "/CP/shop/customer_mgmt/customer_mgmt_guide",
            "/cp/guides-app?g=customer-mgmt",
            "Customer lifecycle in one panel: registration, profile, orders, advances, tax invoices, returns. Also registered at /CP/users/customer_mgmt_guide.",
            [
            OperatorGuidesCatalog.Ch("Customer lifecycle",
                "PHP numbered flow.",
                [
                    "Registration — customer registers on shop. B2B: approve via Approvals tab.",
                    "Customer profile — Tab Customers: buyer name, TRN, address, Peppol endpoint (UAE e-invoicing mandatory fields for B2B).",
                    "Orders — Tab Orders or CP Orders. Sale prices ex VAT; 5% output VAT on UAE sales.",
                    "Advance payment — Tab Advances: record customer prepayment (credit on customer ledger).",
                    "Tax invoice — Tab Invoices: generate UAE e-invoice (PINT-AE) from order. Full ASP submission in ERP E-Invoicing.",
                    "Returns — Tab Returns: view return requests; process in Orders CP."
                ]),
            OperatorGuidesCatalog.Ch("Mandatory e-invoice buyer fields",
                "For B2B UAE customers, complete on the customer profile.",
                [
                    "Buyer name, TRN, legal registration, address line 1, city, emirate, country AE, Peppol electronic address (0235:TIN)."
                ]),
            OperatorGuidesCatalog.Ch("Where this differs from shop menus",
                "Customer-related settings were scattered across Users, Orders, and Finance.",
                [
                    "This panel centralises customer master data, orders overview, invoices, advances, and returns in one place."
                ])
            ],
            ["/cp/users-app", "/cp/guides-app?g=document-control"]),
        new(
            "document-control",
            "cp",
            "shop",
            "Document Control System — Guide",
            "fa-file-text",
            "/CP/shop/document_control/document_control_guide",
            "/cp/guides-app?g=document-control",
            "FTA-ready templates and attachment storage. Replaces the legacy Russian print module.",
            [
            OperatorGuidesCatalog.Ch("1. Company profile",
                "Legal identity that prints on every document.",
                [
                    "Open Company profile and enter legal name, full address, TRN (15-digit UAE VAT registration), phone, email, and bank IBAN.",
                    "Upload your company logo (PNG/JPG). It appears on every printed document.",
                    "Set the Legal footer — FTA retention notice, terms, and disclaimers shown on all documents.",
                    "Optional: Import from E-Invoicing to copy seller details already configured in ERP → E-Invoicing."
                ]),
            OperatorGuidesCatalog.Ch("2. Document templates",
                "Four default templates are pre-installed.",
                [
                    "FTA Tax Invoice — mandatory fields for UAE VAT: supplier TRN, buyer TRN (if registered), invoice number & date, line-level net/VAT/total, amount in words.",
                    "Packing Slip — warehouse picking list (no tax amounts).",
                    "Delivery Note — customer sign-off block for proof of delivery.",
                    "Payment Receipt — records amount received, method, and reference.",
                    "Templates use HTML with placeholders such as {{company_trn}}, {{lines_table}}, {{legal_footer}}. Edit header, body, footer, and CSS directly — changes apply immediately to new prints."
                ]),
            OperatorGuidesCatalog.Ch("3. Printing documents",
                "Print documents tab.",
                [
                    "Find the order and click the document type (opens in a new tab).",
                    "Use the browser Print button or Ctrl+P. Save as PDF for email/archive.",
                    "Invoice numbers prefer the e-invoice number from ERP if one exists; otherwise format INV-000123."
                ]),
            OperatorGuidesCatalog.Ch("4. Supplier & other attachments",
                "Attachments tab.",
                [
                    "Enter order ID, choose category (e.g. Supplier purchase invoice), supplier name, reference, and upload PDF/image.",
                    "Files are stored securely and linked to the order for audit trail (input VAT support)."
                ]),
            OperatorGuidesCatalog.Ch("5. FTA compliance checklist",
                "Requirement → where configured.",
                [
                    "Supplier TRN on tax invoice — Company profile → TRN.",
                    "Buyer TRN (if VAT registered) — Customers → E-invoice buyer profile.",
                    "Unique invoice number & date — auto from order / e-invoice.",
                    "VAT rate & amount — Finance → VAT settings (default 5%).",
                    "Line item description & value — order lines.",
                    "5-year record retention — legal footer + attachment storage."
                ]),
            OperatorGuidesCatalog.Ch("6–7. Legacy module & support",
                "Old Russian print module.",
                [
                    "The old Russian module (/cp/shop/modul-pechati-dokumentov) redirects to this panel.",
                    "Russian TORG-12 / UPD forms remain in the database for reference but are not recommended for UAE operations.",
                    "Setup script (server): /epc-document-control-cp-setup.php?token=…"
                ])
            ],
            ["/cp/document-control-app", "/cp/print-docs-app"]),
        new(
            "api-docs",
            "super",
            "portal",
            "API documentation & tenant keys",
            "fa-code",
            "/CP/control/portal/epc_api_documentation_guide",
            "/cp/guides-app?g=api-docs",
            "Phase 1 public REST API at /epc-api/v1/ — read-only, tenant-scoped via X-API-Key. Super CP (www.ecomae.com) only in PHP.",
            [
            OperatorGuidesCatalog.Ch("Quick links",
                "Marketing + operator surfaces.",
                [
                    "Marketing — API documentation / Catalog & Price PRO API.",
                    "Catalog & Price PRO — client keys (epc_api_clients_manage).",
                    "OpenAPI spec: /epc-api/v1/openapi.json.",
                    "API health probe: /epc-api/v1/health."
                ]),
            OperatorGuidesCatalog.Ch("Issue API keys (operators)",
                "Keys live in platform DB (ecomae), never in marketing HTML.",
                [
                    "Run setup on platform stack once per environment (epc-api-keys-setup.php). Creates epc_api_keys and rotates demo keys for epartscart and asap. Plain keys print in setup output only — copy to password manager.",
                    "Register Super CP menu if missing (epc-api-documentation-cp-setup.php).",
                    "For enterprise tenants, insert a row in epc_api_keys with SHA-256 hash of the key, tenant site_key, and scopes JSON."
                ]),
            OperatorGuidesCatalog.Ch("Scopes (Phase 1)",
                "Read-only.",
                [
                    "read:tenant — GET /epc-api/v1/tenant/info.",
                    "read:orders — GET /epc-api/v1/orders.",
                    "read:products — GET /epc-api/v1/products/search?q=.",
                    "read:erp — GET /epc-api/v1/erp/dashboard-summary.",
                    "read:bi — GET /epc-api/v1/powerbi/* (Power BI JSON/CSV datasets).",
                    "read:* — all authenticated read endpoints."
                ]),
            OperatorGuidesCatalog.Ch("Security rules",
                "PHP security block.",
                [
                    "Send X-API-Key on every request. Never embed keys in storefront HTML.",
                    "Tenant scope is enforced server-side from the key row — clients cannot switch site_key by query.",
                    "Rotate keys after staff leave. Demo keys are for sandbox only."
                ])
            ],
            ["/cp/api-clients-app", "/cp/guides-app?g=power-bi"]),
        new(
            "auto-price",
            "super",
            "portal",
            "Auto Price Engine — operator guide",
            "fa-line-chart",
            "/CP/control/portal/epc_auto_price_guide",
            "/cp/guides-app?g=auto-price",
            "Universal Auto Price AI workflow — discover, compare, import, and keep prices fresh for every tenant.",
            [
            OperatorGuidesCatalog.Ch("Open the engine",
                "PHP standalone route /cp/control/portal/epc_auto_price_guide?site_key=…",
                [
                    "Super CP defaults site_key to electronicae when empty.",
                    "Tenant CP infers epartscart / electronicae / platform from the host.",
                    "Open engine and Compare matrix from the hero actions."
                ]),
            OperatorGuidesCatalog.Ch("Discover → compare → import",
                "Operator workflow from the guide panel.",
                [
                    "Discover competitor / supplier sources for the tenant catalog.",
                    "Compare matrix shows your price vs source price and suggested sell.",
                    "Import approved rows into the warehouse price list.",
                    "Schedule refresh so prices stay fresh."
                ]),
            OperatorGuidesCatalog.Ch("Related surfaces",
                "APAI storefront operator notes live beside this engine.",
                [
                    "Tenant Auto Price app: /cp/auto-price-app.",
                    "Price lists and upload guide stay the ingest path for supplier files."
                ])
            ],
            ["/cp/auto-price-app", "/cp/guides-app?g=prices-upload"]),
        new(
            "workshop",
            "cp",
            "shop",
            "Auto Workshop Online — operator guide",
            "fa-wrench",
            "/CP/control/portal/epc_autoworkshop_guide",
            "/cp/guides-app?g=workshop",
            "Professional repair workshop: check-in → job card (parts + labour) → estimate → bay/tech → QC → ready → handover. Linked to storefront booking and Client ERP.",
            [
            OperatorGuidesCatalog.Ch("Process flow",
                "PHP flow chips.",
                [
                    "1 Check-in",
                    "2 Estimate",
                    "3 Approved",
                    "4 In progress",
                    "5 QC",
                    "6 Ready",
                    "7 Delivered"
                ]),
            OperatorGuidesCatalog.Ch("1 Open the desk",
                "Floor board.",
                [
                    "Open Shop → Workshop → Floor board.",
                    "Use New check-in for a vehicle arriving now.",
                    "Public booking / track: /en/auto-workshop."
                ]),
            OperatorGuidesCatalog.Ch("2 Check in the vehicle",
                "Check-in tab.",
                [
                    "Capture plate, VIN, make/model/year, customer phone.",
                    "OEM catalog (katalog-laximo) helps identify parts."
                ]),
            OperatorGuidesCatalog.Ch("3 Build the job card",
                "Parts + labour.",
                [
                    "Add parts from catalog or warehouse.",
                    "Add labour lines.",
                    "Send estimate to the customer for approval."
                ]),
            OperatorGuidesCatalog.Ch("4 Repair → QC → ready",
                "Bay / tech assignment.",
                [
                    "Assign bay and technician.",
                    "Move status In progress → QC → Ready.",
                    "QC must pass before handover."
                ]),
            OperatorGuidesCatalog.Ch("5–6 Customer booking, parts & invoice",
                "Storefront + ERP.",
                [
                    "Customer books from the public site; the job appears on the board.",
                    "Parts used can post to the shop order / ERP invoice.",
                    "SMS / communications test for ready-for-pickup messages."
                ])
            ],
            ["/cp/workshop-app", "/cp/orders"]),
        new(
            "custom-shipping",
            "super",
            "portal",
            "Custom & Shipping (Phase 1 + reports)",
            "fa-ship",
            "/CP/control/portal/epc_custom_shipping_guide",
            "/cp/guides-app?g=custom-shipping",
            "Super CP deploy guide for the customs declarations module. Tenant daily steps live in the ERP customs book.",
            [
            OperatorGuidesCatalog.Ch("Deploy module files",
                "Push ERP files, then run the setup endpoint on the tenant site.",
                [
                    "Setup URL: /epc-custom-shipping-setup.php?token=…",
                    "Registers declaration types from the C&L Excel workbook plus LGP warehouse intake and five reports."
                ]),
            OperatorGuidesCatalog.Ch("Operator workflow (tenant CP)",
                "ERP Suite → Custom & Shipping.",
                [
                    "Open ERP → Custom & Shipping — dashboard shows KPI tiles and six category cards.",
                    "Pick category or quick action — Import, Export, Transit, Temporary Admission, Transfer, or LGP warehouse intake.",
                    "Choose declaration type from the category list.",
                    "Fill required fields — Company, Customs emirate, Declaration type, Date, Declaration date (LGP uses warehouse intake fields).",
                    "Declaration line items — HS code, origin, qty, unit, volume, amount, weight. Required per row: HS code, country of origin, quantity.",
                    "Save draft and submit — draft until submitted to UAE customs (submitted → cleared).",
                    "Run reports — declaration search, cost summary, duty report (partial), re-export tracking, document expiry."
                ]),
            OperatorGuidesCatalog.Ch("Declaration reports",
                "cs_view=reports.",
                [
                    "Filter, print, export CSV per report.",
                    "Full tenant guide: ERP guide book Customs."
                ])
            ],
            ["/erp/guide-app?book=customs"]),
        new(
            "erp-only-onboard",
            "super",
            "portal",
            "ERP-only deployment (shared ecomae.com)",
            "fa-university",
            "/CP/control/portal/epc_erp_only_onboard_guide",
            "/cp/guides-app?g=erp-only-onboard",
            "Super CP only. Clients who need only ERP — no storefront, no client domain. All companies log in at www.ecomae.com/cp/; each company has its own MySQL database.",
            [
            OperatorGuidesCatalog.Ch("Model — multi-company on one host",
                "PHP model list.",
                [
                    "hosted_on=platform + erp_only_shared=1 in tenant registry.",
                    "Hostname always www.ecomae.com — no DNS, no nginx alias per client.",
                    "Login email maps to tenant DB via platform registry; optional company picker if email exists in multiple DBs.",
                    "access_mode=erp_only — commerce hidden; redirect to ERP shell after login.",
                    "Granular ERP modules per company (Full ERP, Custom & Shipping, etc.).",
                    "Separate tenant DB per company (asap, company2, …) — not multi-entity unless you enable it inside one DB.",
                    "Optional custom domain for ERP-only is deprecated for new clients — use shared ecomae.com only.",
                    "URL separation: Super CP → /cp/ (tenant hub). ECOM AE company ERP → /cp/platform-erp/. Client staff → /cp/client-erp/{site_key}/."
                ]),
            OperatorGuidesCatalog.Ch("Onboarding checklist",
                "Tenant hub → Onboard, then Live & sync.",
                [
                    "Complete the onboard form steps from the tenant hub (tab=onboard).",
                    "Set Live & sync — Tenant hub → status Live pushes access_mode, erp_modules, and CP packs to the company MySQL DB.",
                    "Create users & hand off — CP → Users on www.ecomae.com (tenant context after login). Share https://www.ecomae.com/cp/."
                ]),
            OperatorGuidesCatalog.Ch("Login URL (ERP-only companies)",
                "PHP pre block.",
                [
                    "Super CP operator (tenant hub): https://www.ecomae.com/cp/",
                    "Platform ERP (ECOM AE company, ecomae DB): https://www.ecomae.com/cp/platform-erp/",
                    "ASAP client ERP (site_key=asap): https://www.ecomae.com/cp/client-erp/asap/",
                    "Legacy /cp/shop/finance/erp?epc_erp_shell=1 redirects or blocks."
                ]),
            OperatorGuidesCatalog.Ch("ASAP (reference tenant)",
                "First shared ERP company: site key asap, Full ERP modules.",
                [
                    "Provision with epc-asap-erp-onboard.php on the server."
                ])
            ],
            ["/cp/tenants-app", "/erp/guide-app?book=erp-only"]),
        new(
            "integrations",
            "cp",
            "portal",
            "Integrations Guide",
            "fa-plug",
            "/CP/control/portal/epc_integrations_guide",
            "/cp/guides-app?g=integrations",
            "Every catalog module in one CP page — SMTP, OAuth, WhatsApp, payments, marketplaces, BI, and more.",
            [
            OperatorGuidesCatalog.Ch("Email / SMTP",
                "Deliver order confirmations, OTP codes, and staff alerts through SMTP.",
                [
                    "Open Email / SMTP settings (tenant page for shops; Super CP auth settings for platform defaults).",
                    "Enter host, port, encryption, username, and password. Save.",
                    "Use Send test email — confirm delivery before go-live.",
                    "Place a test order and verify the customer receipt arrives."
                ], "Prefer a dedicated mailbox (orders@…) with SPF/DKIM aligned to your domain."),
            OperatorGuidesCatalog.Ch("OAuth",
                "Google / Microsoft (and related) OAuth for CP or storefront login — Super CP configures app credentials.",
                [
                    "Create OAuth clients in Google Cloud / Microsoft Entra with redirect URIs for your hosts.",
                    "Paste Client ID / Secret under Super CP → Auth settings.",
                    "Enable the providers you want, then test login in an incognito window.",
                    "Toggle the oauth feature per tenant under Tenant features if a shop should not use it."
                ], "Redirect URI mismatches are the #1 failure — copy exact https://host/… callback paths."),
            OperatorGuidesCatalog.Ch("Registration / WhatsApp / payments / channels",
                "Cross-links to the dedicated guides.",
                [
                    "Registration enhanced — review fields and verification under Auth settings; SMTP must work.",
                    "WhatsApp — Phase 1 wa.me sharing; confirm sales display name / phone; train the desk.",
                    "Payments — seed gateways, attach individual accounts, test checkout + IPN.",
                    "Channels — SKU map, inventory sync, import orders, then fulfil in Logistics."
                ]),
            OperatorGuidesCatalog.Ch("Power BI, API keys, webhooks",
                "Read integrations.",
                [
                    "Issue an API key with read:bi or read:erp, then follow the Power BI guide.",
                    "Webhooks / event bus stay Super CP — peek only; no Kafka from this guide.",
                    "Open the Integrations hub for the live catalog tiles."
                ])
            ],
            ["/cp/integrations-app", "/cp/guides-app?g=power-bi", "/cp/guides-app?g=whatsapp"]),
        new(
            "failover",
            "super",
            "portal",
            "Failover & splash",
            "fa-shield",
            "/CP/control/portal/epc_platform_failover_guide",
            "/cp/guides-app?g=failover",
            "Super CP only. Tenants never see generic down / slow / unreachable errors. Immediate splash → local premises backup → sticky LOCAL PREMISES BACKUP banner.",
            [
            OperatorGuidesCatalog.Ch("1. Preview splash (no outage)",
                "Preview without flipping production.",
                [
                    "Open /epc-platform-splash.html?epc_splash_preview=1&mode=backup_active.",
                    "Status JSON: /epc-platform-status.json. Status page: /epc-platform-status.php."
                ]),
            OperatorGuidesCatalog.Ch("2. Configure backup URL (laptop / premises)",
                "backup_base_url in failover config.",
                [
                    "Set the laptop / premises backup URL before switching mode."
                ]),
            OperatorGuidesCatalog.Ch("3. Set failover mode (reference)",
                "primary_ok vs backup_active.",
                [
                    "Change mode only from Super CP. CPU-safe: 60s status poll max, localStorage, static JSON."
                ]),
            OperatorGuidesCatalog.Ch("4. nginx — error_page + Save vhost (required)",
                "error_page must point at the splash.",
                [
                    "Save vhost after editing. Missing error_page shows generic nginx errors to tenants."
                ]),
            OperatorGuidesCatalog.Ch("5–6. Cloudflare 525 / SSL handshake",
                "Custom error page for 525.",
                [
                    "Cloudflare custom error page for 525.",
                    "thejewellerytrend.com 525 / SSL handshake notes stay in the PHP guide."
                ]),
            OperatorGuidesCatalog.Ch("7–9. Deploy, cache, prevention",
                "Deploy files from laptop; optional Cloudflare cache purge.",
                [
                    "Prevention checklist: 404 / 524 / 526 — keep primary healthy, keep backup URL current, do not leave backup_active after recovery."
                ])
            ],
            ["/cp/failover-status-app"]),
        new(
            "power-bi",
            "cp",
            "portal",
            "Power BI — step-by-step guide",
            "fa-bar-chart",
            "/CP/control/portal/epc_power_bi_guide",
            "/cp/guides-app?g=power-bi",
            "Connect Microsoft Power BI to tenant ERP data from Control Panel. Desktop refresh works without Azure AD.",
            [
            OperatorGuidesCatalog.Ch("At a glance",
                "Works now vs needs Microsoft account later.",
                [
                    "Works now: Power BI Desktop / Service Web connector; JSON + CSV datasets with X-API-Key; CP workspace / report ID storage; optional *.powerbi.com iframe preview.",
                    "Needs your Microsoft account later: Azure AD / service principal embed and scheduled Service refresh that Microsoft hosts."
                ]),
            OperatorGuidesCatalog.Ch("Step-by-step",
                "PHP epc_power_bi_guide_steps() order.",
                [
                    "Issue or copy an API key with read:bi (or read:erp / read:*).",
                    "Open Power BI settings and store workspace / report IDs if you embed.",
                    "In Power BI Desktop use Web connector → JSON or CSV dataset URL with X-API-Key header.",
                    "Refresh locally to confirm rows. Publish when ready.",
                    "Optional iframe preview uses *.powerbi.com when a report ID is stored."
                ]),
            OperatorGuidesCatalog.Ch("Dataset cheat sheet",
                "Catalog from epc_power_bi_dataset_catalog.",
                [
                    "ERP dashboard summary, orders, products, and BI datasets under /epc-api/v1/powerbi/.",
                    "Health probe: /epc-api/v1/health."
                ]),
            OperatorGuidesCatalog.Ch("Checklist",
                "Before you hand off to finance.",
                [
                    "API key stored in a password manager — not in a shared spreadsheet.",
                    "Desktop refresh succeeds on at least one dataset.",
                    "Tenant host HTTPS is valid."
                ])
            ],
            ["/cp/power-bi-app", "/cp/guides-app?g=api-docs"]),
        new(
            "super-cp-operator",
            "super",
            "portal",
            "Super CP — Operator workspace guide",
            "fa-sitemap",
            "/CP/control/portal/epc_super_cp_operator_guide",
            "/cp/guides-app?g=super-cp-operator",
            "Platform operators only. Tenant CP has no Operator sidebar group. Fleet, customer board, price configs, info blocks, communication.",
            [
            OperatorGuidesCatalog.Ch("Who is an “Operator”?",
                "Platform administrators on www.ecomae.com — not tenant shop staff.",
                [
                    "Tenant CP — no Operator sidebar group.",
                    "Super CP hosts only: www.ecomae.com, ecomae.com, cp.ecomae.com."
                ]),
            OperatorGuidesCatalog.Ch("Super CP Fleet Dashboard",
                "View all CP instances across industries.",
                [
                    "View all industry groups with their tenant counts.",
                    "Click any tenant card to open its CP, ERP, or storefront.",
                    "Use search to find specific tenants by name, domain, or industry."
                ]),
            OperatorGuidesCatalog.Ch("Super ERP Fleet Dashboard",
                "All ERP instances with module status, BOS control, and fleet-wide operations.",
                [
                    "View all ERP instances (live + demo) across industries.",
                    "Check module activation status for each tenant.",
                    "Access BOS for full fleet control (tenants, billing, security, deployment)."
                ]),
            OperatorGuidesCatalog.Ch("Customer board",
                "Cross-tenant customer search across the platform registry and every live tenant MySQL database.",
                [
                    "Enter email, phone, name, or company in the search box.",
                    "Filter by platform-only or a specific tenant from the registry.",
                    "Open CRM, ERP, or tenant CP from Quick actions for the matching user row."
                ]),
            OperatorGuidesCatalog.Ch("Price configs / Info blocks / Communication",
                "Commercial and content operators.",
                [
                    "Price configs — platform default rule, then tenant overrides. Verify live prices under Shop → Prices.",
                    "Info blocks — placement (homepage, footer, checkout, CP notice), scope Platform or Tenant, stable block_key.",
                    "Communication — platform mail/SMS defaults and test send."
                ]),
            OperatorGuidesCatalog.Ch("Typical operator day",
                "PHP typical day.",
                [
                    "Check fleet health, then tenant hub for onboard / Live.",
                    "Answer support from Customer board — do not log into every tenant CP first.",
                    "Leave Super fleet / BOS / IP off tenant hosts (those URLs 404 there)."
                ])
            ],
            ["/bos/fleet-health-app", "/cp/customer-board-app", "/cp/tenants-app"])
    ];
}
