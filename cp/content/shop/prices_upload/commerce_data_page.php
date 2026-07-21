<?php
/**
 * CP route shop/prices/commerce — retired; Multivendor upload replaces this UI.
 */
defined('_ASTEXE_') or die('No access');

$backend = htmlspecialchars((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), ENT_QUOTES, 'UTF-8');
$mvUrl = '/' . $backend . '/shop/prices/multivendor';
$pricesUrl = '/' . $backend . '/shop/prices';

echo '<div class="col-lg-12">'
	. '<div class="alert alert-info" style="margin-top:16px;">'
	. '<h3 style="margin-top:0;">Commerce data upload removed</h3>'
	. '<p>Use <strong>Multi-vendor upload</strong> instead — one file for many vendors, with warehouses created automatically.</p>'
	. '<p>'
	. '<a class="btn btn-success" href="' . $mvUrl . '"><i class="fa fa-upload"></i> Open Multi-vendor upload</a> '
	. '<a class="btn btn-default" href="' . $pricesUrl . '"><i class="fa fa-list"></i> Price lists</a>'
	. '</p>'
	. '</div></div>';
