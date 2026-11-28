# Organization administration and system administration

These menus do not post sales or purchases. They supply the company, the number, the user, and the batch job that every other form uses.

## Organization administration — menu sections

| Section | Form | Fields | ECOM AE |
| --- | --- | --- | --- |
| Legal entities | `CompanyInfo` | `DataArea`, name, address, `CoRegNum`, language, time zone | `epc_erp_pm_legal_entities`: `code`, `name`, `country_code`, `currency_code`, `trn`, `note`, `active` |
| Operating units | `OMOperatingUnit` | Name, type (department, cost center, business unit, value stream), address | `epc_erp_pm_business_units` (`legal_entity_id`, `parent_id`, `manager`). Cost center as a dimension value is `epc_erp_pm_dimension_values`, not a second business unit |
| Organization hierarchies | `OMHierarchyType` | Purpose (security, expenditure), nodes | `parent_id` is only a tree. Purpose is GAP |
| Number sequences | `NumberSequenceTable` | `NumberSequence`, format, smallest, largest, next, continuous, company scope | `epc_erp_voucher_sequences`: `voucher_type`, `year`, `last_seq`. One row per document type. Do not share `so_no` and `po_no` and `journal_no` on one sequence |
| Workflows | `WorkflowTable` | Template, document type, version, active | Existing ERP workflow. One engine. The document stores the status. The workflow stores the assignment |
| Document types | `DocuType` | Type id, group (note, file, URL) | `notes` on the document, or an attachment row whose parent is the document id |
| Users (also under System) | `UserInfo` | `Id`, network alias, company, language | `admin_id` on each posted row |

The company switcher in the status bar sets `DataAreaId` for the session. Every query in a details form filters on that company. A user with two companies gets two logins worth of data, not one grid with both, unless the form is a cross-company inquiry.

## System administration — menu sections

| Section | Form | Fields the builder must keep | ECOM AE |
| --- | --- | --- | --- |
| Users | `UserInfo` | `Id`, `Enable`, default company | Control Panel user. ERP reads it. ERP does not store a second password |
| Security roles | `SecurityRole` | `AotName`, `Name` | Role on the user |
| Duties | `SecurityDuty` | Duty name, privileges | GAP as a named duty layer. Do not flatten everything into one admin flag |
| Privileges | `SecurityPrivilege` | Entry points (menu item, form) and level (view, edit, create, correct, invoke) | Permission that shows or hides the action pane button |
| Assign users to roles | `SecurityUserRole` | User, role, company (`OMInternalOrganization`) | A role in company A does not open company B |
| Data security | `SecurityPolicy` | Query, role, constrained table | Row filter. Warehouse and company filters belong here, not in the form |
| Batch jobs | `BatchJob` | `Caption`, status, recurrence, `Company` | A batch row runs as a user in a company. It calls the same post method as the form |
| Services | Inbound port | Service name, adapter, restrict to company | Integration writes the same tables as the form. No side door into the GL |
| Database log | `DatabaseLog` | Table, field, type of change | Posted documents already keep a voucher. A field log is extra. Do not use it as the audit trail for money |
| Email | `SysEmailTable` | Template id, subject, body | Notifications table `epc_erp_notifications` |

## What every details form has, from these menus

| Chrome | Source | Rule |
| --- | --- | --- |
| Title and number | Number sequence for that document | Unique inside the company |
| Company | Session `DataAreaId` | Stored on the header. Lines inherit it |
| Action pane New / Edit / Post | Privilege on the menu item | Post is a different privilege from Edit |
| Workflow bar | `WorkflowTable` for that document type | Submit, approve, reject. Reject does not post |
| Attachments | `DocuRef` (`RefTableId`, `RefRecId`, `TypeId`) | Parent is the header id or the line id |
| Print | SSRS report id on the menu item | Print reads the posted document. It does not post |
| Infolog | Return value of the post method | Errors leave the document unposted |

## Compliance and internal controls

Menu path: **Compliance and internal controls**. This menu does not post.

| Form | Fields | Rule |
| --- | --- | --- |
| Audit policy | `SysPolicy` name, start, end, query that selects the documents | A policy watches a document. It does not replace the voucher |
| Policy rule | Rule type (audit, segregation), document, query | Segregation of duties is a pair of duties that one user must not hold |
| Audit case | Case id, policy, document `RefRecId`, status | The case links to the sales order, invoice, or journal. Closing the case does not reverse the document |

ECOM AE audit is the posted journal plus the workflow history. A compliance case, when built, stores the document id. It does not store a second copy of the amount.

## Setup order when a module is built

1. Legal entity and its currency.
2. Number sequence for that document.
3. Posting profile or item group accounts (the main accounts the voucher will use).
4. Terms, tax group, site, warehouse, as the form's FastTabs require.
5. Security privilege for the list page and for Post.
6. The header and lines.
7. The inventory transaction, if the line has an item.
8. The subledger transaction (customer or vendor), if the document is an invoice or a payment.
9. The journal, last, through `ErpGlPostingService.PostJournalAsync`, with `source_type` and `source_id`.
