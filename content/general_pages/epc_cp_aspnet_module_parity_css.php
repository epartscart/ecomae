<?php
/**
 * Serve ASP.NET→PHP CP module presentation parity CSS.
 * Super CP + Tenant CP digest apps share the same epc-scp-* look.
 */
header('Content-Type: text/css; charset=UTF-8');
header('Cache-Control: public, max-age=300');
readfile(__DIR__ . '/epc_cp_aspnet_module_parity.css');
