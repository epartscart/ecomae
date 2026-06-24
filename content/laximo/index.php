<?php
/**
 * Laximo Catalog - Main entry point
 * Loaded via CMS page "/katalog-laximo"
 * Renders the car-mod.com style UI with Syncron-cached proxy API
 */

// ini_set('error_reporting', E_ALL);
// ini_set('display_errors', 1);
// ini_set('display_startup_errors', 1);

// If legacy Guayaquil SDK is needed (VIN search redirect from old forms)
if (isset($_GET['task']) && $_GET['task'] !== '' && file_exists($_SERVER["DOCUMENT_ROOT"] . "/content/laximo/com_guayaquil/router.php")) {
    // Legacy: Autoload Guayaquil classes
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
    // New car-mod.com style UI powered by Laximo proxy API
    ?>
    <link rel="stylesheet" href="/api/Laximo/laximo.css" type="text/css" />
    <div style="padding: 20px 0;">
        <h2 style="margin:0 0 5px;font-size:22px;font-weight:700;">OEM Parts Catalog</h2>
        <p style="margin:0 0 20px;color:#666;font-size:13px;">Search original parts by brand, VIN, or part name. Cross-references with aftermarket analogs.</p>
        <div id="Laximo_container">
            <div class="laximo-loading">
                <div class="spinner"></div>
                <p>Loading Laximo Catalog...</p>
            </div>
        </div>
    </div>
    <script src="/api/Laximo/laximo.js"></script>
    <?php
}
?>
