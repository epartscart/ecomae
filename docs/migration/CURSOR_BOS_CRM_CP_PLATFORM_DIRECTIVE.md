# Cursor directive — BOS, CRM, CP, and the platform experience

> **Authoritative Cursor plan.** Cursor agents follow this file for the surfaces named below.
> It is not an acceptance record. Every scorecard line is **not accepted**. Production evidence for this scorecard is **none**.
>
> ERP transactional correctness stays with Devin. The ERP completion board stays in `docs/migration/ERP_COMPLETION_DIRECTIVE.md`. This file does not move that board, does not edit posting code, and does not authorize a live-host change.
>
> Written from `origin/main` at `3ebfe31fa` (#2026). No calendar estimates.

## 1. Owner and scope

**Owner: Cursor.**

Cursor owns:

- Business Operating System (BOS)
- Super CP
- Control Panel (CP)
- CRM experience
- Customer 360
- Supplier 360 presentation
- Tenant and platform administration
- Frontend and storefront
- Marketing
- Platform UX
- Cross-application navigation

Cursor does not own the ERP transaction engine or the accounting engine.

**Devin owns ERP transactional correctness.** Purchase orders, sales orders, inventory, supplier scoring, customer balances, general ledger, and tax are ERP. Cursor calls those services. Cursor does not reimplement them.

Super CP is the operator plane across tenants. CP is administration inside an authorized tenant. Neither one operates the business. ERP operates the business.

## 2. Architectural law

ECOM AE has one of each:

| Law | Rule |
| --- | --- |
| One transactional truth | A transaction lives once, in ERP. |
| One accounting engine | Posting, reversal, and the accounting result belong to ERP. |
| One authorization model | The same permissions gate BOS, CRM, CP, search, notifications, and the storefront. |
| One workflow framework | Approval is the shared workflow. Screens do not invent a second approval path. |
| One audit model | The action, the actor, the tenant, and the company are audited once. |
| One document lineage model | Quote, order, delivery, invoice, receipt, and return stay linked in ERP. |
| One domain and API contract | UX may differ by role. The contract does not. |

Do not create a separate purchase-order engine, sales-order engine, inventory engine, supplier-scoring engine, customer-balance engine, general-ledger engine, or tax engine.

Consume Devin's ERP services.

Do not copy financial rows into a presentation database. A dashboard, a 360 tab, a portal, or a search result reads the ERP or CRM record under the caller's scope. A cache that can disagree with ERP is a second truth and is forbidden.

## 3. Product jobs

Do not blur these jobs.

| Product | Job | Owner |
| --- | --- | --- |
| ERP | Executes the transaction. | Devin |
| BOS | Answers what needs attention, what is going wrong, what management should do, what changed, where the risk is, and what is forecast. | Cursor |
| CRM | Lead → Opportunity → Quote → Order → Relationship → Service. | Cursor, until the quotation is approved. After quotation, ERP. |
| CP and Super CP | Administer the platform. | Cursor |
| Frontend | The customer, vendor, and public experience. | Cursor |

ERP executes transactions. BOS does not post them. CP does not operate them. The storefront does not become a second order book.

**Visual rule: CP administers ECOM AE. ERP operates the business.**

## 4. BOS is a control tower

BOS is exception-driven. It is not another ERP menu.

A person opens BOS to see what is wrong and what to do next. The ERP document is one drill away. BOS does not recreate purchase, sales, inventory, finance, or warehouse navigation as a second copy of ERP.

### Role centres

Five role centres. Every number is an ERP or CRM figure. Every number drills to the records that produced it. The same measure has the same value on every centre. A centre ranks and filters. It does not recompute.

**CEO**

- Revenue
- Margin
- EBITDA
- Cash
- Working capital
- Inventory
- Receivables
- Payables
- Risks
- Forecast
- Approvals

**CFO** — cash through treasury forecast

- Cash
- Working capital
- Receivables
- Payables
- Risks that move cash
- Treasury forecast

**Sales** — revenue through customer risk

- Revenue
- Margin
- Forecast
- Customer risk

Pipeline value, weighted pipeline, coverage, conversion, cycle time, and win rate sit on this centre and are the same figures as the sales pipeline section.

**Purchasing** — demand through stock-out risk

- Demand
- Inventory
- Stock-out risk

Supplier lead time, on-time delivery, and the supplier performance score sit here and are the ERP supplier-performance figures. Purchasing does not score suppliers again.

**Operations** — orders through capacity

- Orders
- Deliveries
- Inventory where it blocks the order
- Capacity

No disconnected KPI. No sample number. No KPI that cannot name its source transactions. If ERP does not calculate a figure, the centre does not show one.

## 5. Exception first

The path is fixed:

**KPI → variance → cause → transaction → action**

Example. Sales reads AED 10M, 8% below target. The next level is Brand A. The next level is the Dubai branch. The next level is Customer X. The actions are the orders, quotations, opportunities, and collections that explain Customer X. The user acts on those records inside ECOM AE.

A tile that does not open this path is not done.

## 6. CRM

Do not rebuild what already exists:

- Leads
- Lead scoring
- Opportunities
- Weighted pipeline
- Accounts
- Customer 360 concepts
- Quotes
- CRM intelligence

Compose them into one lifecycle:

**Lead → Qualification → Account/Contact → Opportunity → Activities → Products → Quotation → Approval → Sales Order → Delivery → Invoice → Collection → Relationship**

Service continues the relationship. It does not start a second customer record.

After quotation, use Devin's ERP services for the sales order, delivery, invoice, collection, credit, and returns. CRM keeps the relationship, the activities, and the reason the deal was won or lost. CRM does not post the order, the invoice, or the receipt.

## 7. Customer 360 — P0

One workspace per customer. Not a folder of screens.

**Header**

- Customer
- Account number
- Group
- Credit limit
- Available credit
- Outstanding
- Overdue
- Year-to-date sales
- Margin
- Salesperson
- Risk
- Last interaction

Credit limit, available credit, outstanding, overdue, year-to-date sales, and margin are ERP figures. Do not copy them into a presentation database.

**Tabs**

Overview, Contacts, Leads, Opportunities, Activities, Quotations, Orders, Deliveries, Invoices, Receipts, Outstanding, Credit, Returns, Service, Communications, Documents, Profitability.

Each tab opens the source records. Orders, deliveries, invoices, receipts, outstanding, credit, returns, and profitability come from ERP. Leads, opportunities, activities, and communications come from CRM. Documents follow the one document-lineage model.

## 8. Sales pipeline

Views:

- Kanban
- List
- Forecast
- Salesperson
- Customer

Opportunity fields:

- Stage
- Probability
- Expected revenue
- Expected margin
- Expected close
- Products
- Competitor
- Salesperson
- Next action
- Last activity
- Age
- Risk

Won and lost capture the reason, the competitor, and the conversion.

Metrics, all from the same opportunities:

- Pipeline value
- Weighted pipeline
- Forecast
- Coverage ratio
- Conversion rate
- Average sales cycle
- Win rate

A weighted pipeline that does not equal the sum of the open opportunities the user is allowed to see is a fake KPI.

## 9. Supplier 360

Do not rebuild supplier scoring. Consume ERP supplier performance. The score drills to the transactions that produced it.

Sections:

- Profile
- Contacts
- RFQs
- Responses
- Price history
- Purchase orders
- Receipts
- Quality
- Invoices
- Payments
- Outstanding
- Lead time
- On-time delivery
- RFQ response
- Win rate
- Returns
- Documents
- Risk
- Performance score

Purchase orders, receipts, invoices, payments, outstanding, returns, lead time, on-time delivery, and the performance score are ERP. Price history is the ERP price record. RFQ response and win rate use those ERP outcomes. Supplier 360 does not keep a second score.

## 10. CP control plane

CP and Super CP administer. They do not operate.

The control plane covers:

- Tenants
- Subscriptions
- Plans
- Users
- Roles
- Permissions
- Modules
- Country profiles
- Localization
- Features
- Integrations
- API clients
- Webhooks
- Import and export
- Audit
- Deployment
- Monitoring
- Backups
- Recovery
- Security
- Branding
- Configuration

Super CP is the only place that spans tenants: tenant registry, plans, deployment, platform monitoring, backups, recovery, and platform security. A tenant CP session sees one tenant. Platform operators still pass the same authorization model. A tenant administrator does not gain Super CP by navigation.

**Visual rule: CP administers ECOM AE. ERP operates the business.** A CP screen that posts a sales order, a receipt, or a journal is in the wrong product.

## 11. Tenant workspace and readiness

The tenant workspace shows:

- Tenant name and identifier
- Subscription and plan
- Legal entities
- Business units
- Sites
- Country profile and localization
- Modules and features entitled by the plan
- Users, roles, and permissions
- Integrations, API clients, and webhooks
- Branding and configuration
- Import and export state
- Pointers to audit, backup, and recovery

**Readiness score.** The score is a count of gates that have evidence, over the gates that are required. A gate with no evidence is not ready. A missing artifact is not zero credit and it is not a pass. Screen counts, route counts, and "implemented" labels are not evidence.

Required evidence before a tenant can be called ready:

- A negative test that tenant A cannot read tenant B
- A negative test that company A does not remain on screen after a switch to company B
- Entitlements match the subscription
- Search, notifications, and exports obey the same scope
- Backup and recovery are demonstrated, not described

Today those gates have no production evidence. The readiness score is **not accepted**.

## 12. Navigation context

Context is always visible:

- Tenant
- Legal entity
- Business unit
- Site
- User
- Role

A context switch reloads every number, list, search result, notification, and document under the new scope. It must not leak another tenant or another company. Stale figures from the previous company are a defect, including figures left in a header, a tile, a tab, an export, or a notification.

## 13. Frontend

The storefront and the public site must be:

- Fast
- Responsive
- Searchable
- A catalogue
- Priced
- Showing availability
- A cart
- A checkout
- An account
- Order history
- Invoices
- Returns
- B2B where the account is a business account
- SEO
- Accessible

ERP remains authoritative for inventory, the applicable price rules, credit, order status, the invoice, and fulfilment. The storefront displays those results. It does not keep a second stock figure, a second price rule, or a second order status.

## 14. Customer portal and vendor portal — P1

Both portals are P1. Both use ERP APIs only. Neither is a second transaction truth.

The customer portal shows the same orders, deliveries, invoices, receipts, outstanding, credit, and returns as Customer 360, scoped to that customer's account. The vendor portal shows the same purchase orders, receipts, invoices, payments, and performance as Supplier 360, scoped to that supplier. Actions that change a transaction call Devin's ERP services.

## 15. Marketing to CRM

A campaign is judged on this chain:

- Campaign
- Channel
- Cost
- Leads
- Qualified leads
- Opportunities
- Wins
- Revenue
- Margin
- ROI

No vanity metrics. Impressions, clicks, and opens are not success unless they are tied to qualified leads, opportunities, wins, revenue, and margin. ROI uses ERP revenue and margin and the campaign cost. Marketing does not invent a revenue number.

## 16. Enterprise search

Search is permission-safe.

Example. A user searches `NGK`. Results may include catalogue parts, customers, suppliers, opportunities, orders, and invoices that match, and only those the user's tenant, legal entity, and role allow. A user without purchasing rights does not see another company's purchase orders. A user in tenant A does not see tenant B. Search uses the one authorization model. It is not a second index that can return a record the ERP API would refuse.

## 17. One notification centre

One centre for the signed-in user. Not a BOS inbox, a CRM inbox, and a CP inbox.

Sources:

- Approvals waiting on the shared workflow
- KPI variances and exceptions
- Credit, overdue, and customer risk
- Demand and stock-out risk
- CRM next action, aging opportunity, win, and loss
- Orders, deliveries, invoices, receipts, returns, and collection
- Supplier performance exceptions
- Marketing campaigns whose cost has no qualified pipeline
- Platform events for operators: integration failure, backup, recovery, and security

Examples:

- Sales AED 10M, 8% below target, Brand A, Dubai, Customer X. Action: open the variance, then the Customer X orders and opportunities.
- Customer X is overdue. Action: open the Outstanding tab. Recording the receipt is an ERP action the user approves. The centre does not write the receipt itself.
- A part is at stock-out risk. Action: open the ERP inventory and demand records. The purchase action is an ERP command.

Actions on an item:

- Open the source record
- Drill the variance to the transaction
- Approve or reject through the shared workflow, after which ERP executes
- Assign the CRM next action

Dismissing a notification does not clear the underlying overdue, stock-out, or approval while ERP or the workflow still has it open.

## 18. AI — P2

AI is contextual. It shows the evidence. It never silently posts.

**Recommend → explain → user approves → ERP executes → audit**

The explanation names the records. The user approves. ERP executes. The audit model records the actor and the command. A recommendation that posts a document, a payment, a stock movement, or a journal without that sequence is forbidden. P2 does not start before the P0 drill path exists, because a recommendation without evidence is a fake KPI in prose.

## 19. One design system

One design system for BOS, CRM, CP, Super CP, the portals, and the storefront account.

Character:

- Dynamics 365 density
- SAP Fiori clarity
- Oracle Redwood polish

Shared components:

- Application shell
- Context switcher for tenant, legal entity, business unit, site, user, and role
- Alert
- KPI tile with value, variance, and drill
- Trend
- Exception list
- Table
- Kanban
- Customer and supplier header
- Tab workspace
- Activity timeline
- Notification item
- Approval action
- Permission-denied, empty, and error states
- Search results

BOS visual hierarchy, top to bottom:

1. Alerts
2. KPIs
3. Trends
4. Exceptions
5. Tables

The CFO sees the top three problems immediately. Density is not an excuse for a wall of equal tiles.

## 20. Security and production-quality bar

A completed area meets all of the following. None of them are optional polish.

- Every number drills to ERP or CRM records.
- Transactional actions call ERP services.
- The one authorization model is enforced on every read and every write.
- Tenant, legal entity, business unit, and site scope hold on screen, in search, in notifications, and in exports.
- Audit and document lineage stay on the shared models.
- The area is usable on a desktop and a small screen, and it is accessible.
- The area contains no fake KPI, no dead button, and no "coming soon".

Negative tests, all required before acceptance:

1. A user in tenant A requests tenant B's customer, invoice, KPI, search hit, notification, document, or export. The response is denied and contains no tenant B data.
2. Switching from company 1 to company 2 leaves no company 1 figure in a header, tile, tab, export, or notification.
3. A role without credit permission does not see credit limit or available credit.
4. A search for `NGK` does not return another tenant's or another company's documents.
5. A measure ERP does not calculate is not rendered as zero, as a dash that looks like a result, or as a sample.
6. A completed area has no control that does nothing and no "coming soon".
7. An AI recommendation does not post until the user approves, and the post is an ERP command with an audit row.
8. Dismissing a notification does not mark the overdue, the stock-out, or the approval resolved while the source system still has it open.

Until those tests have evidence, the area is **not accepted**.

## 21. Devin / Cursor contract

Eight steps before Cursor touches ERP data. Skipping a step is a defect.

1. Name the ERP aggregate. Order, invoice, receipt, stock, journal, tax, supplier score, or customer balance. Do not create another one.
2. Identify Devin's service and the domain API contract for that aggregate.
3. Confirm authorization and the tenant, legal entity, business unit, and site scope on that contract.
4. Read through the contract. Do not copy financial rows into a presentation store.
5. If the job needs a change, send a command to that ERP service. Cursor does not post.
6. The user sees the proposed action and approves it. AI does not approve itself.
7. Audit, document lineage, and the accounting result stay on the ERP models.
8. Prove the drill. The KPI, the score, or the notification opens the transactions that produced it. If it cannot, do not ship the number.

## 22. Cursor scorecard

This scorecard is Cursor's. It is not mixed with Devin's ERP process board. Weights sum to 100. Status is acceptance, not a percent of screens.

| Area | Weight | Acceptance | Production evidence |
| --- | ---: | --- | --- |
| BOS functional | 20 | Not accepted | None |
| CRM and Customer 360 | 20 | Not accepted | None |
| CP and tenant | 15 | Not accepted | None |
| Frontend and commerce | 10 | Not accepted | None |
| Integration with ERP services | 10 | Not accepted | None |
| Security | 10 | Not accepted | None |
| UX | 10 | Not accepted | None |
| Production acceptance | 5 | Not accepted | None |

No invented evidence. A throwaway database, a dry-run, or a unit test may later be cited as a gate. It is not production evidence, and it does not flip a line to accepted.

### PHP-parity baseline, which is the minimum

This scorecard does not replace PHP parity, and PHP parity does not satisfy this scorecard.

The current Cursor PHP-parity baseline is pull request **#1970**, branch `cursor/cp-frontend-parity-4911`, commit **`4ceff67b4`** (`4ceff67b40ab310b3e989922a6582f114d15a6e3`).

Recorded on that checkpoint:

- `dotnet test aspnet/tests/EcomAE.Platform.Tests`: **5053** passed, 0 failed
- Checklist: **55** matching, **7** still open
- Live release named by that work: **`20260930135252`**

That is the minimum. It is not this scorecard. Do not rebase or edit `cursor/cp-frontend-parity-4911` as part of this directive.

## 23. Priorities

### P0

1. One transactional truth. Consume Devin's ERP services. No second PO, SO, inventory, supplier-scoring, customer-balance, GL, or tax engine.
2. BOS control tower and the five role centres, with the KPI lists in this file.
3. Exception path: KPI → variance → cause → transaction → action.
4. Customer 360 as one workspace, with the header and the tabs in this file.
5. CRM lifecycle composed on the existing lead, scoring, opportunity, pipeline, account, quote, and intelligence concepts. ERP after quotation.
6. Navigation context that does not leak a tenant or a company.
7. One design system. Alerts, then KPIs, then trends, then exceptions, then tables. The CFO sees the top three problems immediately.
8. The security bar, including the negative tests, and the ban on fake KPIs, dead buttons, and "coming soon" inside a completed area.

### P1

1. Sales pipeline: Kanban, list, forecast, salesperson, and customer, with the opportunity fields and metrics in this file.
2. Supplier 360 presentation over ERP supplier performance, including the drill from score to transactions.
3. CP and Super CP control plane, with the visual rule that CP administers and ERP operates.
4. Tenant workspace and a readiness score based only on evidence.
5. Frontend: speed, responsive layout, search, catalogue, pricing, availability, cart, checkout, account, order history, invoices, returns, B2B, SEO, and accessibility. ERP stays authoritative for stock, price rules, credit, order status, invoice, and fulfilment.
6. Customer portal, ERP APIs only.
7. Vendor portal, ERP APIs only.
8. Marketing chain from campaign and cost through leads, qualified leads, opportunities, wins, revenue, margin, and ROI. No vanity metrics.
9. Permission-safe enterprise search, including the `NGK` case.
10. One notification centre, with the sources, examples, and actions in this file.

### P2

1. Contextual AI. Recommend, explain, the user approves, ERP executes, audit records it. Never a silent post.

## 24. P0 benchmark matrix

Acceptance is **not accepted**. Production evidence is **none**. Do not treat a later partial build as a pass unless this matrix is updated with real evidence.

| Capability | Acceptance requires | Owner | Priority | Acceptance | Production evidence |
| --- | --- | --- | --- | --- | --- |
| Single ERP contract consumed by BOS, CRM, CP, and the storefront | No second PO, SO, inventory, score, balance, GL, or tax engine. Reads and commands use Devin's services. | Cursor, services by Devin | P0 | Not accepted | None |
| BOS control tower | Exception-driven. Not an ERP menu. Role centres show only source-backed numbers. | Cursor | P0 | Not accepted | None |
| CEO role centre | Revenue, margin, EBITDA, cash, working capital, inventory, receivables, payables, risks, forecast, approvals. Each drills to records. | Cursor | P0 | Not accepted | None |
| CFO role centre | Cash through treasury forecast, from ERP. Top three cash problems are visible immediately. | Cursor | P0 | Not accepted | None |
| Sales role centre | Revenue through customer risk, same figures as the pipeline. | Cursor | P0 | Not accepted | None |
| Purchasing role centre | Demand through stock-out risk. Supplier score is the ERP score. | Cursor | P0 | Not accepted | None |
| Operations role centre | Orders through capacity. | Cursor | P0 | Not accepted | None |
| Exception path | KPI → variance → cause → transaction → action. The AED 10M / Brand A / Dubai / Customer X path works on live data. | Cursor | P0 | Not accepted | None |
| Customer 360 | One workspace. Header and tabs as listed. Financial fields are not copied into a presentation database. | Cursor | P0 | Not accepted | None |
| CRM lifecycle | Existing leads, scoring, opportunities, weighted pipeline, accounts, quotes, and intelligence composed through collection and relationship. ERP after quotation. | Cursor | P0 | Not accepted | None |
| Context switch | Tenant, legal entity, business unit, site, user, and role. No leak across tenant or company. | Cursor | P0 | Not accepted | None |
| Design system | One system. Alerts, KPIs, trends, exceptions, tables. Shared components in this file. | Cursor | P0 | Not accepted | None |
| Security negative tests | The eight negative tests in this file pass. No fake KPI, dead button, or "coming soon" in a completed area. | Cursor | P0 | Not accepted | None |

## 25. Final rule

Success is a user finishing the job faster, seeing what needs attention, drilling to the source transaction, and acting inside ECOM AE.

Success is not a count of migrated screens.
