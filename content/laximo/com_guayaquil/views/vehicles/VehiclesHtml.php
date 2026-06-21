<?php

namespace guayaquil\views\vehicles;

use guayaquil\Config;
use guayaquil\guayaquillib\data\GuayaquilRequestAM;
use guayaquil\guayaquillib\data\GuayaquilRequestOEM;
use guayaquil\guayaquillib\data\Language;
use guayaquil\guayaquillib\objects\VehicleListObject;
use guayaquil\modules\pathway\Pathway;
use guayaquil\View;


/**
 * @property string notFoundReason
 */
class VehiclesHtml extends View
{

    /**
     * @var array
     */
    private $detailsWithCatalog;

    public function Display($tpl = 'vehicles', $view = 'view')
    {
        if ($this->input->getString('view') === 'checkDetailApplicability') {
            $this->checkDetailApplicability();
        }

        $vin              = $this->input->getString('vin', '');
        $frameNo          = $this->input->getString('frameNo', '');
        $oem              = $this->input->getString('oem', false);
        $operation        = $this->input->getString('operation', '');
        $catalogCode      = $this->input->getString('c');
        $ssd              = $this->input->getString('ssd', '');
        $request          = new \stdClass();
        $params           = [];
        $skipFinalRequest = false;

        $language = new Language();

        $findType     = $this->input->getString('ft');
        $typeValue    = '';
        $notFoundData = [];
        $ident        = '';
        $requests     = [];

        switch ($findType) {
            case 'findByVIN':
                $type      = [
                    'name'  => 'VIN',
                    'value' => $vin
                ];
                $typeValue = $vin;

                $requests['appendFindVehicleByVIN'] = [
                    'vin' => $vin
                ];

                break;
            case 'findByFrame':
                $type = [
                    'name'  => 'Frame',
                    'value' => $frameNo
                ];

                $typeValue = $frameNo;

                $requests['appendFindVehicleByFrameNo'] = [
                    'frameNo' => $frameNo
                ];

                break;
            case 'execCustomOperation':
                $notFoundData = $this->input->get('data');
                $msg          = implode('-', $notFoundData);
                $type         = [
                    'name'  => $language->t($operation),
                    'value' => $msg
                ];

                $typeValue = $msg;

                $requests['appendExecCustomOperation'] = [
                    'operation' => $operation,
                    'data'      => $this->input->get('data')
                ];

                break;
            case 'findByWizard2':
                $type = [
                    'name'  => $language->t('by' . $findType),
                    'value' => ''
                ];

                $requests['appendFindVehicleByWizard2'] = [
                    'ssd' => $ssd,
                ];

                break;
            case 'FindVehicle':
                $ident = $this->input->getString('identString', '');

                $requests['appendFindVehicle'] = [
                    'ident' => $ident,
                ];

                $type = [
                    'name'  => $language->t('by' . strtolower($findType)),
                    'value' => $ident
                ];

                $typeValue = $ident;
                break;

            case 'findByOEM':
                if (!$catalogCode) {
                    $brand = $this->input->getString('brand');

                    $referenceRequest['appendFindPartReferences'] = [
                        'oem' => $oem,
                    ];

                    $catalogs           = $this->getData(['appendListCatalogs' => []])[0]->catalogs;
                    $this->catalogNames = [];

                    $this->catalogsCodes = [];

                    foreach ($catalogs as $catalog) {
                        $this->catalogsCodes[$catalog->brand] = $catalog->code;
                    }

                    foreach ($catalogs as $catalog) {
                        $this->catalogNames[$catalog->code] = $catalog->name;
                    }

                    $params['ignore_error'] = true;

                    $details = $this->getData($referenceRequest, $params)[0];

                    if (!($this->request->error && (strpos($this->request->error, 'E_STANDARD_PART_SEARCH') !== false || strpos($this->request->error, 'E_ACCESSDENIED') !== false))) {
                        $this->searchBy          = $findType;
                        $this->vinExample        = isset($catalogInfo->vinexample) ? $catalog->vinexample : Config::$defaultVin;
                        $this->frameExample      = isset($catalogInfo->frameexample) ? $catalog->frameexample : Config::$defaultFrame;
                        $this->oemExample        = !empty(Config::$oemExample) ? Config::$oemExample : '0913128000';
                        $this->showApplicability = self::showApplicability();
                        $skipFinalRequest        = true;

                        if ($details->referencesList) {
                            $originals = $details->referencesList;

                            if (!$brand) {
                                if ($originals) {
                                    $this->displayVehicleBrands($originals);
                                }
                            }
                        } else {
                            $amDetails = $this->getCrosses($oem);
                            if (!empty($amDetails->oems)) {
                                $brands = $this->getDetailBrands($amDetails->oems);
                                if ($brands) {
                                    $this->displayDetailBrand($brands);
                                }
                            }
                        }
                    } else {
                        $this->createErrors($language);
                        $skipFinalRequest        = true;
                    }
                }

                $type = [
                    'name'  => $language->t('by' . strtolower($findType)),
                    'value' => $oem
                ];

                $requests['appendFindApplicableVehicles'] = [
                    'oem'     => $oem,
                    'Catalog' => $catalogCode
                ];

                break;

            default:
                $request->error = 'err';
                $type           = ['name' => $findType];
                break;
        }

        if ($catalogCode) {
            $requests['appendGetCatalogInfo'] = [
                'c' => $catalogCode
            ];
        }

        $language = new Language();

        $params = array_merge($params, ['c' => $catalogCode, 'ssd' => $ssd, '']);

        $data = $this->getData($requests, $params);

        if (!$skipFinalRequest) {
            $this->createErrors($language);
        }

        if (isset($data)) {
            $vehicles = [];
            if (isset($data[0]) && $data[0] instanceof VehicleListObject) {
                if (!Config::$groupVehicles) {
                    /**
                     * @var VehicleListObject $vehicles
                     */
                    $vehicles = $data[0]->groupColumnsByVehicles();
                } else {
                    $vehicles = $data[0]->groupVehiclesByName();
                }
            }

            $catalogInfo = $catalogCode && isset($data[1]) ? $data[1] : false;

            $pathway = new Pathway();

            if ($catalogInfo) {
                $pathway->addItem($catalogInfo->name, $catalogInfo->link);
            }

            $pathway->addItem($language->t('vehiclesFind'));
            if (isset($typeValue) && !empty($typeValue)) {
                $pathway->addItem($typeValue);
            }

            $this->vin                  = $vin;
            $this->frameNo              = $frameNo;
            $this->type                 = $type;
            $this->pathway              = $pathway->getPathway();
            $this->headers              = !empty($vehicles) ? $vehicles->tableHeaders : [];
            $this->maxField             = Config::$vehiclesMaxField;
            $this->cataloginfo          = $catalogInfo;
            $this->useApplicability     = $catalogInfo ? $catalogInfo->supportdetailapplicability : 0;
            $this->vehicles             = $vehicles ? $vehicles->vehicles : [];
            $this->groupedVehicles      = $vehicles ? $vehicles->groupedByName : false;
            $this->brandName            = $catalogInfo ? $catalogInfo->name : '';
            $this->searchBy             = $findType;
            $this->rest                 = $this->input->getString('r', '');
            $this->vin                  = $vin;
            $this->frameNo              = $frameNo;
            $this->supportQuickGroups   = $catalogInfo && $catalogInfo->supportquickgroups ?: false;
            $this->columns              = Config::$VehiclesColumns;
            $this->oem                  = $this->input->getString('oem');
            $this->customOperationValue = $notFoundData;
            $this->ident                = $ident;
            $this->groupVehicles        = Config::$groupVehicles;
            $this->vinExample           = isset($catalogInfo->vinexample) ? $catalogInfo->vinexample : Config::$defaultVin;
            $this->frameExample         = isset($catalogInfo->frameexample) ? $catalogInfo->frameexample : Config::$defaultFrame;
            $this->oemExample           = !empty(Config::$oemExample) ? Config::$oemExample : '0913128000';
            $this->showApplicability    = self::showApplicability();
        }

        parent::Display($tpl, $view);
    }

    public function checkDetailApplicability()
    {
        $data           = $this->input->formData();
        $details        = json_decode($data['details'], true);
        $catalog        = $data['catalog'];
        $detailsChecked = [];
        $detailsToShow  = [];
        $toCheck        = 5;

        while (count($detailsToShow) < 5 && count($details)) {
            $stack = [];

            while (count($stack) < $toCheck && count($details)) {
                $stack[] = array_shift($details);
            }

            $detailsWithApplicability = $this->checkDetails($stack, $catalog);

            $toCheck = $toCheck - count($detailsWithApplicability);

            $detailsChecked = array_merge($detailsChecked, $stack);
            $detailsToShow  = array_merge($detailsToShow, $detailsWithApplicability);
        }

        header('Content-Type: application/json');
        echo json_encode(['detailsChecked' => $detailsChecked, 'detailsToShow' => $detailsToShow]);
        die();
    }

    private function checkDetails($details, $catalog)
    {
        $oem = new GuayaquilRequestOEM($catalog, '', Config::$catalog_data);
        if (Config::$useLoginAuthorizationMethod) {
            $oem->setUserAuthorizationMethod(Config::$defaultUserLogin, Config::$defaultUserKey);
        }

        foreach ($details as $detail) {
            $oem->appendFindPartReferences($detail['oem']);
        }

        $result = $oem->query();

        $checkedDetails = [];

        foreach ($result as $key => $res) {
            $catalogReferences = [];

            if (!empty($res->referencesList)) {
                $catalogReferences = array_filter($res->referencesList, function ($ref) use ($catalog) {
                    return $ref->code === $catalog;
                });
            }

            if (!empty($res->referencesList) && !empty($catalogReferences)) {
                $checkedDetails[] = $details[$key];
            }
        }

        return $checkedDetails;
    }

    public function displayVehicleBrands($originals)
    {
        $this->originals = $originals;
        $this->oem       = $this->input->getString('oem');

        parent::Display('vehicles', 'selectVehicleBrand');
        die();
    }

    private function getCrosses($oem)
    {
        $language = new Language();
        $locale   = $language->getLocalization();
        $request  = new GuayaquilRequestAM($locale ?: Config::$catalog_data);

        if (Config::$useLoginAuthorizationMethod) {
            $request->setUserAuthorizationMethod(Config::$defaultUserLogin, Config::$defaultUserKey);
        }

        $request->appendFindOEM($oem, 'crosses');

        return $request->query();
    }

    private function getDetailBrands($details)
    {
        $catalogs     = $this->getData(['appendListCatalogs' => []])[0]->catalogs;
        $catalogNames = array_map(function ($catalog) {
            return $catalog->brand;
        }, $catalogs);

        $replacements = [];

        if (!empty($details)) {
            foreach ($details as $detail) {
                if (!empty($detail->replacements)) {
                    $filteredDetails = array_values(array_filter($detail->replacements, function ($replacement) use ($catalogNames) {
                        return in_array($replacement->manufacturer, $catalogNames);
                    }));

                    $filteredGroupedDetails = [];

                    foreach ($filteredDetails as $filteredDetail) {
                        $filteredGroupedDetails[$filteredDetail->manufacturer][] = $filteredDetail;
                    }

                    $replacement = new \stdClass();

                    $replacement->details        = $filteredGroupedDetails;
                    $replacement->oem            = $detail->oem;
                    $replacement->name           = $detail->name;
                    $replacement->formatted_name = $detail->manufacturer . ': ' . $detail->oem . ' ' . $detail->name;
                    $replacement->detail_id      = $detail->detail_id;

                    $replacements[] = $replacement;
                }
            }
        }

        return $replacements;
    }

    public function displayDetailBrand($brands)
    {
        $this->brands = $brands;
        $this->oem    = $this->input->getString('oem');

        parent::Display('vehicles', 'selectDetailBrand');
        die();
    }

    /**
     * @param Language $language
     */
    private function createErrors(Language $language)
    {
        if (strpos($this->request->error, 'E_STANDARD_PART_SEARCH') !== false) {
            $this->notFoundReason = $language->t('E_STANDARD_PART_SEARCH');
        } else {
            $this->error   = !!$this->request->error;
            $this->message = $this->request->error;
        }
    }
}
