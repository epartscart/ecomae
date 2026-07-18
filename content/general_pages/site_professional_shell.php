<?php
defined('_ASTEXE_') or die('No access');
?>
<style>
	body {
		background: #f5f7fb;
		color: #1f2937;
	}
	/* Site layout: ~90% width, centered — wider than 1170px but keeps side margins */
	.top-menu-line > .container,
	.logo-line > .container,
	.schearch-line > .container,
	.schearch-line #dp_menu > .container,
	.main-header > .container,
	#sb-site .boxed > .container,
	.epc-home-pro .container,
	.epc-home-banners .container,
	.section-vin > .container,
	.container.epart-front-original-data,
	header .container,
	#footer-widgets > .container,
	footer#footer .container,
	footer.footer .container,
	body > .container,
	.alert .container {
		width: 90% !important;
		max-width: min(1728px, 90vw) !important;
		margin-left: auto !important;
		margin-right: auto !important;
		padding-left: clamp(12px, 1vw, 24px) !important;
		padding-right: clamp(12px, 1vw, 24px) !important;
	}
	@media (min-width: 1400px) {
		.logo-line .table-group {
			grid-template-columns: minmax(300px, 22vw) minmax(160px, 1fr) minmax(200px, 1fr) minmax(150px, 11vw) 90px;
		}
		.epc-parts-result-hero__card {
			flex: 1 1 420px;
			max-width: 520px;
		}
		.epc-part-search-layout > #filter_div.col-md-3 {
			flex: 0 0 22%;
			max-width: 22%;
			width: 22%;
		}
		.epc-part-search-layout > .epc-part-search-main.col-md-9 {
			flex: 1 1 0;
			max-width: 78%;
			min-width: 0;
			width: 78%;
		}
	}
	#sb-site,
	.boxed,
	#Container,
	.mainContainer,
	#right_col,
	#left_col,
	.panel,
	.panel-body,
	.panel-default,
	.table,
	.table td,
	.table th,
	.dropdown-menu,
	.modal-content,
	.well,
	.alert,
	.breadcrumb,
	.news_box,
	.product_div_tile,
	.product_div_list_photo,
	.manufacturers-index,
	.manufacturer-browse,
	.epc-brand-browse,
	.epc-parts-stock-brands,
	.epart-front-original-data {
		color: #1f2937;
	}
	#Container h1,
	#Container h2,
	#Container h3,
	#Container h4,
	#right_col h1,
	#right_col h2,
	#right_col h3,
	#right_col h4,
	.mainContainer h1,
	.mainContainer h2,
	.mainContainer h3,
	.mainContainer h4,
	.section-title,
	.panel-title,
	.page-title {
		color: #0f172a !important;
	}
	#Container p,
	#Container div,
	#right_col p,
	#right_col div,
	.mainContainer p,
	.mainContainer div,
	.panel,
	.panel p,
	.panel div,
	.table,
	.table a,
	.dropdown-menu,
	.dropdown-menu a {
		color: #1f2937;
	}
	#Container a,
	#right_col a,
	.mainContainer a,
	.panel a {
		color: #1d4ed8;
	}
	#Container a:hover,
	#right_col a:hover,
	.mainContainer a:hover,
	.panel a:hover {
		color: #ef4444;
	}
	.form-control,
	input,
	select,
	textarea {
		background-color: #fff;
		color: #111827 !important;
	}
	.btn-primary,
	.btn-ar.btn-primary,
	button.btn-primary {
		background: #ef4444 !important;
		border-color: #ef4444 !important;
		color: #fff !important;
	}
	.btn-default,
	.btn-ar.btn-default {
		background: #fff !important;
		border-color: #d7dee9 !important;
		color: #1f2937 !important;
	}
	.epc-home-pro,
	.epc-home-pro *,
	.top-menu-line,
	.top-menu-line *,
	.schearch-line,
	.schearch-line *,
	.header-box-mobile .navbar,
	.header-box-mobile .navbar *,
	.mobile-search-div,
	.mobile-search-div *,
	#footer-widgets,
	#footer-widgets *,
	footer#footer,
	footer#footer * {
		color: inherit;
	}
	.top-menu-line {
		background: linear-gradient(135deg, #090f1d 0%, #111827 54%, #1f2937 100%) !important;
		border-bottom: 1px solid rgba(255, 255, 255, .08);
		box-shadow: 0 10px 28px rgba(15, 23, 42, .16);
	}
	.top-menu-line table {
		width: 100%;
	}
	.top-menu-line .navbar,
	.top-menu-line .navbar-default {
		background: transparent;
		border: 0;
		margin-bottom: 0;
		min-height: 42px;
	}
	.top-menu-line a,
	.top-menu-line .navbar-default .navbar-nav > li > a,
	.top-menu-line .new-header-user-box a,
	.top-menu-line .new-header-user-box span {
		color: rgba(255, 255, 255, .84) !important;
		font-weight: 700;
	}
	.top-menu-line a:hover,
	.top-menu-line .navbar-default .navbar-nav > li > a:hover,
	.top-menu-line .new-header-user-box a:hover {
		color: #fff !important;
	}
	.top-menu-line .dropdown-menu,
	.top-menu-line .dropdown-menu *,
	.top-menu-line .navbar-nav .dropdown-menu,
	.top-menu-line .navbar-nav .dropdown-menu * {
		background-color: #fff !important;
		color: #1f2937 !important;
		text-shadow: none !important;
	}
	.top-menu-line .dropdown-menu {
		border: 1px solid #e2e8f0;
		border-radius: 0 0 12px 12px;
		box-shadow: 0 18px 42px rgba(15, 23, 42, .18);
		padding: 8px 0;
	}
	.top-menu-line .dropdown-menu > li > a,
	.top-menu-line .navbar-nav .dropdown-menu > li > a {
		background: #fff !important;
		color: #1f2937 !important;
		display: block;
		font-weight: 700;
		padding: 9px 14px;
		white-space: nowrap;
	}
	.top-menu-line .dropdown-menu > li > a:hover,
	.top-menu-line .dropdown-menu > li > a:focus,
	.top-menu-line .navbar-nav .dropdown-menu > li > a:hover,
	.top-menu-line .navbar-nav .dropdown-menu > li > a:focus {
		background: #f8fafc !important;
		color: #ef4444 !important;
	}
	.dropdown-login-box,
	.dropdown-login-box *,
	.login_form,
	.login_form * {
		color: #1f2937 !important;
		text-shadow: none !important;
	}
	.dropdown-login-box,
	.dropdown-login-box .login_form,
	.dropdown-login-box .panel-body,
	.dropdown-login-box .tab-content,
	.dropdown-login-box .tab-pane,
	.dropdown-login-box .no-auth {
		background: #fff !important;
	}
	.dropdown-login-box .panel-heading,
	.login_form .panel-heading {
		background: #fff !important;
		color: #111827 !important;
		font-size: 15px;
		font-weight: 900;
		letter-spacing: .01em;
		text-align: center;
	}
	.dropdown-login-box .panel-heading:after,
	.login_form .panel-heading:after {
		background: #e5e7eb;
		content: "";
		display: block;
		height: 1px;
		margin: 12px auto 0;
		width: 120px;
	}
	.dropdown-login-box label,
	.dropdown-login-box .checkbox label,
	.dropdown-login-box .form-control,
	.dropdown-login-box .input-group-addon,
	.login_form label,
	.login_form .checkbox label,
	.login_form .form-control,
	.login_form .input-group-addon {
		color: #1f2937 !important;
	}
	.dropdown-login-box .input-group-addon,
	.login_form .input-group-addon {
		background: #f8fafc !important;
		border-color: #ef4444 !important;
	}
	.dropdown-login-box .nav-tabs > li > a,
	.login_form .nav-tabs > li > a,
	.dropdown-login-box .btn-default,
	.login_form .btn-default {
		background: #fff !important;
		color: #111827 !important;
		font-weight: 800;
	}
	.new-header-user-box {
		border-left: 1px solid rgba(255, 255, 255, .1);
		padding-left: 11px;
		padding-right: 8px;
	}
	.new-header-user-box i,
	.top-menu-line .fa {
		color: #ef4444;
	}
	.logo-line {
		background: rgba(255, 255, 255, .96) !important;
		border-bottom: 1px solid #e8edf5;
		box-shadow: 0 12px 32px rgba(15, 23, 42, .08);
	}
	.header-logo img {
		max-height: 76px;
		object-fit: contain;
	}
	.header-logo,
	.logo_min {
		text-decoration: none !important;
	}
	.epc-animated-logo {
		align-items: center;
		display: inline-flex;
		gap: 10px;
		line-height: 1;
		max-width: 100%;
		white-space: nowrap;
	}
	.epc-animated-logo__mark {
		display: inline-flex;
		flex: 0 0 auto;
		height: 62px;
		width: 138px;
	}
	.epc-animated-logo__mark svg {
		display: block;
		height: 100%;
		overflow: visible;
		width: 100%;
	}
	.epc-animated-logo__text {
		color: #dc2626 !important;
		font-family: Arial, Helvetica, sans-serif;
		font-size: 50px;
		font-style: italic;
		font-weight: 900;
		letter-spacing: -.055em;
		text-transform: lowercase;
		transform: skewX(-8deg);
	}
	.epc-logo-speed,
	.epc-logo-cart,
	.epc-logo-handle,
	.epc-logo-basket {
		fill: none;
		stroke: #dc2626;
		stroke-linecap: round;
		stroke-linejoin: round;
	}
	.epc-logo-cart {
		stroke-width: 12;
	}
	.epc-logo-handle,
	.epc-logo-basket {
		stroke-width: 9;
	}
	.epc-logo-speed {
		stroke-width: 8;
		animation: epcLogoSpeed 1.4s ease-in-out infinite;
	}
	.epc-logo-road {
		fill: none;
		stroke: #dc2626;
		stroke-dasharray: 14 12;
		stroke-linecap: round;
		stroke-width: 5;
		animation: epcLogoRoadMove .9s linear infinite;
		opacity: .55;
	}
	.epc-logo-speed--two {
		animation-delay: .15s;
	}
	.epc-logo-speed--three {
		animation-delay: .3s;
	}
	.epc-logo-cart-motion {
		animation: epcLogoCartDrive 1.2s ease-in-out infinite;
		transform-box: fill-box;
		transform-origin: center;
	}
	.epc-logo-gear {
		animation: epcLogoGearSpin 2.4s linear infinite;
		transform-box: fill-box;
		transform-origin: center;
	}
	.epc-logo-gear path,
	.epc-logo-gear circle {
		fill: #dc2626;
	}
	.epc-logo-gear .epc-logo-gear-hole {
		fill: #fff;
	}
	.epc-logo-parts {
		animation: epcLogoPartsBounce 1.6s ease-in-out infinite;
	}
	.epc-logo-piston rect,
	.epc-logo-box {
		fill: #dc2626;
	}
	.epc-logo-piston path {
		fill: none;
		stroke: #fff;
		stroke-linecap: round;
		stroke-width: 3;
	}
	.epc-logo-ring circle,
	.epc-logo-ring path {
		fill: none;
		stroke: #dc2626;
		stroke-linecap: round;
		stroke-width: 5;
	}
	.epc-logo-wheel {
		filter: drop-shadow(0 2px 0 rgba(0, 0, 0, .08));
	}
	.epc-logo-wheel-spin {
		animation: epcLogoWheelRoll .45s linear infinite;
		transform-box: fill-box;
		transform-origin: center;
	}
	.epc-logo-tyre {
		fill: #dc2626;
	}
	.epc-logo-wheel-rim {
		fill: #fff;
	}
	.epc-logo-wheel .epc-logo-wheel-hole {
		fill: #dc2626;
	}
	.epc-logo-wheel-spokes,
	.epc-logo-wheel-tread {
		fill: none;
		stroke: #dc2626;
		stroke-linecap: round;
		stroke-width: 2.4;
	}
	.epc-logo-wheel-tread {
		stroke: #fff;
		stroke-width: 2.8;
	}
	@keyframes epcLogoSpeed {
		0%, 100% {
			opacity: .42;
			transform: translateX(0);
		}
		50% {
			opacity: 1;
			transform: translateX(-10px);
		}
	}
	@keyframes epcLogoGearSpin {
		to {
			transform: rotate(360deg);
		}
	}
	@keyframes epcLogoCartDrive {
		0%, 100% {
			transform: translateX(-4px) translateY(0);
		}
		50% {
			transform: translateX(8px) translateY(2px);
		}
	}
	@keyframes epcLogoWheelRoll {
		to {
			transform: rotate(360deg);
		}
	}
	@keyframes epcLogoRoadMove {
		to {
			stroke-dashoffset: -26;
		}
	}
	@keyframes epcLogoPartsBounce {
		0%, 100% {
			transform: translateY(0);
		}
		50% {
			transform: translateY(-3px);
		}
	}
	.logo_min .epc-animated-logo {
		gap: 5px;
	}
	.logo_min .epc-animated-logo__mark {
		height: 38px;
		width: 84px;
	}
	.logo_min .epc-animated-logo__text {
		font-size: 30px;
	}
	.logo-line {
		background:
			radial-gradient(circle at 12% 20%, rgba(239, 68, 68, .08), transparent 24%),
			linear-gradient(180deg, #ffffff 0%, #f8fafc 100%) !important;
		border-bottom: 1px solid #e8edf5;
		padding: 18px 0 16px;
	}
	.logo-line .table-group {
		align-items: center;
		display: grid !important;
		gap: 16px 24px;
		grid-template-columns: minmax(280px, 32%) minmax(260px, 1fr) minmax(420px, 38%);
		width: 100%;
	}
	.logo-line .table-control {
		display: block !important;
		min-width: 0;
		vertical-align: middle;
	}
	.logo-line .table-control:nth-child(1) {
		grid-column: 1;
		grid-row: 1;
	}
	.logo-line .epc-header-contact-col {
		grid-column: 2;
		grid-row: 1;
	}
	.logo-line .epc-header-right-col {
		grid-column: 3;
		grid-row: 1;
	}
	.epc-header-contact-col {
		display: flex !important;
		flex-direction: column;
		gap: 10px;
		justify-content: center;
		padding: 4px 8px;
	}
	.epc-header-right-col {
		align-items: flex-end;
		display: flex !important;
		flex-direction: column;
		gap: 12px;
		justify-content: center;
	}
	.epc-header-actions-row {
		align-items: center;
		display: flex;
		flex-wrap: wrap;
		gap: 10px;
		justify-content: flex-end;
		width: 100%;
	}
	.header-logo {
		align-items: center;
		background: transparent;
		border: 0;
		border-radius: 0;
		box-shadow: none;
		display: inline-flex !important;
		justify-content: flex-start;
		min-height: 110px;
		overflow: visible;
		padding: 8px 12px 8px 0;
		width: 100%;
	}
	.header-logo .epc-storefront-logo--hub,
	.header-logo .epc-storefront-logo--hub-only {
		min-height: 72px;
	}
	.header-logo .epc-animated-logo,
	.header-logo svg {
		max-width: 100%;
	}
	.header-logo .epc-animated-logo {
		transform: scale(1.18);
		transform-origin: left center;
	}
	.header-logo .epc-animated-logo__mark {
		height: 72px;
		width: 158px;
	}
	.header-logo .epc-animated-logo__text {
		font-size: 56px;
	}
	.geo-point-box,
	.timetable-box {
		align-items: center;
		background: transparent;
		border: 0;
		border-radius: 0;
		box-shadow: none;
		display: flex;
		min-height: 0;
		padding: 0;
		width: 100%;
	}
	.geo-point-box table,
	.timetable-box table {
		margin: 0;
		width: 100%;
	}
	.geo-point-box span,
	.timetable-box span {
		color: #334155;
		display: block;
		font-size: 14px;
		font-weight: 750;
		line-height: 1.45;
	}
	.geo-point-box i,
	.timetable-box i {
		align-items: center;
		color: #ef4444;
		display: inline-flex;
		font-size: 22px;
		height: auto;
		justify-content: center;
		margin-right: 14px;
		width: auto;
	}
	.header-phone-box {
		text-align: right;
		width: 100%;
	}
	.header-phone-box .phone {
		background: transparent;
		border: 0;
		border-radius: 0;
		box-shadow: none;
		color: #0f172a !important;
		display: block;
		font-size: clamp(26px, 2.2vw, 34px);
		font-weight: 900;
		letter-spacing: -.02em;
		line-height: 1.05;
		margin: 0;
		padding: 0;
		text-align: right;
		text-decoration: none !important;
		white-space: nowrap;
	}
	.header-call-box,
	.header-whatsapp-box,
	.header-bulk-upload-box,
	.epc-currency-switcher {
		flex: 0 0 auto;
		margin: 0;
	}
	.header-call-box a,
	.header-whatsapp-box a,
	.header-bulk-upload-box a {
		align-items: center;
		border-radius: 999px;
		box-shadow: 0 10px 22px rgba(15, 23, 42, .12);
		display: inline-flex;
		font-size: 13px;
		font-weight: 800;
		gap: 8px;
		justify-content: center;
		line-height: 1.2;
		min-height: 46px;
		padding: 11px 18px;
		text-decoration: none !important;
		white-space: nowrap;
	}
	.header-call-box a {
		background: #ef4444;
		box-shadow: 0 12px 26px rgba(239, 68, 68, .22);
		color: #fff !important;
	}
	.header-whatsapp-box a {
		background: #16a34a;
		box-shadow: 0 12px 26px rgba(22, 163, 74, .2);
		color: #fff !important;
	}
	.header-whatsapp-box a:hover {
		background: #128c3f;
	}
	.header-bulk-upload-box a {
		background: #2563eb;
		box-shadow: 0 12px 26px rgba(37, 99, 235, .2);
		color: #fff !important;
	}
	.header-bulk-upload-box a:hover {
		background: #1d4ed8;
	}
	.header-call-box a i,
	.header-whatsapp-box a i,
	.header-bulk-upload-box a i {
		font-size: 16px;
	}
	.epc-currency-switcher {
		align-items: center;
		background: #f8fafc;
		border: 1px solid #e2e8f0;
		border-radius: 999px;
		display: inline-flex;
		margin: 0 !important;
		padding: 6px 10px;
	}
	.epc-currency-switcher select {
		background: #fff;
		border: 1px solid #cbd5e1;
		border-radius: 10px;
		color: #0f172a;
		font-size: 14px;
		font-weight: 800;
		height: 38px;
		min-width: 84px;
		padding: 0 10px;
	}
	.epc-currency-note {
		display: none;
	}
	.schearch-line {
		background: linear-gradient(135deg, #111827 0%, #0f172a 100%);
		box-shadow: 0 18px 36px rgba(15, 23, 42, .14);
		padding: 14px 0 16px;
	}
	.schearch-line .row {
		align-items: center;
		display: flex;
		margin-left: 0;
		margin-right: 0;
	}
	.schearch-line .col-sm-7,
	.schearch-line .col-md-8 {
		flex: 1 1 auto;
		min-width: 0;
		padding-left: 0;
		padding-right: 12px;
		width: auto;
	}
	.schearch-line .col-sm-5,
	.schearch-line .col-md-4 {
		flex: 0 0 auto;
		padding-left: 0;
		padding-right: 0;
		width: auto;
	}
	.header-home-btn,
	.header-cat-btn {
		border-radius: 14px !important;
		box-shadow: none !important;
		font-size: 15px !important;
		font-weight: 800;
		min-height: 52px;
		padding: 12px 18px !important;
		transition: transform .2s ease, box-shadow .2s ease;
		vertical-align: middle;
	}
	.header-home-btn {
		background: rgba(255, 255, 255, .1) !important;
		border: 1px solid rgba(255, 255, 255, .14) !important;
		color: #fff !important;
		margin-right: 8px;
	}
	.header-cat-btn {
		background: #ef4444 !important;
		border: 1px solid #ef4444 !important;
		color: #fff !important;
		margin-right: 10px;
	}
	.schearch-line table td:first-child {
		white-space: nowrap;
		width: 1%;
	}
	.header-home-btn:hover,
	.header-cat-btn:hover {
		transform: translateY(-1px);
	}
	#dp_menu {
		background: #ffffff !important;
		border: 1px solid #e2e8f0;
		border-radius: 0 0 22px 22px;
		box-shadow: 0 28px 80px rgba(15, 23, 42, .28);
		left: 0;
		right: 0;
		overflow: auto;
		padding: 18px 0;
	}
	#dp_menu .container {
		background: #fff;
		border-radius: 18px;
		max-width: min(1728px, 90vw) !important;
		overflow: hidden;
		width: 90% !important;
	}
	#dp_menu .vertical-tabs-right {
		background: #0f172a !important;
		border: 1px solid #e2e8f0;
		border-radius: 18px;
		box-shadow: 0 18px 42px rgba(15, 23, 42, .12);
		overflow: hidden;
	}
	#dp_menu .vertical-tab-list {
		background: #0f172a;
		min-width: 260px;
	}
	#dp_menu .vertical-tabs-right .vertical-tab-list ul.nav > li {
		border-bottom: 1px solid rgba(255, 255, 255, .08);
	}
	#dp_menu .vertical-tabs-right .vertical-tab-list ul.nav > li > a {
		color: rgba(255, 255, 255, .86) !important;
		font-weight: 800;
		padding: 12px 14px;
		transition: background .2s ease, color .2s ease;
	}
	#dp_menu .vertical-tabs-right .vertical-tab-list ul.nav > li > a:hover,
	#dp_menu .vertical-tabs-right .vertical-tab-list ul.nav > li.active > a,
	#dp_menu .vertical-tabs-right .vertical-tab-list ul.nav > li.active > a:hover,
	#dp_menu .vertical-tabs-right .vertical-tab-list ul.nav > li.active > a:focus {
		background: #ef4444 !important;
		color: #fff !important;
	}
	#dp_menu .vertical-tabs-right .vertical-tab-list img {
		background: #fff;
		border-radius: 8px;
		padding: 3px;
	}
	#dp_menu .vertical-tabs-right .tab-content {
		background: #ffffff !important;
		padding: 24px 28px !important;
	}
	#dp_menu .box_line {
		background: #f8fafc;
		border: 1px solid #e8edf5;
		border-radius: 14px;
		margin: 0 14px 14px 0;
		padding: 14px;
	}
	#dp_menu .one_line {
		color: #0f172a !important;
		font-size: 16px;
		font-weight: 900;
		line-height: 1.3;
		margin-bottom: 9px;
	}
	#dp_menu .two_line {
		color: #475569 !important;
		font-size: 13px;
		font-weight: 700;
		line-height: 1.35;
	}
	#dp_menu .box_line a:hover {
		color: #ef4444 !important;
		text-decoration: none;
	}
	.fon-catalog {
		background: rgba(15, 23, 42, .62) !important;
		backdrop-filter: blur(2px);
	}
	.header-search-box {
		background: #fff;
		border: 1px solid rgba(255, 255, 255, .22);
		border-radius: 14px;
		box-shadow: 0 14px 32px rgba(0, 0, 0, .16);
		overflow: hidden;
	}
	.epc-header-search {
		display: flex;
		flex-direction: column;
	}
	.epc-header-search__tabs {
		background: #f8fafc;
		border-bottom: 1px solid #e2e8f0;
		display: flex;
		flex-wrap: nowrap;
		gap: 4px;
		overflow-x: auto;
		padding: 6px 8px 0;
		-webkit-overflow-scrolling: touch;
		scrollbar-width: none;
	}
	.epc-header-search__tabs::-webkit-scrollbar {
		display: none;
	}
	.epc-header-search__tab {
		align-items: center;
		background: transparent;
		border: 0;
		border-radius: 10px 10px 0 0;
		color: #64748b;
		cursor: pointer;
		display: inline-flex;
		flex: 0 0 auto;
		font-size: 12px;
		font-weight: 800;
		gap: 6px;
		line-height: 1.1;
		padding: 8px 12px;
		transition: background .18s ease, color .18s ease, box-shadow .18s ease;
		white-space: nowrap;
	}
	.epc-header-search__tab i {
		font-size: 13px;
		opacity: .88;
	}
	.epc-header-search__tab.active {
		background: #fff;
		box-shadow: inset 0 -3px 0 #ef4444;
		color: #0f172a;
	}
	.epc-header-search__tab:hover {
		color: #0f172a;
	}
	.epc-header-search__body {
		background: #fff;
	}
	.epc-header-search .header_search_form_1,
	.epc-header-search .header_search_form_2,
	.epc-header-search .header_search_form_3,
	.epc-header-search .header_search_form_engine,
	.epc-header-search .header_search_form_car {
		display: none !important;
	}
	.epc-header-search[data-active-mode="1"] .header_search_form_1,
	.epc-header-search[data-active-mode="2"] .header_search_form_2,
	.epc-header-search[data-active-mode="3"] .header_search_form_3,
	.epc-header-search[data-active-mode="engine"] .header_search_form_engine,
	.epc-header-search[data-active-mode="car"] .header_search_form_car {
		display: block !important;
	}
	.epc-header-search__tab {
		cursor: pointer;
	}
	.epc-header-search .dropdown {
		display: none !important;
	}
	.epc-header-search__car-link {
		align-items: center;
		background: linear-gradient(135deg, #f8fafc 0%, #eef2ff 100%);
		border-top: 1px solid #e2e8f0;
		color: #0f172a !important;
		display: flex;
		gap: 12px;
		justify-content: space-between;
		min-height: 52px;
		padding: 10px 14px;
		text-decoration: none !important;
		transition: background .18s ease, transform .18s ease;
	}
	.epc-currency-locked {
		cursor: default;
		opacity: .92;
		pointer-events: none;
	}
	.epc-currency-locked .fa-lock {
		font-size: 11px;
		margin-left: 4px;
		opacity: .75;
	}
	.epc-header-search__car-link:hover {
		background: linear-gradient(135deg, #fff7ed 0%, #ffedd5 100%);
		transform: translateY(-1px);
	}
	.epc-header-search__car-copy {
		align-items: center;
		color: #334155;
		display: inline-flex;
		font-size: 14px;
		font-weight: 700;
		gap: 8px;
	}
	.epc-header-search__car-link strong {
		background: #ef4444;
		border-radius: 999px;
		color: #fff;
		font-size: 12px;
		font-weight: 800;
		padding: 8px 14px;
		white-space: nowrap;
	}
	.header-search-box .dropdown-toggle {
		background: #f1f5f9;
		color: #0f172a !important;
	}
	.header-search-box .form-control {
		border: 0 !important;
		box-shadow: none !important;
		font-size: 16px;
		font-weight: 650;
		height: 52px;
		padding-left: 14px;
	}
	.header-search-box .btn-primary,
	.header-search-box .btn-ar {
		background: #ef4444 !important;
		border-color: #ef4444 !important;
		border-radius: 0 14px 14px 0 !important;
		color: #fff !important;
		font-size: 15px !important;
		font-weight: 800;
		height: 52px;
		min-width: 110px;
		padding: 0 20px;
	}
	.menu-box {
		display: flex;
		flex-wrap: nowrap;
		gap: 8px;
		justify-content: flex-end;
		min-width: 0;
		overflow: visible;
	}
	header .schearch-line .menu-box-item {
		flex: 0 0 auto !important;
	}
	header .schearch-line .menu-box-item a,
	.menu-box-item a {
		align-items: center;
		background:
			linear-gradient(180deg, rgba(255, 255, 255, .10), rgba(255, 255, 255, .03)),
			linear-gradient(135deg, #0f172a 0%, #1f2937 100%) !important;
		border: 1px solid rgba(255, 255, 255, .22) !important;
		border-radius: 16px !important;
		box-shadow: 0 12px 28px rgba(15, 23, 42, .20);
		color: #fff !important;
		display: flex !important;
		flex-direction: row;
		font-size: 20px;
		font-weight: 900;
		gap: 0;
		height: 52px !important;
		justify-content: center;
		line-height: 1 !important;
		min-height: 52px;
		min-width: 46px;
		padding: 7px !important;
		position: relative;
		text-decoration: none !important;
		transition: background .2s ease, border-color .2s ease, box-shadow .2s ease, transform .2s ease;
	}
	header .schearch-line .menu-box-item a:after,
	.menu-box-item a:after {
		color: rgba(255, 255, 255, .82);
		content: attr(title);
		display: block;
		display: none;
		font-size: 10px;
		font-weight: 900;
		letter-spacing: .01em;
		line-height: 1.05;
		max-width: 60px;
		overflow: hidden;
		text-align: center;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
	header .schearch-line .menu-box-item a:hover,
	.menu-box-item a:hover {
		background:
			linear-gradient(180deg, rgba(255, 255, 255, .14), rgba(255, 255, 255, .04)),
			linear-gradient(135deg, #ef4444 0%, #b91c1c 100%) !important;
		border-color: rgba(255, 255, 255, .38) !important;
		box-shadow: 0 18px 38px rgba(239, 68, 68, .26);
		color: #fff !important;
		transform: translateY(-2px);
	}
	header .schearch-line .menu-box-icon,
	.menu-box-icon {
		color: #fff !important;
		fill: currentColor !important;
		height: 26px !important;
		max-height: 26px !important;
		max-width: 26px !important;
		stroke: currentColor !important;
		width: 26px !important;
	}
	header .schearch-line .menu-box-icon *,
	.menu-box-icon * {
		fill: currentColor !important;
		stroke: currentColor !important;
	}
	header .schearch-line .menu-box-item .fa,
	.menu-box-item .fa {
		color: #fff !important;
		font-size: 25px !important;
		line-height: 26px !important;
	}
	.menu-box-item #header_cart_items_count {
		background: #ef4444;
		border: 2px solid #0f172a;
		border-radius: 999px;
		color: #fff;
		font-size: 10px;
		font-weight: 950;
		line-height: 1;
		min-width: 18px;
		position: absolute;
		right: -5px;
		padding: 3px 5px;
		text-align: center;
		top: -6px;
		transform: none;
	}
	.header-box-mobile .navbar {
		background: linear-gradient(135deg, #090f1d 0%, #111827 100%) !important;
		border: 0;
	}
	.header-box-mobile .navbar-toggle,
	.header-box-mobile .mobile-box-phone,
	.header-box-mobile .mobile-box-whatsapp,
	.header-box-mobile .mobile-box-bulk-upload {
		color: #fff !important;
	}
	.header-box-mobile .mobile-box-whatsapp,
	.header-box-mobile .mobile-box-bulk-upload {
		align-items: center;
		border-radius: 999px;
		display: inline-flex !important;
		font-size: 12px;
		font-weight: 900;
		gap: 5px;
		margin: 8px 6px 0 0;
		padding: 7px 10px;
		text-decoration: none !important;
		vertical-align: middle;
	}
	.header-box-mobile .mobile-box-whatsapp {
		background: #16a34a;
	}
	.header-box-mobile .mobile-box-bulk-upload {
		background: #2563eb;
	}
	.header-box-mobile .mobile-box-whatsapp:hover,
	.header-box-mobile .mobile-box-whatsapp:focus {
		background: #128c3f;
		color: #fff !important;
		text-decoration: none !important;
	}
	.header-box-mobile .mobile-box-bulk-upload:hover,
	.header-box-mobile .mobile-box-bulk-upload:focus {
		background: #1d4ed8;
		color: #fff !important;
		text-decoration: none !important;
	}
	.mobile-search-div {
		background: #111827;
		padding-bottom: 12px;
	}
	#footer-widgets {
		background:
			radial-gradient(circle at 12% 20%, rgba(239, 68, 68, .2), transparent 28%),
			linear-gradient(135deg, #090f1d 0%, #111827 54%, #1f2937 100%) !important;
		color: rgba(255, 255, 255, .86);
		margin-top: 34px;
		padding: 46px 0 34px;
	}
	#footer-widgets > .container {
		width: 90% !important;
		max-width: min(1728px, 90vw) !important;
	}
	#footer-widgets > .container > .row {
		align-items: flex-start;
		display: flex;
		flex-wrap: wrap;
		gap: 20px 28px;
		margin-left: 0;
		margin-right: 0;
	}
	#footer-widgets > .container > .row > [class*="col-"] {
		float: none;
		padding-left: 0;
		padding-right: 0;
	}
	#footer-widgets > .container > .row > .col-md-7,
	#footer-widgets > .container > .row > .col-md-8 {
		flex: 1 1 58%;
		max-width: none;
		min-width: min(100%, 520px);
		width: auto;
	}
	#footer-widgets > .container > .row > .col-md-4 {
		flex: 1 1 36%;
		max-width: none;
		min-width: min(100%, 320px);
		width: auto;
	}
	#footer-widgets > .container > .row > .col-md-1 {
		display: none !important;
	}
	#footer-widgets > .container > .row .row {
		display: flex;
		flex-wrap: wrap;
		gap: 12px 20px;
		margin-left: 0;
		margin-right: 0;
	}
	#footer-widgets > .container > .row .row > [class*="col-"] {
		flex: 1 1 140px;
		max-width: none;
		min-width: 120px;
		width: auto;
	}
	#footer-widgets,
	#footer-widgets p,
	#footer-widgets div,
	#footer-widgets li {
		color: rgba(255, 255, 255, .86);
		font-size: 14px;
		line-height: 1.65;
	}
	#footer-widgets .row > [class*="col-"] {
		position: relative;
	}
	#footer-widgets .footer-widget-title {
		color: #fff;
		font-size: 17px;
		font-weight: 900;
		letter-spacing: .02em;
		margin-bottom: 16px;
		margin-top: 22px;
		text-transform: uppercase;
	}
	#footer-widgets .module_caption,
	#footer-widgets h1,
	#footer-widgets h2,
	#footer-widgets h3,
	#footer-widgets h4,
	#footer-widgets h5,
	#footer-widgets h6 {
		color: #fff !important;
		font-weight: 900;
	}
	#footer-widgets .module_caption {
		border-bottom: 2px solid #ef4444;
		font-size: 17px;
		letter-spacing: .02em;
		margin-bottom: 12px;
		padding-bottom: 10px;
	}
	#footer-widgets .footer-widget-title:after {
		background: #ef4444;
		border-radius: 999px;
		content: "";
		display: block;
		height: 3px;
		margin-top: 9px;
		width: 38px;
	}
	#footer-widgets a {
		color: rgba(255, 255, 255, .9) !important;
		font-weight: 700;
		text-decoration: none !important;
	}
	#footer-widgets a:hover {
		color: #fff !important;
	}
	#footer-widgets ul,
	#footer-widgets ol {
		padding-left: 0;
	}
	#footer-widgets li {
		list-style: none;
		margin-bottom: 8px;
	}
	#footer-widgets .btn-primary {
		background: #ef4444 !important;
		border-color: #ef4444 !important;
		border-radius: 12px;
		box-shadow: 0 14px 28px rgba(239, 68, 68, .22);
		font-weight: 900;
	}
	.footer_pay_box {
		background: rgba(255, 255, 255, .08);
		border: 1px solid rgba(255, 255, 255, .12);
		border-radius: 10px;
		margin: 4px;
		padding: 6px;
	}
	.epc-footer-locations {
		display: grid;
		gap: 10px;
	}
	.epc-footer-office,
	.epc-footer-global {
		background: rgba(255, 255, 255, .06);
		border: 1px solid rgba(255, 255, 255, .1);
		border-radius: 12px;
		padding: 12px;
	}
	.epc-footer-office strong,
	.epc-footer-global strong {
		color: #fff;
		display: block;
		font-size: 14px;
		font-weight: 950;
		margin-bottom: 6px;
	}
	.epc-footer-office strong i,
	.epc-footer-global strong i {
		color: #ef4444;
		margin-right: 5px;
	}
	.epc-footer-office span,
	.epc-footer-global span,
	.epc-footer-global small {
		color: rgba(255, 255, 255, .82);
		display: block;
		font-size: 13px;
		line-height: 1.55;
	}
	.epc-footer-global small {
		color: rgba(255, 255, 255, .68);
		margin-top: 5px;
	}
	.epc-footer-location-list {
		display: grid;
		gap: 8px;
		margin-top: 8px;
	}
	.epc-footer-location-label {
		color: rgba(255, 255, 255, .72);
		display: block;
		font-size: 12px;
		font-weight: 800;
		margin: 8px 0 5px;
	}
	.epc-footer-location-select {
		background: rgba(255, 255, 255, .95);
		border: 1px solid rgba(255, 255, 255, .18);
		border-radius: 9px;
		color: #0f172a;
		font-size: 12px;
		font-weight: 800;
		height: 34px;
		max-width: 100%;
		padding: 5px 8px;
		width: 100%;
	}
	.epc-footer-location-item {
		background: rgba(15, 23, 42, .22);
		border: 1px solid rgba(255, 255, 255, .08);
		border-radius: 10px;
		color: rgba(255, 255, 255, .78);
		font-size: 12px;
		line-height: 1.5;
		padding: 8px 9px;
	}
	.epc-footer-map-link {
		background: rgba(239, 68, 68, .18);
		border: 1px solid rgba(248, 113, 113, .28);
		border-radius: 999px;
		color: #fff !important;
		display: inline-flex;
		font-size: 12px;
		font-weight: 900;
		gap: 6px;
		margin-top: 8px;
		padding: 6px 10px;
	}
	footer#footer {
		background: #070b14 !important;
		border-top: 1px solid rgba(255, 255, 255, .08);
		color: rgba(255, 255, 255, .68);
		min-height: 54px;
		padding: 16px 0;
		text-align: center;
	}
	footer#footer p {
		color: rgba(255, 255, 255, .68);
		font-weight: 700;
		margin: 0 auto;
		max-width: min(1728px, 90vw);
		padding-left: clamp(12px, 1vw, 24px);
		padding-right: clamp(12px, 1vw, 24px);
		width: 90%;
	}
	#back-top a {
		background: #ef4444;
		border-radius: 999px;
		box-shadow: 0 12px 28px rgba(239, 68, 68, .28);
		color: #fff;
	}
	#Container .section-title {
		border-bottom: 1px solid #e2e8f0 !important;
		color: #0f172a !important;
		font-size: 26px;
		font-weight: 900;
		letter-spacing: -.02em;
		margin-bottom: 16px;
		margin-top: 24px;
		padding-bottom: 11px;
		position: relative;
	}
	#Container .section-title:after {
		background: linear-gradient(135deg, #ef4444, #2563eb);
		border-radius: 999px;
		bottom: -2px;
		content: "";
		display: block;
		height: 3px;
		left: 0;
		position: absolute;
		width: 92px;
	}
	#Container .new-cat-block-catalog {
		background: #fff !important;
		border: 1px solid #e2e8f0 !important;
		border-radius: 16px !important;
		box-shadow: 0 14px 34px rgba(15, 23, 42, .08);
		color: #0f172a !important;
		overflow: hidden;
		transition: border-color .2s ease, box-shadow .2s ease, transform .2s ease;
	}
	#Container .new-cat-block-catalog:hover {
		border-color: #ef4444 !important;
		box-shadow: 0 18px 42px rgba(15, 23, 42, .14);
		transform: translateY(-2px);
	}
	#Container .new-cat-block-catalog .new-cat-block-text {
		background: #fff !important;
		color: #0f172a !important;
		font-weight: 900;
	}
	#Container .new-cat-block-catalog:after {
		background: rgba(239, 68, 68, .08);
		border-radius: 999px;
		content: "";
		height: 90px;
		position: absolute;
		right: -34px;
		top: -30px;
		width: 90px;
	}
	#Container .epc-goods-catalog {
		background:
			radial-gradient(circle at 10% 12%, rgba(239, 68, 68, .18), transparent 30%),
			linear-gradient(135deg, #0b1220 0%, #111827 58%, #1f2937 100%);
		border: 1px solid rgba(255, 255, 255, .10);
		border-radius: 22px;
		box-shadow: 0 24px 70px rgba(15, 23, 42, .18);
		margin: 18px 0 16px;
		padding: 22px 24px 16px;
	}
	#Container .epc-goods-catalog .section-title {
		border-bottom-color: rgba(255, 255, 255, .14) !important;
		color: #fff !important;
		margin-top: 0;
	}
	#Container .epc-goods-catalog .new-cat-block {
		padding: 10px;
	}
	#Container .epc-goods-catalog .new-cat-block-catalog {
		background: rgba(255, 255, 255, .98) !important;
		border-color: rgba(255, 255, 255, .22) !important;
		border-radius: 16px !important;
		box-shadow: 0 14px 34px rgba(0, 0, 0, .16);
		min-height: 150px;
	}
	#Container .epc-goods-catalog .new-cat-block-catalog:hover {
		border-color: #ef4444 !important;
		box-shadow: 0 20px 44px rgba(0, 0, 0, .24);
	}
	#Container .epc-goods-catalog .new-cat-block-catalog .new-cat-block-text {
		color: #0f172a !important;
		font-weight: 900;
	}
	.news_box {
		margin-top: -4px !important;
		padding: 0 8px 14px !important;
	}
	.news_box .col-sm-6 {
		padding: 10px;
	}
	.news_box .news_item_box {
		background: #fff !important;
		border: 1px solid #e2e8f0 !important;
		border-radius: 16px !important;
		box-shadow: 0 14px 34px rgba(15, 23, 42, .08);
		height: 100%;
		overflow: hidden;
		transition: border-color .2s ease, box-shadow .2s ease, transform .2s ease;
	}
	.news_box .news_item_box:hover {
		border-color: #2563eb !important;
		box-shadow: 0 18px 42px rgba(15, 23, 42, .14);
		transform: translateY(-2px);
	}
	.news_box .news_item_box > a {
		color: inherit !important;
		text-decoration: none !important;
	}
	.news_box .news_item_box > a > div {
		padding: 16px !important;
	}
	.news_box .news_item_img {
		align-items: center;
		background: linear-gradient(135deg, #f8fafc, #eef2ff) !important;
		border: 1px solid #e2e8f0;
		border-radius: 14px;
		display: flex;
		justify-content: center;
		margin-bottom: 14px !important;
	}
	.news_box .news_item_img i {
		color: #94a3b8 !important;
		padding-top: 0 !important;
	}
	.news_box .news_item_name {
		color: #0f172a !important;
		font-size: 17px;
		font-weight: 900;
		margin-bottom: 6px;
	}
	.news_box .news_item_text {
		color: #475569 !important;
		font-size: 13px;
		line-height: 1.5;
	}
	.news_box .news_item_clock {
		color: #64748b !important;
		display: block;
		font-weight: 700;
		margin-top: 18px;
		text-align: right;
	}
	/* Hide crawlable cross-ref list from UI (keep in DOM for SEO). */
	.epc-seo-cross-refs {
		border: 0 !important;
		clip: rect(0, 0, 0, 0) !important;
		height: 1px !important;
		margin: -1px !important;
		overflow: hidden !important;
		padding: 0 !important;
		position: absolute !important;
		white-space: nowrap !important;
		width: 1px !important;
	}
	/* Result chrome: hide Searching-for hero + related-parts strip (header search is enough). */
	.epc-parts-result-hero,
	.epc-parts-result-hero__content,
	.epc-parts-result-hero__card,
	.epc-complementary-panel {
		display: none !important;
	}
	.epc-parts-result-hero {
		align-items: stretch;
		background:
			radial-gradient(circle at 12% 18%, rgba(239, 68, 68, .28), transparent 28%),
			radial-gradient(circle at 86% 10%, rgba(37, 99, 235, .24), transparent 30%),
			linear-gradient(135deg, #08111f 0%, #111827 58%, #1f2937 100%);
		border: 1px solid rgba(255, 255, 255, .12);
		border-radius: 24px;
		box-shadow: 0 26px 70px rgba(15, 23, 42, .22);
		display: flex;
		gap: 22px;
		justify-content: center;
		margin: 8px 0 18px;
		overflow: hidden;
		padding: 18px;
		position: relative;
	}
	.epc-parts-result-hero:after {
		background: linear-gradient(90deg, rgba(255, 255, 255, .18), transparent);
		content: "";
		height: 1px;
		left: 28px;
		position: absolute;
		right: 28px;
		top: 0;
	}
	.epc-parts-result-hero__eyebrow {
		background: rgba(239, 68, 68, .16);
		border: 1px solid rgba(248, 113, 113, .35);
		border-radius: 999px;
		color: #fecaca !important;
		display: inline-flex;
		font-size: 12px;
		font-weight: 900;
		gap: 7px;
		letter-spacing: .08em;
		margin-bottom: 14px;
		padding: 8px 12px;
		text-transform: uppercase;
	}
	.epc-parts-result-hero h2 {
		color: #fff !important;
		font-size: clamp(28px, 4vw, 44px);
		font-weight: 950;
		letter-spacing: -.04em;
		line-height: 1.05;
		margin: 0 0 12px;
		text-transform: uppercase;
	}
	.epc-parts-result-hero p {
		color: rgba(226, 232, 240, .82) !important;
		font-size: 15px;
		font-weight: 650;
		line-height: 1.6;
		margin: 0;
		max-width: 680px;
	}
	.epc-parts-result-hero__chips {
		display: flex;
		flex-wrap: wrap;
		gap: 10px;
		margin-top: 18px;
	}
	.epc-parts-result-hero__chips span {
		background: rgba(255, 255, 255, .09);
		border: 1px solid rgba(255, 255, 255, .13);
		border-radius: 999px;
		color: #fff !important;
		font-size: 12px;
		font-weight: 850;
		padding: 9px 12px;
	}
	.epc-parts-result-hero__chips i {
		color: #f87171;
		margin-right: 5px;
	}
	.epc-parts-result-hero__card {
		background: rgba(255, 255, 255, .96);
		border: 1px solid rgba(255, 255, 255, .35);
		border-radius: 20px;
		box-shadow: 0 18px 48px rgba(0, 0, 0, .22);
		color: #0f172a !important;
		flex: 1 1 420px;
		max-width: 520px;
		padding: 20px;
		width: 100%;
	}
	.epc-parts-result-hero__label {
		color: #64748b !important;
		font-size: 12px;
		font-weight: 900;
		letter-spacing: .08em;
		text-transform: uppercase;
	}
	.epc-parts-result-hero__brand {
		color: #ef4444 !important;
		font-size: 20px;
		font-weight: 950;
		margin-top: 8px;
		text-transform: uppercase;
	}
	.epc-parts-result-hero__article {
		color: #0f172a !important;
		font-size: 34px;
		font-weight: 950;
		letter-spacing: -.03em;
		line-height: 1.05;
		margin: 4px 0 16px;
		word-break: break-word;
	}
	.epc-parts-result-hero__card .form-control {
		border: 1px solid #cbd5e1 !important;
		border-radius: 12px 0 0 12px !important;
		box-shadow: none !important;
		font-weight: 800;
		height: 44px;
	}
	.epc-parts-result-hero__card .btn {
		border-radius: 0 12px 12px 0 !important;
		height: 44px;
		padding-left: 18px;
		padding-right: 18px;
	}
	.epc-fitment-check-btn {
		align-items: center;
		background: linear-gradient(135deg, #2563eb, #1d4ed8) !important;
		border: 0 !important;
		border-radius: 12px !important;
		box-shadow: 0 12px 26px rgba(37, 99, 235, .22);
		color: #fff !important;
		display: inline-flex;
		font-size: 12px;
		font-weight: 950;
		gap: 7px;
		height: auto !important;
		justify-content: center;
		margin-top: 10px;
		padding: 10px 14px !important;
		text-decoration: none !important;
		text-transform: uppercase;
		width: 100%;
	}
	.epc-fitment-check-btn:hover,
	.epc-fitment-check-btn:focus {
		background: linear-gradient(135deg, #1d4ed8, #1e40af) !important;
		color: #fff !important;
		outline: none;
	}
	.epc-cross-search-btn {
		align-items: center;
		background: linear-gradient(135deg, #16a34a, #15803d) !important;
		border: 0 !important;
		border-radius: 12px !important;
		box-shadow: 0 12px 26px rgba(22, 163, 74, .22);
		color: #fff !important;
		display: inline-flex;
		font-size: 12px;
		font-weight: 950;
		gap: 7px;
		height: auto !important;
		justify-content: center;
		margin-top: 10px;
		padding: 10px 14px !important;
		text-decoration: none !important;
		text-transform: uppercase;
		width: 100%;
	}
	.epc-cross-search-btn:hover:not(:disabled),
	.epc-cross-search-btn:focus:not(:disabled) {
		background: linear-gradient(135deg, #15803d, #166534) !important;
		color: #fff !important;
		outline: none;
	}
	.epc-cross-search-btn:disabled {
		background: linear-gradient(135deg, #94a3b8, #64748b) !important;
		box-shadow: none;
		cursor: not-allowed;
		opacity: .85;
	}
	.epc-cross-search-btn i {
		font-size: 14px;
	}
	.epc-avail-badge {
		border-radius: 999px;
		display: inline-block;
		font-size: 11px;
		font-weight: 800;
		line-height: 1.2;
		padding: 3px 8px;
		white-space: nowrap;
	}
	.epc-avail-badge--yes {
		background: #dcfce7;
		color: #166534;
	}
	.epc-avail-badge--no {
		background: #f1f5f9;
		color: #64748b;
	}
	.epc-fitment-panel {
		background: #f8fafc;
		border: 1px solid rgba(219, 234, 254, .95);
		border-radius: 22px;
		box-shadow:
			0 0 0 9999px rgba(15, 23, 42, .68),
			0 32px 90px rgba(15, 23, 42, .35);
		display: none;
		flex-direction: column;
		margin: 0;
		overflow: hidden;
		padding: 16px 18px 18px;
		position: fixed;
		z-index: 10050;
	}
	.epc-fitment-panel--centered {
		height: calc(100vh - 36px);
		left: 50%;
		max-height: calc(100vh - 36px);
		max-width: 980px;
		top: 18px;
		transform: translateX(-50%);
		width: calc(100vw - 34px);
	}
	.epc-fitment-panel--anchored {
		max-width: calc(100vw - 24px);
	}
	.epc-fitment-panel.active {
		display: flex;
	}
	.epc-fitment-panel__head {
		align-items: flex-start;
		display: flex;
		flex-shrink: 0;
		gap: 10px;
		justify-content: space-between;
		margin-bottom: 10px;
	}
	.epc-fitment-panel__body {
		display: flex;
		flex: 1 1 auto;
		flex-direction: column;
		gap: 8px;
		min-height: 0;
		overflow: hidden;
	}
	#epc-fitment-brands {
		flex-shrink: 0;
		max-height: 96px;
		overflow-x: hidden;
		overflow-y: auto;
	}
	.epc-fitment-panel__title {
		color: #0f172a !important;
		font-size: 17px;
		font-weight: 950;
		text-transform: uppercase;
	}
	.epc-fitment-panel__hint {
		color: #64748b !important;
		font-size: 11px;
		font-weight: 750;
		line-height: 1.35;
		margin-top: 2px;
	}
	.epc-fitment-panel__close {
		background: #fff !important;
		border: 0 !important;
		border-radius: 999px !important;
		box-shadow: 0 8px 18px rgba(15, 23, 42, .1);
		color: #64748b !important;
		font-size: 22px;
		font-weight: 900;
		height: 34px !important;
		line-height: 1;
		padding: 0 !important;
		width: 34px;
	}
	.epc-fitment-brand-grid {
		display: grid;
		gap: 8px;
		grid-template-columns: repeat(auto-fit, minmax(148px, 1fr));
	}
	.epc-fitment-brand-card {
		align-items: center;
		background: #fff;
		border: 1px solid #dbe4f0;
		border-radius: 12px;
		cursor: pointer;
		display: flex;
		gap: 8px;
		min-height: 74px;
		padding: 8px;
		text-align: left;
		transition: border-color .18s ease, box-shadow .18s ease, transform .18s ease;
		width: 100%;
	}
	.epc-fitment-brand-card__thumb {
		align-items: center;
		background: #f8fafc;
		border: 1px solid #e2e8f0;
		border-radius: 10px;
		display: flex;
		flex: 0 0 52px;
		height: 52px;
		justify-content: center;
		overflow: hidden;
		width: 52px;
	}
	.epc-fitment-brand-card__thumb img {
		max-height: 46px;
		max-width: 46px;
		object-fit: contain;
	}
	.epc-fitment-brand-card__thumb--empty {
		color: #94a3b8;
		font-size: 18px;
	}
	.epc-fitment-brand-card__text {
		flex: 1 1 auto;
		min-width: 0;
	}
	.epc-fitment-brand-card:hover,
	.epc-fitment-brand-card.active {
		border-color: #2563eb;
		box-shadow: 0 10px 22px rgba(37, 99, 235, .14);
		transform: translateY(-1px);
	}
	.epc-fitment-brand-card strong {
		color: #0f172a;
		display: block;
		font-size: 12px;
		font-weight: 950;
		text-transform: uppercase;
	}
	.epc-fitment-brand-card span {
		color: #2563eb;
		display: block;
		font-size: 17px;
		font-weight: 950;
		margin-top: 4px;
		text-transform: uppercase;
	}
	.epc-fitment-brand-card small {
		color: #64748b;
		display: block;
		font-size: 10px;
		font-weight: 800;
		margin-top: 3px;
	}
	.epc-fitment-type-tabs {
		display: flex;
		flex-shrink: 0;
		flex-wrap: wrap;
		gap: 6px;
		margin: 0 0 10px;
	}
	.epc-fitment-type-tabs button {
		background: #fff !important;
		border: 1px solid #cbd5e1 !important;
		border-radius: 999px !important;
		color: #334155 !important;
		font-size: 11px;
		font-weight: 900;
		height: auto !important;
		padding: 6px 10px !important;
	}
	.epc-fitment-type-tabs button.active {
		background: #2563eb !important;
		border-color: #2563eb !important;
		color: #fff !important;
	}
	.epc-fitment-widget-shell {
		background: #fff;
		border: 1px solid #e2e8f0;
		border-radius: 12px;
		display: flex;
		flex: 1 1 0;
		flex-direction: column;
		gap: 10px;
		min-height: 0;
		overflow: hidden;
		padding: 10px;
	}
	.epc-fitment-part {
		flex: 0 1 auto;
		max-height: 24vh;
		min-height: 0;
		overflow-x: hidden;
		overflow-y: auto;
		-webkit-overflow-scrolling: touch;
	}
	.epc-fitment-part-card {
		background: linear-gradient(180deg, #f8fafc 0%, #fff 100%);
		border: 1px solid #e2e8f0;
		border-radius: 12px;
		display: grid;
		gap: 8px 10px;
		grid-template-columns: 72px minmax(0, 1fr);
		grid-template-rows: auto auto;
		padding: 10px;
	}
	.epc-fitment-part-card__media {
		align-items: center;
		background: #fff;
		border: 1px solid #e2e8f0;
		border-radius: 10px;
		display: flex;
		grid-row: 1 / span 2;
		justify-content: center;
		min-height: 72px;
		padding: 6px;
	}
	.epc-fitment-part-card__media img {
		max-height: 64px;
		max-width: 64px;
		object-fit: contain;
	}
	.epc-fitment-part-card__media--clickable {
		cursor: zoom-in;
		transition: border-color .18s ease, box-shadow .18s ease;
	}
	.epc-fitment-part-card__media--clickable:hover {
		border-color: #2563eb;
		box-shadow: 0 8px 18px rgba(37, 99, 235, .12);
	}
	.epc-image-lightbox {
		align-items: center;
		display: none;
		inset: 0;
		justify-content: center;
		padding: 24px;
		position: fixed;
		z-index: 10050;
	}
	.epc-image-lightbox.active {
		display: flex;
	}
	.epc-image-lightbox__backdrop {
		background: rgba(15, 23, 42, .82);
		inset: 0;
		position: absolute;
	}
	.epc-image-lightbox__panel {
		background: #fff;
		border-radius: 16px;
		box-shadow: 0 24px 60px rgba(15, 23, 42, .35);
		max-height: calc(100vh - 48px);
		max-width: min(920px, calc(100vw - 48px));
		overflow: auto;
		padding: 14px;
		position: relative;
		z-index: 1;
	}
	.epc-image-lightbox__panel img {
		display: block;
		height: auto;
		margin: 0 auto;
		max-height: calc(100vh - 120px);
		max-width: 100%;
		object-fit: contain;
	}
	.epc-image-lightbox__close {
		background: #fff;
		border: 1px solid #cbd5e1;
		border-radius: 999px;
		color: #0f172a;
		cursor: pointer;
		font-size: 22px;
		height: 36px;
		line-height: 1;
		position: absolute;
		right: 10px;
		top: 10px;
		width: 36px;
		z-index: 2;
	}
	.epc-parts-result-hero__photo {
		margin: 10px 0 4px;
	}
	.epc-parts-result-hero__photo-btn {
		background: #fff;
		border: 1px solid #dbe4f0;
		border-radius: 12px;
		cursor: pointer;
		display: inline-flex;
		height: 72px;
		padding: 6px;
		width: 72px;
		align-items: center;
		justify-content: center;
		color: #94a3b8;
		font-size: 22px;
	}
	.epc-parts-result-hero__photo-btn img {
		height: 100%;
		object-fit: contain;
		width: 100%;
	}
	.epc-fitment-part-card__placeholder {
		align-items: center;
		color: #94a3b8;
		display: flex;
		font-size: 22px;
		height: 64px;
		justify-content: center;
		width: 64px;
	}
	.epc-fitment-part-card__brand {
		color: #64748b !important;
		font-size: 11px;
		font-weight: 800;
		margin: 0;
		text-transform: uppercase;
	}
	.epc-fitment-part-card__brand span {
		color: #2563eb !important;
		font-size: 13px;
		font-weight: 950;
	}
	.epc-fitment-part-card__name {
		color: #0f172a !important;
		font-size: 13px;
		font-weight: 900;
		line-height: 1.25;
		margin: 2px 0 6px;
	}
	.epc-fitment-part-card__facts {
		display: grid;
		gap: 4px 12px;
		grid-template-columns: repeat(2, minmax(0, 1fr));
		margin: 0;
	}
	.epc-fitment-part-card__facts div {
		min-width: 0;
	}
	.epc-fitment-part-card__facts dt {
		color: #64748b !important;
		font-size: 9px;
		font-weight: 900;
		letter-spacing: .04em;
		margin: 0;
		text-transform: uppercase;
	}
	.epc-fitment-part-card__facts dd {
		color: #0f172a !important;
		font-size: 11px;
		font-weight: 800;
		margin: 1px 0 0;
	}
	.epc-fitment-part-card__specs {
		grid-column: 1 / -1;
	}
	.epc-fitment-part-card__specs-title {
		color: #64748b !important;
		font-size: 9px;
		font-weight: 900;
		letter-spacing: .05em;
		margin-bottom: 5px;
		text-transform: uppercase;
	}
	.epc-fitment-part-card__chips {
		display: flex;
		flex-wrap: wrap;
		gap: 5px;
		max-height: 4.5em;
		overflow-x: hidden;
		overflow-y: auto;
		-webkit-overflow-scrolling: touch;
	}
	.epc-fitment-spec-chip {
		background: #fff;
		border: 1px solid #dbe4f0;
		border-radius: 999px;
		color: #334155 !important;
		font-size: 10px;
		font-weight: 700;
		line-height: 1.2;
		padding: 3px 8px;
		white-space: nowrap;
	}
	.epc-fitment-spec-chip b {
		color: #0f172a !important;
		font-weight: 900;
		margin-right: 3px;
	}
	.epc-fitment-spec-chip--muted {
		background: #f1f5f9;
	}
	.epc-fitment-widget-shell #applicability_widget,
	.epc-fitment-widget-shell #applicability_widget.epc-fitment-widget-table-host {
		display: flex;
		flex: 1 1 0;
		flex-direction: column;
		min-height: 0;
		overflow: hidden;
	}
	.epc-fitment-widget-shell #applicability_widget.epc-fitment-widget-table-host {
		background: transparent;
		border: 0;
		padding: 0;
	}
	.epc-fitment-widget-shell #applicability_widget > .epc-fitment-message {
		flex: 1 1 auto;
		min-height: 0;
		overflow-y: auto;
	}
	.epc-fitment-results-toolbar {
		align-items: center;
		background: #f8fafc;
		border-bottom: 1px solid #e2e8f0;
		display: flex;
		flex: 0 0 auto;
		flex-wrap: wrap;
		gap: 8px 12px;
		justify-content: space-between;
		padding: 8px 10px;
	}
	.epc-fitment-results-toolbar__count {
		color: #475569;
		font-size: 12px;
		font-weight: 700;
	}
	.epc-fitment-results-toolbar__count strong {
		color: #0f172a;
	}
	.epc-fitment-results-toolbar__meta {
		color: #64748b;
		font-weight: 600;
	}
	.epc-fitment-download-btn {
		border-radius: 8px !important;
		font-size: 11px !important;
		font-weight: 800 !important;
		padding: 5px 10px !important;
		white-space: nowrap;
	}
	.epc-fitment-download-btn .fa {
		color: #15803d;
		margin-right: 4px;
	}
	.epc-fitment-widget-shell #applicability_widget > .epc-fitment-results-toolbar {
		flex: 0 0 auto;
	}
	.epc-fitment-widget-shell #applicability_widget > .epc-fitment-table-scroll {
		flex: 1 1 0;
		max-height: 100%;
		min-height: calc(7 * 2.05rem + 2.5rem);
	}
	.epc-fitment-table-scroll {
		-webkit-overflow-scrolling: touch;
		overflow-x: auto;
		overflow-y: auto;
		overscroll-behavior: contain;
		scrollbar-gutter: stable;
	}
	.epc-fitment-panel--anchored .epc-fitment-table-scroll {
		min-height: calc(7 * 2.05rem + 2.5rem);
	}
	.epc-fitment-panel--centered .epc-fitment-table-scroll {
		min-height: calc(7 * 2.05rem + 2.5rem);
	}
	.epc-fitment-table-scroll .epc-umapi-table {
		margin: 0;
	}
	.epc-fitment-table-scroll .epc-umapi-table thead th {
		background: #1e3a8a;
		color: #fff;
		position: sticky;
		top: 0;
		z-index: 2;
	}
	.epc-fitment-widget-shell .epc-umapi-table {
		font-size: 11px;
		margin: 0;
	}
	.epc-fitment-widget-shell .epc-umapi-table th,
	.epc-fitment-widget-shell .epc-umapi-table td {
		padding: 6px 7px;
		vertical-align: top;
	}
	@media (max-width: 720px) {
		.epc-fitment-part-card {
			grid-template-columns: 56px minmax(0, 1fr);
		}
		.epc-fitment-part-card__facts {
			grid-template-columns: 1fr;
		}
	}
	.epc-fitment-message {
		background: #eff6ff;
		border: 1px dashed #bfdbfe;
		border-radius: 12px;
		color: #1e3a8a !important;
		font-size: 12px;
		font-weight: 800;
		padding: 10px;
	}
	.epc-parts-result-hero__cross {
		border-top: 1px solid #e2e8f0;
		margin-top: 14px;
		padding-top: 13px;
	}
	.epc-parts-result-hero__cross-title {
		align-items: center;
		color: #0f172a !important;
		display: flex;
		font-size: 13px;
		font-weight: 950;
		gap: 7px;
		justify-content: space-between;
		text-transform: uppercase;
	}
	.epc-parts-result-hero__cross-title i {
		color: #ef4444;
	}
	.epc-parts-result-hero__cross-title span {
		background: #eff6ff;
		border-radius: 999px;
		color: #1d4ed8;
		font-size: 11px;
		padding: 4px 8px;
		text-transform: none;
	}
	.epc-parts-result-hero__cross-title span.epc-cross-count-clickable {
		cursor: pointer;
		transition: background .18s ease, box-shadow .18s ease;
	}
	.epc-parts-result-hero__cross-title span.epc-cross-count-clickable:hover,
	.epc-parts-result-hero__cross-title span.epc-cross-count-clickable:focus {
		background: #dbeafe;
		box-shadow: 0 0 0 3px rgba(37, 99, 235, .14);
		outline: none;
	}
	.epc-parts-result-hero__cross-list {
		display: grid;
		gap: 6px;
		margin-top: 9px;
		max-height: 230px;
		overflow: auto;
		padding-right: 2px;
	}
	.epc-parts-result-hero__cross-row {
		align-items: center;
		background: #f8fafc;
		border: 1px solid #e2e8f0;
		border-radius: 10px;
		display: grid;
		gap: 8px;
		grid-template-columns: minmax(0, 1fr) auto;
		padding: 8px 9px;
	}
	.epc-parts-result-hero__cross-row span {
		color: #334155;
		font-size: 12px;
		min-width: 0;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
	.epc-parts-result-hero__cross-row strong {
		color: #0f172a;
		font-weight: 950;
	}
	.epc-parts-result-hero__cross-row a {
		color: #ef4444 !important;
		font-size: 11px;
		font-weight: 900;
		text-decoration: none !important;
		white-space: nowrap;
	}
	.epc-parts-result-hero__cross-empty {
		background: #f8fafc;
		border: 1px dashed #cbd5e1;
		border-radius: 10px;
		color: #64748b;
		font-size: 12px;
		font-weight: 800;
		padding: 10px;
	}
	.epc-parts-result-hero__cross-stock {
		display: grid;
		gap: 6px;
		margin-top: 10px;
	}
	.epc-parts-result-hero__stock-title {
		color: #047857;
		font-size: 12px;
		font-weight: 950;
		text-transform: uppercase;
	}
	.epc-parts-result-hero__stock-row {
		background: #ecfdf5;
		border: 1px solid #a7f3d0;
		border-radius: 10px;
		color: #065f46;
		display: grid;
		gap: 4px;
		grid-template-columns: 1fr auto;
		padding: 8px 9px;
	}
	.epc-parts-result-hero__stock-row strong,
	.epc-parts-result-hero__stock-row span {
		min-width: 0;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
	.epc-parts-result-hero__stock-row span {
		color: #047857;
		font-size: 11px;
		grid-column: 1 / 2;
	}
	.epc-parts-result-hero__stock-row b {
		color: #064e3b;
		font-size: 12px;
		grid-column: 2 / 3;
		grid-row: 1 / 2;
		white-space: nowrap;
	}
	.epc-parts-result-hero__stock-row em {
		color: #047857;
		font-size: 11px;
		font-style: normal;
		font-weight: 900;
		grid-column: 2 / 3;
		grid-row: 2 / 3;
		white-space: nowrap;
	}
	.epc-parts-result-hero__cross-more,
	.epc-parts-result-hero__cross-hint {
		color: #64748b;
		font-size: 12px;
		font-weight: 700;
		line-height: 1.4;
		margin-top: 8px;
	}
	.epc-cross-modal {
		align-items: center;
		background: rgba(15, 23, 42, .72);
		bottom: 0;
		display: flex;
		justify-content: center;
		left: 0;
		padding: 20px;
		position: fixed;
		right: 0;
		top: 0;
		z-index: 99999;
	}
	.epc-cross-modal__dialog {
		background: #fff;
		border-radius: 18px;
		box-shadow: 0 28px 90px rgba(0, 0, 0, .34);
		max-height: 92vh;
		max-width: 1120px;
		overflow: hidden;
		width: 100%;
	}
	.epc-cross-modal__head {
		align-items: center;
		background: linear-gradient(135deg, #0f172a, #1e293b);
		color: #fff;
		display: flex;
		justify-content: space-between;
		padding: 18px 20px;
	}
	.epc-cross-modal__head strong {
		display: block;
		font-size: 20px;
		font-weight: 950;
	}
	.epc-cross-modal__head span {
		color: #cbd5e1;
		display: block;
		font-size: 13px;
		margin-top: 3px;
	}
	.epc-cross-modal__close {
		background: rgba(255, 255, 255, .12);
		border: 0;
		border-radius: 999px;
		color: #fff;
		font-size: 28px;
		height: 42px;
		line-height: 1;
		width: 42px;
	}
	.epc-cross-modal__tools {
		align-items: center;
		background: #f8fafc;
		border-bottom: 1px solid #e2e8f0;
		display: flex;
		flex-wrap: wrap;
		gap: 10px;
		padding: 14px 16px;
	}
	.epc-cross-modal__tools .form-control {
		max-width: 360px;
	}
	.epc-cross-modal__body {
		display: grid;
		gap: 14px;
		grid-template-columns: minmax(0, 1fr) 360px;
		max-height: calc(92vh - 130px);
		overflow: auto;
		padding: 16px;
	}
	.epc-cross-modal__table {
		border: 1px solid #e2e8f0;
		border-radius: 14px;
		overflow: auto;
	}
	.epc-cross-modal__table table {
		margin: 0 !important;
	}
	.epc-cross-modal__table th {
		background: #f8fafc !important;
		color: #334155 !important;
		font-weight: 900 !important;
		position: sticky;
		top: 0;
		z-index: 1;
	}
	.epc-cross-modal__stock {
		background: #f0fdf4;
		border: 1px solid #bbf7d0;
		border-radius: 14px;
		padding: 12px;
	}
	.epc-cross-modal__stock > strong {
		color: #065f46;
		display: block;
		font-size: 14px;
		font-weight: 950;
		margin-bottom: 10px;
		text-transform: uppercase;
	}
	.epc-cross-modal__stock-row {
		background: #fff;
		border: 1px solid #a7f3d0;
		border-radius: 10px;
		display: grid;
		gap: 5px 8px;
		grid-template-columns: minmax(0, 1fr) auto;
		margin-bottom: 8px;
		padding: 9px;
	}
	.epc-cross-modal__stock-row span,
	.epc-cross-modal__stock-row small {
		display: block;
		min-width: 0;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
	.epc-cross-modal__stock-row small {
		color: #047857;
		font-size: 11px;
		margin-top: 3px;
	}
	.epc-cross-modal__stock-row b {
		color: #064e3b;
		white-space: nowrap;
	}
	.epc-cross-modal__stock-row em {
		color: #047857;
		font-style: normal;
		font-weight: 900;
	}
	.epc-cross-modal__stock-row a {
		grid-column: 1 / -1;
		justify-self: start;
	}
	.epc-cross-modal__empty {
		background: #fff;
		border: 1px dashed #a7f3d0;
		border-radius: 10px;
		color: #047857;
		font-weight: 800;
		padding: 12px;
	}
	.epc-parts-stock-brands {
		margin: 0 0 36px;
	}
	.epc-parts-stock-brands__hero {
		align-items: stretch;
		background: linear-gradient(135deg, #0f172a 0%, #1e3a5f 55%, #0f766e 100%);
		border-radius: 22px;
		box-shadow: 0 22px 50px rgba(15, 23, 42, .18);
		color: #fff;
		display: grid;
		gap: 20px;
		grid-template-columns: 1fr auto;
		margin-bottom: 22px;
		padding: 26px 28px;
	}
	.epc-parts-stock-brands__eyebrow {
		color: #a7f3d0;
		display: inline-block;
		font-size: 12px;
		font-weight: 800;
		letter-spacing: .06em;
		margin-bottom: 8px;
		text-transform: uppercase;
	}
	.epc-parts-stock-brands__title {
		color: #fff !important;
		font-size: 28px;
		font-weight: 900;
		line-height: 1.15;
		margin: 0 0 10px;
	}
	.epc-parts-stock-brands__lead {
		color: rgba(255, 255, 255, .88);
		font-size: 14px;
		line-height: 1.5;
		margin: 0;
		max-width: 640px;
	}
	.epc-parts-stock-brands__stats {
		align-self: center;
		display: flex;
		flex-wrap: wrap;
		gap: 12px;
	}
	.epc-parts-stock-brands__stat {
		background: rgba(255, 255, 255, .12);
		border: 1px solid rgba(255, 255, 255, .2);
		border-radius: 16px;
		min-width: 130px;
		padding: 14px 18px;
		text-align: center;
	}
	.epc-parts-stock-brands__stat strong {
		color: #fff;
		display: block;
		font-size: 26px;
		font-weight: 900;
		line-height: 1.1;
	}
	.epc-parts-stock-brands__stat span {
		color: rgba(255, 255, 255, .82);
		display: block;
		font-size: 11px;
		font-weight: 700;
		margin-top: 4px;
		text-transform: uppercase;
	}
	.epc-parts-stock-brands__toolbar {
		align-items: center;
		display: flex;
		flex-wrap: wrap;
		gap: 12px;
		margin-bottom: 14px;
	}
	.epc-parts-stock-brands__search {
		flex: 1 1 280px;
		max-width: 420px;
	}
	.epc-parts-stock-brands__letters {
		display: flex;
		flex-wrap: wrap;
		gap: 6px;
		margin-bottom: 14px;
	}
	.epc-parts-stock-brands__letter {
		background: #fff;
		border: 1px solid #dbe4f0;
		border-radius: 8px;
		color: #334155;
		font-size: 12px;
		font-weight: 800;
		min-width: 34px;
		padding: 7px 10px;
	}
	.epc-parts-stock-brands__letter.active {
		background: #2563eb;
		border-color: #2563eb;
		color: #fff;
	}
	.epc-parts-stock-brands__letter:disabled {
		cursor: not-allowed;
		opacity: .35;
	}
	.epc-parts-stock-brands__summary {
		color: #64748b;
		font-size: 13px;
		margin-bottom: 16px;
	}
	.epc-parts-stock-brands__summary strong {
		color: #0f172a;
	}
	.epc-parts-stock-brands__grid {
		display: grid;
		gap: 12px;
		grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
	}
	.epc-parts-stock-brands__card {
		align-items: center;
		background: #fff;
		border: 1px solid #e2e8f0;
		border-radius: 14px;
		box-shadow: 0 10px 28px rgba(15, 23, 42, .06);
		color: #0f172a !important;
		display: flex;
		gap: 12px;
		padding: 14px 16px;
		text-decoration: none !important;
		transition: border-color .15s ease, box-shadow .15s ease, transform .15s ease;
	}
	.epc-parts-stock-brands__card:hover,
	.epc-parts-stock-brands__card:focus {
		border-color: #93c5fd;
		box-shadow: 0 16px 36px rgba(37, 99, 235, .14);
		color: #0f172a !important;
		transform: translateY(-2px);
	}
	.epc-parts-stock-brands__card-logo {
		align-items: center;
		background: linear-gradient(135deg, #eff6ff, #ecfdf5);
		border: 1px solid #dbeafe;
		border-radius: 12px;
		color: #1d4ed8;
		display: inline-flex;
		flex: 0 0 44px;
		font-size: 18px;
		font-weight: 900;
		height: 44px;
		justify-content: center;
		text-transform: uppercase;
	}
	.epc-parts-stock-brands__card-body {
		display: flex;
		flex: 1 1 auto;
		flex-direction: column;
		gap: 4px;
		min-width: 0;
	}
	.epc-parts-stock-brands__card-name {
		color: #0f172a;
		font-size: 15px;
		font-weight: 800;
		line-height: 1.25;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
	.epc-parts-stock-brands__count {
		background: #ecfdf5;
		border-radius: 999px;
		color: #047857;
		display: inline-block;
		font-size: 11px;
		font-weight: 800;
		padding: 3px 10px;
	}
	.epc-parts-stock-brands__card-arrow {
		color: #94a3b8;
		flex: 0 0 auto;
		font-size: 12px;
	}
	.epc-parts-stock-brands__empty {
		background: #fff8e6;
		border: 1px solid #f0e4b5;
		border-radius: 12px;
		color: #92400e;
		grid-column: 1 / -1;
		padding: 18px;
	}
	.epc-parts-stock-section {
		margin-bottom: 28px;
	}
	.epc-parts-stock-section__head {
		align-items: center;
		border-radius: 14px;
		display: flex;
		flex-wrap: wrap;
		gap: 10px;
		justify-content: center;
		margin-bottom: 14px;
		padding: 14px 18px;
		text-align: center;
	}
	.epc-parts-stock-section--genuine .epc-parts-stock-section__head {
		background: linear-gradient(180deg, #a7f3d0 0%, #d1fae5 42%, #ecfdf5 100%);
		border: 2px solid #34d399;
		border-top: 3px solid #059669;
	}
	.epc-parts-stock-section--aftermarket .epc-parts-stock-section__head {
		background: linear-gradient(180deg, #93c5fd 0%, #bfdbfe 42%, #eff6ff 100%);
		border: 2px solid #60a5fa;
		border-top: 3px solid #2563eb;
	}
	.epc-parts-stock-section__title {
		color: #0f172a !important;
		font-size: 20px;
		font-weight: 950;
		line-height: 1.35;
		margin: 0;
	}
	.epc-parts-stock-section__catalog {
		color: #047857 !important;
		font-weight: 950;
		text-decoration: underline;
	}
	.epc-parts-stock-section__count {
		color: #334155;
		font-size: 16px;
		font-weight: 800;
	}
	@media (max-width: 991px) {
		.epc-parts-stock-brands__hero {
			grid-template-columns: 1fr;
		}
		.epc-parts-stock-brands__search {
			max-width: 100%;
			width: 100%;
		}
	}
	.epc-brand-browse {
		margin: 0 0 36px;
	}
	.epc-brand-browse__hero {
		align-items: stretch;
		background: linear-gradient(135deg, #1e3a5f 0%, #2563eb 55%, #0ea5e9 100%);
		border-radius: 22px;
		box-shadow: 0 22px 50px rgba(15, 23, 42, .16);
		color: #fff;
		display: grid;
		gap: 18px;
		grid-template-columns: 1fr auto;
		margin-bottom: 18px;
		padding: 24px 26px;
	}
	.epc-brand-browse__back {
		color: rgba(255, 255, 255, .92) !important;
		display: inline-block;
		font-size: 12px;
		font-weight: 700;
		margin-bottom: 10px;
		text-decoration: none !important;
	}
	.epc-brand-browse__eyebrow {
		color: #bfdbfe;
		display: block;
		font-size: 12px;
		font-weight: 800;
		letter-spacing: .06em;
		margin-bottom: 6px;
		text-transform: uppercase;
	}
	.epc-brand-browse__title {
		color: #fff !important;
		font-size: 30px;
		font-weight: 900;
		margin: 0 0 8px;
	}
	.epc-brand-browse__lead {
		color: rgba(255, 255, 255, .9);
		font-size: 14px;
		line-height: 1.5;
		margin: 0;
		max-width: 640px;
	}
	.epc-brand-browse__stat {
		align-self: center;
		background: rgba(255, 255, 255, .14);
		border: 1px solid rgba(255, 255, 255, .22);
		border-radius: 16px;
		min-width: 150px;
		padding: 16px 20px;
		text-align: center;
	}
	.epc-brand-browse__stat strong {
		color: #fff;
		display: block;
		font-size: 30px;
		font-weight: 900;
		line-height: 1.1;
	}
	.epc-brand-browse__stat span {
		color: rgba(255, 255, 255, .85);
		display: block;
		font-size: 11px;
		font-weight: 700;
		margin-top: 4px;
		text-transform: uppercase;
	}
	.epc-brand-browse__search {
		background: #fff;
		border: 1px solid #dbe4f0;
		border-radius: 16px;
		box-shadow: 0 12px 32px rgba(15, 23, 42, .08);
		margin-bottom: 14px;
		padding: 16px 18px;
	}
	.epc-brand-browse__search-label {
		color: #0f172a;
		display: block;
		font-size: 13px;
		font-weight: 800;
		margin-bottom: 10px;
	}
	.epc-brand-browse__search-row {
		align-items: stretch;
		display: flex;
		flex-wrap: wrap;
		gap: 10px;
	}
	.epc-brand-browse__search-row .form-control {
		flex: 1 1 220px;
		max-width: 420px;
	}
	.epc-brand-browse__search-btn {
		border-radius: 10px !important;
		font-weight: 800;
		white-space: nowrap;
	}
	.epc-brand-browse__search-hint {
		color: #64748b;
		font-size: 12px;
		margin: 10px 0 0;
	}
	.epc-brand-browse__filter {
		margin-bottom: 12px;
	}
	.epc-brand-browse__filter .form-control {
		max-width: 420px;
	}
	.epc-brand-browse__summary {
		color: #64748b;
		font-size: 13px;
		margin-bottom: 12px;
	}
	.epc-brand-browse__summary strong {
		color: #0f172a;
	}
	.epc-brand-browse__list-wrap {
		background: #fff;
		border: 1px solid #e2e8f0;
		border-radius: 14px;
		box-shadow: 0 10px 28px rgba(15, 23, 42, .06);
		max-height: 62vh;
		max-width: 54rem;
		overflow: auto;
		-webkit-overflow-scrolling: touch;
		width: 100%;
	}
	.epc-brand-browse__list-head,
	.epc-brand-browse__row {
		align-items: center;
		column-gap: 12px;
		display: grid;
		grid-template-columns: minmax(9.5rem, 11.5rem) minmax(5rem, 16rem) auto;
		padding: 0 14px;
	}
	.epc-brand-browse__list-head {
		background: #1e3a8a;
		color: #fff;
		font-size: 11px;
		font-weight: 800;
		letter-spacing: .03em;
		min-height: 38px;
		position: sticky;
		top: 0;
		z-index: 2;
	}
	.epc-brand-browse__col-h--actions {
		justify-self: start;
	}
	.epc-brand-browse__list {
		display: flex;
		flex-direction: column;
	}
	.epc-brand-browse__row {
		border-bottom: 1px solid #e8eef5;
		font-size: 13px;
		min-height: 46px;
		padding-bottom: 8px;
		padding-top: 8px;
	}
	.epc-brand-browse__row:nth-child(even) {
		background: #f8fafc;
	}
	.epc-brand-browse__row:hover {
		background: #eff6ff;
	}
	.epc-brand-browse__cell-article {
		min-width: 0;
	}
	.epc-brand-browse__article {
		color: #0f172a;
		font-size: 13px;
		font-weight: 800;
		word-break: break-word;
	}
	.epc-brand-browse__cell-desc {
		color: #475569;
		min-width: 0;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
	.epc-brand-browse__actions {
		align-items: center;
		display: inline-flex;
		flex-wrap: nowrap;
		gap: 8px;
		justify-self: start;
		min-width: 0;
	}
	.epc-brand-browse__actions .btn {
		border-radius: 8px !important;
		font-size: 12px;
		font-weight: 800;
		line-height: 1.2;
		margin: 0;
		min-height: 34px;
		padding: 7px 12px;
		white-space: nowrap;
	}
	.epc-brand-browse__fitment-btn {
		border-color: #cbd5e1 !important;
		color: #1e3a8a !important;
	}
	.epc-brand-browse__price-btn {
		box-shadow: 0 4px 12px rgba(37, 99, 235, .22);
	}
	.epc-brand-browse__empty {
		background: #fff8e6;
		border: 1px solid #f0e4b5;
		border-radius: 12px;
		color: #92400e;
		padding: 16px;
	}
	.epc-brand-browse__pager {
		align-items: center;
		display: flex;
		flex-wrap: wrap;
		gap: 10px;
		justify-content: flex-end;
		margin-bottom: 10px;
	}
	.epc-brand-browse__pager-label {
		color: #64748b;
		font-size: 13px;
		font-weight: 700;
	}
	@media (max-width: 991px) {
		.epc-brand-browse__hero {
			grid-template-columns: 1fr;
		}
		.epc-brand-browse__search-row .form-control {
			max-width: 100%;
			width: 100%;
		}
		.epc-brand-browse__list-head,
		.epc-brand-browse__row {
			grid-template-columns: 1fr;
			row-gap: 8px;
		}
		.epc-brand-browse__list-head {
			display: none;
		}
		.epc-brand-browse__actions {
			justify-self: stretch;
			width: 100%;
		}
		.epc-brand-browse__actions .btn {
			flex: 1 1 0;
			justify-content: center;
			text-align: center;
		}
	}
	@media (min-width: 992px) and (max-width: 1199px) {
		.epc-brand-browse__list-head,
		.epc-brand-browse__row {
			grid-template-columns: minmax(8.5rem, 10rem) minmax(4rem, 12rem) auto;
		}
	}
	#back_to_brands_box {
		margin: 6px 0 14px;
	}
	#back_to_brands_box span {
		background: #fff;
		border: 1px solid #dbe4f0;
		border-radius: 999px;
		box-shadow: 0 10px 26px rgba(15, 23, 42, .08);
		color: #0f172a !important;
		font-weight: 900;
		padding: 10px 14px;
	}
	.search_limo .panel,
	#filter_div .panel,
	#procenka_div #work_area {
		background: #fff !important;
		border: 1px solid #e2e8f0 !important;
		border-radius: 18px !important;
		box-shadow: 0 16px 42px rgba(15, 23, 42, .08);
		overflow: hidden;
	}
	.search_limo .panel-heading,
	#filter_div .panel-heading {
		background: linear-gradient(135deg, #0f172a, #1f2937) !important;
		border: 0 !important;
		color: #fff !important;
		font-weight: 950;
		letter-spacing: .01em;
		padding: 14px 16px;
	}
	.search_limo .panel-heading *,
	#filter_div .panel-heading *,
	#filter_div .panel-heading a {
		color: #fff !important;
		text-decoration: none !important;
	}
	.search_limo .panel-body,
	#filter_div .panel-body {
		padding: 16px !important;
	}
	.search_limo .form-control,
	#filter_div .form-control,
	#filter_div select {
		border: 1px solid #cbd5e1 !important;
		border-radius: 12px !important;
		box-shadow: none !important;
		font-weight: 750;
		height: 42px;
	}
	.search_limo .input-group .form-control {
		border-radius: 12px 0 0 12px !important;
	}
	.search_limo .input-group .btn {
		border-radius: 0 12px 12px 0 !important;
		height: 42px;
	}
	.epc-chpu-direct-part-search .epc-parts-result-hero {
		display: none !important;
	}
	/* CHPU results: header search only — hide redundant in-page search card. */
	.epc-chpu-direct-part-search .search_limo,
	.epc-chpu-direct-part-search .epc-parts-result-tools:empty,
	.epc-chpu-direct-part-search .epc-parts-result-hero__photo,
	#epc-search-result-photo {
		display: none !important;
	}
	.epc-chpu-direct-part-search #work_area {
		min-height: 0;
	}
	.epc-chpu-direct-part-search #products_area {
		min-height: 120px;
	}
	.epc-chpu-direct-part-search #all_table_products {
		display: table !important;
		visibility: visible !important;
		opacity: 1 !important;
	}
	.epc-chpu-actions-bar {
		align-items: center;
		display: flex;
		flex-wrap: wrap;
		gap: 10px;
		margin: 0 0 14px;
		padding: 0 10px;
	}
	.epc-chpu-actions-bar .epc-cross-search-btn,
	.epc-chpu-actions-bar .epc-fitment-check-btn {
		display: inline-flex !important;
	}
	.epc-chpu-direct-part-search .epc-cross-fallback {
		margin-top: 18px;
	}
	.epc-brand-picker-mode .epc-parts-result-hero {
		display: none !important;
	}
	.epc-brand-picker-mode .search_limo,
	.epc-brand-picker-mode .epc-chpu-actions-bar {
		display: none !important;
	}
	.epc-brand-picker-mode #filter_div,
	.epc-brand-picker-mode #footer-filter,
	.epc-brand-picker-mode #footer_filter_reset {
		display: none !important;
	}
	.epc-brand-picker-mode .epc-part-search-main {
		width: 100%;
		flex: 0 0 100%;
		max-width: 100%;
		padding-left: 0;
		padding-right: 0;
	}
	.epc-brand-picker-mode .epc-brand-picker-procenka-hidden {
		display: none !important;
	}
	.epc-brand-picker-top {
		margin: 0 0 18px;
		padding: 0 10px;
		width: 100%;
	}
	.epc-brand-picker-top__head {
		background: linear-gradient(135deg, #fff 0%, #f8fafc 100%);
		border: 1px solid #e2e8f0;
		border-radius: 16px;
		box-shadow: 0 12px 30px rgba(15, 23, 42, .06);
		margin-bottom: 14px;
		padding: 18px 20px;
	}
	.epc-brand-picker-top__eyebrow {
		color: #64748b;
		display: block;
		font-size: 12px;
		font-weight: 700;
		letter-spacing: .08em;
		margin-bottom: 8px;
		text-transform: uppercase;
	}
	.epc-brand-picker-top__title {
		color: #0f172a;
		font-size: 30px;
		font-weight: 800;
		line-height: 1.1;
		margin: 0 0 8px;
	}
	.epc-brand-picker-top__hint {
		color: #475569;
		font-size: 15px;
		line-height: 1.5;
		margin: 0;
	}
	.epc-brand-picker-mode .epc-brand-picker-work-area,
	.epc-brand-picker-mode #work_area.epc-brand-picker-work-area {
		margin: 0;
		min-height: 0;
		padding: 0;
		text-align: left;
	}
	.epc-brand-picker-mode .epc-brand-picker-work-area #processing_indicator {
		margin-bottom: 12px;
	}
	.epc-brand-picker-mode #products_area .epc-brand-picker-table,
	.epc-brand-picker-mode #table-manufacturers {
		background: #fff;
		border: 1px solid #e2e8f0;
		border-radius: 16px;
		box-shadow: 0 14px 34px rgba(15, 23, 42, .07);
		margin-top: 8px;
	}
	.epc-brand-picker-mode #products_area .epc-cross-stock-results {
		display: none !important;
	}
	#all_table_products .epc-required-not-found-caption,
	.epc-required-not-found-caption {
		color: #c62828 !important;
		font-weight: 700 !important;
	}
	#all_table_products .epc-cross-in-stock-caption {
		border-bottom: 1px solid #e2e8f0 !important;
		border-top: 0 !important;
		color: #0f172a !important;
		font-size: 15px !important;
		font-weight: 800 !important;
		padding-top: 28px !important;
		text-align: center !important;
	}
	#all_table_products .epc-cross-not-found-caption {
		background: #fef2f2 !important;
		border-bottom: 1px solid #fecaca !important;
		border-top: 3px solid #f87171 !important;
		color: #b91c1c !important;
		font-size: 17px !important;
		font-weight: 900 !important;
		letter-spacing: 0.02em;
		padding: 44px 16px 14px !important;
		text-align: center !important;
	}
	#all_table_products .epc-required-not-found-caption strong {
		color: #c62828;
		font-weight: 700;
	}
	.epc-part-search-unavailable {
		border: 1px solid #f0d9a8;
		border-radius: 8px;
		margin: 12px 0 18px;
		padding: 14px 16px;
	}
	.epc-part-search-unavailable__title {
		font-size: 16px;
		font-weight: 700;
		margin-bottom: 6px;
	}
	.epc-part-search-unavailable__meta {
		margin: 8px 0 0;
	}
	.epc-chpu-direct-part-search.epc-part-search-layout {
		margin-top: 8px;
	}
	.epc-part-search-layout #epc_prime_storages_blok,
	.epc-chpu-direct-part-search .one_property[data-epc-filter="storages"] {
		display: none !important;
	}
	.epc-part-search-layout {
		align-items: flex-start;
		display: flex;
		flex-direction: row;
		flex-wrap: nowrap;
		margin-left: 0;
		margin-right: 0;
	}
	.epc-part-search-layout > #filter_div {
		flex: 0 0 26%;
		float: none;
		max-width: 26%;
		min-width: 240px;
		width: 26%;
	}
	.epc-part-search-layout > #filter_div.col-md-12 {
		flex: 1 1 100%;
		max-width: 100%;
		min-width: 0;
		width: 100%;
	}
	.epc-part-search-layout > #filter_div.col-md-3 {
		flex: 0 0 26%;
		max-width: 26%;
		min-width: 240px;
		width: 26%;
	}
	.epc-part-search-layout > #filter_div .panel-body {
		display: block;
	}
	.epc-part-search-layout > .epc-part-search-main {
		flex: 1 1 0;
		float: none;
		max-width: 74%;
		min-width: 0;
		padding-left: 12px;
		padding-right: 0;
		width: 74%;
	}
	.epc-part-search-layout > .epc-part-search-main.col-md-12 {
		flex: 1 1 100%;
		max-width: 100%;
		padding-left: 0;
		width: 100%;
	}
	.epc-part-search-layout > .epc-part-search-main.col-md-9 {
		flex: 1 1 0;
		max-width: 74%;
		min-width: 0;
		width: 74%;
	}
	@media (max-width: 991px) {
		.epc-part-search-layout {
			flex-direction: column;
			flex-wrap: wrap;
		}
		.epc-part-search-layout > #filter_div,
		.epc-part-search-layout > #filter_div.col-md-3,
		.epc-part-search-layout > .epc-part-search-main,
		.epc-part-search-layout > .epc-part-search-main.col-md-9 {
			flex: 1 1 100%;
			max-width: 100%;
			min-width: 0;
			padding-left: 0;
			width: 100%;
		}
	}
	#procenka_div {
		padding-left: 0;
		padding-right: 0;
		width: 100%;
	}
	#procenka_div #work_area {
		margin-bottom: 18px;
		min-height: 170px;
		padding: 18px;
		text-align: left;
	}
	#processing_indicator {
		background: linear-gradient(135deg, #f8fafc, #eef2ff);
		border: 1px dashed #cbd5e1;
		border-radius: 16px;
		color: #334155 !important;
		font-weight: 850;
		margin: 0 0 14px;
		padding: 18px;
		text-align: center;
	}
	#processing_indicator p {
		color: #334155 !important;
		font-weight: 850;
		margin-bottom: 10px;
	}
	#products_area .table-responsive {
		background: #fff;
		border: 1px solid #e2e8f0;
		border-radius: 16px;
		box-shadow: 0 14px 34px rgba(15, 23, 42, .07);
		overflow: hidden;
	}
	#products_area table.table,
	#procenka_div table.table {
		margin-bottom: 0;
	}
	#products_area table.table > thead > tr > th,
	#procenka_div table.table > thead > tr > th {
		background: #0f172a !important;
		border-color: rgba(255, 255, 255, .08) !important;
		color: #fff !important;
		font-size: 12px;
		font-weight: 950;
		letter-spacing: .04em;
		text-transform: uppercase;
	}
	#products_area table.table > tbody > tr > td,
	#procenka_div table.table > tbody > tr > td {
		border-color: #e5edf6 !important;
		color: #1f2937 !important;
		font-weight: 700;
		vertical-align: middle;
	}
	#products_area table.table-striped > tbody > tr:nth-of-type(odd),
	#procenka_div table.table-striped > tbody > tr:nth-of-type(odd) {
		background-color: #f8fafc !important;
	}
	#products_area table.table > tbody > tr:hover,
	#procenka_div table.table > tbody > tr:hover {
		background: #fff1f2 !important;
	}
	#products_area .btn,
	#procenka_div .btn {
		border-radius: 10px !important;
		font-weight: 900;
	}
	#all_table_products {
		table-layout: fixed;
		width: 100%;
		font-size: 12px;
		line-height: 1.2;
		word-break: normal;
	}
	#all_table_products > thead > tr > th,
	#all_table_products > tbody > tr > td {
		padding: 3px 5px !important;
		line-height: 1.2 !important;
		vertical-align: middle !important;
	}
	#all_table_products .th_photo,
	#all_table_products .td_photo {
		width: 52px;
		padding: 3px 4px !important;
		text-align: center;
	}
	.epc-search-row-photo {
		align-items: center;
		background: #f8fafc;
		border: 1px solid #e2e8f0;
		border-radius: 8px;
		color: #94a3b8;
		display: inline-flex;
		height: 40px;
		justify-content: center;
		width: 40px;
	}
	.epc-search-row-photo--pending {
		font-size: 15px;
	}
	.epc-search-row-photo__btn {
		background: #fff;
		border: 1px solid #dbe4f0;
		border-radius: 8px;
		cursor: pointer;
		display: block;
		height: 40px;
		padding: 2px;
		width: 40px;
	}
	.epc-search-row-photo__btn--load {
		color: #94a3b8;
		font-size: 15px;
	}
	.epc-search-row-photo__btn--load:hover {
		border-color: #2563eb;
		color: #2563eb;
	}
	.epc-fitment-brand-card__thumb--load {
		cursor: pointer;
		font: inherit;
	}
	.epc-fitment-brand-card__thumb--load:hover {
		border-color: #2563eb;
		color: #2563eb;
	}
	.epc-search-row-photo__btn img {
		height: 100%;
		object-fit: contain;
		width: 100%;
	}
	#all_table_products .th_manufacturer,
	#all_table_products .td_manufacturer {
		width: 8%;
	}
	#all_table_products .th_article,
	#all_table_products .td_article {
		width: 11%;
	}
	#all_table_products .th_name,
	#all_table_products .td_name {
		width: 20%;
		max-width: 0;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
	#all_table_products .th_exist,
	#all_table_products .td_exist {
		width: 6%;
		white-space: nowrap;
	}
	#all_table_products .th_time_to_exe,
	#all_table_products .td_time_to_exe {
		width: 9%;
		white-space: nowrap;
		line-height: 1.2 !important;
	}
	#all_table_products .th_info,
	#all_table_products .td_info {
		width: 8%;
		white-space: nowrap;
	}
	#all_table_products .th_price,
	#all_table_products .td_price {
		width: 11%;
		min-width: 76px;
		white-space: nowrap;
		text-align: right;
		overflow: visible;
	}
	#all_table_products .th_add_to_cart,
	#all_table_products .td_add_to_cart {
		width: 34%;
		min-width: 320px;
	}
	#all_table_products .th_color,
	#all_table_products .td_color {
		width: 5px;
		padding: 0 !important;
	}
	#all_table_products .td_name .info_box,
	#all_table_products .td_info .info_box {
		display: inline-block;
		max-width: 100%;
		overflow: hidden;
		text-overflow: ellipsis;
		vertical-align: middle;
		white-space: nowrap;
	}
	#procenka_div .td_add_to_cart,
	#procenka_div .th_add_to_cart,
	#products_area .td_add_to_cart,
	#products_area .th_add_to_cart {
		display: table-cell !important;
		min-width: 0;
		padding: 3px 5px !important;
		text-align: right;
		vertical-align: middle;
		white-space: nowrap;
	}
	.epc-product-actions {
		align-items: center;
		display: inline-flex;
		flex-direction: row;
		flex-wrap: nowrap;
		gap: 8px;
		justify-content: flex-end;
		min-width: 0;
		width: 100%;
	}
	.epc-product-actions__tools,
	.epc-product-actions__buy {
		align-items: center;
		display: inline-flex;
		flex: 0 0 auto;
		flex-wrap: nowrap;
		gap: 8px;
		justify-content: flex-start;
		width: auto;
	}
	.epc-product-actions .epc-fitment-check-btn--row,
	.epc-product-actions .epc-btn-fitment {
		flex: 0 0 auto;
	}
	.epc-product-actions__qty {
		flex: 0 0 auto;
	}
	.epc-product-actions .epc-btn-cart {
		flex: 0 0 auto;
	}
	.epc-product-actions .epc-btn-quote {
		flex: 0 0 auto;
	}
	.epc-product-actions .epc-fitment-check-btn--row,
	#all_table_products .epc-fitment-check-btn--row {
		align-items: center;
		background: linear-gradient(135deg, #2563eb, #1d4ed8) !important;
		border: 0 !important;
		border-radius: 999px !important;
		box-shadow: none;
		color: #fff !important;
		display: inline-flex;
		flex: 0 0 auto;
		font-size: 11px;
		font-weight: 800;
		gap: 5px;
		height: auto !important;
		justify-content: center;
		line-height: 1.2;
		margin: 0;
		padding: 6px 10px !important;
		text-decoration: none !important;
		text-transform: none;
		white-space: nowrap;
		width: auto;
	}
	.epc-product-actions .epc-fitment-check-btn--row:hover,
	.epc-product-actions .epc-fitment-check-btn--row:focus,
	#all_table_products .epc-fitment-check-btn--row:hover,
	#all_table_products .epc-fitment-check-btn--row:focus {
		background: linear-gradient(135deg, #1d4ed8, #1e40af) !important;
		color: #fff !important;
		outline: none;
	}
	.epc-product-actions--quote-only .epc-fitment-check-btn--row {
		margin-right: 0;
	}
	.epc-product-actions--quote-only {
		flex-wrap: nowrap;
		justify-content: flex-end;
	}
	.epc-product-actions .epc-wa-share-btn {
		align-items: center;
		background: #25d366 !important;
		border: 0 !important;
		border-radius: 999px !important;
		color: #fff !important;
		display: inline-flex;
		flex: 0 0 auto;
		font-size: 11px;
		font-weight: 800;
		gap: 4px;
		line-height: 1.2;
		margin: 0;
		padding: 6px 10px !important;
		text-decoration: none !important;
		white-space: nowrap;
	}
	.epc-product-actions .epc-wa-share-btn:hover,
	.epc-product-actions .epc-wa-share-btn:focus {
		background: #1da851 !important;
		color: #fff !important;
	}
	#all_table_products .td_add_to_cart .epc-product-actions {
		max-width: 100%;
		overflow-x: auto;
		-webkit-overflow-scrolling: touch;
		scrollbar-width: thin;
	}
	.epc-product-actions .epc-btn-cart,
	.epc-product-actions .epc-btn-quote {
		border-radius: 999px !important;
		display: inline-block;
		flex: 0 0 auto;
		font-size: 12px;
		font-weight: 800;
		line-height: 1.2;
		margin: 0;
		padding: 7px 14px;
		white-space: nowrap;
		width: auto;
	}
	.epc-product-actions .epc-btn-cart {
		background: #e53935 !important;
		border-color: #c62828 !important;
		color: #fff !important;
	}
	.epc-product-actions .epc-btn-quote {
		background: #ef4444 !important;
		border-color: #dc2626 !important;
		color: #fff !important;
	}
	.epc-product-actions__qty {
		align-items: center;
		display: inline-flex;
		flex: 0 0 auto;
		flex-direction: row;
		gap: 2px;
	}
	.epc-product-actions__qty .count_need_minus,
	.epc-product-actions__qty .count_need_plus {
		align-items: center;
		display: inline-flex;
		font-size: 12px;
		height: 28px;
		justify-content: center;
		line-height: 1;
		padding: 0;
		width: 26px;
	}
	.epc-product-actions__qty .epc-qty-input,
	.epc-product-actions__qty input[type="text"] {
		border: 1px solid #ddd;
		border-radius: 6px;
		font-size: 12px;
		height: 28px !important;
		margin: 0 !important;
		padding: 2px 4px !important;
		text-align: center;
		width: 34px !important;
	}
	.epc-product-actions__qty table {
		display: inline-flex;
		margin: 0;
	}
	.epc-product-actions__qty table tr {
		display: inline-flex;
		align-items: center;
		gap: 2px;
	}
	.epc-product-actions__qty table td {
		border: 0;
		display: inline-block;
		padding: 0;
	}
	.epc-brand-notice {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 12px;
		flex-wrap: wrap;
	}
	.epc-brand-notice__text {
		flex: 1 1 260px;
	}
	#all_table_products .epc-part-type-split td {
		border: 0 !important;
		padding: 12px 18px !important;
		text-align: center !important;
	}
	.epc-part-type-split__inner {
		align-items: center;
		display: flex;
		flex-direction: column;
		gap: 0;
		margin: 0 auto;
		max-width: 920px;
		text-align: center;
	}
	.epc-part-type-split__head {
		align-items: center;
		display: inline-flex;
		flex-wrap: wrap;
		gap: 10px;
		justify-content: center;
	}
	.epc-part-type-split--genuine td {
		background: linear-gradient(180deg, #a7f3d0 0%, #d1fae5 42%, #ecfdf5 100%) !important;
		border-bottom: 2px solid #34d399 !important;
		border-top: 3px solid #059669 !important;
		box-shadow: inset 0 1px 0 rgba(255, 255, 255, .55);
	}
	.epc-part-type-split--aftermarket td {
		background: linear-gradient(180deg, #93c5fd 0%, #bfdbfe 42%, #eff6ff 100%) !important;
		border-bottom: 2px solid #60a5fa !important;
		border-top: 3px solid #2563eb !important;
		box-shadow: inset 0 1px 0 rgba(255, 255, 255, .55);
	}
	.epc-part-type-badge {
		border-radius: 999px;
		box-shadow: 0 4px 10px rgba(15, 23, 42, .12);
		display: inline-block;
		font-size: 11px;
		font-weight: 900;
		letter-spacing: 0.06em;
		line-height: 1;
		padding: 6px 11px;
		text-transform: uppercase;
	}
	.epc-part-type-badge--genuine {
		background: #047857;
		color: #fff;
	}
	.epc-part-type-badge--am {
		background: #1d4ed8;
		color: #fff;
	}
	.epc-part-type-split__title {
		color: #0f172a;
		font-size: 20px;
		font-weight: 950;
		letter-spacing: 0.02em;
		line-height: 1.35;
	}
	.epc-part-type-split__title .epc-part-type-catalog-link {
		color: #047857;
		font-size: inherit;
		font-weight: 950;
		text-decoration: underline;
		white-space: nowrap;
	}
	.epc-part-type-count {
		color: #334155;
		font-size: 16px;
		font-weight: 800;
	}
	.epc-part-type-catalog-link {
		color: #047857;
		font-weight: 950;
		text-decoration: underline;
		white-space: nowrap;
	}
	#all_table_products tr.epc-part-type-row--genuine td {
		background: #f0fdf4 !important;
		border-bottom-color: #d1fae5 !important;
	}
	#all_table_products tr.epc-part-type-row--genuine:nth-child(even) td {
		background: #ecfdf5 !important;
	}
	#all_table_products tr.epc-part-type-row--aftermarket td {
		background: #f8fbff !important;
		border-bottom-color: #dbeafe !important;
	}
	#all_table_products tr.epc-part-type-row--aftermarket:nth-child(even) td {
		background: #eff6ff !important;
	}
	.epc-chpu-direct-part-search #all_table_products .th_add_to_cart,
	.epc-chpu-direct-part-search #all_table_products .td_add_to_cart {
		display: table-cell !important;
	}
	#all_table_products tbody .td_article,
	#all_table_products tbody .td_article a {
		font-weight: 700 !important;
	}
	#filter_position .one_property {
		background: #f8fafc;
		border: 1px solid #e2e8f0;
		border-radius: 14px;
		margin-bottom: 12px;
		padding: 12px;
	}
	#filter_position .one_property strong {
		color: #0f172a !important;
		display: block;
		font-size: 13px;
		font-weight: 950;
		margin-bottom: 8px;
		text-transform: uppercase;
	}
	.epc-balance-panel {
		align-items: center;
		background:
			radial-gradient(circle at 10% 18%, rgba(239, 68, 68, .24), transparent 28%),
			linear-gradient(135deg, #08111f 0%, #111827 60%, #1f2937 100%);
		border: 1px solid rgba(255, 255, 255, .12);
		border-radius: 24px;
		box-shadow: 0 24px 64px rgba(15, 23, 42, .18);
		display: flex;
		gap: 22px;
		margin: 6px 0 18px;
		overflow: hidden;
		padding: 28px;
		position: relative;
	}
	.epc-balance-panel:after {
		background: linear-gradient(90deg, rgba(255, 255, 255, .18), transparent);
		content: "";
		height: 1px;
		left: 28px;
		position: absolute;
		right: 28px;
		top: 0;
	}
	.epc-balance-panel__icon {
		align-items: center;
		background: rgba(239, 68, 68, .14);
		border: 1px solid rgba(248, 113, 113, .35);
		border-radius: 22px;
		box-shadow: inset 0 0 0 1px rgba(255, 255, 255, .08);
		color: #fecaca !important;
		display: flex;
		flex: 0 0 82px;
		font-size: 34px;
		height: 82px;
		justify-content: center;
		width: 82px;
	}
	.epc-balance-panel__content {
		flex: 1 1 auto;
		min-width: 0;
	}
	.epc-balance-panel__eyebrow {
		color: #fecaca !important;
		display: block;
		font-size: 12px;
		font-weight: 950;
		letter-spacing: .1em;
		margin-bottom: 8px;
		text-transform: uppercase;
	}
	.epc-balance-panel h2 {
		color: #fff !important;
		font-size: clamp(26px, 3vw, 38px);
		font-weight: 950;
		letter-spacing: -.035em;
		line-height: 1.08;
		margin: 0 0 10px;
	}
	.epc-balance-panel p {
		color: rgba(226, 232, 240, .82) !important;
		font-size: 15px;
		font-weight: 650;
		line-height: 1.6;
		margin: 0;
		max-width: 760px;
	}
	.epc-balance-panel__amount {
		color: #fff !important;
		font-size: clamp(34px, 5vw, 56px);
		font-weight: 950;
		letter-spacing: -.04em;
		line-height: 1;
		margin: 12px 0 8px;
	}
	.epc-balance-panel__chips {
		display: flex;
		flex-wrap: wrap;
		gap: 10px;
		margin-top: 16px;
	}
	.epc-balance-panel__chips span {
		background: rgba(255, 255, 255, .09);
		border: 1px solid rgba(255, 255, 255, .13);
		border-radius: 999px;
		color: #fff !important;
		font-size: 12px;
		font-weight: 850;
		padding: 9px 12px;
	}
	.epc-balance-panel__chips i {
		color: #f87171;
		margin-right: 5px;
	}
	.epc-quotes-panel {
		align-items: center;
		background:
			radial-gradient(circle at 10% 18%, rgba(239, 68, 68, .24), transparent 28%),
			radial-gradient(circle at 88% 12%, rgba(37, 99, 235, .20), transparent 30%),
			linear-gradient(135deg, #08111f 0%, #111827 60%, #1f2937 100%);
		border: 1px solid rgba(255, 255, 255, .12);
		border-radius: 24px;
		box-shadow: 0 24px 64px rgba(15, 23, 42, .18);
		display: flex;
		gap: 22px;
		margin: 6px 0 18px;
		overflow: hidden;
		padding: 28px;
		position: relative;
	}
	.epc-quotes-panel:after {
		background: linear-gradient(90deg, rgba(255, 255, 255, .18), transparent);
		content: "";
		height: 1px;
		left: 28px;
		position: absolute;
		right: 28px;
		top: 0;
	}
	.epc-quotes-panel__icon {
		align-items: center;
		background: rgba(239, 68, 68, .14);
		border: 1px solid rgba(248, 113, 113, .35);
		border-radius: 22px;
		color: #fecaca !important;
		display: flex;
		flex: 0 0 82px;
		font-size: 34px;
		height: 82px;
		justify-content: center;
		width: 82px;
	}
	.epc-quotes-panel__content {
		flex: 1 1 auto;
		min-width: 0;
	}
	.epc-quotes-panel__eyebrow {
		color: #fecaca !important;
		display: block;
		font-size: 12px;
		font-weight: 950;
		letter-spacing: .1em;
		margin-bottom: 8px;
		text-transform: uppercase;
	}
	.epc-quotes-panel h2 {
		color: #fff !important;
		font-size: clamp(26px, 3vw, 38px);
		font-weight: 950;
		letter-spacing: -.035em;
		line-height: 1.08;
		margin: 0 0 10px;
	}
	.epc-quotes-panel p {
		color: rgba(226, 232, 240, .82) !important;
		font-size: 15px;
		font-weight: 650;
		line-height: 1.6;
		margin: 0;
		max-width: 780px;
	}
	.epc-quotes-panel__chips {
		display: flex;
		flex-wrap: wrap;
		gap: 10px;
		margin-top: 16px;
	}
	.epc-quotes-panel__chips span {
		background: rgba(255, 255, 255, .09);
		border: 1px solid rgba(255, 255, 255, .13);
		border-radius: 999px;
		color: #fff !important;
		font-size: 12px;
		font-weight: 850;
		padding: 9px 12px;
	}
	.epc-quotes-panel__chips i {
		color: #f87171;
		margin-right: 5px;
	}
	.epc-quotes-panel__actions {
		margin-top: 18px;
	}
	.epc-quotes-panel__actions .btn {
		border-radius: 12px !important;
		box-shadow: 0 14px 30px rgba(239, 68, 68, .24);
		font-weight: 950;
		padding: 11px 16px;
	}
	.epc-quotes-detail,
	.epc-quotes-login-form,
	.epc-quotes-empty {
		background: #fff !important;
		border: 1px solid #e2e8f0 !important;
		border-radius: 18px !important;
		box-shadow: 0 16px 42px rgba(15, 23, 42, .08);
		margin-bottom: 18px;
		padding: 18px;
	}
	.epc-quotes-detail table.table,
	#Container table.table {
		background: #fff;
	}
	.epc-quotes-detail table.table > thead > tr > th,
	#Container table.table > thead > tr > th {
		background: #0f172a !important;
		border-color: rgba(255, 255, 255, .08) !important;
		color: #fff !important;
		font-size: 12px;
		font-weight: 950;
		letter-spacing: .04em;
		text-transform: uppercase;
	}
	.epc-quotes-detail table.table > tbody > tr > td,
	#Container table.table > tbody > tr > td {
		border-color: #e5edf6 !important;
		color: #1f2937 !important;
		font-weight: 700;
		vertical-align: middle;
	}
	.epc-quotes-admin-note,
	.epc-quotes-empty {
		color: #334155 !important;
		font-weight: 800;
	}
	.product_page {
		background: #f8fafc;
		border-radius: 24px;
		box-shadow: inset 0 0 0 1px rgba(226, 232, 240, .8);
		margin-bottom: 24px;
		overflow: hidden;
		padding: 18px;
	}
	.product_page h1 {
		background: linear-gradient(135deg, #0f172a, #1e293b 58%, #991b1b);
		border-radius: 22px;
		color: #fff !important;
		font-size: clamp(28px, 3vw, 42px);
		font-weight: 950;
		letter-spacing: -.035em;
		margin: 0 0 20px;
		padding: 26px 30px;
	}
	.product_page .div_product_img_big,
	.product_page .div_product_price,
	.product_page .product_suggestions_box,
	.product_page .tab-content {
		background: #fff !important;
		border: 1px solid #e2e8f0 !important;
		border-radius: 20px !important;
		box-shadow: 0 18px 44px rgba(15, 23, 42, .08);
		overflow: hidden;
	}
	.product_page .div_product_img_big {
		align-items: center;
		display: flex;
		justify-content: center;
		min-height: 330px;
		padding: 18px;
	}
	.product_page .div_product_img_big img {
		height: auto;
		max-height: 300px;
		max-width: 100%;
		object-fit: contain;
	}
	.epc-product-placeholder {
		align-items: center;
		background: radial-gradient(circle at top, rgba(239, 68, 68, .12), transparent 36%), linear-gradient(135deg, #f8fafc, #eef2f7);
		border: 1px dashed #cbd5e1;
		border-radius: 18px;
		color: #334155;
		display: flex;
		flex-direction: column;
		gap: 8px;
		min-height: 280px;
		padding: 26px;
		text-align: center;
		width: 100%;
	}
	.epc-product-placeholder i {
		color: #dc2626;
		font-size: 52px;
	}
	.epc-product-placeholder span {
		color: #64748b;
		font-size: 12px;
		font-weight: 950;
		letter-spacing: .08em;
		text-transform: uppercase;
	}
	.epc-product-placeholder strong {
		color: #0f172a;
		font-size: 18px;
		line-height: 1.35;
	}
	.product_page .div_product_price {
		padding: 22px;
	}
	.product_page .price_div_text {
		color: #dc2626 !important;
		font-size: 28px;
		font-weight: 950;
		line-height: 1.1;
	}
	.product_page .product_div_manufacturer,
	.product_page .product_div_article {
		background: #f1f5f9;
		border-radius: 999px;
		color: #0f172a !important;
		display: inline-flex;
		font-weight: 950;
		margin: 8px 8px 0 0;
		padding: 8px 12px;
	}
	.epc-product-quote-note,
	.epc-product-availability-card {
		background: linear-gradient(135deg, #0f172a, #1e293b);
		border-radius: 18px;
		box-shadow: 0 18px 40px rgba(15, 23, 42, .18);
		color: #fff;
		margin-top: 16px;
		padding: 18px;
	}
	.epc-product-quote-note {
		align-items: center;
		display: flex;
		gap: 14px;
		justify-content: space-between;
	}
	.epc-product-quote-note strong,
	.epc-product-availability-card h3 {
		color: #fff !important;
		display: block;
		font-weight: 950;
		margin: 0 0 4px;
	}
	.epc-product-quote-note span,
	.epc-product-availability-card p {
		color: rgba(226, 232, 240, .86) !important;
		font-weight: 700;
	}
	.epc-product-availability-card__eyebrow {
		color: #fecaca;
		display: block;
		font-size: 12px;
		font-weight: 950;
		letter-spacing: .08em;
		margin-bottom: 7px;
		text-transform: uppercase;
	}
	.epc-product-availability-card__chips {
		display: flex;
		flex-wrap: wrap;
		gap: 8px;
		margin: 14px 0;
	}
	.epc-product-availability-card__chips span {
		background: rgba(255, 255, 255, .1);
		border-radius: 999px;
		color: #fff !important;
		font-size: 12px;
		font-weight: 850;
		padding: 8px 10px;
	}
	.product_page .product_suggestions_header {
		background: #0f172a !important;
		color: #fff !important;
		font-weight: 950;
	}
	.product_page .section-title {
		color: #0f172a !important;
		font-weight: 950;
	}
	.product_page .nav-tabs > li > a {
		color: #0f172a !important;
		font-weight: 900;
	}
	@media (max-width: 1199px) {
		.logo-line .table-group {
			gap: 14px 16px;
			grid-template-columns: minmax(220px, 34%) 1fr;
		}
		.logo-line .table-control:nth-child(1) {
			grid-column: 1;
			grid-row: 1;
		}
		.logo-line .epc-header-contact-col {
			grid-column: 2;
			grid-row: 1;
		}
		.logo-line .epc-header-right-col {
			grid-column: 1 / -1;
			grid-row: 2;
		}
		.epc-header-actions-row {
			justify-content: center;
		}
		.header-phone-box,
		.header-phone-box .phone {
			text-align: center;
		}
		.epc-header-right-col {
			align-items: center;
		}
	}
	@media (max-width: 991px) {
		.logo-line .table-group {
			grid-template-columns: 1fr;
		}
		.logo-line .table-control:nth-child(1),
		.logo-line .epc-header-contact-col,
		.logo-line .epc-header-right-col {
			grid-column: 1 / -1;
			grid-row: auto;
		}
		.header-logo {
			justify-content: center;
		}
		.header-logo .epc-animated-logo {
			transform: scale(1.05);
			transform-origin: center center;
		}
		.epc-header-contact-col {
			align-items: center;
		}
		.epc-header-right-col {
			align-items: center;
		}
		.header-phone-box .phone {
			font-size: 24px;
			text-align: center;
		}
		.epc-header-actions-row {
			justify-content: center;
		}
		.schearch-line .row {
			flex-wrap: wrap;
		}
		.schearch-line .col-sm-7,
		.schearch-line .col-md-8,
		.schearch-line .col-sm-5,
		.schearch-line .col-md-4 {
			flex: 1 1 100%;
			width: 100%;
		}
		.menu-box {
			justify-content: center;
			margin-top: 12px;
		}
		.epc-parts-result-hero {
			flex-direction: column;
			padding: 22px;
		}
		.epc-parts-result-hero__card {
			flex-basis: auto;
		}
		.epc-balance-panel {
			align-items: flex-start;
			flex-direction: column;
			padding: 22px;
		}
		.epc-quotes-panel {
			align-items: flex-start;
			flex-direction: column;
			padding: 22px;
		}
		.header-phone-box .phone {
			font-size: 18px;
		}
	}
	@media (max-width: 767px) {
		.epc-cross-modal {
			padding: 8px;
		}
		.epc-cross-modal__body {
			grid-template-columns: 1fr;
			max-height: calc(92vh - 168px);
			padding: 10px;
		}
		.epc-cross-modal__tools .form-control,
		.epc-cross-modal__tools .btn {
			width: 100%;
		}
		.epc-parts-result-hero {
			border-radius: 18px;
			margin-top: 4px;
			padding: 18px;
		}
		.epc-parts-result-hero__chips {
			gap: 8px;
		}
		.epc-parts-result-hero__chips span {
			width: 100%;
		}
		.epc-parts-result-hero__article {
			font-size: 28px;
		}
		.epc-balance-panel {
			border-radius: 18px;
			padding: 18px;
		}
		.epc-balance-panel__icon {
			flex-basis: 64px;
			font-size: 26px;
			height: 64px;
			width: 64px;
		}
		.epc-balance-panel__chips span {
			width: 100%;
		}
		.epc-quotes-panel {
			border-radius: 18px;
			padding: 18px;
		}
		.epc-quotes-panel__icon {
			flex-basis: 64px;
			font-size: 26px;
			height: 64px;
			width: 64px;
		}
		.epc-quotes-panel__chips span {
			width: 100%;
		}
		#footer-widgets {
			padding: 32px 0 24px;
		}
		.header-search-box {
			border-radius: 12px;
		}
		.epc-animated-logo {
			gap: 5px;
		}
		.epc-animated-logo__mark {
			height: 34px;
			width: 76px;
		}
		.epc-animated-logo__text {
			font-size: 27px;
		}
	}
	.epc-umapi-manufacturer-tabs {
		display: flex;
		flex-wrap: wrap;
		gap: 7px;
		margin: 12px 0 18px;
	}
	.epc-umapi-manufacturer-tabs button {
		align-items: center;
		background: #fff !important;
		border: 1px solid #d8e0ea !important;
		border-radius: 10px !important;
		box-shadow: 0 8px 18px rgba(15, 23, 42, .06);
		color: #172536 !important;
		display: inline-flex;
		font-weight: 900;
		justify-content: center;
		min-height: 36px;
		min-width: 38px;
		padding: 8px 11px;
	}
	.epc-umapi-manufacturer-tabs button.active {
		background: linear-gradient(135deg, #2563eb, #1d4ed8) !important;
		border-color: #2563eb !important;
		color: #fff !important;
	}
	.epc-umapi-manufacturer-tabs button:disabled {
		cursor: not-allowed;
		opacity: .42;
	}
	.epc-umapi-manufacturer-card {
		border: 1px solid #e2e8f0 !important;
		border-radius: 14px !important;
		box-shadow: 0 10px 22px rgba(15, 23, 42, .06);
		min-height: 118px !important;
	}
	.epc-umapi-popular-section {
		display: block !important;
	}
	.epc-umapi-popular-section .epc-umapi-manufacturer-card {
		background: linear-gradient(180deg, #fff, #f8fbff) !important;
	}
	.epc-umapi-other-section {
		margin-top: 10px !important;
	}
	.epc-umapi-other-section .epc-umapi-section-title {
		background: #fff8ef;
		border-radius: 4px;
		color: #d97706 !important;
		font-size: 13px !important;
		font-weight: 900 !important;
		margin: 10px 0 6px !important;
		padding: 7px 12px !important;
	}
	.epc-umapi-other-section .epc-umapi-section-title span {
		color: #8a5a14 !important;
		font-size: 12px !important;
	}
	.epc-umapi-other-section .epc-umapi-manufacturer-grid {
		gap: 5px !important;
		grid-template-columns: repeat(auto-fill, minmax(145px, 1fr)) !important;
	}
	.epc-umapi-other-section .epc-umapi-manufacturer-card {
		align-items: center !important;
		background: #fff !important;
		border: 1px solid #e6ebf1 !important;
		border-radius: 5px !important;
		box-shadow: none !important;
		gap: 2px !important;
		justify-content: center !important;
		min-height: 48px !important;
		padding: 6px 24px 5px 8px !important;
		position: relative !important;
		text-align: center !important;
		transition: border-color .18s ease, box-shadow .18s ease, transform .18s ease;
	}
	.epc-umapi-other-section .epc-umapi-manufacturer-card:hover {
		border-color: #f59e0b !important;
		box-shadow: 0 6px 14px rgba(15, 23, 42, .08) !important;
		transform: translateY(-1px);
	}
	.epc-umapi-other-section .epc-umapi-manufacturer-card .epc-umapi-logo {
		display: none !important;
	}
	.epc-umapi-other-section .epc-umapi-manufacturer-card strong {
		color: #1f2937 !important;
		display: block !important;
		font-size: 11px !important;
		font-weight: 900 !important;
		letter-spacing: .01em;
		line-height: 1.15 !important;
		max-width: 100%;
		overflow: hidden;
		text-overflow: ellipsis;
		text-transform: uppercase;
		white-space: nowrap;
	}
	.epc-umapi-other-section .epc-umapi-compact-name {
		color: #6b7280;
		display: block;
		font-size: 10px;
		line-height: 1.1;
		max-width: 100%;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
	.epc-umapi-other-section .epc-umapi-manufacturer-card small {
		font-size: 12px !important;
		line-height: 1 !important;
		min-height: 0 !important;
		position: absolute;
		right: 6px;
		top: 6px;
	}
	.epc-umapi-section[data-epc-letter-section] {
		display: none;
	}
	.epc-umapi-section[data-epc-letter-section].active {
		display: block;
	}
</style>
<script>
(function(){
	function esc(value) {
		return String(value || '').replace(/[&<>"']/g, function(ch) {
			return ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'})[ch];
		});
	}
	function letterFor(title) {
		var letter = String(title || '').trim().charAt(0).toUpperCase();
		return /^[A-Z]$/.test(letter) ? letter : '0-9';
	}
	function keepOnlyCountryFlag(card) {
		var country = card ? card.querySelector('small') : null;
		if (!country || country.getAttribute('data-epc-flag-only') === '1') {
			return;
		}
		var flag = country.querySelector('img');
		if (flag) {
			country.innerHTML = '';
			country.appendChild(flag);
			country.setAttribute('data-epc-flag-only', '1');
		} else {
			country.style.display = 'none';
		}
	}
	function umapiManufacturersKey(output) {
		var cards = output.querySelectorAll('.epc-umapi-manufacturer-card');
		if (!cards.length) {
			return '';
		}
		var first = cards[0].querySelector('strong');
		var last = cards[cards.length - 1].querySelector('strong');
		return cards.length + '|' + (first ? first.textContent : '') + '|' + (last ? last.textContent : '');
	}
	function shouldRebuildUmapiManufacturers(output, mutations) {
		var i;
		var m;
		var nodes;
		var n;
		var node;
		for (i = 0; i < mutations.length; i++) {
			m = mutations[i];
			if (m.type !== 'childList') {
				continue;
			}
			if (m.target === output) {
				return true;
			}
			nodes = [];
			Array.prototype.forEach.call(m.addedNodes, function (node) { nodes.push(node); });
			Array.prototype.forEach.call(m.removedNodes, function (node) { nodes.push(node); });
			for (n = 0; n < nodes.length; n++) {
				node = nodes[n];
				if (node.nodeType !== 1) {
					continue;
				}
				if (node.classList && node.classList.contains('epc-umapi-manufacturer-tabs')) {
					continue;
				}
				if (node.classList && (node.classList.contains('epc-umapi-section') || node.classList.contains('epc-umapi-manufacturer-card'))) {
					return true;
				}
				if (node.querySelector && node.querySelector('.epc-umapi-manufacturer-card')) {
					return true;
				}
			}
		}
		return false;
	}
	function scheduleUmapiManufacturerEnhance(root, force) {
		if (!root) {
			return;
		}
		if (root._epcUmapiEnhanceTimer) {
			window.clearTimeout(root._epcUmapiEnhanceTimer);
		}
		var tries = 0;
		function attempt() {
			var output = root.querySelector('#epc-umapi-output');
			var ready = output && output.querySelector('.epc-umapi-manufacturer-card');
			if (!ready) {
				if (tries < 30) {
					tries += 1;
					root._epcUmapiEnhanceTimer = window.setTimeout(attempt, 200);
				}
				return;
			}
			if (enhanceManufacturerTabs(root, force)) {
				return;
			}
			if (tries < 30) {
				tries += 1;
				root._epcUmapiEnhanceTimer = window.setTimeout(attempt, 200);
			}
		}
		root._epcUmapiEnhanceTimer = window.setTimeout(attempt, 120);
	}
	function enhanceManufacturerTabs(root, force) {
		if (!root) {
			return false;
		}
		var output = root.querySelector('#epc-umapi-output');
		if (!output || !output.querySelector('.epc-umapi-manufacturer-card')) {
			return false;
		}
		var mfgKey = umapiManufacturersKey(output);
		if (!force && mfgKey && root._epcUmapiMfgKey === mfgKey && output.querySelector('.epc-umapi-manufacturer-tabs')) {
			return true;
		}
		root._epcUmapiEnhancing = true;
		root._epcUmapiMfgKey = mfgKey;
		Array.prototype.forEach.call(output.querySelectorAll('.epc-umapi-manufacturer-tabs'), function(existingTabs) {
			if (existingTabs.parentNode) {
				existingTabs.parentNode.removeChild(existingTabs);
			}
		});
		Array.prototype.forEach.call(output.querySelectorAll('.epc-umapi-section[data-epc-letter-section]'), function(section) {
			section.removeAttribute('data-epc-letter-section');
			section.classList.remove('active', 'epc-umapi-other-section');
			section.style.display = '';
			Array.prototype.forEach.call(section.querySelectorAll('.epc-umapi-compact-name'), function(label) {
				if (label.parentNode) {
					label.parentNode.removeChild(label);
				}
			});
		});
		var sections = Array.prototype.slice.call(output.querySelectorAll('.epc-umapi-section'));
		var popularSection = null;
		var letterSections = [];
		sections.forEach(function(section) {
			var title = section.querySelector('.epc-umapi-section-title');
			var text = title ? title.textContent : '';
			if (/popular/i.test(text)) {
				popularSection = section;
				section.classList.add('epc-umapi-popular-section');
				Array.prototype.forEach.call(section.querySelectorAll('.epc-umapi-manufacturer-card'), keepOnlyCountryFlag);
			} else if (section.querySelector('.epc-umapi-manufacturer-card')) {
				letterSections.push(section);
			}
		});
		if (!letterSections.length) {
			root._epcUmapiEnhancing = false;
			return false;
		}
		var available = {};
		letterSections.forEach(function(section) {
			var title = section.querySelector('.epc-umapi-section-title');
			var letter = letterFor(title ? title.textContent : '');
			var idMatch = (section.getAttribute('id') || '').match(/^epc-letter-(.+)$/);
			if (idMatch && idMatch[1]) {
				letter = idMatch[1];
			}
			section.setAttribute('data-epc-letter-section', letter);
			section.classList.add('epc-umapi-other-section');
			Array.prototype.forEach.call(section.querySelectorAll('.epc-umapi-manufacturer-card'), function(card) {
				keepOnlyCountryFlag(card);
				var strong = card.querySelector('strong');
				if (!strong || card.querySelector('.epc-umapi-compact-name')) {
					return;
				}
				var compactName = document.createElement('span');
				compactName.className = 'epc-umapi-compact-name';
				compactName.textContent = strong.textContent.toLowerCase();
				strong.parentNode.insertBefore(compactName, strong.nextSibling);
			});
			available[letter] = true;
		});
		var letters = ['All','0-9','A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z'];
		var tabs = document.createElement('div');
		tabs.className = 'epc-umapi-manufacturer-tabs';
		tabs.innerHTML = letters.map(function(letter) {
			var disabled = letter !== 'All' && !available[letter] ? ' disabled' : '';
			return '<button type="button" data-epc-letter="' + esc(letter) + '"' + disabled + '>' + esc(letter) + '</button>';
		}).join('');
		var oldAlpha = output.querySelector('.epc-umapi-alpha');
		if (oldAlpha && oldAlpha.parentNode) {
			oldAlpha.parentNode.replaceChild(tabs, oldAlpha);
		} else if (popularSection && popularSection.parentNode) {
			popularSection.parentNode.insertBefore(tabs, popularSection.nextSibling);
		} else {
			output.insertBefore(tabs, letterSections[0]);
		}
		function defaultLetter() {
			if (available['A']) {
				return 'A';
			}
			var order = ['0-9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'];
			for (var i = 0; i < order.length; i++) {
				if (available[order[i]]) {
					return order[i];
				}
			}
			return 'All';
		}
		function activate(letter) {
			Array.prototype.forEach.call(tabs.querySelectorAll('button'), function(button) {
				button.classList.toggle('active', button.getAttribute('data-epc-letter') === letter);
			});
			letterSections.forEach(function(section) {
				section.classList.toggle('active', letter === 'All' || section.getAttribute('data-epc-letter-section') === letter);
			});
			if (popularSection) {
				popularSection.style.display = '';
			}
		}
		Array.prototype.forEach.call(tabs.querySelectorAll('button'), function(button) {
			button.onclick = function() {
				if (!button.disabled) {
					activate(button.getAttribute('data-epc-letter'));
				}
			};
		});
		activate(defaultLetter());
		root._epcUmapiEnhancing = false;
		return true;
	}
	function watchUmapiCatalog() {
		var root = document.getElementById('epc-umapi');
		if (!root) {
			return;
		}
		var output = document.getElementById('epc-umapi-output');
		if (!output) {
			return;
		}
		scheduleUmapiManufacturerEnhance(root, true);
		root.addEventListener('click', function (event) {
			var tab = event.target && event.target.closest ? event.target.closest('.epc-umapi-tab') : null;
			if (tab && root.contains(tab)) {
				root._epcUmapiMfgKey = '';
				scheduleUmapiManufacturerEnhance(root, true);
			}
		});
		var timer = null;
		new MutationObserver(function(mutations) {
			if (root._epcUmapiEnhancing) {
				return;
			}
			if (!shouldRebuildUmapiManufacturers(output, mutations)) {
				return;
			}
			if (timer) {
				window.clearTimeout(timer);
			}
			timer = window.setTimeout(function() {
				scheduleUmapiManufacturerEnhance(root, false);
			}, 280);
		}).observe(output, {childList:true, subtree:true});
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', watchUmapiCatalog);
	} else {
		watchUmapiCatalog();
	}
})();
</script>
<style>
	.epc-applicability-btn {
		background: #0f766e !important;
		border-color: #0f766e !important;
		margin-left: 6px;
	}
	.epc-applicability-panel {
		background: #fff;
		border: 1px solid #dbe5ef;
		border-radius: 14px;
		box-shadow: 0 16px 36px rgba(15, 23, 42, .10);
		margin: 12px 0 16px;
		overflow: hidden;
	}
	.epc-applicability-panel__head {
		align-items: center;
		background: linear-gradient(135deg, #ecfeff, #f8fafc);
		border-bottom: 1px solid #dbe5ef;
		display: flex;
		gap: 12px;
		justify-content: space-between;
		padding: 12px 14px;
	}
	.epc-applicability-panel__head strong {
		color: #0f172a;
		font-size: 15px;
	}
	.epc-applicability-panel__body {
		padding: 14px;
	}
	.epc-applicability-panel__loader {
		background: #f8fafc;
		border: 1px dashed #cbd5e1;
		border-radius: 10px;
		color: #475569;
		padding: 14px;
	}
	.epc-cross-fallback {
		background: #fff;
		border: 1px solid #dbe5ef;
		border-radius: 14px;
		box-shadow: 0 16px 36px rgba(15, 23, 42, .08);
		margin: 18px 0;
		overflow: hidden;
		text-align: left;
	}
	.epc-cross-fallback__head {
		background: linear-gradient(135deg, #eff6ff, #f8fafc);
		border-bottom: 1px solid #dbe5ef;
		padding: 14px 16px;
	}
	.epc-cross-fallback__head strong {
		color: #0f172a;
		display: block;
		font-size: 17px;
		font-weight: 900;
	}
	.epc-cross-fallback__head span {
		color: #64748b;
		display: block;
		font-size: 13px;
		margin-top: 4px;
	}
	.epc-cross-fallback__table {
		margin: 0 !important;
	}
	.epc-cross-fallback__table th {
		background: #f8fafc !important;
		color: #334155 !important;
		font-weight: 900 !important;
	}
	.epc-cross-stock-results {
		background: #fff;
		border: 1px solid #bbf7d0;
		border-radius: 16px;
		box-shadow: 0 16px 42px rgba(15, 23, 42, .08);
		margin-top: 18px;
		overflow: hidden;
		text-align: left;
	}
	.epc-cross-stock-results__head {
		background: linear-gradient(135deg, #ecfdf5, #f8fafc);
		border-bottom: 1px solid #bbf7d0;
		padding: 15px 17px;
	}
	.epc-cross-stock-results__head strong {
		color: #064e3b;
		display: block;
		font-size: 18px;
		font-weight: 950;
	}
	.epc-cross-stock-results__head span {
		color: #047857;
		display: block;
		font-size: 13px;
		font-weight: 750;
		margin-top: 4px;
	}
	.epc-cross-stock-results table {
		margin: 0 !important;
	}
	.epc-cross-stock-results th {
		background: #f0fdf4 !important;
		color: #065f46 !important;
		font-weight: 900 !important;
	}
	.epc-cross-section {
		background: #fff;
		border: 1px solid #e2e8f0;
		border-radius: 16px;
		box-shadow: 0 12px 32px rgba(15, 23, 42, .06);
		margin-top: 22px;
		overflow: hidden;
		text-align: left;
	}
	.epc-cross-section__head {
		background: linear-gradient(135deg, #f8fafc, #fff);
		border-bottom: 1px solid #e2e8f0;
		padding: 14px 16px;
	}
	.epc-cross-section__head strong {
		color: #334155;
		display: block;
		font-size: 17px;
		font-weight: 900;
	}
	.epc-cross-section__head span {
		color: #64748b;
		display: block;
		font-size: 13px;
		font-weight: 600;
		margin-top: 4px;
	}
	.epc-cross-section__table-wrap {
		padding: 0;
	}
	.epc-cross-section__table {
		margin: 0 !important;
	}
	.epc-cross-section__table thead th {
		background: #f0fdf4 !important;
		color: #065f46 !important;
		font-weight: 900 !important;
	}
	.epc-cross-section__table .epc-cross-section__dash {
		color: #94a3b8;
	}
	.epc-cross-section__more {
		color: #64748b;
		font-size: 13px;
		font-weight: 600;
		margin: 0;
		padding: 12px 16px 16px;
	}
	.epc-cross-in-stock__table-wrap,
	.epc-cross-in-stock__table {
		padding: 0;
		margin: 0 !important;
	}
	.epc-umapi-category-shell {
		align-items: flex-start;
		display: grid;
		gap: 24px;
		grid-template-columns: 150px minmax(0, 1fr);
	}
	.epc-umapi-category-icons {
		display: grid !important;
		gap: 8px !important;
		grid-template-columns: repeat(2, 64px) !important;
	}
	.epc-umapi-category-icons .epc-umapi-card {
		align-items: center !important;
		background: linear-gradient(135deg, #315f91, #244a75) !important;
		border: 0 !important;
		border-radius: 8px !important;
		box-shadow: 0 8px 18px rgba(15, 23, 42, .16);
		color: #fff !important;
		display: flex !important;
		height: 58px !important;
		justify-content: center !important;
		min-height: 58px !important;
		padding: 0 !important;
		position: relative;
		width: 64px !important;
	}
	.epc-umapi-category-icons .epc-umapi-card strong {
		display: none !important;
	}
	.epc-umapi-cat-icon {
		color: #fff;
		font-size: 28px;
		line-height: 1;
	}
	.epc-umapi-category-list h3,
	.epc-umapi-category-icons-panel h3 {
		font-size: 18px;
		font-weight: 850;
		margin: 0 0 12px;
	}
	.epc-umapi-category-list .epc-umapi-tree ul {
		list-style: none !important;
		margin: 0 !important;
		padding-left: 20px !important;
	}
	.epc-umapi-category-list .epc-umapi-tree > ul {
		padding-left: 0 !important;
	}
	.epc-umapi-category-list .epc-umapi-tree li {
		margin: 0 !important;
		padding: 3px 0 !important;
	}
	.epc-umapi-category-list .epc-umapi-tree li > ul {
		display: none;
	}
	.epc-umapi-category-list .epc-umapi-tree li.expanded > ul {
		display: block;
	}
	.epc-umapi-cat-expander {
		align-items: center;
		color: #334155;
		cursor: pointer;
		display: inline-flex;
		font-size: 18px;
		font-weight: 950;
		height: 24px;
		justify-content: center;
		margin-right: 8px;
		vertical-align: middle;
		width: 18px;
	}
	.epc-umapi-category-list .epc-umapi-tree button {
		background: transparent !important;
		border: 0 !important;
		color: #1f4f7a !important;
		font-size: 15px;
		font-weight: 650;
		padding: 5px 4px !important;
		text-align: left;
	}
	.epc-umapi-category-list .epc-umapi-tree button:hover {
		color: #ef4444 !important;
		text-decoration: underline;
	}
	#applicability_widget table {
		max-width: 100%;
		width: 100%;
	}
	@media (max-width: 767px) {
		.epc-umapi-category-shell {
			grid-template-columns: 1fr;
		}
		.epc-umapi-category-icons {
			grid-template-columns: repeat(auto-fill, minmax(58px, 1fr)) !important;
		}
		.epc-applicability-btn {
			display: block;
			margin: 6px 0 0;
			width: 100%;
		}
	}
</style>
<script>
(function(){
	function clearUmapiFilter() {
		var filter = document.getElementById('epc-umapi-filter');
		var output = document.getElementById('epc-umapi-output');
		if (!filter || !output || filter.value === '') {
			return;
		}
		filter.value = '';
		Array.prototype.forEach.call(output.querySelectorAll('[data-search]'), function(node) {
			node.style.display = '';
		});
	}
	function recoverHiddenUmapiList() {
		var filter = document.getElementById('epc-umapi-filter');
		var output = document.getElementById('epc-umapi-output');
		if (!filter || !output || filter.value === '') {
			return;
		}
		var rows = Array.prototype.slice.call(output.querySelectorAll('[data-search]'));
		if (!rows.length) {
			return;
		}
		var visible = rows.some(function(node) {
			return node.style.display !== 'none';
		});
		if (!visible) {
			clearUmapiFilter();
		}
	}
	function watchUmapiFilterReset() {
		var root = document.getElementById('epc-umapi');
		var output = document.getElementById('epc-umapi-output');
		if (!root || !output || root.getAttribute('data-epc-filter-reset-ready') === '1') {
			return;
		}
		root.setAttribute('data-epc-filter-reset-ready', '1');
		root.addEventListener('click', function(event) {
			var target = event.target;
			while (target && target !== root) {
				if (
					target.hasAttribute('data-index') ||
					target.hasAttribute('data-category') ||
					target.hasAttribute('data-product') ||
					target.hasAttribute('data-step') ||
					target.id === 'epc-umapi-back-categories'
				) {
					window.setTimeout(clearUmapiFilter, 0);
					window.setTimeout(clearUmapiFilter, 300);
					return;
				}
				target = target.parentNode;
			}
		});
		var timer = null;
		new MutationObserver(function() {
			if (timer) {
				window.clearTimeout(timer);
			}
			timer = window.setTimeout(recoverHiddenUmapiList, 120);
		}).observe(output, {childList:true, subtree:true});
		recoverHiddenUmapiList();
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', watchUmapiFilterReset);
	} else {
		watchUmapiFilterReset();
	}
})();
</script>
<script>
(function(){
	function iconForCategory(label) {
		label = String(label || '').toLowerCase();
		if (/body|bonnet|bumper|panel/.test(label)) { return 'fa-car'; }
		if (/engine|motor/.test(label)) { return 'fa-cogs'; }
		if (/drive|clutch|gearbox|transmission|axle/.test(label)) { return 'fa-sitemap'; }
		if (/filter/.test(label)) { return 'fa-filter'; }
		if (/window|cleaning|wiper/.test(label)) { return 'fa-tint'; }
		if (/fuel|mixture|tank/.test(label)) { return 'fa-fire'; }
		if (/suspension|shock/.test(label)) { return 'fa-road'; }
		if (/brake/.test(label)) { return 'fa-dot-circle-o'; }
		if (/exhaust/.test(label)) { return 'fa-cloud'; }
		if (/cooling|air conditioning|heating/.test(label)) { return 'fa-snowflake-o'; }
		if (/steering/.test(label)) { return 'fa-life-ring'; }
		if (/interior|seat/.test(label)) { return 'fa-wheelchair-alt'; }
		if (/light/.test(label)) { return 'fa-lightbulb-o'; }
		if (/electric|battery/.test(label)) { return 'fa-battery-full'; }
		if (/spark|ignition|glow/.test(label)) { return 'fa-bolt'; }
		if (/accessor|service/.test(label)) { return 'fa-wrench'; }
		return 'fa-plus-square';
	}
	function enhanceUmapiCategoryCards(output) {
		var iconGrid = output.querySelector('.epc-umapi-category-icons');
		if (!iconGrid) {
			return;
		}
		Array.prototype.forEach.call(iconGrid.querySelectorAll('.epc-umapi-card'), function(card) {
			if (card.querySelector('.epc-umapi-cat-icon')) {
				return;
			}
			var label = card.textContent || card.getAttribute('data-search') || '';
			var icon = document.createElement('i');
			icon.className = 'fa ' + iconForCategory(label) + ' epc-umapi-cat-icon';
			icon.setAttribute('aria-hidden', 'true');
			card.insertBefore(icon, card.firstChild);
			card.setAttribute('title', label.trim());
		});
	}
	function enhanceUmapiCategoryTree(output) {
		Array.prototype.forEach.call(output.querySelectorAll('.epc-umapi-category-list .epc-umapi-tree li'), function(li) {
			var child = li.querySelector(':scope > ul');
			if (!child || li.getAttribute('data-epc-tree-ready') === '1') {
				return;
			}
			var expander = document.createElement('span');
			expander.className = 'epc-umapi-cat-expander';
			expander.textContent = '+';
			expander.onclick = function(event) {
				event.preventDefault();
				event.stopPropagation();
				li.classList.toggle('expanded');
				expander.textContent = li.classList.contains('expanded') ? '-' : '+';
			};
			li.insertBefore(expander, li.firstChild);
			li.setAttribute('data-epc-tree-ready', '1');
		});
	}
	function enhanceUmapiCategories() {
		var output = document.getElementById('epc-umapi-output');
		if (!output || !output.querySelector('.epc-umapi-tree')) {
			return;
		}
		if (!output.querySelector('.epc-umapi-category-shell')) {
			var headings = Array.prototype.slice.call(output.querySelectorAll(':scope > h3'));
			var popularHeading = null;
			var allHeading = null;
			headings.forEach(function(heading) {
				if (/popular/i.test(heading.textContent || '')) {
					popularHeading = heading;
				}
				if (/all categor/i.test(heading.textContent || '')) {
					allHeading = heading;
				}
			});
			var iconGrid = popularHeading ? popularHeading.nextElementSibling : null;
			var tree = output.querySelector('.epc-umapi-tree');
			if (iconGrid && tree && allHeading) {
				var shell = document.createElement('div');
				shell.className = 'epc-umapi-category-shell';
				var iconPanel = document.createElement('div');
				iconPanel.className = 'epc-umapi-category-icons-panel';
				var listPanel = document.createElement('div');
				listPanel.className = 'epc-umapi-category-list';
				output.insertBefore(shell, popularHeading);
				shell.appendChild(iconPanel);
				shell.appendChild(listPanel);
				iconGrid.classList.add('epc-umapi-category-icons');
				iconPanel.appendChild(popularHeading);
				iconPanel.appendChild(iconGrid);
				listPanel.appendChild(allHeading);
				listPanel.appendChild(tree);
			}
		}
		enhanceUmapiCategoryCards(output);
		enhanceUmapiCategoryTree(output);
	}
	function watchUmapiCategories() {
		var output = document.getElementById('epc-umapi-output');
		if (!output || output.getAttribute('data-epc-category-style-ready') === '1') {
			return;
		}
		output.setAttribute('data-epc-category-style-ready', '1');
		var timer = null;
		new MutationObserver(function() {
			if (timer) {
				window.clearTimeout(timer);
			}
			timer = window.setTimeout(enhanceUmapiCategories, 80);
		}).observe(output, {childList:true, subtree:true});
		enhanceUmapiCategories();
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', watchUmapiCategories);
	} else {
		watchUmapiCategories();
	}
})();
</script>
<script>
(function(){
	function epcEsc(value) {
		return String(value || '').replace(/[&<>"']/g, function(ch) {
			return ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'})[ch];
		});
	}
	function epcApplicabilityLang() {
		var lang = (document.documentElement.getAttribute('lang') || '').toLowerCase();
		if (!lang && location.pathname) {
			var match = location.pathname.match(/^\/([a-z]{2})(\/|$)/i);
			lang = match ? match[1].toLowerCase() : '';
		}
		return lang === 'ru' ? 'ru' : 'en';
	}
	function epcOpenApplicability(article) {
		article = String(article || '').trim();
		if (!article) {
			return;
		}
		var output = document.getElementById('epc-brands-output');
		if (!output) {
			return;
		}
		var oldWidget = document.getElementById('applicability_widget');
		if (oldWidget && oldWidget.parentNode) {
			oldWidget.parentNode.removeChild(oldWidget);
		}
		var oldScript = document.getElementById('epc-epartscross-widget-script');
		if (oldScript && oldScript.parentNode) {
			oldScript.parentNode.removeChild(oldScript);
		}
		var panel = document.getElementById('epc-applicability-panel');
		if (!panel) {
			panel = document.createElement('div');
			panel.id = 'epc-applicability-panel';
			panel.className = 'epc-applicability-panel';
			var head = output.querySelector('.epc-brand-parts-head');
			if (head && head.nextSibling) {
				output.insertBefore(panel, head.nextSibling);
			} else {
				output.insertBefore(panel, output.firstChild);
			}
		}
		panel.innerHTML =
			'<div class="epc-applicability-panel__head"><strong>Applicability of spare part: ' + epcEsc(article) + '</strong><button type="button" class="btn btn-xs btn-default" id="epc-applicability-close">Close</button></div>' +
			'<div class="epc-applicability-panel__body"><div id="applicability_widget"><div class="epc-applicability-panel__loader">Loading applicability data...</div></div></div>';
		var close = document.getElementById('epc-applicability-close');
		if (close) {
			close.onclick = function() {
				panel.parentNode.removeChild(panel);
			};
		}
		var script = document.createElement('script');
		script.id = 'epc-epartscross-widget-script';
		script.type = 'text/javascript';
		script.async = true;
		script.onerror = function() {
			var widget = document.getElementById('applicability_widget');
			if (widget) {
				widget.innerHTML = '<div class="epc-brands-message">Applicability data is temporarily unavailable for this article.</div>';
			}
		};
		script.src = '/api/epartscross_fitment.js.php?n=' + encodeURIComponent(article) + '&lang=' + encodeURIComponent(epcApplicabilityLang()) + '&_=' + Date.now();
		document.body.appendChild(script);
		panel.scrollIntoView({behavior: 'smooth', block: 'start'});
	}
	function epcEnhanceBrandPartsApplicability() {
		var output = document.getElementById('epc-brands-output');
		if (!output) {
			return;
		}
		Array.prototype.forEach.call(output.querySelectorAll('.epc-brand-parts-table tbody tr'), function(row) {
			if (row.getAttribute('data-epc-applicability-ready') === '1') {
				return;
			}
			var cells = row.children;
			if (!cells || cells.length < 6) {
				return;
			}
			var articleCell = cells[1];
			var articleNode = articleCell ? (articleCell.querySelector('strong') || articleCell) : null;
			var article = articleNode ? articleNode.textContent.replace(/\s+/g, '').trim() : '';
			if (!article) {
				return;
			}
			var actionCell = cells[cells.length - 1];
			var button = document.createElement('button');
			button.type = 'button';
			button.className = 'btn btn-xs btn-primary epc-applicability-btn';
			button.textContent = 'Applicability';
			button.onclick = function() {
				epcOpenApplicability(article);
			};
			actionCell.appendChild(button);
			row.setAttribute('data-epc-applicability-ready', '1');
		});
	}
	function epcWatchBrandApplicability() {
		var output = document.getElementById('epc-brands-output');
		if (!output) {
			return;
		}
		epcEnhanceBrandPartsApplicability();
		var timer = null;
		var runs = 0;
		var observer = new MutationObserver(function() {
			if (timer) {
				window.clearTimeout(timer);
			}
			timer = window.setTimeout(function() {
				epcEnhanceBrandPartsApplicability();
				runs += 1;
				if (runs >= 3 && observer) {
					observer.disconnect();
					observer = null;
				}
			}, 280);
		});
		observer.observe(output, {childList:true, subtree:true});
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', epcWatchBrandApplicability);
	} else {
		epcWatchBrandApplicability();
	}
})();
</script>
<script>
(function () {
	'use strict';
	function isLazyHost(src) {
		return src.indexOf('image.umapi.ru') !== -1 || src.indexOf('flagcdn.com') !== -1;
	}
	var io = window.IntersectionObserver ? new IntersectionObserver(function (entries) {
		entries.forEach(function (entry) {
			if (!entry.isIntersecting) {
				return;
			}
			var img = entry.target;
			var src = img.getAttribute('data-epc-lazy-src');
			if (src && !img.getAttribute('src')) {
				img.setAttribute('src', src);
			}
			io.unobserve(img);
		});
	}, { rootMargin: '240px 0px', threshold: 0.01 }) : null;

	function armLazyImage(img) {
		if (!img || img.getAttribute('data-epc-lazy-armed') === '1') {
			return;
		}
		var src = img.getAttribute('src') || img.getAttribute('data-epc-lazy-src') || '';
		if (!src || !isLazyHost(src)) {
			return;
		}
		img.setAttribute('data-epc-lazy-armed', '1');
		img.setAttribute('decoding', 'async');
		if (!img.getAttribute('loading')) {
			img.setAttribute('loading', 'lazy');
		}
		// Keep native src — removing it broke brand logos on Available brands.
	}

	function scanLazyImages(root) {
		var scope = root && root.querySelectorAll ? root : document;
		Array.prototype.forEach.call(scope.querySelectorAll('img'), armLazyImage);
	}

	var scanTimer = null;
	function scheduleScan(root) {
		if (scanTimer) {
			window.clearTimeout(scanTimer);
		}
		scanTimer = window.setTimeout(function () {
			scanLazyImages(root || document);
		}, 80);
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', function () { scheduleScan(document); });
	} else {
		scheduleScan(document);
	}

	if (window.MutationObserver) {
		var domObserver = new MutationObserver(function (mutations) {
			for (var i = 0; i < mutations.length; i++) {
				var nodes = mutations[i].addedNodes;
				for (var j = 0; j < nodes.length; j++) {
					var node = nodes[j];
					if (node.nodeType !== 1) {
						continue;
					}
					if (node.tagName === 'IMG') {
						armLazyImage(node);
					} else {
						scheduleScan(node);
					}
				}
			}
		});
		domObserver.observe(document.documentElement, { childList: true, subtree: true });
	}
})();
</script>
<script>
(function () {
	'use strict';
	function activateBrandsLetter(letter) {
		var letters = document.getElementById('epc-brands-letters');
		if (!letters) {
			return;
		}
		var button = letters.querySelector('button[data-letter="' + letter + '"]:not([disabled])');
		if (button && !button.classList.contains('active')) {
			button.click();
		}
	}
	function ensureBrandsDefaultLetter() {
		var letters = document.getElementById('epc-brands-letters');
		if (!letters || letters.getAttribute('data-epc-default-letter') === '1') {
			return;
		}
		var active = letters.querySelector('button.active[data-letter]');
		if (active && active.getAttribute('data-letter') === 'All') {
			activateBrandsLetter('A');
		}
		if (letters.querySelector('button[data-letter="A"].active')) {
			letters.setAttribute('data-epc-default-letter', '1');
		}
	}
	function watchBrandsDefaultLetter() {
		if (!document.getElementById('epc-brands')) {
			return;
		}
		var letters = document.getElementById('epc-brands-letters');
		if (!letters) {
			return;
		}
		var timer = null;
		var observer = new MutationObserver(function () {
			if (timer) {
				window.clearTimeout(timer);
			}
			timer = window.setTimeout(ensureBrandsDefaultLetter, 60);
		});
		observer.observe(letters, { childList: true, attributes: true, subtree: true });
		var summary = document.getElementById('epc-brands-summary');
		if (summary) {
			observer.observe(summary, { childList: true, subtree: true });
		}
		ensureBrandsDefaultLetter();
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', watchBrandsDefaultLetter);
	} else {
		watchBrandsDefaultLetter();
	}
})();
</script>
<script>
(function () {
	function epcHeaderSearchSetMode(root, mode) {
		if (!root) { return; }
		mode = String(mode || '1');
		root.setAttribute('data-active-mode', mode);
		var tabs = root.querySelectorAll('.epc-header-search__tab');
		for (var i = 0; i < tabs.length; i++) {
			var tab = tabs[i];
			var active = tab.getAttribute('data-search-mode') === mode;
			tab.classList.toggle('active', active);
			tab.setAttribute('aria-selected', active ? 'true' : 'false');
		}
		var map = {
			'1': '.header_search_form_1',
			'2': '.header_search_form_2',
			'3': '.header_search_form_3',
			'engine': '.header_search_form_engine',
			'car': '.header_search_form_car'
		};
		Object.keys(map).forEach(function (key) {
			var nodes = root.querySelectorAll(map[key]);
			for (var j = 0; j < nodes.length; j++) {
				nodes[j].classList.toggle('hidden', mode !== key);
			}
		});
		var selector = map[mode];
		if (selector) {
			var input = root.querySelector(selector + ' input[type="text"], ' + selector + ' input[type="search"]');
			if (input) {
				window.setTimeout(function () { input.focus(); }, 40);
			}
		}
	}
	window.epcHeaderSearchSetMode = epcHeaderSearchSetMode;
	window.epcHeaderSearchTab = function (btn) {
		if (!btn) { return false; }
		var root = btn.closest ? btn.closest('.epc-header-search') : null;
		if (root) {
			epcHeaderSearchSetMode(root, btn.getAttribute('data-search-mode'));
		}
		return false;
	};
	window.change_header_search_form = function (id) {
		var roots = document.querySelectorAll('.epc-header-search');
		for (var i = 0; i < roots.length; i++) {
			epcHeaderSearchSetMode(roots[i], id);
		}
		return false;
	};
	window.epcHeaderVinSubmit = function (form) {
		if (!form) { return false; }
		var input = form.querySelector('input[name="vin"]');
		if (!input) { return true; }
		var vin = String(input.value || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
		input.value = vin;
		if (vin.length < 11) {
			window.alert('Enter a valid VIN (11–17 characters).');
			input.focus();
			return false;
		}
		return true;
	};
	window.epcHeaderEngineSubmit = function (form) {
		if (!form) { return false; }
		var input = form.querySelector('input[name="engine"]');
		if (!input) { return true; }
		var code = String(input.value || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
		input.value = code;
		if (code.length < 2 || code.length > 12) {
			window.alert('Enter a valid engine code (2–12 characters, e.g. 3L, 12R, 5L).');
			input.focus();
			return false;
		}
		return true;
	};
	function epcInitHeaderSearch() {
		var roots = document.querySelectorAll('.epc-header-search');
		for (var i = 0; i < roots.length; i++) {
			epcHeaderSearchSetMode(roots[i], roots[i].getAttribute('data-active-mode') || '1');
		}
	}
	document.addEventListener('click', function (e) {
		var tab = e.target && e.target.closest ? e.target.closest('.epc-header-search__tab') : null;
		if (!tab) { return; }
		e.preventDefault();
		epcHeaderSearchTab(tab);
	});
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', epcInitHeaderSearch);
	} else {
		epcInitHeaderSearch();
	}
})();
</script>
