<?php
/**
 * Integrations hub + guide — external CSS (CP base href safe).
 */
header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=3600');
$ver = 'inthub3';
header('ETag: "epc-inthub-' . $ver . '"');
?>
.epc-inthub {
	--inthub-teal: #0f766e;
	--inthub-teal-deep: #115e59;
	--inthub-sky: #0284c7;
	--inthub-slate: #0f172a;
	--inthub-muted: #64748b;
	--inthub-line: #e2e8f0;
	--inthub-soft: #f0fdfa;
	--inthub-card: #ffffff;
	position: relative;
}
.epc-inthub::before {
	content: "";
	position: absolute;
	inset: -8px -4px auto -4px;
	height: 220px;
	background:
		radial-gradient(ellipse 80% 60% at 10% 0%, rgba(15, 118, 110, 0.14), transparent 60%),
		radial-gradient(ellipse 50% 50% at 90% 10%, rgba(2, 132, 199, 0.12), transparent 55%),
		linear-gradient(180deg, #f8fafc 0%, transparent 100%);
	pointer-events: none;
	z-index: 0;
	border-radius: 16px;
}
.epc-inthub > * { position: relative; z-index: 1; }

.epc-inthub .epc-inthub-shell {
	margin-bottom: 18px;
}
.epc-inthub .epc-inthub-brand {
	display: flex;
	flex-wrap: wrap;
	align-items: flex-end;
	justify-content: space-between;
	gap: 14px;
	margin: 0 0 18px;
	padding: 18px 20px;
	border-radius: 16px;
	background:
		linear-gradient(135deg, rgba(15, 118, 110, 0.95) 0%, rgba(17, 94, 89, 0.92) 48%, rgba(2, 132, 199, 0.88) 100%);
	color: #fff;
	box-shadow: 0 16px 40px rgba(15, 118, 110, 0.22);
	animation: epc-inthub-rise 0.55s ease-out both;
}
.epc-inthub .epc-inthub-brand__mark {
	font-size: 11px;
	letter-spacing: 0.14em;
	text-transform: uppercase;
	opacity: 0.85;
	font-weight: 700;
}
.epc-inthub .epc-inthub-brand h2 {
	margin: 4px 0 6px;
	color: #fff;
	font-size: 28px;
	font-weight: 800;
	letter-spacing: -0.02em;
	line-height: 1.15;
}
.epc-inthub .epc-inthub-brand p {
	margin: 0;
	max-width: 560px;
	opacity: 0.92;
	font-size: 14px;
	line-height: 1.5;
}
.epc-inthub .epc-inthub-brand__actions .btn {
	margin-left: 6px;
	margin-bottom: 4px;
	border-radius: 8px;
	font-weight: 600;
}
.epc-inthub .epc-inthub-brand__actions .btn-primary {
	background: #fff;
	border-color: #fff;
	color: var(--inthub-teal-deep);
}
.epc-inthub .epc-inthub-brand__actions .btn-default {
	background: rgba(255, 255, 255, 0.12);
	border-color: rgba(255, 255, 255, 0.35);
	color: #fff;
}

.epc-inthub .epc-inthub-stats {
	display: grid;
	grid-template-columns: repeat(3, minmax(0, 1fr));
	gap: 12px;
	margin: 0 0 18px;
}
.epc-inthub .epc-inthub-stat {
	background: var(--inthub-card);
	border: 1px solid var(--inthub-line);
	border-radius: 12px;
	padding: 14px 16px;
	animation: epc-inthub-rise 0.6s ease-out both;
}
.epc-inthub .epc-inthub-stat:nth-child(2) { animation-delay: 0.06s; }
.epc-inthub .epc-inthub-stat:nth-child(3) { animation-delay: 0.12s; }
.epc-inthub .epc-inthub-stat__val {
	font-size: 26px;
	font-weight: 800;
	color: var(--inthub-teal-deep);
	line-height: 1.1;
}
.epc-inthub .epc-inthub-stat__lbl {
	font-size: 11px;
	text-transform: uppercase;
	letter-spacing: 0.06em;
	color: var(--inthub-muted);
	margin-top: 4px;
}

.epc-inthub .epc-inthub-toolbar {
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 10px;
	margin: 0 0 16px;
	padding: 12px 14px;
	background: rgba(255, 255, 255, 0.9);
	border: 1px solid var(--inthub-line);
	border-radius: 12px;
	backdrop-filter: blur(6px);
}
.epc-inthub .epc-inthub-search {
	flex: 1 1 220px;
	position: relative;
}
.epc-inthub .epc-inthub-search i {
	position: absolute;
	left: 12px;
	top: 50%;
	transform: translateY(-50%);
	color: var(--inthub-muted);
}
.epc-inthub .epc-inthub-search input {
	width: 100%;
	height: 38px;
	padding: 0 12px 0 34px;
	border: 1px solid var(--inthub-line);
	border-radius: 8px;
	background: #fff;
	font-size: 13px;
}
.epc-inthub .epc-inthub-search input:focus {
	outline: none;
	border-color: var(--inthub-teal);
	box-shadow: 0 0 0 3px rgba(15, 118, 110, 0.15);
}
.epc-inthub .epc-inthub-chips {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
}
.epc-inthub .epc-inthub-chip {
	border: 1px solid var(--inthub-line);
	background: #fff;
	color: #334155;
	border-radius: 999px;
	padding: 6px 12px;
	font-size: 12px;
	font-weight: 600;
	cursor: pointer;
	transition: background 0.2s, border-color 0.2s, color 0.2s, transform 0.15s;
}
.epc-inthub .epc-inthub-chip:hover { transform: translateY(-1px); }
.epc-inthub .epc-inthub-chip.is-active {
	background: var(--inthub-soft);
	border-color: #99f6e4;
	color: var(--inthub-teal-deep);
}

.epc-inthub .epc-inthub-market {
	display: flex;
	align-items: flex-start;
	gap: 10px;
	margin: 0 0 16px;
	padding: 12px 14px;
	border-radius: 12px;
	background: linear-gradient(90deg, #ecfeff, #f0fdfa);
	border: 1px solid #a5f3fc;
	color: #155e75;
	font-size: 13px;
}
.epc-inthub .epc-inthub-market i { margin-top: 2px; }

.epc-inthub .epc-inthub-section {
	margin: 0 0 22px;
	animation: epc-inthub-rise 0.5s ease-out both;
}
.epc-inthub .epc-inthub-section.is-hidden { display: none; }
.epc-inthub .epc-inthub-section__head {
	display: flex;
	align-items: baseline;
	justify-content: space-between;
	gap: 10px;
	margin: 0 0 12px;
	padding-bottom: 8px;
	border-bottom: 1px solid var(--inthub-line);
}
.epc-inthub .epc-inthub-section__head h3 {
	margin: 0;
	font-size: 16px;
	font-weight: 750;
	color: var(--inthub-slate);
}
.epc-inthub .epc-inthub-section__head h3 i {
	color: var(--inthub-teal);
	margin-right: 6px;
}
.epc-inthub .epc-inthub-section__head span {
	font-size: 12px;
	color: var(--inthub-muted);
}

.epc-inthub .epc-inthub-grid {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
	gap: 14px;
}
.epc-inthub .epc-inthub-card {
	display: flex;
	flex-direction: column;
	min-height: 196px;
	padding: 16px;
	border-radius: 14px;
	background: var(--inthub-card);
	border: 1px solid var(--inthub-line);
	box-shadow: 0 4px 14px rgba(15, 23, 42, 0.04);
	transition: transform 0.2s ease, box-shadow 0.2s ease, border-color 0.2s ease, opacity 0.2s ease;
	animation: epc-inthub-card-in 0.45s ease-out both;
}
.epc-inthub .epc-inthub-card:hover {
	transform: translateY(-3px);
	border-color: #99f6e4;
	box-shadow: 0 14px 28px rgba(15, 118, 110, 0.12);
}
.epc-inthub .epc-inthub-card.is-hidden { display: none; }
.epc-inthub .epc-inthub-card.is-inactive { opacity: 0.72; }
.epc-inthub .epc-inthub-card__top {
	display: flex;
	align-items: flex-start;
	justify-content: space-between;
	gap: 10px;
	margin-bottom: 10px;
}
.epc-inthub .epc-inthub-card__icon {
	width: 44px;
	height: 44px;
	border-radius: 12px;
	display: flex;
	align-items: center;
	justify-content: center;
	color: #fff;
	font-size: 18px;
	box-shadow: inset 0 -2px 0 rgba(0, 0, 0, 0.08);
}
.epc-inthub .epc-inthub-pill {
	display: inline-block;
	padding: 3px 9px;
	border-radius: 999px;
	font-size: 11px;
	font-weight: 700;
	letter-spacing: 0.02em;
}
.epc-inthub .epc-inthub-pill--on {
	background: #dcfce7;
	color: #166534;
}
.epc-inthub .epc-inthub-pill--off {
	background: #f1f5f9;
	color: #64748b;
}
.epc-inthub .epc-inthub-pill--super {
	background: #e0f2fe;
	color: #075985;
	margin-left: 4px;
}
.epc-inthub .epc-inthub-card__title {
	margin: 0 0 6px;
	font-size: 15px;
	font-weight: 750;
	color: var(--inthub-slate);
}
.epc-inthub .epc-inthub-card__blurb {
	margin: 0 0 14px;
	flex: 1 1 auto;
	font-size: 13px;
	line-height: 1.45;
	color: var(--inthub-muted);
}
.epc-inthub .epc-inthub-card__actions {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
	margin-top: auto;
}
.epc-inthub .epc-inthub-card__actions .btn {
	border-radius: 8px;
	font-weight: 600;
}
.epc-inthub .epc-inthub-card__actions .btn-primary {
	background: var(--inthub-teal);
	border-color: var(--inthub-teal);
}
.epc-inthub .epc-inthub-card__actions .btn-primary:hover {
	background: var(--inthub-teal-deep);
	border-color: var(--inthub-teal-deep);
}
.epc-inthub .epc-inthub-card__muted {
	font-size: 12px;
	color: var(--inthub-muted);
	align-self: center;
}

.epc-inthub .epc-inthub-empty {
	display: none;
	padding: 28px 16px;
	text-align: center;
	color: var(--inthub-muted);
	border: 1px dashed var(--inthub-line);
	border-radius: 12px;
	background: #fff;
}
.epc-inthub .epc-inthub-empty.is-visible { display: block; }

.epc-inthub .epc-inthub-playbook {
	display: grid;
	grid-template-columns: repeat(3, minmax(0, 1fr));
	gap: 12px;
	margin: 8px 0 18px;
}
.epc-inthub .epc-inthub-step {
	padding: 16px;
	border-radius: 12px;
	background: #fff;
	border: 1px solid var(--inthub-line);
}
.epc-inthub .epc-inthub-step__n {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	width: 28px;
	height: 28px;
	border-radius: 8px;
	background: var(--inthub-soft);
	color: var(--inthub-teal-deep);
	font-weight: 800;
	font-size: 13px;
	margin-bottom: 8px;
}
.epc-inthub .epc-inthub-step h4 {
	margin: 0 0 6px;
	font-size: 14px;
	font-weight: 750;
	color: var(--inthub-slate);
}
.epc-inthub .epc-inthub-step p {
	margin: 0;
	font-size: 12px;
	line-height: 1.45;
	color: var(--inthub-muted);
}

/* Guide page */
.epc-intguide {
	--inthub-teal: #0f766e;
	--inthub-teal-deep: #115e59;
	--inthub-muted: #64748b;
	--inthub-line: #e2e8f0;
	--inthub-slate: #0f172a;
}
.epc-intguide .epc-intguide-layout {
	display: grid;
	grid-template-columns: 240px minmax(0, 1fr);
	gap: 18px;
	align-items: start;
}
.epc-intguide .epc-intguide-toc {
	position: sticky;
	top: 12px;
	padding: 14px;
	border-radius: 12px;
	background: #fff;
	border: 1px solid var(--inthub-line);
	max-height: calc(100vh - 40px);
	overflow: auto;
}
.epc-intguide .epc-intguide-toc h4 {
	margin: 0 0 10px;
	font-size: 12px;
	text-transform: uppercase;
	letter-spacing: 0.06em;
	color: var(--inthub-muted);
}
.epc-intguide .epc-intguide-toc a {
	display: block;
	padding: 6px 8px;
	border-radius: 6px;
	font-size: 12px;
	color: #334155;
	text-decoration: none;
}
.epc-intguide .epc-intguide-toc a:hover,
.epc-intguide .epc-intguide-toc a.is-active {
	background: #f0fdfa;
	color: var(--inthub-teal-deep);
}
.epc-intguide .epc-intguide-section {
	scroll-margin-top: 16px;
	margin: 0 0 16px;
	padding: 18px;
	border-radius: 14px;
	background: #fff;
	border: 1px solid var(--inthub-line);
}
.epc-intguide .epc-intguide-section h3 {
	margin: 0 0 8px;
	font-size: 18px;
	font-weight: 750;
	color: var(--inthub-slate);
}
.epc-intguide .epc-intguide-section h3 i { color: var(--inthub-teal); margin-right: 6px; }
.epc-intguide .epc-intguide-section .lead {
	margin: 0 0 12px;
	color: var(--inthub-muted);
	font-size: 13px;
}
.epc-intguide .epc-intguide-section ol,
.epc-intguide .epc-intguide-section ul {
	margin: 0 0 12px;
	padding-left: 18px;
	font-size: 13px;
	line-height: 1.55;
	color: #334155;
}
.epc-intguide .epc-intguide-section code {
	background: #f1f5f9;
	padding: 1px 5px;
	border-radius: 4px;
	font-size: 12px;
}
.epc-intguide .epc-intguide-actions { margin-top: 10px; }
.epc-intguide .epc-intguide-actions .btn { margin-right: 6px; margin-bottom: 6px; border-radius: 8px; }

@keyframes epc-inthub-rise {
	from { opacity: 0; transform: translateY(10px); }
	to { opacity: 1; transform: translateY(0); }
}
@keyframes epc-inthub-card-in {
	from { opacity: 0; transform: translateY(8px) scale(0.98); }
	to { opacity: 1; transform: translateY(0) scale(1); }
}

@media (max-width: 900px) {
	.epc-inthub .epc-inthub-stats,
	.epc-inthub .epc-inthub-playbook { grid-template-columns: 1fr; }
	.epc-intguide .epc-intguide-layout { grid-template-columns: 1fr; }
	.epc-intguide .epc-intguide-toc { position: static; max-height: none; }
}
@media (max-width: 600px) {
	.epc-inthub .epc-inthub-brand h2 { font-size: 22px; }
	.epc-inthub .epc-inthub-grid { grid-template-columns: 1fr; }
}
