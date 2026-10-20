# AX 2012 client study — `Ax32.exe` — for Devin.ai migration review

Audience: the Cursor agent [Devin.ai migration review](https://cursor.com/agents/bc-d97081dc-aff3-46b1-a904-2a60549a5dbd) (`bc-d97081dc-aff3-46b1-a904-2a60549a5dbd`), and anyone reviewing ECOM AE against the Dynamics family.

Date: 2026-10-16. Author: Cursor agent `bc-41b73909-9014-4a5e-8dfd-7a085b44f39b` (chat title "Ax32.exe application study").

The field-level build map (menu section, details form, FastTab, and the key that joins each field) is in `docs/migration/ax2012/00-build-rules.md` and the module files next to it. Read that folder before building a form. This note does not move an ERP acceptance row. It does not replace `ASPNET_MIGRATION_TRACKER.md` or `ERP_COMPLETION_DIRECTIVE.md`. The acceptance bar in those files stays Dynamics 365 Finance & Operations / Sales, SAP S/4HANA / Sales Cloud, and Oracle Fusion ERP / SCM. AX 2012 is the desktop client the owner asked to study. D365 F&O is the successor workspace, not this executable.

## What was reachable

The requested binary is:

`C:\Program Files (x86)\Microsoft Dynamics AX\60\Client\Bin\Ax32.exe`

This run is a Linux cloud agent. That path is not mounted. No self-hosted Cursor worker is connected, so the client was not started, and no customer configuration, company, license, or customization was read. Facts below are the AX 2012 product, checked against Microsoft Learn where cited. Anything that depends on the installed bits (kernel build, R2 versus R3, active `.axc`, AOS host, license, CUS/USR models) is marked **not observed**.

## What the file is

`Ax32.exe` is the 32-bit Microsoft Dynamics AX 2012 rich client (kernel 6.0). The `(x86)` directory and the `60` folder are the client install for every AX 2012 release that uses this kernel: AX 2012, Feature Pack, R2, and R3. The same folder name does not tell you which of those releases is installed. That comes from the file version of `Ax32.exe` and from Help > About inside a running client. **Not observed.**

Microsoft documents this path as the place configuration commands are passed to the client: [Client configuration commands](https://learn.microsoft.com/en-us/dynamicsax-2012/appuser-itpro/client-configuration-commands).

The client is a shell. It does not own the database and it does not run the posting engine. It logs on to an Application Object Server (AOS), then draws forms from metadata the AOS serves. Business data and the application model live in SQL Server, behind the AOS.

The AOS executable is `Ax32Serv.exe`, default directory `C:\Program Files\Microsoft Dynamics AX\60\Server\<instance>\bin` (the 64-bit Program Files tree, not `(x86)`). See [Execute configuration commands](https://learn.microsoft.com/en-us/dynamicsax-2012/appuser-itpro/execute-configuration-commands).

## How a session runs

```
Ax32.exe  --TCP-->  AOS (Ax32Serv.exe)  --TDS-->  SQL Server
                         |
                         +--> SSRS, Enterprise Portal / SharePoint, Workflow, Batch
```

1. The user starts `Ax32.exe` (shortcut, command line, or Remote Desktop session). The process reads the active client configuration.
2. It connects to an AOS. Default AOS TCP port is **2712**. The services WSDL port defaults to **8101**. The services endpoint port defaults to **8201**. A second AOS instance on the same computer defaults to 2713 / 8102 / 8202. The TCP port can be changed. The services ports are chosen at install time. Source: [Change AOS ports](https://learn.microsoft.com/en-us/dynamicsax-2012/appuser-itpro/change-aos-ports) and [Firewall settings](https://learn.microsoft.com/en-us/dynamicsax-2012/appuser-itpro/firewall-settings-for-microsoft-dynamics-ax-components).
3. Setup creates a Windows Firewall inbound rule named like "Dynamics AX 6.0 – MicrosoftDynamicsAX (RPC)" for the AOS TCP port. The client itself also needs to be allowed to open a TCP connection to that port.
4. Authentication is Windows integrated in a normal install. Legacy configuration keys still include `sqluser`, `sqlpwd`, and `windowsauth`. Microsoft marks those as legacy and says not to change them. The client is not a SQL client for daily work.
5. Optional `aosencryption` encrypts the client-to-AOS stream.
6. After logon the AOS applies the user's security and the selected legal entity (company). The client then shows the navigation pane, the role center, and the list pages that user is allowed to open.
7. X++ business logic runs on the AOS, mostly as compiled CIL on the server. The client still interprets some client-tier X++ (form controls, lookups, client callbacks). A few behaviors differ between interpreted X++ and CIL. Microsoft calls out string truncation as one of those cases (`logstrtrunc`).

Load-balanced AOS clusters are normal. A client can be pinned to one AOS with `-loadbalance=0 -aos2=<host>:<port>` (community note consistent with the documented `-aos2` switch).

## Startup and configuration

Configurations are stored as named client configurations (registry) and as `.axc` files, edited by the Microsoft Dynamics AX 2012 Configuration utility. Commands can also be passed on the `Ax32.exe` command line or in the utility's "Configuration Command to run at kernel startup" field. Microsoft says there is no syntax check before the client receives them.

Documented switches that matter for a study of this executable ([Client configuration commands](https://learn.microsoft.com/en-us/dynamicsax-2012/appuser-itpro/client-configuration-commands)):

| Command line | What it does |
| --- | --- |
| `-regconfig=<name>` | Named configuration to load. Not settable inside an `.axc` file. |
| `-aos2=<host>` or `-aos2=<host>:<port>` | Additional AOS. Port defaults to 2712. |
| `-company=<id>` | Legal entity that opens at startup. |
| `-language=<code>` | Client language (`en-us`, `ar`, …). Needs the matching language files under `Client\Bin` and a license. |
| `-startupcmd=<SysStartupCmd>` | Runs a startup command class after logon. |
| `-startupmsg=<text>` | Message box at startup. |
| `-development` | Opens the developer workspace and the AOT. |
| `-aol=<layer>` | Layer the AOT opens in. |
| `-aolcode=<license>` | Developer license code for that layer. |
| `-model=<name>` | Current model for new AOT objects, inside the startup layer. |
| `-log=<dir>` | Alternate AOT change log directory. |
| `-masterauc=<path>` | Shared Application Unicode Cache to speed first start. |
| `-roamingprofile` | Client files follow roaming profiles in a Remote Desktop farm. |
| `-aosencryption=0\|1` | Encrypt client-to-AOS traffic. |
| `-useserverprinters` | Use printers configured on the AOS. |
| `-aotimportfile=<file.xpo>` | Import an xpo and start a full AOT compile. Developer only. |
| `-noauto` | Skip custom startup logic so a broken startup can be diagnosed. |
| `-globalbreakpoints` | Breakpoints in Business Connector or client X++. |
| `-xppdebug=0\|1` | User breakpoints for Business Connector sessions. |

**Not observed:** which configuration this PC uses, which company it opens, and whether `-development` is available to the logged-on user.

## What the user sees

AX 2012 replaced the older Axapta menu with a role-tailored shell. After logon the client is:

- **Navigation pane.** Area pages (modules). Favorites. The areas a user sees are the menu items their roles allow.
- **Address bar and list-page filter.** Users jump to a list or a form the way they jump to a URL.
- **Role center.** The home page is usually an Enterprise Portal page (SharePoint) hosted in the client: cues, KPI lists, links into work lists. If EP is not deployed, the home page is thinner. **Whether EP is deployed here was not observed.**
- **List page.** Grid of records, action pane (New, Maintain, related documents), filter pane, preview pane, fact boxes on the right.
- **Details form.** Header and lines, FastTabs, action pane. This is the document workspace AX 2012 users live in. D365 F&O kept this pattern and moved it to the browser.
- **Status bar.** Current company, user, and (when the user is a developer) the layer. R2 and later also have a partition. **Not observed** on this install.
- **Infolog.** The yellow message stack for warnings, errors, and posting results.

There is no separate "web ERP" inside `Ax32.exe`. Enterprise Portal is a different site. The client can host those pages. Users who only have a browser do not use this executable.

## Company, ledger, and dimensions

AX 2012's data model, which the client is only a window onto:

- **Legal entity (company).** `DataAreaId`. Almost every transaction is posted in one company. The company switcher is in the status bar and at startup (`-company`).
- **Partition (R2+).** A harder isolation boundary than company. Several legal entities share a partition. **Not observed** whether this install is R2/R3 (partitions exist) or the original 2012 release (no partitions).
- **Organization hierarchy.** Operating units (department, cost center, business unit, value stream) used for reporting and security, distinct from the legal entity.
- **Chart of accounts and account structures.** Main account plus required financial dimensions. A journal line is not "an account string the user typed." It is a combination the account structure allows.
- **Financial dimensions.** Shared across modules. Typical seeds are department and cost center, plus custom dimensions. Defaulting can come from the customer, vendor, item, or worker, and the user can override on the line when the structure allows it.
- **Number sequences.** Per company or shared, per document type.
- **Source documents and subledger accounting.** Sales orders, purchase orders, invoices, and inventory transactions generate ledger vouchers through the accounting distribution framework. Users do not hand-type the voucher for a normal document post. Manual journals still exist for adjustments.
- **Intercompany.** Orders and journals between legal entities, with due-to / due-from and elimination at consolidation. The client shows both companies only if the user has access to both.

ECOM AE's matching contract is already written in `docs/migration/D365_FO_STRUCTURE_PARITY_AUDIT.md`: legal entity, business unit, financial dimensions, and intercompany. That audit says PHP is still authoritative and the due-to / due-from, currency, tax, and elimination evidence is not proven. This client study does not change that status.

## Security

Enforced on the AOS, displayed by the client:

- **Roles, duties, privileges, entry points.** A user sees a menu item only when a privilege grants that entry point. AX 2012 ships duty-based role templates (accountant, purchasing agent, warehouse worker, and so on).
- **Record-level security / XDS (extensible data security).** Policies can filter rows (by warehouse, by legal entity, by a custom predicate). The client does not implement this. It only shows the rows the AOS returns.
- **Segregation of duties.** Conflicts between duties are a setup check, not a client feature.
- **Developer access** is separate: a license code plus a layer (`-aol`, `-aolcode`). Without it, `-development` does not open a usable AOT.

ECOM AE rule that matches this: one authorization model. Control Panel configures users and roles. ERP enforces them on the transaction. BOS and CRM do not grow a second decision store. See `ERP_COMPLETION_DIRECTIVE.md`.

## Module map the migration review should use

AX 2012 area pages, the documents a user opens from `Ax32.exe`, and the ECOM AE process that owns the same work. "Accepted" is not claimed for any row. The board in `ERP_COMPLETION_DIRECTIVE.md` is still open.

| AX 2012 area in the client | Documents the user posts | ECOM AE process | Owner in the current plan |
| --- | --- | --- | --- |
| Sales and marketing | Sales quotation, sales order, packing slip, sales invoice, return order, free text invoice | Order-to-Cash | Devin |
| Accounts receivable | Customer, free text, collection, interest, write-off | Order-to-Cash and Treasury | Devin |
| Procurement and sourcing | Purchase requisition, RFQ, purchase order, confirmation | Procure-to-Pay | Devin |
| Accounts payable | Vendor invoice, invoice matching, payment journal | Procure-to-Pay and Treasury | Devin |
| Inventory and warehouse management | Arrival, registration, picking, transfer, counting, quarantine, reservation | Inventory / WMS | Devin |
| Product information management | Product, released product, variants, BOM reference | Master data for Inventory and Production | Devin |
| General ledger | Journal, account structure, period close, consolidation, financial reports | Record-to-Report | Devin |
| Cash and bank management | Bank account, deposit, reconciliation, promissory | Treasury | Devin |
| Fixed assets | Acquisition, depreciation, disposal | Fixed assets | Devin |
| Organization administration | Legal entities, hierarchies, number sequences, workflows | Shared foundation (Wave 1) | Devin |
| System administration | Users, roles, batch, services, partitions | Control plane, not a posting module | Cursor for CP; Devin for ERP security on the transaction |
| Compliance and internal controls | Audit policies, compliance site | Audit trail on every process | Devin for the ledger trail |
| Human resources / Payroll | Worker, position, payroll (country pack) | HR / payroll | Devin |
| Production control | Production order, BOM, route, costing | Production | Devin |
| Retail / Commerce | POS statement into inventory and ledger | Retail | Devin |
| Project management and accounting | Project, beginning balance, invoice proposal, revenue recognition | Fit-out is the closest ECOM AE process; AX project accounting is the pattern | Devin |
| Service management | Service order, agreement, repair | Service | Devin |
| No jewellery suite | Metal weight, purity, and assay are customizations on sales, inventory, and POS | Jewellery | Devin |
| Enterprise Portal role center, not a module | Cues and work lists | ERP role workspaces (CFO, accountant, sales, purchasing, warehouse) | Devin for the queries; Cursor for BOS views that only read them |

CRM in AX 2012 is thin (sales quotations and activities live under Sales and marketing). The ECOM AE CRM bar is Dynamics 365 Sales, not this client. Do not grade the CRM pipeline against `Ax32.exe`.

## Development stack (only if the AOT opens)

- **AOT** (Application Object Tree): tables, forms, classes, menu items, security, SSRS reports.
- **Layers**, low to high: SYS, SYP, GLS, GLP, FPK, FPP, SLN, SLP, ISV, ISP, VAR, VAP, CUS, CUP, USR, USP. A change in a higher layer overlays the lower one. Patch layers (the `*P` layers) hold hotfixes.
- **Models** (AX 2012): a named group of elements inside a layer. New objects land in the current model (`-model`).
- **X++** on the AOS, compiled to CIL for server runs. Labels in `Client\Bin` language files.
- **Export** is an `.xpo` or a model file. `-aotimportfile` is the unattended import-and-compile path.
- **MorphX** is the form designer inside this client. D365 F&O replaced it with Visual Studio and a browser client. ECOM AE does not host MorphX. The relevant lesson is the document shape (header, lines, action pane, posting, infolog), which `ErpDocumentWorkspace` is already aimed at.

**Not observed:** whether this install has CUS, VAR, or ISV models. A study of `Ax32.exe` without the AOS model store cannot see customizations. If the owner wants those read, the next step is a worker on that Windows PC, or an export of the model store. Do not guess the customizations.

## Reporting, batch, integration

- **Operational reports** are SSRS reports the client launches. The report server is separate from `Ax32.exe`.
- **Financial statements** are Management Reporter (Financial reporting), also outside the exe.
- **Excel add-in** refreshes list data. It is another client of the AOS services, not a second ledger.
- **Batch** runs on an AOS batch group. The client only schedules the job. Recurrence, alerts, and history are System administration forms.
- **Integration** is AIF (inbound and outbound ports, document services) plus, on R2/R3, the Data Import Export Framework. Windows Client is not the integration endpoint. Services listen on the AOS services ports (8101/8201 by default).
- **Workflow** is a server feature. The client shows the yellow workflow bar and the work items. Approvals are not stored in the client.

## AX 2012 client versus the D365 bar already in the tracker

| | AX 2012 `Ax32.exe` | D365 Finance & Operations (tracker bar) |
| --- | --- | --- |
| Host | 32-bit Windows desktop | Browser workspace |
| Metadata | Model store, MorphX, layers | Visual Studio packages, no layers |
| Home | Role center, often Enterprise Portal | Workspaces |
| Document UI | Action pane, FastTabs, fact boxes, list pages | Same ideas, HTML |
| Company | Legal entity, optional partition | Legal entity in the company context |
| Posting | Subledger accounting on the AOS | Source documents and accounting distributions |
| Security | Roles, duties, privileges, XDS | Security roles, duties, privileges, extensible data security |
| Extensibility | Overlayering in CUS/USR | Extensions. Overlayering is gone. |

Use AX 2012 as a checklist of documents and controls a desktop ERP user expects. Use D365 F&O as the UX and extensibility bar the tracker already names. Do not retarget the matrix to AX 2012.

## What this study could not verify

- File version, cumulative update, and whether the install is AX 2012, FP, R2, or R3.
- The active configuration: AOS host, port, company, language, partition.
- License (which area pages exist).
- Custom models and any industry add-on (jewellery would be here if it exists).
- That the process starts, or which user it logs on as.
- Screenshots of this PC's client. There are none.

## What the migration review agent should do with this

1. Read this file. Treat it as reference, not as a build list and not as acceptance evidence.
2. Keep the benchmark in `ASPNET_MIGRATION_TRACKER.md`. Do not swap D365 for AX 2012.
3. When a process is reviewed, use the module map above to name the AX 2012 documents a user would post from `Ax32.exe`, then check the ECOM AE process for the same chain: create, validate, approve, post, accounting impact, reversal, audit, report, permission, company isolation.
4. Do not open a second posting path to "look more like AX." One ledger, `ErpGlPostingService.PostJournalAsync`.
5. Wave 1 in `ERP_COMPLETION_DIRECTIVE.md` (shell, company switch, document header, action bar, status, totals, attachments, notes, audit, print, approval, validation) is the client-shell equivalent. Grade that wave against the list-page and details-form behavior in this note.
6. Leave Control Panel, BOS, CRM, storefront, and tenants on the Cursor side of the ownership split. CRM is graded against D365 Sales, not against this executable.

## Sources

- Microsoft Learn, archived AX 2012: [Client configuration commands](https://learn.microsoft.com/en-us/dynamicsax-2012/appuser-itpro/client-configuration-commands), [Execute configuration commands](https://learn.microsoft.com/en-us/dynamicsax-2012/appuser-itpro/execute-configuration-commands), [Change AOS ports](https://learn.microsoft.com/en-us/dynamicsax-2012/appuser-itpro/change-aos-ports), [Firewall settings for Microsoft Dynamics AX components](https://learn.microsoft.com/en-us/dynamicsax-2012/appuser-itpro/firewall-settings-for-microsoft-dynamics-ax-components).
- ECOM AE: `docs/migration/ERP_COMPLETION_DIRECTIVE.md`, `docs/migration/ASPNET_MIGRATION_TRACKER.md`, `docs/migration/D365_FO_STRUCTURE_PARITY_AUDIT.md`, `docs/migration/NOTE_FOR_DEVIN_2026-10-09.md`.
