<?php
/**
 * Marketing Broadcast — external CSS (nginx-safe wrapper).
 */
declare(strict_types=1);

header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=86400');

$ver = '20260722mb1';
echo "/* epc-mb $ver */\n";
?>
@import url('https://fonts.googleapis.com/css2?family=DM+Sans:opsz,wght@9..40,400;9..40,500;9..40,600;9..40,700&family=IBM+Plex+Mono:wght@500;600&display=swap');

.epc-mb-hub {
	--mb-ink: #1a2332;
	--mb-teal: #0f766e;
	--mb-teal-deep: #115e59;
	--mb-coral: #e11d48;
	--mb-wa: #128c7e;
	--mb-sand: #f3f7f6;
	--mb-panel: #fff;
	--mb-muted: #5b6b73;
	--mb-border: #d7e3df;
	--mb-shadow: 0 12px 28px rgba(26, 35, 50, 0.08);
	font-family: 'DM Sans', 'Segoe UI', sans-serif;
	color: var(--mb-ink);
	background:
		radial-gradient(900px 340px at 0% 0%, rgba(15, 118, 110, 0.12), transparent 55%),
		radial-gradient(700px 300px at 100% 0%, rgba(225, 29, 72, 0.08), transparent 50%),
		linear-gradient(180deg, #eef5f3 0%, #f8faf9 40%, #fff 100%);
	border-radius: 18px;
	padding: 14px 14px 20px;
	margin-bottom: 20px;
}

.epc-mb-brandbar {
	display: flex;
	align-items: center;
	gap: 14px;
	flex-wrap: wrap;
	padding: 16px 18px;
	border-radius: 16px;
	color: #f7fffc;
	background:
		linear-gradient(120deg, rgba(255,255,255,0.08), transparent 40%),
		linear-gradient(135deg, #1a2332 0%, #115e59 52%, #0f766e 100%);
	box-shadow: var(--mb-shadow);
	margin-bottom: 12px;
	animation: epc-mb-fade .35s ease both;
}
.epc-mb-brandbar__mark {
	width: 50px; height: 50px; border-radius: 14px;
	display: grid; place-items: center;
	background: rgba(255,255,255,0.12); font-size: 22px; flex-shrink: 0;
}
.epc-mb-brandbar__name { font-size: 24px; font-weight: 700; letter-spacing: -.02em; line-height: 1.15; }
.epc-mb-brandbar__sub { opacity: .9; font-size: 13px; margin-top: 3px; }
.epc-mb-brandbar__actions { margin-left: auto; display: flex; flex-wrap: wrap; gap: 8px; }
.epc-mb-chip-link {
	display: inline-flex; align-items: center; gap: 6px;
	padding: 8px 12px; border-radius: 999px;
	background: rgba(255,255,255,0.12); color: #fff !important;
	text-decoration: none !important; font-size: 12px; font-weight: 700;
	border: 1px solid rgba(255,255,255,0.18);
}
.epc-mb-chip-link:hover { background: rgba(255,255,255,0.22); }

.epc-mb-kpi {
	display: grid;
	grid-template-columns: repeat(5, minmax(0, 1fr));
	gap: 10px;
	margin: 12px 0 14px;
}
@media (max-width: 1100px) { .epc-mb-kpi { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
@media (max-width: 700px) { .epc-mb-kpi { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
.epc-mb-kpi__item {
	display: flex; gap: 10px; align-items: center;
	background: var(--mb-panel); border: 1px solid var(--mb-border);
	border-radius: 14px; padding: 12px 14px; min-height: 74px;
	animation: epc-mb-fade .35s ease both;
}
.epc-mb-kpi__item:nth-child(2) { animation-delay: .04s; }
.epc-mb-kpi__item:nth-child(3) { animation-delay: .08s; }
.epc-mb-kpi__item:nth-child(4) { animation-delay: .12s; }
.epc-mb-kpi__item:nth-child(5) { animation-delay: .16s; }
.epc-mb-kpi__icon {
	width: 38px; height: 38px; border-radius: 11px;
	display: grid; place-items: center; background: #e7f5f2; color: var(--mb-teal); flex-shrink: 0;
}
.epc-mb-kpi__icon--wa { background: #dcfce7; color: var(--mb-wa); }
.epc-mb-kpi__icon--mail { background: #ffe4e6; color: var(--mb-coral); }
.epc-mb-kpi__val {
	font-family: 'IBM Plex Mono', ui-monospace, monospace;
	font-size: 20px; font-weight: 600; line-height: 1.15;
}
.epc-mb-kpi__label {
	font-size: 11px; font-weight: 700; color: var(--mb-muted);
	text-transform: uppercase; letter-spacing: .04em; margin-top: 2px;
}

.epc-mb-tabs {
	display: flex; flex-wrap: wrap; gap: 8px;
	margin: 0 0 14px; padding: 6px;
	background: var(--mb-panel); border: 1px solid var(--mb-border);
	border-radius: 999px; width: fit-content; max-width: 100%;
}
.epc-mb-tabs a {
	display: inline-flex; align-items: center; gap: 7px;
	padding: 9px 14px; border-radius: 999px;
	font-size: 13px; font-weight: 700; text-decoration: none !important;
	color: var(--mb-ink) !important; background: transparent; border: 0;
}
.epc-mb-tabs a.is-active {
	background: var(--mb-teal); color: #fff !important;
	box-shadow: 0 0 0 3px rgba(15, 118, 110, 0.15);
}
.epc-mb-tabs a:hover:not(.is-active) { background: #eef5f3; }

.epc-mb-status-row {
	display: flex; flex-wrap: wrap; gap: 8px; align-items: center;
	margin-bottom: 12px;
}
.epc-mb-badge {
	display: inline-flex; align-items: center; gap: 6px;
	padding: 5px 11px; border-radius: 999px; font-size: 12px; font-weight: 700;
}
.epc-mb-badge--ok { background: #dcfce7; color: #166534; }
.epc-mb-badge--warn { background: #ffedd5; color: #9a3412; }
.epc-mb-badge--info { background: #e0f2fe; color: #075985; }

.epc-mb-compose {
	display: grid;
	grid-template-columns: minmax(320px, 1.05fr) minmax(300px, 0.95fr);
	gap: 14px; align-items: start;
}
@media (max-width: 992px) { .epc-mb-compose { grid-template-columns: 1fr; } }

.epc-mb-panel {
	background: var(--mb-panel);
	border: 1px solid var(--mb-border);
	border-radius: 16px;
	padding: 0;
	margin-bottom: 14px;
	box-shadow: var(--mb-shadow);
	overflow: hidden;
	animation: epc-mb-fade .35s ease both;
}
.epc-mb-panel__head {
	display: flex; justify-content: space-between; align-items: center; gap: 10px;
	padding: 13px 16px; border-bottom: 1px solid var(--mb-border);
	background: linear-gradient(180deg, #f8fbfa, #eef5f3);
}
.epc-mb-panel__head h4 { margin: 0; font-size: 15px; font-weight: 700; }
.epc-mb-panel__body { padding: 16px; }
.epc-mb-panel__hint { font-size: 12px; color: var(--mb-muted); font-weight: 500; }

.epc-mb-step {
	border: 1px solid var(--mb-border); border-radius: 12px;
	padding: 12px 14px; margin-bottom: 12px; background: #f8fbfa;
}
.epc-mb-step__label {
	display: flex; align-items: center; gap: 8px;
	font-size: 12px; font-weight: 800; text-transform: uppercase;
	letter-spacing: .04em; color: var(--mb-muted); margin-bottom: 10px;
}
.epc-mb-step__num {
	width: 22px; height: 22px; border-radius: 999px;
	display: grid; place-items: center;
	background: var(--mb-teal); color: #fff; font-size: 11px;
}

.epc-mb-modes {
	display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px;
}
@media (max-width: 520px) { .epc-mb-modes { grid-template-columns: 1fr; } }
.epc-mb-mode {
	display: block; position: relative; cursor: pointer;
	border: 1px solid var(--mb-border); border-radius: 12px;
	padding: 10px 12px; background: #fff; transition: border-color .15s, box-shadow .15s;
}
.epc-mb-mode input { position: absolute; opacity: 0; pointer-events: none; }
.epc-mb-mode strong { display: block; font-size: 13px; }
.epc-mb-mode span { display: block; font-size: 11px; color: var(--mb-muted); margin-top: 2px; }
.epc-mb-mode.is-active,
.epc-mb-mode:has(input:checked) {
	border-color: var(--mb-teal);
	box-shadow: 0 0 0 3px rgba(15, 118, 110, 0.14);
	background: #eef8f5;
}

.epc-mb-count {
	display: inline-flex; align-items: center; gap: 6px;
	margin-top: 10px; padding: 7px 12px; border-radius: 999px;
	background: #e7f5f2; color: var(--mb-teal-deep);
	font-size: 13px; font-weight: 700;
}
.epc-mb-count.is-pop { animation: epc-mb-pop .35s ease; }

.epc-mb-templates {
	display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 8px;
}
.epc-mb-tpl {
	border: 1px solid var(--mb-border); border-radius: 12px;
	padding: 12px; background: #fff; cursor: pointer; text-align: left;
	transition: border-color .15s, transform .08s, box-shadow .15s;
}
.epc-mb-tpl:hover { border-color: var(--mb-teal); transform: translateY(-1px); }
.epc-mb-tpl.is-active {
	border-color: var(--mb-teal);
	box-shadow: 0 0 0 3px rgba(15, 118, 110, 0.14);
	background: #eef8f5;
}
.epc-mb-tpl__icon {
	width: 32px; height: 32px; border-radius: 9px;
	display: grid; place-items: center; background: #e7f5f2; color: var(--mb-teal);
	margin-bottom: 8px;
}
.epc-mb-tpl__label { font-size: 12px; font-weight: 700; line-height: 1.3; }

.epc-mb-field { margin-bottom: 12px; }
.epc-mb-field label {
	display: block; font-size: 11px; font-weight: 700;
	text-transform: uppercase; letter-spacing: .03em;
	color: var(--mb-muted); margin-bottom: 4px;
}
.epc-mb-field .form-control,
.epc-mb-field select,
.epc-mb-field textarea {
	border-radius: 10px; border-color: var(--mb-border); min-height: 40px;
}
.epc-mb-field textarea.epc-mb-html-body {
	font-family: 'IBM Plex Mono', Consolas, monospace; font-size: 12px; min-height: 180px;
}
.epc-mb-vars {
	display: flex; flex-wrap: wrap; gap: 6px; margin-top: 6px;
}
.epc-mb-vars code {
	background: #eef5f3; border: 1px solid var(--mb-border);
	border-radius: 6px; padding: 2px 7px; font-size: 11px; color: var(--mb-teal-deep);
}

.epc-mb-actions {
	display: flex; flex-wrap: wrap; gap: 10px; align-items: center;
	padding-top: 8px; border-top: 1px dashed var(--mb-border); margin-top: 8px;
}
.epc-mb-actions .btn-primary {
	background: var(--mb-teal); border-color: var(--mb-teal); font-weight: 700;
	border-radius: 11px; min-height: 44px; padding: 10px 18px;
}
.epc-mb-actions .btn-success {
	background: var(--mb-wa); border-color: var(--mb-wa); font-weight: 700;
	border-radius: 11px; min-height: 44px; padding: 10px 18px;
}
.epc-mb-actions .btn-default { border-radius: 11px; min-height: 44px; font-weight: 600; }

.epc-mb-preview {
	position: sticky; top: 12px;
}
.epc-mb-preview__frame {
	border: 1px solid var(--mb-border); border-radius: 12px;
	background: #e8f0ed; min-height: 420px; overflow: hidden;
}
.epc-mb-preview__frame iframe {
	width: 100%; min-height: 420px; border: 0; background: #fff;
}
.epc-mb-wa-bubble-wrap {
	padding: 18px; min-height: 420px;
	background: linear-gradient(180deg, #d1fae5, #ecfdf5);
}
.epc-mb-wa-bubble {
	max-width: 92%; background: #fff; border-radius: 14px 14px 14px 4px;
	padding: 12px 14px; box-shadow: 0 4px 14px rgba(0,0,0,.06);
	white-space: pre-wrap; font-size: 13px; line-height: 1.45; color: #134e4a;
}
.epc-mb-wa-bubble__meta {
	font-size: 11px; color: #64748b; margin-top: 8px; text-align: right;
}

.epc-mb-history-list { display: flex; flex-direction: column; gap: 10px; }
.epc-mb-campaign {
	display: grid; grid-template-columns: auto 1fr auto; gap: 12px; align-items: center;
	border: 1px solid var(--mb-border); border-radius: 12px; padding: 12px 14px; background: #fff;
}
.epc-mb-campaign__icon {
	width: 40px; height: 40px; border-radius: 11px;
	display: grid; place-items: center; background: #e7f5f2; color: var(--mb-teal);
}
.epc-mb-campaign__icon.is-wa { background: #dcfce7; color: var(--mb-wa); }
.epc-mb-campaign__title { font-weight: 700; font-size: 14px; }
.epc-mb-campaign__meta { font-size: 12px; color: var(--mb-muted); margin-top: 2px; }
.epc-mb-campaign__stats {
	display: flex; gap: 10px; font-family: 'IBM Plex Mono', ui-monospace, monospace; font-size: 12px;
}
.epc-mb-campaign__stats .ok { color: #15803d; font-weight: 700; }
.epc-mb-campaign__stats .fail { color: #b91c1c; font-weight: 700; }
.epc-mb-status-pill {
	display: inline-block; padding: 3px 9px; border-radius: 999px;
	font-size: 11px; font-weight: 700; background: #eef5f3; color: var(--mb-teal-deep);
}

.epc-mb-guide { padding: 4px; }
.epc-mb-guide__intro {
	padding: 16px; border-radius: 14px; margin-bottom: 14px;
	background: linear-gradient(135deg, #e7f5f2, #fff); border: 1px solid var(--mb-border);
}
.epc-mb-guide-step {
	display: grid; grid-template-columns: 44px 1fr; gap: 12px;
	border: 1px solid var(--mb-border); border-radius: 14px;
	padding: 14px 16px; margin: 0 0 10px; background: #fff;
}
.epc-mb-guide-step__num {
	width: 44px; height: 44px; border-radius: 12px;
	display: grid; place-items: center;
	background: #e7f5f2; color: var(--mb-teal-deep);
	font-family: 'IBM Plex Mono', ui-monospace, monospace; font-weight: 700; font-size: 16px;
}
.epc-mb-guide-step h5 { margin: 0 0 6px; font-size: 15px; font-weight: 700; }
.epc-mb-guide-step p { margin: 0 0 8px; font-size: 13px; line-height: 1.5; color: #334155; }
.epc-mb-guide-compliance {
	border-radius: 14px; padding: 14px 16px; margin-top: 8px;
	background: #fff7ed; border: 1px solid #fed7aa;
}
.epc-mb-guide-compliance ul { margin: 8px 0 0; padding-left: 18px; }
.epc-mb-guide-compliance li { margin-bottom: 4px; font-size: 13px; }

.epc-mb-empty {
	text-align: center; padding: 40px 20px; color: var(--mb-muted);
}
.epc-mb-empty__icon {
	width: 56px; height: 56px; margin: 0 auto 12px; border-radius: 16px;
	display: grid; place-items: center; background: #e7f5f2; color: var(--mb-teal); font-size: 22px;
}
.epc-mb-empty__title { font-size: 17px; font-weight: 700; color: var(--mb-ink); margin-bottom: 6px; }

.epc-mb-wa-links {
	background: #ecfdf5; border: 1px solid #86efac; border-radius: 14px;
	padding: 14px 18px; margin-bottom: 14px;
}
.epc-mb-wa-links h4 { margin: 0 0 10px; }
.epc-mb-wa-links .btn { margin: 0 6px 6px 0; border-radius: 999px; }

.epc-mb-hub .alert { border-radius: 12px; }

@keyframes epc-mb-fade {
	from { opacity: 0; transform: translateY(6px); }
	to { opacity: 1; transform: translateY(0); }
}
@keyframes epc-mb-pop {
	0% { transform: scale(1); }
	40% { transform: scale(1.06); }
	100% { transform: scale(1); }
}
