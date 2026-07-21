<?php
/** Social Media Hub — external CSS (loaded via epc_cp_page_assets.php) */
header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=3600');
?>
.epc-social-hub { --epc-social-cyan: #00ccff; --epc-social-mint: #00ffb2; --epc-social-navy: #050d1a; }
.epc-social-hub .epc-social-tabs { display: flex; flex-wrap: wrap; gap: 8px; margin: 16px 0 20px; }
.epc-social-hub .epc-social-tabs .btn { border-radius: 999px; }
.epc-social-hub .epc-social-tabs .btn-primary { background: linear-gradient(135deg, #00ccff, #00ffb2); border: none; color: #050d1a; font-weight: 700; }
.epc-social-hub .epc-social-intro { background: linear-gradient(135deg, #f0f9ff, #ecfdf5); border: 1px solid #bae6fd; border-radius: 12px; padding: 16px 18px; margin-bottom: 18px; }
.epc-social-hub .epc-social-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 16px; }
.epc-social-hub .epc-social-post { border: 1px solid #e2e8f0; border-radius: 14px; overflow: hidden; background: #fff; box-shadow: 0 4px 14px rgba(15, 23, 42, 0.06); transition: transform .2s, box-shadow .2s; }
.epc-social-hub .epc-social-post:hover { transform: translateY(-2px); box-shadow: 0 12px 28px rgba(15, 23, 42, 0.1); }
.epc-social-hub .epc-social-post__head { padding: 12px 14px; background: #f8fafc; border-bottom: 1px solid #e2e8f0; font-weight: 700; font-size: 13px; }
.epc-social-hub .epc-social-post__body { padding: 14px; font-size: 13px; line-height: 1.55; white-space: pre-line; color: #334155; max-height: 220px; overflow-y: auto; }
.epc-social-hub .epc-social-post__bar { display: flex; justify-content: space-between; align-items: center; padding: 10px 14px; background: rgba(0, 204, 255, 0.06); border-top: 1px solid #e2e8f0; }
.epc-social-hub .epc-social-tag { display: inline-block; padding: 4px 10px; margin: 3px; border-radius: 999px; background: rgba(0, 204, 255, 0.1); border: 1px solid rgba(0, 204, 255, 0.25); font-size: 11px; color: #0369a1; cursor: pointer; }
.epc-social-hub .epc-social-tag:hover { background: rgba(0, 204, 255, 0.18); }
.epc-social-hub .epc-social-account-card { border: 1px solid #e2e8f0; border-radius: 12px; padding: 16px; margin-bottom: 12px; background: #fff; }
.epc-social-hub .epc-social-account-card__head { display: flex; align-items: center; gap: 10px; margin-bottom: 12px; }
.epc-social-hub .epc-social-platform-icon { width: 36px; height: 36px; border-radius: 8px; display: flex; align-items: center; justify-content: center; color: #fff; font-size: 16px; }
.epc-social-hub .epc-social-ai-card { border-left: 4px solid var(--epc-social-cyan); background: #f8fafc; border-radius: 0 10px 10px 0; padding: 14px 16px; margin-bottom: 10px; }
.epc-social-hub .epc-social-specs dt { font-weight: 600; color: #64748b; font-size: 12px; margin-top: 8px; }
.epc-social-hub .epc-social-specs dd { margin: 2px 0 0; font-size: 13px; }
.epc-social-hub .epc-social-guide-step { border-left: 4px solid #0ea5e9; padding: 12px 16px; margin: 12px 0; background: #f8fafc; border-radius: 0 8px 8px 0; }
.epc-social-hub .epc-social-kpi { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 10px; margin-bottom: 16px; }
.epc-social-hub .epc-social-kpi__item { background: #fff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 12px; text-align: center; }
.epc-social-hub .epc-social-kpi__val { font-size: 22px; font-weight: 800; color: #0f766e; }
.epc-social-hub .epc-social-kpi__label { font-size: 11px; color: #64748b; text-transform: uppercase; letter-spacing: .5px; }

/* Marketer hero — subtle attention cues (cyan/mint CP theme) */
@keyframes epc-social-hero-glow {
	0%, 100% { box-shadow: 0 10px 28px rgba(0, 204, 255, 0.22), 0 0 0 0 rgba(0, 255, 178, 0); }
	50% { box-shadow: 0 12px 34px rgba(0, 204, 255, 0.32), 0 0 20px 2px rgba(0, 255, 178, 0.14); }
}
@keyframes epc-social-title-underline {
	0%, 100% { transform: scaleX(0.4); opacity: 0.55; }
	50% { transform: scaleX(1); opacity: 1; }
}
@keyframes epc-social-shimmer {
	0% { background-position: 200% 0; }
	100% { background-position: -200% 0; }
}
@keyframes epc-social-bounce-down {
	0%, 100% { transform: translateY(0); }
	50% { transform: translateY(5px); }
}
@keyframes epc-social-tabs-pulse {
	0%, 100% { box-shadow: none; opacity: 0; }
	50% { box-shadow: 0 0 0 2px rgba(0, 204, 255, 0.22); opacity: 1; }
}
@keyframes epc-social-tab-nudge {
	0%, 100% { box-shadow: none; }
	50% { box-shadow: 0 0 0 2px rgba(0, 204, 255, 0.28); }
}

.epc-social-hub .epc-scp-dashboard__hero {
	animation: epc-social-hero-glow 3.6s ease-in-out infinite;
}
.epc-social-hub .epc-scp-dashboard__title,
.epc-social-hub .epc-social-hub__hero-tenant h4 {
	position: relative;
	display: inline-block;
}
.epc-social-hub .epc-scp-dashboard__title::after,
.epc-social-hub .epc-social-hub__hero-tenant h4::after {
	content: '';
	display: block;
	height: 3px;
	margin-top: 6px;
	border-radius: 2px;
	background: linear-gradient(90deg, var(--epc-social-cyan), var(--epc-social-mint), var(--epc-social-cyan));
	background-size: 200% 100%;
	animation: epc-social-title-underline 2.8s ease-in-out infinite, epc-social-shimmer 4.2s linear infinite;
	transform-origin: left center;
}

.epc-social-hub .epc-social-explore-hint {
	display: flex;
	align-items: center;
	justify-content: center;
	flex-wrap: wrap;
	gap: 10px;
	padding: 10px 16px;
	margin: 0 0 14px;
	border-radius: 10px;
	background: linear-gradient(135deg, rgba(0, 204, 255, 0.07), rgba(0, 255, 178, 0.07));
	border: 1px dashed rgba(0, 204, 255, 0.35);
	font-size: 13px;
	font-weight: 600;
	color: #0369a1;
}
.epc-social-hub .epc-social-explore-hint__icon {
	animation: epc-social-bounce-down 1.7s ease-in-out infinite;
	color: var(--epc-social-cyan);
	font-size: 16px;
}
.epc-social-hub .epc-social-explore-hint__text {
	background: linear-gradient(90deg, #0369a1, #0d9488, #00ccff, #0369a1);
	background-size: 220% auto;
	-webkit-background-clip: text;
	background-clip: text;
	-webkit-text-fill-color: transparent;
	animation: epc-social-shimmer 3.2s linear infinite;
}

.epc-social-hub .epc-social-tabs {
	position: relative;
}
.epc-social-hub .epc-social-tabs::before {
	content: '';
	position: absolute;
	inset: -4px;
	border-radius: 14px;
	pointer-events: none;
	animation: epc-social-tabs-pulse 3.2s ease-in-out infinite;
}
.epc-social-hub .epc-social-tabs .btn-default {
	animation: epc-social-tab-nudge 2.6s ease-in-out infinite;
}
.epc-social-hub .epc-social-tabs .btn-primary {
	animation: none;
	box-shadow: 0 0 0 2px rgba(0, 255, 178, 0.35);
}

@media (prefers-reduced-motion: reduce) {
	.epc-social-hub .epc-scp-dashboard__hero,
	.epc-social-hub .epc-scp-dashboard__title::after,
	.epc-social-hub .epc-social-hub__hero-tenant h4::after,
	.epc-social-hub .epc-social-explore-hint__icon,
	.epc-social-hub .epc-social-explore-hint__text,
	.epc-social-hub .epc-social-tabs::before,
	.epc-social-hub .epc-social-tabs .btn-default {
		animation: none !important;
	}
}

@media (max-width: 768px) {
	.epc-social-hub .epc-social-grid { grid-template-columns: 1fr; }
	.epc-social-hub .epc-social-tabs .btn { font-size: 11px; padding: 6px 10px; }
	.epc-social-hub .epc-social-video-grid--reels { grid-template-columns: 1fr 1fr; }
}

.epc-social-hub .epc-social-video-grid {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
	gap: 14px;
}
.epc-social-hub .epc-social-video-grid--reels {
	grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
}
.epc-social-hub .epc-social-video {
	border: 1px solid #e2e8f0;
	border-radius: 12px;
	overflow: hidden;
	background: #fff;
	box-shadow: 0 4px 14px rgba(15, 23, 42, 0.06);
}
.epc-social-hub .epc-social-video__player {
	background: #0f172a;
}
.epc-social-hub .epc-social-video video {
	display: block;
	width: 100%;
	max-height: 220px;
	background: #0f172a;
}
.epc-social-hub .epc-social-video--vertical video {
	max-height: 320px;
	object-fit: contain;
}
.epc-social-hub .epc-social-video__meta {
	padding: 10px 12px 12px;
}
.epc-social-hub .epc-social-video__actions {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
}
