<?php
// Sanitize VIN parameter if provided
if (isset($_GET["vin"])) {
    $_GET["vin"] = htmlentities($_GET["vin"]);
}

// Start timer and output buffering
$scriptStartTime = microtime(true);
ob_start();
session_start();

// Set content-type header and error reporting
header("Content-Type: text/html; charset=utf-8");
error_reporting(~E_ALL);

// Default partInfo value
$partInfoValue = 1;

// Include optional maintenance or injection scripts if they exist
if (file_exists('underConstruction.php')) {
    include('underConstruction.php');
}

$IlcatsInjections = false;
if (file_exists('IlcatsInjections.php')) {
    $IlcatsInjections = true;
    $IlcatsInjection = 'Index1';
    include('IlcatsInjections.php');
}

if (file_exists('IlcatsInjections2.php')) {
    include_once('IlcatsInjections2.php');
}

// Include main settings and API function files (adjust paths as necessary)
include_once(dirname(__FILE__) . '/../content/originalnye-katalogi/settings.php');
include_once(dirname(__FILE__) . '/../content/originalnye-katalogi/API.v2/PHP/Functions.Common.php');
include_once(dirname(__FILE__) . '/../content/originalnye-katalogi/API.v2/PHP/Functions.Blocks.php');

// Re-include injection if exists (seems required by original code)
if ($IlcatsInjections) {
    $IlcatsInjection = 'Index1.2';
    include('IlcatsInjections.php');
}

// Set default GET parameters if not present
if (!isset($_GET['function']) || $_GET['function'] == '') {
    $_GET['function'] = 'defaultFunction';
}

if (!isset($_GET['language']) || $_GET['language'] == '') {
    if (isset($_COOKIE['language']) && $_COOKIE['language'] != '') {
        $_GET['language'] = $_COOKIE['language'];
    } else {
        $_GET['language'] = 'ru';
    }
}

// Prepare VIN parameter array if VIN provided
$vinTmp = array();
if (!empty($_GET["vin"])) {
    $vinTmp = array("vin" => $_GET["vin"]);
}

// Fetch data via API, depending on presence of brand
if (isset($_GET['brand']) && $_GET['brand'] != '') {
    $data = getApiData($_GET);
} else {
    $defaultParams = array(
        "function" => "catalogsList",
        "brand" => 'cataloglist',
        "apiVersion" => '2.0',
        "shopClientId" => isset($_GET["clid"]) ? $_GET["clid"] : null,
        "catalogId" => isset($_GET["pid"]) ? $_GET["pid"] : null,
        "shopid" => isset($_GET["shopid"]) ? $_GET["shopid"] : null,
        "language" => $_GET["language"]
    );
    $data = getApiData(array_merge($defaultParams, $vinTmp));
}

// If debug hash parameter present, output API response for debugging
if (isset($_GET["debughash"]) && $_GET["debughash"] != '') {
    ShowApiAnswer($data, $_GET["debughash"]);
}

// Handle AJAX response
if (isset($_GET['Ajax']) && $_GET['Ajax'] == 1) {
    $_GET['filterData'] = base64_decode($_GET['filterData']);
    $Answer['filterData'] = $_GET['filterData'];
    $Answer['PageSelector'] = $data['data'][1]['format']($data['data'][1]);
    $Answer['Tiles'] = $data['data'][2]['format']($data['data'][2]);
    exit(json_encode($Answer));
}

// Possibly include injections again if needed
if ($IlcatsInjections) {
    $IlcatsInjection = 'Index1.5';
    include('IlcatsInjections.php');
}

// Extract site labels and build page parts
$SiteLabels = isset($data['siteLabels']) ? $data['siteLabels'] : array();

if (!empty($data['mainMenu'])) {
    $Page['MainMenu'] = MainMenu($data['mainMenu']);
} else {
    $Page['MainMenu'] = '';
}

if (!empty($data['availableLanguages'])) {
    $Page['Languages'] = Languages($data['availableLanguages'], $apiActiveLanguages);
} else {
    $Page['Languages'] = "No 'availableLanguages'";
}

$Page['Content'] = array();

if (!empty($data['data'])) {
    foreach ($data["data"] as $Data) {
        $caption = (!empty($Data['caption'])) ? "<h2>" . $Data['caption'] . "</h2>" : "";
        $Page['Content'][] = $caption . $Data['format']($Data, $SiteLabels);
    }
} else {
    if (!empty($data["errors"])) {
        $Page['Content'][] = "<div class='ApiError'>" . ImplodeIfArray($data["errors"], '<br>') . "</div>";
    } else {
        $Page['Content'][] = "Wrong answer";
    }
}
?>
<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="utf-8" />
    <meta http-equiv="X-UA-Compatible" content="IE=edge" />
    <meta name="viewport" content="width=device-width, initial-scale=0.7" />
    <?php
    echo "<meta name='description' content='" . (isset($data["metas"]["description"]) ? $data["metas"]["description"] : '') . "'>";
    echo "<meta name='keyword' content='" . (isset($data["metas"]["keywords"]) ? ImplodeIfArray($data["metas"]["keywords"], ', ') : '') . "'>";
    echo "<title>" . (isset($data["metas"]["title"]) ? $data["metas"]["title"] : '') . "</title>";
    ?>
    <script type="text/javascript" src="<?php echo apiStaticContentHost; ?>/API.v2/JS/JQuery-3.1.0.min.js"></script>
    <script type="text/javascript" src="<?php echo apiStaticContentHost; ?>/API.v2/JS/JQueryUI-1.12.0/JQueryUI.min.js"></script>
    <link type="text/css" rel="stylesheet" href="<?php echo apiStaticContentHost; ?>/API.v2/JS/JQueryUI-1.12.0/JQueryUI.css" />
    <script type="text/javascript" src="<?php echo apiStaticContentHost; ?>/API.v2/JS/jquery.scrollTo.190301.min.js"></script>
    <script type="text/javascript" src="<?php echo apiStaticContentHost; ?>/API.v2/JS/jquery.pep.js"></script>
    <?php
    echo "<link type='text/css' rel='stylesheet' href='" . apiStaticContentHost . "/fonts/ProximaNova/Font.css'>";
    echo "<link type='text/css' rel='stylesheet' href='" . apiStaticContentHost . "/API.v2/CSS/Template2.190228.css'>";
    echo "<link type='text/css' rel='stylesheet' href='//www.ilcats.ru/getCss.php?clid=15691&host=incar62.ru'>";
    echo "<script type='text/javascript' src='" . apiStaticContentHost . "/API.v2/JS/Common2.190228.js'></script>";

    if ($IlcatsInjections) {
        $IlcatsInjection = 'Index2';
        include('IlcatsInjections.php');
    }
    ?>
</head>
<body class="<?php echo isset($_GET['brand']) ? htmlspecialchars($_GET['brand']) : ''; ?>">
<?php
if ($IlcatsInjections) {
    $IlcatsInjection = 'Counters';
    include('IlcatsInjections.php');
}
?>
<div class="PageHeader">
    <?php
    echo "<div class='Top'>{$Page['MainMenu']}{$Page['Languages']}</div>";
    echo VinForm(isset($data['vinSearchParameters']) ? $data['vinSearchParameters'] : array());
    ?>
</div>
<div id="Body" class="<?php echo isset($data['data'][0]['format']) ? htmlspecialchars($data['data'][0]['format']) : ''; ?>Body">
    <?php
    if ($IlcatsInjections) {
        $IlcatsInjection = 'Advert1';
        include('IlcatsInjections.php');
    }
    ?>
    <h1><?php echo isset($data["stageName"]) ? htmlspecialchars($data["stageName"]) : ''; ?></h1>
    <?php
    if (isset($data['data'][0]['format']) && $data['data'][0]['format'] == 'ifImage') {
        $TempPageContent = array();
        $TempPageContent[0] = $Page['Content'][0];
        array_shift($Page['Content']);
        $TempPageContent[1] = "<div class='Info'>" . ImplodeIfArray($Page['Content']) . "</div>";
        $Page['Content'] = "<div class='ifImage'>" . ImplodeIfArray($TempPageContent) . "</div>";
    }
    echo ImplodeIfArray($Page['Content']);

    if ($IlcatsInjections) {
        $IlcatsInjection = 'Advert2';
        include('IlcatsInjections.php');
    }
    ?>
</div>
<div id="Dialog"></div>
<footer>
    <?php
    echo "<div>" . (isset($data['siteLabels']['advertLinkUrl']) ? $data['siteLabels']['advertLinkUrl'] : '') . "</div>";

    if ($IlcatsInjections) {
        $IlcatsInjection = 'Index3';
        include('IlcatsInjections.php');
    }

    echo isset($ErrorFound) ? $ErrorFound : '';
    ?>
</footer>
</body>
<?php
echo "<!-- " . (isset($data['serverInfo']['dataGenerateTime']) ? $data['serverInfo']['dataGenerateTime'] : '') . " " . (microtime(true) - $scriptStartTime) . " -->";
ob_end_flush();
?>