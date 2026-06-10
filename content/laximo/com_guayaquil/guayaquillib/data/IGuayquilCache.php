<?php
namespace guayaquil\guayaquillib\data;

/**
 * @package     ${NAMESPACE}
 * @subpackage
 *
 * @copyright   A copyright
 * @license     A "Slug" license name e.g. GPL2
 */
interface IGuayquilCache
{
    function GetCachedData($request);
    function PutCachedData($request, $data);
}