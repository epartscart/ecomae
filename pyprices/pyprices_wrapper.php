<?php
// Windows wrapper for pyprices/api.py

// Use the project's virtualenv Python (same as api.py shebang for Apache CGI)
$python = str_replace('\\', '/', __DIR__ . '/Scripts/python.exe');
$api_py = __DIR__ . '/api.py';

// Forward POST data as environment variables for CGI
foreach ($_POST as $key => $value) {
    putenv("$key=$value");
}

// Run api.py
$output = shell_exec("$python $api_py 2>&1");

// Return JSON output
header('Content-Type: application/json; charset=utf-8');
echo $output;
?>