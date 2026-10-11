namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP Ilcats / OEM-catalog leftovers and the UCatalog index include. Live
/// vehicle and OEM browsing stay on <c>/storefront/original-catalog-app</c>,
/// <c>/storefront/vin-app</c>, and <c>/storefront/ucats-app</c>.
/// </summary>
public static class StorefrontOemIlcatsPages
{
    public const string SettingsPath = "content/originalnye-katalogi/settings.php";
    public const string FunctionsBlocksPath = "content/originalnye-katalogi/API.v2/PHP/Functions.Blocks.php";
    public const string FunctionsCommonPath = "content/originalnye-katalogi/API.v2/PHP/Functions.Common.php";
    public const string AutoxpClicksPath = "autoxp_clicks_control.php";
    public const string UCatalogIndexPath = "api/UCatalog/ucatalog_index.php";

    // Functions.Blocks.php
    public const string ifAdvancedForm = nameof(ifAdvancedForm);
    public const string AdvancedFormField = nameof(AdvancedFormField);
    public const string AdvancedFormOption = nameof(AdvancedFormOption);
    public const string ifPageSelector = nameof(ifPageSelector);
    public const string ifForm = nameof(ifForm);
    public const string ifNoScriptData = nameof(ifNoScriptData);
    public const string ifTile = nameof(ifTile);
    public const string ifAdvancedTile = nameof(ifAdvancedTile);
    public const string ifImage = nameof(ifImage);
    public const string Listing = nameof(Listing);
    public const string ifList = nameof(ifList);
    public const string ifMultilist = nameof(ifMultilist);
    public const string ifTable = nameof(ifTable);
    public const string Caption = nameof(Caption);
    public const string MainMenu = nameof(MainMenu);
    public const string Languages = nameof(Languages);
    public const string generateImageMap = nameof(generateImageMap);
    public const string VinForm = nameof(VinForm);
    public const string ifButtonsSet = nameof(ifButtonsSet);

    // Functions.Common.php
    public const string generateArticleUrl2 = nameof(generateArticleUrl2);
    public const string generateBrandUrl = nameof(generateBrandUrl);
    public const string generateLink2 = nameof(generateLink2);
    public const string getApiData = nameof(getApiData);
    public const string ImplodeIfArray = nameof(ImplodeIfArray);
    public const string Show = nameof(Show);
    public const string SendMail = nameof(SendMail);
    public const string ShowApiAnswer = nameof(ShowApiAnswer);
}
