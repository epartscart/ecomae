<?php
/**
 * Laximo Catalog - Main entry point
 * Loaded via CMS page "/katalog-laximo"
 * Renders the car-mod.com style UI with Syncron-cached proxy API
 */

// ini_set('error_reporting', E_ALL);
// ini_set('display_errors', 1);
// ini_set('display_startup_errors', 1);

// Use the Guayaquil SDK for catalog rendering.
// Default to catalogs view when no task is specified.
if (!isset($_GET['task']) || $_GET['task'] === '') {
    $_GET['task'] = 'catalogs';
}

if (file_exists($_SERVER["DOCUMENT_ROOT"] . "/content/laximo/com_guayaquil/router.php")) {
    set_include_path(get_include_path() . PATH_SEPARATOR . $_SERVER["DOCUMENT_ROOT"] . "/content/laximo/");
    spl_autoload_register(function($class) {
        $path = $_SERVER["DOCUMENT_ROOT"] . "/content/laximo/";
        $file = preg_replace('/guayaquil/', 'com_guayaquil', $class);
        $file = preg_replace('/\\\\/', '/', $file);
        if (file_exists($path . $file . '.php')) {
            require_once($path . $file . '.php');
        }
    });
    if (file_exists($_SERVER["DOCUMENT_ROOT"] . '/content/laximo/vendor/autoload.php')) {
        require_once($_SERVER["DOCUMENT_ROOT"] . '/content/laximo/vendor/autoload.php');
    }
    require_once($_SERVER["DOCUMENT_ROOT"] . '/content/laximo/com_guayaquil/index.php');
} else {
    echo '<div style="padding:20px;color:#c00;">Laximo SDK not available. Please contact support.</div>';
}
?>
