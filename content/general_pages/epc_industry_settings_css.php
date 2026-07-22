<?php
/**
 * Industry settings — external CSS (CP base-href safe).
 */
header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=3600');
header('ETag: "epc-inds-ui3"');
?>
.epc-inds {
	--inds-ink: #0f172a;
	--inds-muted: #64748b;
	--inds-line: #e2e8f0;
	--inds-teal: #0f766e;
	--inds-teal-deep: #115e59;
	--inds-sky: #0369a1;
	--inds-soft: #f0fdfa;
	--inds-card: #fff;
	position: relative;
	margin-bottom: 72px;
}
.epc-inds::before {
	content: "";
	position: absolute;
	inset: -6px -2px auto;
	height: 200px;
	background:
		radial-gradient(ellipse 70% 55% at 8% 0%, rgba(15, 118, 110, 0.12), transparent 60%),
		radial-gradient(ellipse 45% 45% at 92% 8%, rgba(3, 105, 161, 0.1), transparent 55%),
		linear-gradient(180deg, #f8fafc 0%, transparent 100%);
	pointer-events: none;
	z-index: 0;
	border-radius: 16px;
}
.epc-inds > * { position: relative; z-index: 1; }

.epc-inds-brand {
	display: flex;
	flex-wrap: wrap;
	align-items: flex-end;
	justify-content: space-between;
	gap: 14px;
	margin: 0 0 16px;
	padding: 18px 20px;
	border-radius: 16px;
	color: #fff;
	background: linear-gradient(135deg, rgba(15, 118, 110, 0.96) 0%, rgba(17, 94, 89, 0.94) 52%, rgba(3, 105, 161, 0.9) 100%);
	box-shadow: 0 16px 36px rgba(15, 118, 110, 0.2);
	animation: epc-inds-rise .5s ease-out both;
}
.epc-inds-brand__mark {
	font-size: 11px;
	font-weight: 700;
	letter-spacing: .14em;
	text-transform: uppercase;
	opacity: .85;
}
.epc-inds-brand h2 {
	margin: 4px 0 6px;
	color: #fff;
	font-size: 26px;
	font-weight: 800;
	letter-spacing: -.02em;
	line-height: 1.15;
}
.epc-inds-brand p {
	margin: 0;
	max-width: 640px;
	font-size: 13px;
	line-height: 1.5;
	opacity: .92;
}
.epc-inds-brand__actions .btn {
	margin: 0 0 4px 6px;
	border-radius: 8px;
	font-weight: 600;
}
.epc-inds-brand__actions .btn-primary {
	background: #fff;
	border-color: #fff;
	color: var(--inds-teal-deep);
}
.epc-inds-brand__actions .btn-default {
	background: rgba(255,255,255,.12);
	border-color: rgba(255,255,255,.35);
	color: #fff;
}

.epc-inds-stats {
	display: grid;
	grid-template-columns: repeat(4, minmax(0, 1fr));
	gap: 10px;
	margin: 0 0 16px;
}
.epc-inds-stat {
	background: var(--inds-card);
	border: 1px solid var(--inds-line);
	border-radius: 12px;
	padding: 12px 14px;
	animation: epc-inds-rise .55s ease-out both;
}
.epc-inds-stat:nth-child(2) { animation-delay: .05s; }
.epc-inds-stat:nth-child(3) { animation-delay: .1s; }
.epc-inds-stat:nth-child(4) { animation-delay: .15s; }
.epc-inds-stat__val {
	font-size: 18px;
	font-weight: 800;
	color: var(--inds-teal-deep);
	line-height: 1.2;
	word-break: break-word;
}
.epc-inds-stat__lbl {
	margin-top: 3px;
	font-size: 11px;
	text-transform: uppercase;
	letter-spacing: .05em;
	color: var(--inds-muted);
}

.epc-inds-nav {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
	margin: 0 0 16px;
	padding: 10px 12px;
	background: rgba(255,255,255,.92);
	border: 1px solid var(--inds-line);
	border-radius: 12px;
	position: sticky;
	top: 0;
	z-index: 20;
	backdrop-filter: blur(6px);
}
.epc-inds-nav a {
	display: inline-flex;
	align-items: center;
	gap: 6px;
	padding: 7px 12px;
	border-radius: 999px;
	border: 1px solid var(--inds-line);
	background: #fff;
	color: #334155;
	font-size: 12px;
	font-weight: 650;
	text-decoration: none;
	transition: background .15s, border-color .15s, color .15s;
}
.epc-inds-nav a:hover,
.epc-inds-nav a.is-active {
	background: var(--inds-soft);
	border-color: #99f6e4;
	color: var(--inds-teal-deep);
}

.epc-inds-section {
	scroll-margin-top: 64px;
	margin: 0 0 16px;
	background: var(--inds-card);
	border: 1px solid var(--inds-line);
	border-radius: 14px;
	box-shadow: 0 4px 14px rgba(15, 23, 42, 0.04);
	overflow: hidden;
	animation: epc-inds-rise .45s ease-out both;
}
.epc-inds-section__head {
	display: flex;
	align-items: flex-start;
	justify-content: space-between;
	gap: 12px;
	padding: 14px 16px;
	border-bottom: 1px solid var(--inds-line);
	background: linear-gradient(180deg, #f8fafc, #fff);
}
.epc-inds-section__head h3 {
	margin: 0 0 4px;
	font-size: 16px;
	font-weight: 750;
	color: var(--inds-ink);
}
.epc-inds-section__head h3 i { color: var(--inds-teal); margin-right: 6px; }
.epc-inds-section__head p {
	margin: 0;
	font-size: 12px;
	color: var(--inds-muted);
	line-height: 1.4;
	max-width: 640px;
}
.epc-inds-section__body { padding: 16px; }

.epc-inds .form-group { margin-bottom: 14px; }
.epc-inds .form-group > label {
	display: block;
	margin-bottom: 5px;
	font-size: 12px;
	font-weight: 700;
	color: #334155;
}
.epc-inds .help-block {
	margin-top: 6px;
	font-size: 12px;
	color: var(--inds-muted);
	line-height: 1.45;
}
.epc-inds .form-control {
	border-radius: 8px;
	border-color: #cbd5e1;
}
.epc-inds .form-control:focus {
	border-color: var(--inds-teal);
	box-shadow: 0 0 0 3px rgba(15, 118, 110, 0.14);
}

.epc-inds .epc-portal-settings__styles { gap: 10px; }
.epc-inds .epc-portal-settings__style {
	border-radius: 12px;
	transition: transform .15s, border-color .15s, box-shadow .15s;
}
.epc-inds .epc-portal-settings__style:hover { transform: translateY(-2px); }
.epc-inds .epc-portal-settings__style.is-selected {
	border-color: var(--inds-teal);
	box-shadow: 0 0 0 3px rgba(15, 118, 110, 0.14);
}
.epc-inds .epc-portal-settings__pack {
	border-radius: 12px;
	transition: border-color .15s, box-shadow .15s, transform .15s;
}
.epc-inds .epc-portal-settings__pack:hover {
	border-color: #99f6e4;
	transform: translateY(-1px);
}
.epc-inds .epc-portal-settings__pack-icon {
	background: var(--inds-soft);
	color: var(--inds-teal-deep);
}

.epc-inds-presets {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
	margin-bottom: 12px;
}
.epc-inds-presets .btn {
	border-radius: 999px;
	font-weight: 600;
}

.epc-inds-modules {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
	gap: 10px;
}
.epc-inds-modules .epc-portal-settings__pack { margin: 0; height: 100%; }

.epc-inds-sidebar-toolbar {
	display: flex;
	flex-wrap: wrap;
	gap: 10px;
	align-items: center;
	margin-bottom: 12px;
}
.epc-inds-sidebar-search {
	flex: 1 1 220px;
	position: relative;
}
.epc-inds-sidebar-search i {
	position: absolute;
	left: 12px;
	top: 50%;
	transform: translateY(-50%);
	color: var(--inds-muted);
}
.epc-inds-sidebar-search input {
	width: 100%;
	height: 38px;
	padding: 0 12px 0 34px;
	border: 1px solid var(--inds-line);
	border-radius: 8px;
	font-size: 13px;
}
.epc-inds-sidebar-search input:focus {
	outline: none;
	border-color: var(--inds-teal);
	box-shadow: 0 0 0 3px rgba(15, 118, 110, 0.14);
}

.epc-inds-acc {
	border: 1px solid var(--inds-line);
	border-radius: 12px;
	margin-bottom: 10px;
	background: #fff;
	overflow: hidden;
}
.epc-inds-acc.is-hidden { display: none; }
.epc-inds-acc__toggle {
	width: 100%;
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 12px 14px;
	border: 0;
	background: #f8fafc;
	text-align: left;
	cursor: pointer;
}
.epc-inds-acc__toggle:hover { background: var(--inds-soft); }
.epc-inds-acc__toggle .epc-inds-acc__check { margin: 0; }
.epc-inds-acc__meta { flex: 1 1 auto; min-width: 0; }
.epc-inds-acc__meta strong {
	display: block;
	font-size: 13px;
	color: var(--inds-ink);
}
.epc-inds-acc__meta small {
	display: block;
	font-size: 11px;
	color: var(--inds-muted);
}
.epc-inds-acc__count {
	font-size: 11px;
	font-weight: 700;
	color: var(--inds-teal-deep);
	background: var(--inds-soft);
	border: 1px solid #99f6e4;
	border-radius: 999px;
	padding: 3px 8px;
	white-space: nowrap;
}
.epc-inds-acc__chev {
	color: var(--inds-muted);
	transition: transform .2s;
}
.epc-inds-acc.is-open .epc-inds-acc__chev { transform: rotate(180deg); }
.epc-inds-acc__body {
	display: none;
	padding: 8px 14px 12px 40px;
	border-top: 1px solid var(--inds-line);
	max-height: 220px;
	overflow: auto;
}
.epc-inds-acc.is-open .epc-inds-acc__body { display: block; }
.epc-inds-acc__body label {
	display: flex;
	align-items: center;
	gap: 8px;
	font-weight: 500;
	font-size: 12px;
	color: #334155;
	padding: 4px 0;
	margin: 0;
}
.epc-inds-acc__body label.is-hidden { display: none; }
.epc-inds-acc__body .epc-inds-item-url {
	margin-left: auto;
	font-size: 10px;
	color: #94a3b8;
	max-width: 40%;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.epc-inds-sticky {
	position: fixed;
	left: 0;
	right: 0;
	bottom: 0;
	z-index: 40;
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 10px;
	padding: 12px 18px;
	background: rgba(15, 23, 42, 0.92);
	color: #fff;
	backdrop-filter: blur(8px);
	box-shadow: 0 -8px 24px rgba(15, 23, 42, 0.18);
}
.epc-inds-sticky .btn-primary {
	background: #14b8a6;
	border-color: #14b8a6;
	font-weight: 700;
	border-radius: 8px;
}
.epc-inds-sticky .btn-default {
	background: rgba(255,255,255,.1);
	border-color: rgba(255,255,255,.25);
	color: #fff;
	border-radius: 8px;
}
.epc-inds-sticky__msg {
	font-size: 12px;
	opacity: .9;
	margin-left: auto;
}

.epc-inds .epc-portal-settings__deploy-table > thead > tr > th {
	background: #f8fafc;
	font-size: 11px;
	text-transform: uppercase;
	letter-spacing: .04em;
	color: var(--inds-muted);
	border-bottom: 1px solid var(--inds-line);
}
.epc-inds .table > tbody > tr > td { vertical-align: middle; font-size: 13px; }

@keyframes epc-inds-rise {
	from { opacity: 0; transform: translateY(8px); }
	to { opacity: 1; transform: translateY(0); }
}

@media (max-width: 900px) {
	.epc-inds-stats { grid-template-columns: repeat(2, minmax(0, 1fr)); }
	.epc-inds-brand h2 { font-size: 22px; }
	.epc-inds-nav { position: static; }
}
@media (max-width: 600px) {
	.epc-inds-stats { grid-template-columns: 1fr; }
	.epc-inds-modules { grid-template-columns: 1fr; }
	.epc-inds-sticky { padding: 10px 12px; }
	.epc-inds-sticky__msg { width: 100%; margin-left: 0; }
}
