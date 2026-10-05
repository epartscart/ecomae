# ERP workspace writes cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

| Step | Route | Result |
|---|---|---|
| agenda | POST /erp/ajax/agenda-save | ok — `Agenda event added` → `epc_erp_agenda_events` 4 (meeting, start/end, entity link) |
| knowledge base | POST /erp/ajax/kb-save | ok — `Knowledge article published` → `epc_erp_kb_articles` 1 (title, category, summary, body_html) |
| multi-entity | POST /erp/ajax/multi-entity-save (form-encoded) | 302 `?ok=Multi-entity preference saved` → `epc_erp_platform_settings.multi_entity_enabled=1` |

Contract note: `multi-entity-save` is a native-form route — form-encoded body only; a JSON `confirmWrites` body falls through to the dry-run envelope (as designed for native PHP form parity).

Cleanup: agenda event, KB article, setting deleted — residue 0.
