<?php
/**
 * Created by Laximo.
 * User: Elnikov.A.
 * Date: 07.10.17
 * Time: 9:36
 */

namespace guayaquil\views\aftermarket;


use guayaquil\Config;
use guayaquil\guayaquillib\data\GuayaquilRequestAM;
use guayaquil\guayaquillib\data\Language;
use guayaquil\modules\pathway\Pathway;
use guayaquil\View;

class AftermarketHtml extends View
{
    public function Display($tpl = 'aftermarket', $view = 'view')
    {
        if (!$this->isAuthoriseInAm()) {
            $this->renderAuthPage();
        }

        $view = $this->input->getString('view', 'view');
        $this->input->getString('view');
        $format   = $this->input->getString('format');
        $language = new Language();
        if (Config::$useEnvParams) {
            $this->redirect($language->createUrl('catalogs'));
        }

        switch ($view) {
            case 'view':
                $this->displayAftermarket();
                parent::Display($tpl, $view);

                break;
            case 'manufacturerinfo':
                $this->displayManufacturerInfo();
                break;
            case 'findOem':
                if ($format === 'raw') {
                    $this->displayFindOem();
                } else {
                    $this->displayAftermarket();
                    parent::Display($tpl, 'view');
                }

                break;
        }
    }

    public function displayAftermarket()
    {
        $oem              = $this->input->getString('oem');
        $brand            = $this->input->getString('brand');
        $detailId         = $this->input->getString('detail_id');
        $input            = $this->input->getArray();
        $options          = isset($input['options']) ? $input['options'] : '';
        $replacementtypes = isset($input['replacementtypes']) ? $input['replacementtypes'] : [];
        $data             = [];

        if ($oem || $brand || $detailId) {
            if ($detailId) {
                $request = [
                    'appendFindDetail' => [
                        'detail_id' => $detailId,
                        'options'   => $options
                    ]
                ];

            } else {
                if ($options) {
                    $options = implode(',', $options);
                } else {
                    $options = '';
                }

                $request = [
                    'appendFindOEM' => [
                        'oem'              => $oem,
                        'options'          => $options,
                        'brand'            => $brand,
                        'replacementtypes' => implode(',', $replacementtypes),
                    ]
                ];
            }

            $data = $this->getAftermarketData($request);
        }

        if (!$replacementtypes) {
            $replacementtypes = ['Default'];
        }

        $pathway = new Pathway();

        $pathway->addItem('AfterMarket', '');

        $this->pathway = $pathway->getPathway();

        $this->oem              = $oem;
        $this->brand            = $brand;
        $this->options          = $options;
        $this->replacementtypes = $replacementtypes;
        $this->details          = $data;

    }

    public function displayManufacturerInfo()
    {

        $manufacturerid = $_GET['manufacturerid'];

        $request = new GuayaquilRequestAM('en_US');
        if (Config::$useLoginAuthorizationMethod) {
            $request->setUserAuthorizationMethod(Config::$defaultUserLogin, Config::$defaultUserKey);
        }
        $request->appendManufacturerInfo($manufacturerid);
        $data = $request->query();

        if ($request->error != '') {
            echo $request->error;
        } else {
            $data = $data[0]->ManufacturerInfo->row;
        }

        $this->loadTwig('aftermarket/tmpl', 'manufacturerInfo.twig', [
            'manufacturerInfo' => $data
        ]);
    }

    public function displayFindOem()
    {
        $brand            = $this->input->getString('brand', null);
        $oem              = $this->input->getString('oem', '');
        $options          = $this->input->getString('options', '');
        $detailId         = $this->input->getString('detail_id');
        $replacementtypes = !empty($this->input->getArray()['replacementtypes']) ? $this->input->getArray()['replacementtypes'] : '';
        $language         = new Language();

        if ($replacementtypes) {
            $replacementtypes = implode($replacementtypes, ',');
        } else {
            $replacementtypes = 'Default';
        }


        $request = new GuayaquilRequestAM('ru_RU');
        if (Config::$useLoginAuthorizationMethod) {
            $request->setUserAuthorizationMethod(Config::$defaultUserLogin, Config::$defaultUserKey);
        }

        if ($detailId) {
            $request->appendFindDetail($detailId, $options);
            $data = $request->query();

            if (!$data || $request->error) {
                $this->loadTwig('error/tmpl', 'default.twig',
                    ['message' => $request->error, 'more' => []]);
                $this->loadTwig('aftermarket/tmpl', 'view.twig', []);
            } else {
                if ($data) {

                    $this->loadTwig('aftermarket/tmpl', 'findOem.twig', [
                        'details' => $data
                    ]);
                }
            }

        } else {
            if ($options) {
                $options = implode(',', $options);
            } else {
                $options = '';
            }

            $request->appendFindOEM($oem, $options, $brand, $replacementtypes);


            $data = $request->query();

            if ($request->error != '') {
                echo $request->error;
            } else {
                if (!$data) {
                    $request = new GuayaquilRequestAM('ru_RU');

                    if (Config::$useLoginAuthorizationMethod) {
                        $request->setUserAuthorizationMethod(Config::$defaultUserLogin, Config::$defaultUserKey);
                    }

                    $request->appendFindOEMCorrection($oem);
                    $data = $request->query();
                }

                if ($data) {

                    $this->loadTwig('aftermarket/tmpl', 'findOem.twig', [
                        'details' => $data
                    ]);
                }
            }
        }
    }
}