<?php
/**
 * Laximo Catalog - Main entry point
 * Loaded via CMS page "/katalog-laximo"
 */

// Default to catalogs view when no task is specified in the URL.
// The Guayaquil router parses $_SERVER['REQUEST_URI'], so we also inject into the URI.
if (!isset($_GET['task']) || $_GET['task'] === '') {
    $_GET['task'] = 'catalogs';
    if (strpos($_SERVER['REQUEST_URI'], 'task=') === false) {
        $_SERVER['REQUEST_URI'] .= (strpos($_SERVER['REQUEST_URI'], '?') !== false ? '&' : '?') . 'task=catalogs';
    }
}

$laximoRoot = $_SERVER["DOCUMENT_ROOT"] . "/content/laximo";

if (file_exists($laximoRoot . "/com_guayaquil/router.php")) {
    try {
        set_include_path(get_include_path() . PATH_SEPARATOR . $laximoRoot . "/");
        spl_autoload_register(function($class) {
            $path = $_SERVER["DOCUMENT_ROOT"] . "/content/laximo/";
            $file = preg_replace('/guayaquil/', 'com_guayaquil', $class);
            $file = str_replace('\\', '/', $file);
            if (file_exists($path . $file . '.php')) {
                require_once($path . $file . '.php');
            }
        });
        if (file_exists($laximoRoot . '/vendor/autoload.php')) {
            require_once($laximoRoot . '/vendor/autoload.php');
        }
        require_once($laximoRoot . '/com_guayaquil/index.php');
    } catch (\Throwable $e) {
        echo '<div style="padding:20px;color:#c00;">Laximo error: ' . htmlspecialchars($e->getMessage()) . ' in ' . basename($e->getFile()) . ':' . $e->getLine() . '</div>';
    }
} else {
    echo '<div style="padding:20px;color:#c00;">Laximo SDK not available. Please contact support.</div>';
}
?>
