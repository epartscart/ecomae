<?php
/**
 * Serve ASP.NET→PHP ERP module presentation parity CSS.
 */
header('Content-Type: text/css; charset=UTF-8');
header('Cache-Control: public, max-age=300');
readfile(__DIR__ . '/epc_erp_aspnet_module_parity.css');
