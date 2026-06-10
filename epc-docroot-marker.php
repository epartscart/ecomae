<?php
/** Deploy marker — if you see this in /en/ source, docroot is platform Model C. */
header('Content-Type: text/plain');
echo 'epc-docroot-marker platform-ecomae ' . gmdate('c') . "\n";
echo 'DOCUMENT_ROOT=' . ($_SERVER['DOCUMENT_ROOT'] ?? '') . "\n";
