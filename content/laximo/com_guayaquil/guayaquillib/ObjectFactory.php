<?php

namespace guayaquil\guayaquillib;

use guayaquil\guayaquillib\objects\AftermarketDetailsListObject;
use guayaquil\guayaquillib\objects\CatalogListObject;
use guayaquil\guayaquillib\objects\CatalogObject;
use guayaquil\guayaquillib\objects\CategoryListObject;
use guayaquil\guayaquillib\objects\CategoryObject;
use guayaquil\guayaquillib\objects\DetailListObject;
use guayaquil\guayaquillib\objects\DetailObject;
use guayaquil\guayaquillib\objects\DetailReferencesListObject;
use guayaquil\guayaquillib\objects\FilterObject;
use guayaquil\guayaquillib\objects\GroupObject;
use guayaquil\guayaquillib\objects\ImageMapObject;
use guayaquil\guayaquillib\objects\QuickDetailListObject;
use guayaquil\guayaquillib\objects\UnitListObject;
use guayaquil\guayaquillib\objects\UnitObject;
use guayaquil\guayaquillib\objects\VehicleListObject;
use guayaquil\guayaquillib\objects\VehicleObject;
use guayaquil\guayaquillib\objects\WizardObject;
use guayaquil\guayaquillib\objects\PartsListObject;
use SimpleXMLElement;

class ObjectFactory
{
    static $supportedTypes = [
        'QuickGroups',
        'DetailList',
        'ImageMap',
        'CatalogList',
        'Catalog',
        'Wizard',
        'VehicleList',
        'Vehicle',
        'QuickDetails',
        'CategoryList',
        'Category',
        'UnitList',
        'Unit',
        'Filter',
        'PartsList',
        'AftermarketDetailsList',
        'DetailReferencesList'
    ];

    public static function getObject($name, $data = null)
    {
        switch ((string)$name) {
            case 'QuickGroups':
                return new GroupObject($data);
                break;
            case 'DetailList':
                return new DetailListObject($data);
                break;
            case 'ImageMap':
                return new ImageMapObject($data);
                break;
            case 'CatalogList':
                return new CatalogListObject($data);
                break;
            case 'Catalog':
                return $data instanceof SimpleXMLElement ? new CatalogObject($data->row) : new CatalogObject($data);
                break;
            case 'Wizard':
                return new WizardObject($data);
                break;
            case 'VehicleList':
                return new VehicleListObject($data);
                break;
            case 'Vehicle':
                return $data instanceof SimpleXMLElement ? new VehicleObject($data->row) : new VehicleObject($data);
                break;
            case 'CategoryList':
                return new CategoryListObject($data);
                break;
            case 'Category':
                return $data instanceof SimpleXMLElement ? new CategoryObject($data->row) : new CategoryObject($data);
                break;
            case 'QuickDetails':
                return new QuickDetailListObject($data);
                break;
            case 'UnitList':
                return new UnitListObject($data);
                break;
            case 'Unit':
                return $data instanceof SimpleXMLElement ? new UnitObject($data->row) : new UnitObject($data);
                break;
            case 'Filter':
                return new FilterObject($data);
                break;
            case 'PartsList':
                return new PartsListObject($data);
                break;
            case 'AftermarketDetailsList':
                return new AftermarketDetailsListObject($data);
                break;
            case 'DetailReferencesList':
                return new DetailReferencesListObject($data);
                break;
            default:
                return $data;
                break;
        }
    }
}