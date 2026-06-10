<?php
namespace guayaquil\guayaquillib\data;

use guayaquil\guayaquillib\data\IGuayquilCache;
use guayaquil\guayaquillib\data\GuayaquilSoapWrapper;
use guayaquil\guayaquillib\ObjectFactory;
use SimpleXMLElement;

class GuayaquilRequestAM
{
	//	Function parameters
    protected $locale;

	// Temporery varibles
	protected $query = '';

	// soap wrapper object
    /** @var \GuayaquilSoapWrapper */
    protected $soap;

	//	Results
	public $error;
	public $data;

    /**
     * @var string
     */
    protected $resultObjectName;

    /**
     * @var string
     */
    protected $xmlObjectName;

    /**
     * @var array
     */
    public $textRequests;


    function __construct($locale = 'ru_RU')
	{
		$this->locale = $this->checkParam($locale);
        $this->soap = new GuayaquilSoapWrapper();
        $this->soap->setCertificateAuthorizationMethod();
	}

    public function setUserAuthorizationMethod($login, $key)
    {
        $this->soap->setUserAuthorizationMethod($login, $key, false);
    }

    function checkParam($value)
	{
		return $value;
	}

	function appendCommand($command)
	{
		if ($this->query == '')
			$this->query = $command;
		else
			$this->query .= "\n".$command;
	}

    public function appendFindOEM($oem, $options = '', $brand = null, $replacementtypes = 'default')
    {
        $this->appendCommand('FindOEM:Locale='.$this->locale.'|OEM='.$oem.'|ReplacementTypes='.$replacementtypes.'|Options='.$options.($brand ? '|Brand='.$brand : ''));
        $this->xmlObjectName = 'FindOEM';
        $this->resultObjectName = 'AftermarketDetailsList';
    }

    public function appendFindOEMCorrection($oem)
    {
        $this->appendCommand('FindOEMCorrection:Locale='.$this->locale.'|OEM='.$oem);
        $this->xmlObjectName = 'FindOEMCorrection';
        $this->resultObjectName = 'AftermarketDetailsList';
    }

    public function appendFindDetail($id, $options, $replacementtypes = 'default')
    {
        $this->appendCommand('FindDetail:Locale='.$this->locale.'|DetailId='.$id.'|ReplacementTypes='.$replacementtypes.'|Options='.$options);
        $this->resultObjectName = 'AftermarketDetailsList';
        $this->xmlObjectName = 'FindDetails';
    }

    public function appendManufacturerInfo($id)
    {
        $this->appendCommand('ManufacturerInfo:Locale='.$this->locale.'|ManufacturerId='.$id);
    }

    public function appendListManufacturer()
    {
        $this->appendCommand('ListManufacturer:Locale='.$this->locale);
    }

    public function appendFindReplacements($id)
    {
        $this->appendCommand('FindReplacements:Locale='.$this->locale.'|DetailId='.$id);
    }

	function query()
	{
        $this->data = $this->soap->queryData($this->query, false);
        $this->textRequests = $this->soap->getTextRequests();
        $this->query = '';
		$this->error = $this->soap->getError();

        $result  = simplexml_load_string($this->data);
        $name    = $this->resultObjectName;
        $xmlName = $this->xmlObjectName;

        if (in_array($name, ObjectFactory::$supportedTypes)) {
            if (!empty($result->{$xmlName})) {
                $resultObject = ObjectFactory::getObject($name,
                    $result->{$xmlName}instanceof SimpleXMLElement ? $result->{$xmlName}->children() : $result->{$xmlName});
            } else {
                $resultObject = new \stdClass();
            }
        } else {
            $resultObject = $result;
        }

		return $resultObject;
	}
}
?>