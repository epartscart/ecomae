<?php
namespace guayaquil\guayaquillib\data;

use guayaquil\Config;
use guayaquil\guayaquillib\data\IGuayquilCache;
use guayaquil\guayaquillib\data\GuayaquilSoapWrapper;
use guayaquil\guayaquillib\ObjectFactory;


class GuayaquilRequestOEM
{
    //	Function parameters
    protected $locale;
    protected $catalog;
    protected $ssd;
    protected $cache;
    protected $resultObjectNames = [];

    // Temporary varibles
    protected $queries = array();

    // soap wrapper object
    /** @var \GuayaquilSoapWrapper */
    public $soap;

    //	Results
    public $error;
    public $errorTrace;
    public $data;
    public $authorized;
    public $responseData;
    public $textRequests = [];

    function __construct($catalog = '', $ssd = '', $locale = 'ru_RU', IGuayquilCache $cache = null)
    {
        $language = new Language();

        $locale = $language->getLocalization();

        if (getenv('UUE_LOCALE')) {
            $locale = $this->checkParam(base64_decode(getenv('UUE_LOCALE')));
        }

        if (!$locale) {
            $locale = Config::$catalog_data;
        }

        $this->locale  = $this->checkParam($locale);
        $this->catalog = $this->checkParam($catalog);
        $this->ssd     = $this->checkParam($ssd);
        $this->cache   = $cache;
        $this->soap    = new GuayaquilSoapWrapper();
        $this->soap->setCertificateAuthorizationMethod();
    }

    public function setUserAuthorizationMethod($login, $key)
    {
        $this->soap->setUserAuthorizationMethod($login, $key);
    }

    function checkParam($value)
    {
        return $value;
    }

    function appendCommand($command, $params)
    {
        $item          = new \stdClass();
        $item->command = $command;
        $item->params  = $params;
        if (isset($params) && is_array($params)) {
            $command .= ':';
            $first   = true;
            foreach ($params as $key => $value) {
                if ($first) {
                    $first = false;
                } else {
                    $command .= '|';
                }
                $command .= $key . '=' . $value;
            }

            $item->command_text = $command;
        } else {
            $item->command_text = $command;
        }

        $this->queries[] = $item;
    }

    function appendGetCatalogInfo()
    {
        $this->resultObjectNames[] = 'Catalog';
        $this->appendCommand('GetCatalogInfo',
            array('Locale' => $this->locale, 'Catalog' => $this->catalog, 'ssd' => $this->ssd));
    }

    function appendFindVehicle($identString)
    {
        $this->resultObjectNames[] = 'VehicleList';
        $this->appendCommand('FindVehicle', ['Locale' => $this->locale, 'IdentString' => $identString]);
    }

    function appendListCatalogs()
    {
        $this->resultObjectNames[] = 'CatalogList';
        $this->appendCommand('ListCatalogs', array('Locale' => $this->locale, 'ssd' => $this->ssd));
    }

    function appendFindVehicleByVIN($vin, $inCatalog = false)
    {
        $this->resultObjectNames[] = 'VehicleList';
        $this->appendCommand('FindVehicleByVIN', array(
            'Locale'    => $this->locale,
            'Catalog'   => $inCatalog ? $this->catalog : '',
            'VIN'       => $this->checkParam($vin),
            'ssd'       => $this->ssd,
            'Localized' => 'true'
        ));
    }

    //    function appendFindVehicleByFrame($frame, $frameNo, $inCatalog = false)
    //    {
    //        $this->appendCommand('FindVehicleByFrame', array('Locale' => $this->locale, 'Catalog' => $inCatalog ? $this->catalog : '', 'Frame' => $this->checkParam($frame), 'FrameNo' => $this->checkParam($frameNo), 'ssd' => $this->ssd, 'Localized' => 'true'));
    //    }

    function appendFindVehicleByFrameNo($frameNo, $inCatalog = false)
    {
        $this->resultObjectNames[] = 'VehicleList';
        $this->appendCommand('FindVehicleByFrameNo', array(
            'Locale'    => $this->locale,
            'Catalog'   => $inCatalog ? $this->catalog : '',
            'FrameNo'   => $this->checkParam($frameNo),
            'FrameNo'   => $this->checkParam($frameNo),
            'ssd'       => $this->ssd ?: '',
            'Localized' => 'true'
        ));
    }

    function appendFindVehicleByWizard2($ssd)
    {
        $this->resultObjectNames[] = 'VehicleList';
        $this->appendCommand('FindVehicleByWizard2', array(
            'Locale'    => $this->locale,
            'Catalog'   => $this->catalog,
            'ssd'       => $this->checkParam($ssd),
            'Localized' => 'true'
        ));
    }

    function appendGetVehicleInfo($vehicleid)
    {
        $this->resultObjectNames[] = 'Vehicle';
        $this->appendCommand('GetVehicleInfo', array(
            'Locale'    => $this->locale,
            'Catalog'   => $this->catalog,
            'VehicleId' => $this->checkParam($vehicleid),
            'ssd'       => $this->ssd,
            'Localized' => 'true'
        ));
    }

    function appendListCategories($vehicleid, $categoryid, $ssd = null)
    {
        $this->resultObjectNames[] = 'CategoryList';
        $this->appendCommand('ListCategories', array(
            'Locale'     => $this->locale,
            'Catalog'    => $this->catalog,
            'VehicleId'  => $this->checkParam($vehicleid),
            'CategoryId' => $this->checkParam($categoryid),
            'ssd'        => $ssd ?: $this->ssd
        ));
    }

    function appendListUnits($vehicleid, $categoryid, $ssd = null)
    {
        $this->resultObjectNames[] = 'UnitList';
        $this->appendCommand('ListUnits', array(
            'Locale'     => $this->locale,
            'Catalog'    => $this->catalog,
            'VehicleId'  => $this->checkParam($vehicleid),
            'CategoryId' => $this->checkParam($categoryid),
            'ssd'        => $ssd ?: $this->ssd,
            'Localized'  => 'true'
        ));
    }

    function appendGetUnitInfo($unitid)
    {
        $this->resultObjectNames[] = 'Unit';
        $this->appendCommand('GetUnitInfo', array(
            'Locale'    => $this->locale,
            'Catalog'   => $this->catalog,
            'UnitId'    => $this->checkParam($unitid),
            'ssd'       => $this->ssd,
            'Localized' => 'true'
        ));
    }

    function appendListImageMapByUnit($unitid)
    {
        $this->resultObjectNames[] = 'ImageMap';
        $this->appendCommand('ListImageMapByUnit', [
            'Catalog' => $this->catalog,
            'UnitId' => $this->checkParam($unitid),
            'ssd' => $this->ssd,
            'WithLinks' => 'true'
        ]);
    }

    function appendListDetailByUnit($unitid)
    {
        $this->resultObjectNames[] = 'DetailList';
        $this->appendCommand('ListDetailByUnit', array(
            'Locale'    => $this->locale,
            'Catalog'   => $this->catalog,
            'UnitId'    => $this->checkParam($unitid),
            'ssd'       => $this->ssd,
            'Localized' => 'true',
            'WithLinks'  => 'true'
        ));
    }

    function appendGetWizard2($ssd = false)
    {
        $this->resultObjectNames[] = 'Wizard';
        $this->appendCommand('GetWizard2',
            array('Locale' => $this->locale, 'Catalog' => $this->catalog, 'ssd' => $this->checkParam($ssd)));
    }

    function appendGetFilterByUnit($filter, $vehicle_id, $unit_id)
    {
        $this->resultObjectNames[] = 'Filter';
        $this->appendCommand('GetFilterByUnit', array(
            'Locale'    => $this->locale,
            'Catalog'   => $this->catalog,
            'Filter'    => $this->checkParam($filter),
            'VehicleId' => $this->checkParam($vehicle_id),
            'UnitId'    => $this->checkParam($unit_id),
            'ssd'       => $this->ssd
        ));
    }

    function appendGetFilterByDetail($filter, $vehicle_id, $unit_id, $detail_id)
    {
        $this->resultObjectNames[] = 'Filter';
        $this->appendCommand('GetFilterByDetail', array(
            'Locale'    => $this->locale,
            'Catalog'   => $this->catalog,
            'Filter'    => $this->checkParam($filter),
            'VehicleId' => $this->checkParam($vehicle_id),
            'UnitId'    => $this->checkParam($unit_id),
            'DetailId'  => $this->checkParam($detail_id),
            'ssd'       => $this->ssd
        ));
    }

    function appendListQuickGroup($vehicle_id)
    {
        $this->resultObjectNames[] = 'QuickGroups';
        $this->appendCommand('ListQuickGroup', array(
            'Locale'    => $this->locale,
            'Catalog'   => $this->catalog,
            'VehicleId' => $this->checkParam($vehicle_id),
            'ssd'       => $this->ssd
        ));
    }

    function appendFindVehicleCustom($searchType, $searchParams)
    {
        $this->resultObjectNames[] = 'VehicleList';
        $params                    = array(
            'Locale'  => $this->locale,
            'Catalog' => $this->catalog,
            'Code'    => $this->checkParam($searchType)
        );
        $this->appendCommand('FindVehicleCustom',
            $searchParams && is_array($searchParams) ? array_merge($params, $searchParams) : $params);
    }

    function appendListQuickDetail($vehicle_id, $group_id, $all = 0)
    {
        $this->resultObjectNames[] = 'QuickDetails';
        $params                    = array(
            'Locale'       => $this->locale,
            'Catalog'      => $this->catalog,
            'VehicleId'    => $this->checkParam($vehicle_id),
            'QuickGroupId' => $group_id,
            'ssd'          => $this->ssd,
            'Localized'    => 'true'
        );

        if ($all) {
            $params['All'] = 1;
        }

        $this->appendCommand('ListQuickDetail', $params);
    }

    public function appendExecCustomOperation($operation, $data)
    {
        $this->resultObjectNames[] = 'VehicleList';
        if (!is_array($data)) {
            $data = array();
        }

        $this->appendCommand('ExecCustomOperation', array_merge(array(
            'Locale'    => $this->locale,
            'Catalog'   => $this->catalog,
            'operation' => $this->checkParam($operation)
        ), $data));
    }

    public function appendFindApplicableVehicles($oem)
    {
        $this->resultObjectNames[] = 'VehicleList';
        $this->appendCommand('FindApplicableVehicles', [
            'OEM'     => $this->checkParam($oem),
            'Catalog' => $this->catalog,
            'ssd'     => $this->ssd,
            'Locale'  => $this->locale
        ]);
    }

    public function appendGetOemPartApplicability($oem)
    {
        $this->resultObjectNames[] = 'QuickDetails';
        $this->appendCommand('GetOEMPartApplicability', [
            'Catalog' => $this->catalog,
            'OEM'     => $oem,
            'ssd'     => $this->ssd,
            'Locale'  => $this->locale
        ]);
    }

    public function appendFindPartReferences($oem) {
        $this->resultObjectNames[] = 'DetailReferencesList';
        $this->appendCommand('FINDPARTREFERENCES', [
            'Locale' => $this->locale,
            'OEM'    => $oem
        ]);
    }

    function query()
    {
        $time_start = microtime(true);
        $result  = array();
        $request = array();
        $count   = count($this->queries);

        for ($index = 0; $index < $count; $index++) {
            if ($this->cache) {
                // Try get data from local cache
                $data = $this->cache->GetCachedData($this->queries[$index]);
                if ($data) {
                    if (!is_object($data)) {
                        $data = simplexml_load_string($data);
                    }

                    $result[$index]  = $data;
                    $request[$index] = null;
                } else {
                    $request[$index] = $this->queries[$index];
                    $result[$index]  = null;
                }
            } else {
                $result[$index]  = null;
                $request[$index] = $this->queries[$index];
            }
        }

        $commands_index = 0;
        $query          = '';
        $indexes        = array();
        for ($index = 0; $index < $count; $index++) {
            if ($request[$index]) {
                if ($query) {
                    $query .= "\n";
                }
                $query                .= $request[$index]->command_text;
                $indexes[]            = $index;
                $this->textRequests[] = $request[$index]->command_text;

                if ($commands_index == 5) {
                    if (!$this->_query($query, $indexes, $result)) {
                        return false;
                    }

                    $commands_index = 0;
                    $query          = '';
                    $indexes        = array();
                }

                $commands_index++;
            }
        }

        if ($commands_index > 0) {
            (!$this->_query($query, $indexes, $result));
        }

        $this->data = $result;

        $this->queries = array();

        $resultObject = [];


        foreach ($this->data as $key => $chunk) {
            if (isset($this->resultObjectNames[$key])) {
                if (in_array($this->resultObjectNames[$key], ObjectFactory::$supportedTypes)) {
                    $resultObject[$key] = ObjectFactory::getObject($this->resultObjectNames[$key],
                        $chunk instanceof SimpleXMLElement ? $chunk->children() : $chunk);
                } else {
                    $resultObject[$key] = $chunk;
                }
            }
        }

        $time_end = microtime(true);

        $this->requestTime = $time_end - $time_start;

        return $resultObject;
    }

    function _query($query, $indexes, &$result)
    {
        try {
            $data = $this->soap->queryData($query);
            $this->responseData = $data;
        } catch (\Exception $exc) {
            $this->error      = $exc->getMessage();
            $this->errorTrace = $exc->getTrace();
        }

        if ($this->soap->getError()) {
            $this->error      = $this->soap->getError();
            $this->errorTrace = $this->soap->getErrorTrace();

            return false;
        }

        $data  = simplexml_load_string($data);
        $index = 0;

        //  Merge results
        if ($data && method_exists(get_class($data), 'children')) {
            foreach ($data->children() as $row) {
                $result[$indexes[$index]] = $row;

                if ($this->cache)   // Put in cache
                {
                    $this->cache->PutCachedData($this->queries[$indexes[$index]], $row->asXML());
                }

                $index++;
            }
        }
    }
}

?>
