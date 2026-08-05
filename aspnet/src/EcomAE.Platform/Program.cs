using EcomAE.Platform.Api.Catalog;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Components;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Modules;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Security;
using EcomAE.Platform.Services;
using EcomAE.Platform.Surfaces;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents();

builder.Services.Configure<EcomAeOptions>(builder.Configuration.GetSection(EcomAeOptions.SectionName));
builder.Services.Configure<MigrationRouteCutoverOptions>(builder.Configuration.GetSection(MigrationRouteCutoverOptions.SectionName));
builder.Services.Configure<PhpReferenceOptions>(builder.Configuration.GetSection(PhpReferenceOptions.SectionName));
builder.Services.Configure<PriceLookupOptions>(builder.Configuration.GetSection(PriceLookupOptions.SectionName));
builder.Services.Configure<SessionCacheOptions>(builder.Configuration.GetSection(SessionCacheOptions.SectionName));
builder.Services.Configure<TenantDbPoolOptions>(builder.Configuration.GetSection(TenantDbPoolOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ConfigurationTenantRegistry>();
builder.Services.AddSingleton<ITenantDbConnectionFactory, MySqlTenantDbConnectionFactory>();
builder.Services.AddSingleton<ITenantRegistry>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    var seed = sp.GetRequiredService<ConfigurationTenantRegistry>();
    if (!connections.IsConfigured)
    {
        return seed;
    }

    return ActivatorUtilities.CreateInstance<DbBackedTenantRegistry>(sp);
});
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<ILegacySessionStore>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    if (!connections.IsConfigured)
    {
        return new MigrationLegacySessionStore();
    }

    var inner = ActivatorUtilities.CreateInstance<DbLegacySessionStore>(sp);
    var cacheOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SessionCacheOptions>>().Value;
    return cacheOpts.Enabled
        ? ActivatorUtilities.CreateInstance<CachingLegacySessionStore>(sp, inner)
        : inner;
});
builder.Services.AddSingleton<ILegacySessionValidator, DbBackedLegacySessionValidator>();
builder.Services.AddSingleton<ILegacySessionParityReporter, LegacySessionParityReporter>();
builder.Services.AddSingleton<LegacyLogoutService>();
builder.Services.AddSingleton<ILegacyAdminLoginService>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EcomAeOptions>>();
    return connections.IsConfigured && !string.IsNullOrWhiteSpace(options.Value.SecretSuccession)
        ? ActivatorUtilities.CreateInstance<DbLegacyAdminLoginService>(sp)
        : new UnconfiguredLegacyAdminLoginService();
});

builder.Services.AddSingleton<ILegacyApiClientStore, DbLegacyApiClientStore>();
builder.Services.AddSingleton<ILegacyApiUsageLogger, DbLegacyApiUsageLogger>();
builder.Services.AddSingleton<ILegacyApiClientAuthenticator, LegacyApiClientAuthenticator>();
builder.Services.AddSingleton<ILegacyApiClientParityReporter, LegacyApiClientParityReporter>();
builder.Services.AddSingleton<ITenantResolver, RouteTenantResolver>();
builder.Services.AddEcomAeAuthorization();
builder.Services.AddEcomAeSurfaceModules();
builder.Services.AddSingleton<ISurfaceShellCatalog, MigrationSurfaceShellCatalog>();
builder.Services.AddSingleton<ILegacyHtmlShellRenderer, LegacyHtmlShellRenderer>();
builder.Services.AddSingleton<IPresentationParityReporter, PresentationParityReporter>();
builder.Services.AddSingleton<IPriceOfferRepository>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var csvPath = configuration["PriceLookup:FixtureCsvPath"];
    if (!string.IsNullOrWhiteSpace(csvPath))
    {
        return new CsvPriceOfferRepository(csvPath);
    }

    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    if (connections.IsConfigured)
    {
        return ActivatorUtilities.CreateInstance<DbPriceOfferRepository>(sp);
    }

    return new MigrationPriceOfferRepository();
});
builder.Services.AddSingleton<IPriceLookupService, RepositoryPriceLookupService>();
builder.Services.AddSingleton<IPriceLookupParityReporter, PriceLookupParityReporter>();
builder.Services.AddSingleton<ICatalogStatusRepository>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    return connections.IsConfigured
        ? ActivatorUtilities.CreateInstance<DbCatalogStatusRepository>(sp)
        : new MigrationCatalogStatusRepository();
});
builder.Services.AddSingleton<ICatalogStatusService, CatalogStatusService>();
builder.Services.AddSingleton<ICatalogManufacturerRepository>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    return connections.IsConfigured
        ? ActivatorUtilities.CreateInstance<DbCatalogManufacturerRepository>(sp)
        : new MigrationCatalogManufacturerRepository();
});
builder.Services.AddSingleton<ICatalogManufacturerService, CatalogManufacturerService>();
builder.Services.AddSingleton<ICatalogVehicleCacheRepository>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    return connections.IsConfigured
        ? ActivatorUtilities.CreateInstance<DbCatalogVehicleCacheRepository>(sp)
        : new MigrationCatalogVehicleCacheRepository();
});
builder.Services.AddSingleton<ICatalogVehicleCacheService, CatalogVehicleCacheService>();
builder.Services.AddSingleton<ICatalogOfflineCacheRepository>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    return connections.IsConfigured
        ? ActivatorUtilities.CreateInstance<DbCatalogOfflineCacheRepository>(sp)
        : new MigrationCatalogOfflineCacheRepository();
});
builder.Services.AddSingleton<ICatalogOfflineCacheService, CatalogOfflineCacheService>();
builder.Services.AddSingleton<ICatalogBrandPartsRepository>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    return connections.IsConfigured
        ? ActivatorUtilities.CreateInstance<DbCatalogBrandPartsRepository>(sp)
        : new MigrationCatalogBrandPartsRepository();
});
builder.Services.AddSingleton<ICatalogBrandPartsService, CatalogBrandPartsService>();
builder.Services.AddSingleton<ICatalogParityReporter, CatalogParityReporter>();
builder.Services.AddSingleton<IMigrationParityReporter, MigrationParityReporter>();
builder.Services.AddSingleton<IControlPanelParityReporter, ControlPanelParityReporter>();
builder.Services.AddSingleton<IErpParityReporter, ErpParityReporter>();
builder.Services.AddSingleton<IBosParityReporter, BosParityReporter>();
builder.Services.AddSingleton<IStorefrontParityReporter, StorefrontParityReporter>();
builder.Services.AddSingleton<ITenantWorkspaceParityReporter, TenantWorkspaceParityReporter>();
builder.Services.AddSingleton<IMigrationReadinessReporter, MigrationReadinessReporter>();
builder.Services.AddSingleton<IMigrationCutoverPlanner, MigrationCutoverPlanner>();
builder.Services.AddSingleton<IMigrationRouteCutoverPolicy, MigrationRouteCutoverPolicy>();
builder.Services.AddSingleton<IDataParityReporter, DataParityReporter>();
builder.Services.AddSingleton<ICutoverValidationReporter, CutoverValidationReporter>();
builder.Services.AddSingleton<IPhpReferenceModeReporter, PhpReferenceModeReporter>();
builder.Services.AddSingleton<IMigrationProgressReporter, MigrationProgressReporter>();
builder.Services.AddSingleton<ISurfaceParityReporter, SurfaceParityReporter>();
builder.Services.AddSingleton<IZeroPhpCompletionReporter, ZeroPhpCompletionReporter>();
builder.Services.AddSingleton<IPhpDecommissionReadinessReporter, PhpDecommissionReadinessReporter>();
builder.Services.AddSingleton<ILiveSurfaceLinkReporter, LiveSurfaceLinkReporter>();
builder.Services.AddSingleton<IMarketingPresentationLockReporter, MarketingPresentationLockReporter>();
builder.Services.AddSingleton<IAspNetZeroPhpPathReporter, AspNetZeroPhpPathReporter>();
builder.Services.AddSingleton<IOnPremisesParityReporter, OnPremisesParityReporter>();
builder.Services.AddSingleton<IBosAjaxWriteCatalog, BosAjaxWriteCatalog>();
builder.Services.AddSingleton<IBosAjaxWriteRegistryDryRun, BosAjaxWriteRegistryDryRun>();
builder.Services.AddSingleton<IBosAjaxMfaPolicyDryRun, BosAjaxMfaPolicyDryRun>();
builder.Services.AddSingleton<IBosAjaxDesignTokensDryRun, BosAjaxDesignTokensDryRun>();
builder.Services.AddSingleton<IBosAjaxCreditLimitDryRun, BosAjaxCreditLimitDryRun>();
builder.Services.AddSingleton<IBosAjaxRunAuditDryRun, BosAjaxRunAuditDryRun>();
builder.Services.AddSingleton<IBosAjaxSaveDryRun, BosAjaxSaveDryRun>();
builder.Services.AddSingleton<IBosAjaxUpdateDryRun, BosAjaxUpdateDryRun>();
builder.Services.AddSingleton<IBosAjaxDeleteDryRun, BosAjaxDeleteDryRun>();
builder.Services.AddSingleton<IBosAjaxGetTokensDryRun, BosAjaxGetTokensDryRun>();
builder.Services.AddSingleton<IBosAjaxSaveTokenDryRun, BosAjaxSaveTokenDryRun>();
builder.Services.AddSingleton<IBosAjaxPrefsGetDryRun, BosAjaxPrefsGetDryRun>();
builder.Services.AddSingleton<IBosAjaxPrefsSaveDryRun, BosAjaxPrefsSaveDryRun>();
builder.Services.AddSingleton<IBosAjaxStatusDryRun, BosAjaxStatusDryRun>();
builder.Services.AddSingleton<IBosAjaxRunAllDryRun, BosAjaxRunAllDryRun>();
builder.Services.AddSingleton<IBosAjaxSetLimitDryRun, BosAjaxSetLimitDryRun>();
builder.Services.AddSingleton<IBosAjaxOrderStatusDryRun, BosAjaxOrderStatusDryRun>();
builder.Services.AddSingleton<IBosAjaxCreateDryRun, BosAjaxCreateDryRun>();
builder.Services.AddSingleton<IBosAjaxApproveDryRun, BosAjaxApproveDryRun>();
builder.Services.AddSingleton<IBosAjaxKeyGenerateDryRun, BosAjaxKeyGenerateDryRun>();
builder.Services.AddSingleton<IBosAjaxKeyRevokeDryRun, BosAjaxKeyRevokeDryRun>();
builder.Services.AddSingleton<IBosAjaxCreateWaveDryRun, BosAjaxCreateWaveDryRun>();
builder.Services.AddSingleton<IBosAjaxSeedHsDryRun, BosAjaxSeedHsDryRun>();
builder.Services.AddSingleton<IBosAjaxGroupsDryRun, BosAjaxGroupsDryRun>();
builder.Services.AddSingleton<IBosAjaxGroupDryRun, BosAjaxGroupDryRun>();
builder.Services.AddSingleton<IBosAjaxSetRateDryRun, BosAjaxSetRateDryRun>();
builder.Services.AddSingleton<IBosAjaxSeedRatesDryRun, BosAjaxSeedRatesDryRun>();
builder.Services.AddSingleton<IBosAjaxProviderGetDryRun, BosAjaxProviderGetDryRun>();
builder.Services.AddSingleton<IBosAjaxProviderCreateDryRun, BosAjaxProviderCreateDryRun>();
builder.Services.AddSingleton<IBosAjaxProviderToggleDryRun, BosAjaxProviderToggleDryRun>();
builder.Services.AddSingleton<IBosAjaxProviderDeleteDryRun, BosAjaxProviderDeleteDryRun>();
builder.Services.AddSingleton<IBosAjaxCreateRunDryRun, BosAjaxCreateRunDryRun>();
builder.Services.AddSingleton<IBosAjaxApproveRunDryRun, BosAjaxApproveRunDryRun>();
builder.Services.AddSingleton<IBosAjaxRunDetailsDryRun, BosAjaxRunDetailsDryRun>();
builder.Services.AddSingleton<IBosAjaxProfileCreateDryRun, BosAjaxProfileCreateDryRun>();
builder.Services.AddSingleton<IBosAjaxAddInvoiceDryRun, BosAjaxAddInvoiceDryRun>();
builder.Services.AddSingleton<IBosAjaxUpdateStatusDryRun, BosAjaxUpdateStatusDryRun>();
builder.Services.AddSingleton<IBosAjaxRmaCreateDryRun, BosAjaxRmaCreateDryRun>();
builder.Services.AddSingleton<IBosAjaxRmaTransitionDryRun, BosAjaxRmaTransitionDryRun>();
builder.Services.AddSingleton<IBosAjaxRmaListDryRun, BosAjaxRmaListDryRun>();
builder.Services.AddSingleton<IBosAjaxRmaDetailDryRun, BosAjaxRmaDetailDryRun>();
builder.Services.AddSingleton<IBosAjaxSeedDryRun, BosAjaxSeedDryRun>();
builder.Services.AddSingleton<IBosAjaxCreateGroupDryRun, BosAjaxCreateGroupDryRun>();
builder.Services.AddSingleton<IBosAjaxMembersDryRun, BosAjaxMembersDryRun>();
builder.Services.AddSingleton<IBosAjaxAddMemberDryRun, BosAjaxAddMemberDryRun>();
builder.Services.AddSingleton<IBosAjaxFoldersDryRun, BosAjaxFoldersDryRun>();
builder.Services.AddSingleton<IBosAjaxCreateFolderDryRun, BosAjaxCreateFolderDryRun>();
builder.Services.AddSingleton<IBosAjaxPlansDryRun, BosAjaxPlansDryRun>();
builder.Services.AddSingleton<IBosAjaxCreatePlanDryRun, BosAjaxCreatePlanDryRun>();
builder.Services.AddSingleton<IBosAjaxInvoicesDryRun, BosAjaxInvoicesDryRun>();
builder.Services.AddSingleton<IBosAjaxControlsDryRun, BosAjaxControlsDryRun>();
builder.Services.AddSingleton<IBosAjaxUpdateControlDryRun, BosAjaxUpdateControlDryRun>();
builder.Services.AddSingleton<IBosAjaxAddEvidenceDryRun, BosAjaxAddEvidenceDryRun>();
builder.Services.AddSingleton<IBosAjaxEvidenceDryRun, BosAjaxEvidenceDryRun>();
builder.Services.AddSingleton<IBosAjaxCreatePolicyDryRun, BosAjaxCreatePolicyDryRun>();
builder.Services.AddSingleton<ICpPosOpenSessionDryRun, CpPosOpenSessionDryRun>();
builder.Services.AddSingleton<ICpPosCloseSessionDryRun, CpPosCloseSessionDryRun>();
builder.Services.AddSingleton<ICpPosCompleteSaleDryRun, CpPosCompleteSaleDryRun>();
builder.Services.AddSingleton<ICpPosSaveSettingsDryRun, CpPosSaveSettingsDryRun>();
builder.Services.AddSingleton<ICpPortalSaveSettingsDryRun, CpPortalSaveSettingsDryRun>();
builder.Services.AddSingleton<ICpPortalDeploySiteDryRun, CpPortalDeploySiteDryRun>();
builder.Services.AddSingleton<ICpCrmActionDryRun, CpCrmActionDryRun>();
builder.Services.AddSingleton<ICpModuleAjaxWriteCatalog, CpModuleAjaxWriteCatalog>();
builder.Services.AddSingleton<ICpModuleAjaxWriteRegistryDryRun, CpModuleAjaxWriteRegistryDryRun>();
builder.Services.AddSingleton<ICpModuleAjaxWriteDedicatedDryRun, CpModuleAjaxWriteDedicatedDryRun>();
builder.Services.AddSingleton<IOnPremisesActivateLicenseCliDryRun, OnPremisesActivateLicenseCliDryRun>();
builder.Services.AddSingleton<IOnPremisesHealthCheckPackDryRun, OnPremisesHealthCheckPackDryRun>();
builder.Services.AddSingleton<IOnPremisesHealthDryRun, OnPremisesHealthDryRun>();
builder.Services.AddSingleton<IOnPremisesLicenseActivateDryRun, OnPremisesLicenseActivateDryRun>();
builder.Services.AddSingleton<IErpAjaxWriteCatalog, ErpAjaxWriteCatalog>();
builder.Services.AddSingleton<IErpAjaxWriteRegistryDryRun, ErpAjaxWriteRegistryDryRun>();
builder.Services.AddSingleton<IOnPremisesSetupWizardDryRun, OnPremisesSetupWizardDryRun>();
builder.Services.AddSingleton<IOnPremisesBackupDryRun, OnPremisesBackupDryRun>();
builder.Services.AddSingleton<IErpEditLockAcquireDryRun, ErpEditLockAcquireDryRun>();
builder.Services.AddSingleton<IErpEditLockHeartbeatDryRun, ErpEditLockHeartbeatDryRun>();
builder.Services.AddSingleton<IErpEditLockReleaseDryRun, ErpEditLockReleaseDryRun>();
builder.Services.AddSingleton<IErpPresenceHeartbeatDryRun, ErpPresenceHeartbeatDryRun>();
builder.Services.AddSingleton<IErpBosComplianceAddObligationDryRun, ErpBosComplianceAddObligationDryRun>();
builder.Services.AddSingleton<IErpBosComplianceDisableObligationDryRun, ErpBosComplianceDisableObligationDryRun>();
builder.Services.AddSingleton<IErpBosComplianceFileDryRun, ErpBosComplianceFileDryRun>();
builder.Services.AddSingleton<IErpBosComplianceSaveRetentionDryRun, ErpBosComplianceSaveRetentionDryRun>();
builder.Services.AddSingleton<IErpBosWfSaveRuleDryRun, ErpBosWfSaveRuleDryRun>();
builder.Services.AddSingleton<IErpBosWfDisableRuleDryRun, ErpBosWfDisableRuleDryRun>();
builder.Services.AddSingleton<IErpBosWfDecideDryRun, ErpBosWfDecideDryRun>();
builder.Services.AddSingleton<IErpBosWfRaiseTestDryRun, ErpBosWfRaiseTestDryRun>();
builder.Services.AddSingleton<IErpBosIntelToggleControlDryRun, ErpBosIntelToggleControlDryRun>();
builder.Services.AddSingleton<IErpBosVatRefundSaveDryRun, ErpBosVatRefundSaveDryRun>();
builder.Services.AddSingleton<IErpBosVatRefundStatusDryRun, ErpBosVatRefundStatusDryRun>();
builder.Services.AddSingleton<IErpOplParamsSaveDryRun, ErpOplParamsSaveDryRun>();
builder.Services.AddSingleton<IErpOplSetStatusDryRun, ErpOplSetStatusDryRun>();
builder.Services.AddSingleton<IErpOplConfirmAllDryRun, ErpOplConfirmAllDryRun>();
builder.Services.AddSingleton<IErpOplCreatePosDryRun, ErpOplCreatePosDryRun>();
builder.Services.AddSingleton<IErpPfProcessSaveDryRun, ErpPfProcessSaveDryRun>();
builder.Services.AddSingleton<IErpPfStepSaveDryRun, ErpPfStepSaveDryRun>();
builder.Services.AddSingleton<IErpPfStepDeleteDryRun, ErpPfStepDeleteDryRun>();
builder.Services.AddSingleton<IErpPfCaseStartDryRun, ErpPfCaseStartDryRun>();
builder.Services.AddSingleton<IErpPfCaseActDryRun, ErpPfCaseActDryRun>();
builder.Services.AddSingleton<IErpSubGenerateDryRun, ErpSubGenerateDryRun>();
builder.Services.AddSingleton<IErpSubInvoicePaidDryRun, ErpSubInvoicePaidDryRun>();
builder.Services.AddSingleton<IErpCtrStatusDryRun, ErpCtrStatusDryRun>();
builder.Services.AddSingleton<IErpCtrSignDryRun, ErpCtrSignDryRun>();
builder.Services.AddSingleton<IErpCollCasePromiseDryRun, ErpCollCasePromiseDryRun>();
builder.Services.AddSingleton<IErpCollActivityLogDryRun, ErpCollActivityLogDryRun>();
builder.Services.AddSingleton<IErpCollDunningRunDryRun, ErpCollDunningRunDryRun>();
builder.Services.AddSingleton<IErpProcCategorySaveDryRun, ErpProcCategorySaveDryRun>();
builder.Services.AddSingleton<IErpProcPolicySaveDryRun, ErpProcPolicySaveDryRun>();
builder.Services.AddSingleton<IErpProcReqAddLineDryRun, ErpProcReqAddLineDryRun>();
builder.Services.AddSingleton<IErpProcReqConvertDryRun, ErpProcReqConvertDryRun>();
builder.Services.AddSingleton<IErpBplanSaveDryRun, ErpBplanSaveDryRun>();
builder.Services.AddSingleton<IErpBplanAdvanceDryRun, ErpBplanAdvanceDryRun>();
builder.Services.AddSingleton<IErpAmlKycSaveDryRun, ErpAmlKycSaveDryRun>();
builder.Services.AddSingleton<IErpAmlAlertStatusDryRun, ErpAmlAlertStatusDryRun>();
builder.Services.AddSingleton<IErpAmlSettingsSaveDryRun, ErpAmlSettingsSaveDryRun>();
builder.Services.AddSingleton<IErpBankImportDryRun, ErpBankImportDryRun>();
builder.Services.AddSingleton<IErpBankReconcileDryRun, ErpBankReconcileDryRun>();
builder.Services.AddSingleton<IErpFxPostRevaluationDryRun, ErpFxPostRevaluationDryRun>();
builder.Services.AddSingleton<IErpSupplierPaymentDryRun, ErpSupplierPaymentDryRun>();
builder.Services.AddSingleton<IErpInvSyncWarehousesDryRun, ErpInvSyncWarehousesDryRun>();
builder.Services.AddSingleton<IErpInvCreateWarehouseDryRun, ErpInvCreateWarehouseDryRun>();
builder.Services.AddSingleton<IErpInvCreateItemDryRun, ErpInvCreateItemDryRun>();
builder.Services.AddSingleton<IErpInvSetReorderLevelDryRun, ErpInvSetReorderLevelDryRun>();
builder.Services.AddSingleton<IErpInvRecordMovementDryRun, ErpInvRecordMovementDryRun>();
builder.Services.AddSingleton<IErpInvScanLookupDryRun, ErpInvScanLookupDryRun>();
builder.Services.AddSingleton<IErpInvTransferDryRun, ErpInvTransferDryRun>();
builder.Services.AddSingleton<IErpInvImportCsvDryRun, ErpInvImportCsvDryRun>();
builder.Services.AddSingleton<IErpInvRunClosingDryRun, ErpInvRunClosingDryRun>();
builder.Services.AddSingleton<IErpHrEmpSaveDryRun, ErpHrEmpSaveDryRun>();
builder.Services.AddSingleton<IErpHrAttendanceDryRun, ErpHrAttendanceDryRun>();
builder.Services.AddSingleton<IErpHrLeaveRequestDryRun, ErpHrLeaveRequestDryRun>();
builder.Services.AddSingleton<IErpHrLeaveStatusDryRun, ErpHrLeaveStatusDryRun>();
builder.Services.AddSingleton<IErpHrExpenseSaveDryRun, ErpHrExpenseSaveDryRun>();
builder.Services.AddSingleton<IErpHrExpenseStatusDryRun, ErpHrExpenseStatusDryRun>();
builder.Services.AddSingleton<IErpHrUpdateDaysDryRun, ErpHrUpdateDaysDryRun>();
builder.Services.AddSingleton<IErpEinvoiceCreateDryRun, ErpEinvoiceCreateDryRun>();
builder.Services.AddSingleton<IErpEinvoiceSaveSellerDryRun, ErpEinvoiceSaveSellerDryRun>();
builder.Services.AddSingleton<IErpEinvoiceSaveBuyerDryRun, ErpEinvoiceSaveBuyerDryRun>();
builder.Services.AddSingleton<IErpEinvoiceSaveAspDryRun, ErpEinvoiceSaveAspDryRun>();
builder.Services.AddSingleton<IErpEinvoiceSubmitDryRun, ErpEinvoiceSubmitDryRun>();
builder.Services.AddSingleton<IErpEinvoiceCreditNoteDryRun, ErpEinvoiceCreditNoteDryRun>();
builder.Services.AddSingleton<IErpEinvoicePollAspDryRun, ErpEinvoicePollAspDryRun>();
builder.Services.AddSingleton<IErpOrderFulfillmentBootstrapDryRun, ErpOrderFulfillmentBootstrapDryRun>();
builder.Services.AddSingleton<IErpOrderFulfillmentStatusDryRun, ErpOrderFulfillmentStatusDryRun>();
builder.Services.AddSingleton<IErpOrderFulfillmentSyncDryRun, ErpOrderFulfillmentSyncDryRun>();
builder.Services.AddSingleton<IErpOrderFulfillmentPostPoDryRun, ErpOrderFulfillmentPostPoDryRun>();
builder.Services.AddSingleton<IErpOrderFulfillmentPostSalesDryRun, ErpOrderFulfillmentPostSalesDryRun>();
builder.Services.AddSingleton<IErpOrderFulfillmentAutoPostDryRun, ErpOrderFulfillmentAutoPostDryRun>();
builder.Services.AddSingleton<IErpOrderFulfillmentSwapSupplierDryRun, ErpOrderFulfillmentSwapSupplierDryRun>();
builder.Services.AddSingleton<IErpPmSaveDryRun, ErpPmSaveDryRun>();
builder.Services.AddSingleton<IErpPmToggleDryRun, ErpPmToggleDryRun>();
builder.Services.AddSingleton<IErpPmBudgetSaveDryRun, ErpPmBudgetSaveDryRun>();
builder.Services.AddSingleton<IErpPmBudgetLineSaveDryRun, ErpPmBudgetLineSaveDryRun>();
builder.Services.AddSingleton<IErpPmListingSaveDryRun, ErpPmListingSaveDryRun>();
builder.Services.AddSingleton<IErpPmListingAttachDryRun, ErpPmListingAttachDryRun>();
builder.Services.AddSingleton<IErpPmChequeSaveDryRun, ErpPmChequeSaveDryRun>();
builder.Services.AddSingleton<IErpMfgrWcSaveDryRun, ErpMfgrWcSaveDryRun>();
builder.Services.AddSingleton<IErpMfgrRouteSaveDryRun, ErpMfgrRouteSaveDryRun>();
builder.Services.AddSingleton<IErpMfgrMrpRunDryRun, ErpMfgrMrpRunDryRun>();
builder.Services.AddSingleton<IErpMfgrPlannedFirmDryRun, ErpMfgrPlannedFirmDryRun>();
builder.Services.AddSingleton<IErpQmPlanSaveDryRun, ErpQmPlanSaveDryRun>();
builder.Services.AddSingleton<IErpQmTestAddDryRun, ErpQmTestAddDryRun>();
builder.Services.AddSingleton<IErpQmOrderCreateDryRun, ErpQmOrderCreateDryRun>();
builder.Services.AddSingleton<IErpQmOrderRecordDryRun, ErpQmOrderRecordDryRun>();
builder.Services.AddSingleton<IErpQmNcrCreateDryRun, ErpQmNcrCreateDryRun>();
builder.Services.AddSingleton<IErpQmNcrUpdateDryRun, ErpQmNcrUpdateDryRun>();
builder.Services.AddSingleton<IErpRbacPrivSaveDryRun, ErpRbacPrivSaveDryRun>();
builder.Services.AddSingleton<IErpRbacDutySaveDryRun, ErpRbacDutySaveDryRun>();
builder.Services.AddSingleton<IErpRbacDutyPrivDryRun, ErpRbacDutyPrivDryRun>();
builder.Services.AddSingleton<IStorefrontNewsletterSubscribeDryRun, StorefrontNewsletterSubscribeDryRun>();
builder.Services.AddSingleton<IStorefrontAddEvaluationDryRun, StorefrontAddEvaluationDryRun>();
builder.Services.AddSingleton<IStorefrontCreateOperationDryRun, StorefrontCreateOperationDryRun>();
builder.Services.AddSingleton<IStorefrontCheckOrderNotAuthorizedDryRun, StorefrontCheckOrderNotAuthorizedDryRun>();
builder.Services.AddSingleton<IStorefrontSetUserOptionDryRun, StorefrontSetUserOptionDryRun>();
builder.Services.AddSingleton<IErpPeriodLogDryRun, ErpPeriodLogDryRun>();
builder.Services.AddSingleton<IErpOplAutoplanDryRun, ErpOplAutoplanDryRun>();
builder.Services.AddSingleton<IErpOplSeedDemoDryRun, ErpOplSeedDemoDryRun>();
builder.Services.AddSingleton<IErpOplClearDemoDryRun, ErpOplClearDemoDryRun>();
builder.Services.AddSingleton<IErpPfSetDeptHeadDryRun, ErpPfSetDeptHeadDryRun>();
builder.Services.AddSingleton<IErpPfCaseReassignDryRun, ErpPfCaseReassignDryRun>();
builder.Services.AddSingleton<IErpPfCaseCancelDryRun, ErpPfCaseCancelDryRun>();
builder.Services.AddSingleton<IErpPfSeedDemoDryRun, ErpPfSeedDemoDryRun>();
builder.Services.AddSingleton<IErpPfClearDemoDryRun, ErpPfClearDemoDryRun>();
builder.Services.AddSingleton<IErpPfSyncOrdersDryRun, ErpPfSyncOrdersDryRun>();
builder.Services.AddSingleton<IErpDemoSeedSalesDryRun, ErpDemoSeedSalesDryRun>();
builder.Services.AddSingleton<IErpDemoClearSalesDryRun, ErpDemoClearSalesDryRun>();
builder.Services.AddSingleton<IErpCtrOcrDryRun, ErpCtrOcrDryRun>();
builder.Services.AddSingleton<IErpDocxSaveDryRun, ErpDocxSaveDryRun>();
builder.Services.AddSingleton<IErpDocxDeleteDryRun, ErpDocxDeleteDryRun>();
builder.Services.AddSingleton<IErpDocxRunRemindersDryRun, ErpDocxRunRemindersDryRun>();
builder.Services.AddSingleton<IErpInsSaveDryRun, ErpInsSaveDryRun>();
builder.Services.AddSingleton<IErpInsDeleteDryRun, ErpInsDeleteDryRun>();
builder.Services.AddSingleton<IErpInsDocAddDryRun, ErpInsDocAddDryRun>();
builder.Services.AddSingleton<IErpInsDocDeleteDryRun, ErpInsDocDeleteDryRun>();
builder.Services.AddSingleton<IErpInsClaimAddDryRun, ErpInsClaimAddDryRun>();
builder.Services.AddSingleton<IErpFinPeriodsGenerateDryRun, ErpFinPeriodsGenerateDryRun>();
builder.Services.AddSingleton<IErpFinFxRevalueDryRun, ErpFinFxRevalueDryRun>();
builder.Services.AddSingleton<IErpFinAllocSaveDryRun, ErpFinAllocSaveDryRun>();
builder.Services.AddSingleton<IErpFinAllocRunDryRun, ErpFinAllocRunDryRun>();
builder.Services.AddSingleton<IErpFinAccrualSaveDryRun, ErpFinAccrualSaveDryRun>();
builder.Services.AddSingleton<IErpCollHoldSetDryRun, ErpCollHoldSetDryRun>();
builder.Services.AddSingleton<IErpBplanLineAddDryRun, ErpBplanLineAddDryRun>();
builder.Services.AddSingleton<IErpBplanPositionAddDryRun, ErpBplanPositionAddDryRun>();
builder.Services.AddSingleton<IErpHrtJobSaveDryRun, ErpHrtJobSaveDryRun>();
builder.Services.AddSingleton<IErpHrtApplicantAddDryRun, ErpHrtApplicantAddDryRun>();
builder.Services.AddSingleton<IErpHrtApplicantStageDryRun, ErpHrtApplicantStageDryRun>();
builder.Services.AddSingleton<IErpHrtReviewSaveDryRun, ErpHrtReviewSaveDryRun>();
builder.Services.AddSingleton<IErpHrtGoalAddDryRun, ErpHrtGoalAddDryRun>();
builder.Services.AddSingleton<IErpHrtReviewFinalizeDryRun, ErpHrtReviewFinalizeDryRun>();
builder.Services.AddSingleton<IErpCftForecastSaveDryRun, ErpCftForecastSaveDryRun>();
builder.Services.AddSingleton<IErpCftLineAddDryRun, ErpCftLineAddDryRun>();
builder.Services.AddSingleton<IErpCftInstrumentSaveDryRun, ErpCftInstrumentSaveDryRun>();
builder.Services.AddSingleton<IErpCftInstrumentStatusDryRun, ErpCftInstrumentStatusDryRun>();
builder.Services.AddSingleton<IErpWhtCodeSaveDryRun, ErpWhtCodeSaveDryRun>();
builder.Services.AddSingleton<IErpWhtRecordDryRun, ErpWhtRecordDryRun>();
builder.Services.AddSingleton<IErpWhtCertificateDryRun, ErpWhtCertificateDryRun>();
builder.Services.AddSingleton<IErpWhtSettleDryRun, ErpWhtSettleDryRun>();
builder.Services.AddSingleton<IErpConcurrencyStatusDryRun, ErpConcurrencyStatusDryRun>();
builder.Services.AddSingleton<IErpSettlementOpenDocsDryRun, ErpSettlementOpenDocsDryRun>();
builder.Services.AddSingleton<IErpDashboardDryRun, ErpDashboardDryRun>();
builder.Services.AddSingleton<IErpCommandCenterDryRun, ErpCommandCenterDryRun>();
builder.Services.AddSingleton<IErpCcKpiTilesDryRun, ErpCcKpiTilesDryRun>();
builder.Services.AddSingleton<IErpCcApprovalQueueDryRun, ErpCcApprovalQueueDryRun>();
builder.Services.AddSingleton<IErpPeriodListDryRun, ErpPeriodListDryRun>();
builder.Services.AddSingleton<IErpPeriodChecklistDryRun, ErpPeriodChecklistDryRun>();
builder.Services.AddSingleton<IErpPeriodSummaryDryRun, ErpPeriodSummaryDryRun>();
builder.Services.AddSingleton<IErpFxRevaluationPreviewDryRun, ErpFxRevaluationPreviewDryRun>();
builder.Services.AddSingleton<IErpBosComplianceFetchDryRun, ErpBosComplianceFetchDryRun>();
builder.Services.AddSingleton<IErpRtlAssortmentSetDryRun, ErpRtlAssortmentSetDryRun>();
builder.Services.AddSingleton<IErpRtlDiscountSaveDryRun, ErpRtlDiscountSaveDryRun>();
builder.Services.AddSingleton<IErpRtlPosSaleDryRun, ErpRtlPosSaleDryRun>();
builder.Services.AddSingleton<IErpInsClaimStatusDryRun, ErpInsClaimStatusDryRun>();
builder.Services.AddSingleton<IErpPrjSaveDryRun, ErpPrjSaveDryRun>();
builder.Services.AddSingleton<IErpPrjTaskSaveDryRun, ErpPrjTaskSaveDryRun>();
builder.Services.AddSingleton<IErpPrjLogTimeDryRun, ErpPrjLogTimeDryRun>();
builder.Services.AddSingleton<IErpConsEntitySaveDryRun, ErpConsEntitySaveDryRun>();
builder.Services.AddSingleton<IErpConsEntityDeleteDryRun, ErpConsEntityDeleteDryRun>();
builder.Services.AddSingleton<IErpConsFiguresSaveDryRun, ErpConsFiguresSaveDryRun>();
builder.Services.AddSingleton<IErpConsIcSaveDryRun, ErpConsIcSaveDryRun>();
builder.Services.AddSingleton<IErpConsIcDeleteDryRun, ErpConsIcDeleteDryRun>();
builder.Services.AddSingleton<IErpMfgBomSaveDryRun, ErpMfgBomSaveDryRun>();
builder.Services.AddSingleton<IErpMfgWoCreateDryRun, ErpMfgWoCreateDryRun>();
builder.Services.AddSingleton<IErpMfgWoIssueDryRun, ErpMfgWoIssueDryRun>();
builder.Services.AddSingleton<IErpMfgWoCompleteDryRun, ErpMfgWoCompleteDryRun>();
builder.Services.AddSingleton<IErpPayrollGenerateDryRun, ErpPayrollGenerateDryRun>();
builder.Services.AddSingleton<IErpPayrollApproveDryRun, ErpPayrollApproveDryRun>();
builder.Services.AddSingleton<IErpPayrollPayDryRun, ErpPayrollPayDryRun>();
builder.Services.AddSingleton<IErpPayrollUpdateDaysDryRun, ErpPayrollUpdateDaysDryRun>();
builder.Services.AddSingleton<IErpUaeTaxFtaFetchDryRun, ErpUaeTaxFtaFetchDryRun>();
builder.Services.AddSingleton<IErpAmlCheckDryRun, ErpAmlCheckDryRun>();
builder.Services.AddSingleton<IErpAmlReportGenerateDryRun, ErpAmlReportGenerateDryRun>();
builder.Services.AddSingleton<IErpAmlSeedRulesDryRun, ErpAmlSeedRulesDryRun>();
builder.Services.AddSingleton<IErpUaeTaxLegislationRegenSummariesDryRun, ErpUaeTaxLegislationRegenSummariesDryRun>();
builder.Services.AddSingleton<IErpUaeTaxLegislationAskDryRun, ErpUaeTaxLegislationAskDryRun>();
builder.Services.AddSingleton<IErpUaeTaxSaveCtAdjustmentsDryRun, ErpUaeTaxSaveCtAdjustmentsDryRun>();
builder.Services.AddSingleton<IErpUaeTaxLegislationChecklistSetDryRun, ErpUaeTaxLegislationChecklistSetDryRun>();
builder.Services.AddSingleton<IErpInvoiceSaveDryRun, ErpInvoiceSaveDryRun>();
builder.Services.AddSingleton<IErpInvoiceListDryRun, ErpInvoiceListDryRun>();
builder.Services.AddSingleton<IErpInvoiceFromOrderDryRun, ErpInvoiceFromOrderDryRun>();
builder.Services.AddSingleton<IErpAiQueryDryRun, ErpAiQueryDryRun>();
builder.Services.AddSingleton<IErpIntegrityScanDryRun, ErpIntegrityScanDryRun>();
builder.Services.AddSingleton<IErpIntegrityApplyFksDryRun, ErpIntegrityApplyFksDryRun>();
builder.Services.AddSingleton<IErpFaCreateAssetDryRun, ErpFaCreateAssetDryRun>();
builder.Services.AddSingleton<IErpFaRunDepreciationDryRun, ErpFaRunDepreciationDryRun>();
builder.Services.AddSingleton<IErpOpeningCreateBatchDryRun, ErpOpeningCreateBatchDryRun>();
builder.Services.AddSingleton<IErpOpeningAddCoaLineDryRun, ErpOpeningAddCoaLineDryRun>();
builder.Services.AddSingleton<IErpOpeningAddInvLineDryRun, ErpOpeningAddInvLineDryRun>();
builder.Services.AddSingleton<IErpOpeningPostBatchDryRun, ErpOpeningPostBatchDryRun>();
builder.Services.AddSingleton<IErpSaveRfqDryRun, ErpSaveRfqDryRun>();
builder.Services.AddSingleton<IErpDeliveryNoteCreateDryRun, ErpDeliveryNoteCreateDryRun>();
builder.Services.AddSingleton<IErpSaveContactDryRun, ErpSaveContactDryRun>();
builder.Services.AddSingleton<IErpSyncContactsDryRun, ErpSyncContactsDryRun>();
builder.Services.AddSingleton<IErpDocumentUploadDryRun, ErpDocumentUploadDryRun>();
builder.Services.AddSingleton<IErpDocumentDeleteDryRun, ErpDocumentDeleteDryRun>();
builder.Services.AddSingleton<IErpSaveCompanyDryRun, ErpSaveCompanyDryRun>();
builder.Services.AddSingleton<IErpSaveTemplateDryRun, ErpSaveTemplateDryRun>();
builder.Services.AddSingleton<IErpUploadLogoDryRun, ErpUploadLogoDryRun>();
builder.Services.AddSingleton<IErpUploadAttachmentDryRun, ErpUploadAttachmentDryRun>();
builder.Services.AddSingleton<IErpDeleteAttachmentDryRun, ErpDeleteAttachmentDryRun>();
builder.Services.AddSingleton<IErpSyncEinvoiceSellerDryRun, ErpSyncEinvoiceSellerDryRun>();
builder.Services.AddSingleton<IErpExpenseReportSaveDryRun, ErpExpenseReportSaveDryRun>();
builder.Services.AddSingleton<IErpPoSaveDryRun, ErpPoSaveDryRun>();
builder.Services.AddSingleton<IErpPoStatusDryRun, ErpPoStatusDryRun>();
builder.Services.AddSingleton<IErpPoReceiveLinesDryRun, ErpPoReceiveLinesDryRun>();
builder.Services.AddSingleton<IErpPoToInvoiceDryRun, ErpPoToInvoiceDryRun>();
builder.Services.AddSingleton<IErpCustomerCreateDryRun, ErpCustomerCreateDryRun>();
builder.Services.AddSingleton<IErpSoSaveDryRun, ErpSoSaveDryRun>();
builder.Services.AddSingleton<IErpSoStatusDryRun, ErpSoStatusDryRun>();
builder.Services.AddSingleton<IErpSoToInvoiceDryRun, ErpSoToInvoiceDryRun>();
builder.Services.AddSingleton<IErpTransferVoucherDryRun, ErpTransferVoucherDryRun>();
builder.Services.AddSingleton<IErpPaymentBatchSaveDryRun, ErpPaymentBatchSaveDryRun>();
builder.Services.AddSingleton<IErpPettyCashSaveDryRun, ErpPettyCashSaveDryRun>();
builder.Services.AddSingleton<IErpAgendaSaveDryRun, ErpAgendaSaveDryRun>();
builder.Services.AddSingleton<IErpKbSaveDryRun, ErpKbSaveDryRun>();
builder.Services.AddSingleton<IErpMultiEntitySaveDryRun, ErpMultiEntitySaveDryRun>();
builder.Services.AddSingleton<IErpCsSaveDeclarationDryRun, ErpCsSaveDeclarationDryRun>();
builder.Services.AddSingleton<IErpCsSubmitDeclarationDryRun, ErpCsSubmitDeclarationDryRun>();
builder.Services.AddSingleton<IErpCsDeleteDeclarationDryRun, ErpCsDeleteDeclarationDryRun>();
builder.Services.AddSingleton<IErpCsListDeclarationsDryRun, ErpCsListDeclarationsDryRun>();
builder.Services.AddSingleton<IErpCsImportDeclarationPdfDryRun, ErpCsImportDeclarationPdfDryRun>();
builder.Services.AddSingleton<IErpShortcutListDryRun, ErpShortcutListDryRun>();
builder.Services.AddSingleton<IErpShortcutAddDryRun, ErpShortcutAddDryRun>();
builder.Services.AddSingleton<IErpShortcutDeleteDryRun, ErpShortcutDeleteDryRun>();
builder.Services.AddSingleton<IErpShortcutDeleteKeyDryRun, ErpShortcutDeleteKeyDryRun>();
builder.Services.AddSingleton<IErpShortcutResetDryRun, ErpShortcutResetDryRun>();
builder.Services.AddSingleton<IErpShortcutReorderDryRun, ErpShortcutReorderDryRun>();
builder.Services.AddSingleton<IErpErpFavAddDryRun, ErpErpFavAddDryRun>();
builder.Services.AddSingleton<IErpErpFavRemoveDryRun, ErpErpFavRemoveDryRun>();
builder.Services.AddSingleton<IErpErpGlobalSearchDryRun, ErpErpGlobalSearchDryRun>();
builder.Services.AddSingleton<IErpJwRepairCreateDryRun, ErpJwRepairCreateDryRun>();
builder.Services.AddSingleton<IErpJwRepairUpdateStatusDryRun, ErpJwRepairUpdateStatusDryRun>();
builder.Services.AddSingleton<IErpJwSeedSampleDataDryRun, ErpJwSeedSampleDataDryRun>();
builder.Services.AddSingleton<IErpAiAssistantQueryDryRun, ErpAiAssistantQueryDryRun>();
builder.Services.AddSingleton<IErpPrintDesignerSaveDryRun, ErpPrintDesignerSaveDryRun>();
builder.Services.AddSingleton<IErpWorkflowSaveDryRun, ErpWorkflowSaveDryRun>();
builder.Services.AddSingleton<IErpWorkflowRunDryRun, ErpWorkflowRunDryRun>();
builder.Services.AddSingleton<IErpAutomationActivateDryRun, ErpAutomationActivateDryRun>();
builder.Services.AddSingleton<IErpAutomationDeactivateDryRun, ErpAutomationDeactivateDryRun>();
builder.Services.AddSingleton<IErpAutomationInstallTemplateDryRun, ErpAutomationInstallTemplateDryRun>();
builder.Services.AddSingleton<IErpAutomationEnableCategoryDryRun, ErpAutomationEnableCategoryDryRun>();
builder.Services.AddSingleton<IErpAutomationTickDryRun, ErpAutomationTickDryRun>();
builder.Services.AddSingleton<IErpTenantConfigSaveDryRun, ErpTenantConfigSaveDryRun>();
builder.Services.AddSingleton<ICpLangSetIsCustomDryRun, CpLangSetIsCustomDryRun>();
builder.Services.AddSingleton<ICpLangSetIsErrorDryRun, CpLangSetIsErrorDryRun>();
builder.Services.AddSingleton<ICpLangSetSameDryRun, CpLangSetSameDryRun>();
builder.Services.AddSingleton<ICpLangSetUsedFoundDryRun, CpLangSetUsedFoundDryRun>();
builder.Services.AddSingleton<ICpLangSearchUsedFoundDryRun, CpLangSearchUsedFoundDryRun>();
builder.Services.AddSingleton<ICpVersionGetUpdatePackDryRun, CpVersionGetUpdatePackDryRun>();
builder.Services.AddSingleton<IStorefrontGetArticleListDryRun, StorefrontGetArticleListDryRun>();
builder.Services.AddSingleton<IStorefrontLoadReturnsDataDryRun, StorefrontLoadReturnsDataDryRun>();
builder.Services.AddSingleton<IErpErFormatSaveDryRun, ErpErFormatSaveDryRun>();
builder.Services.AddSingleton<IErpErFieldAddDryRun, ErpErFieldAddDryRun>();
builder.Services.AddSingleton<IErpPrjaBudgetSaveDryRun, ErpPrjaBudgetSaveDryRun>();
builder.Services.AddSingleton<IErpPrjaTxnAddDryRun, ErpPrjaTxnAddDryRun>();
builder.Services.AddSingleton<IErpPrjaRecognizeDryRun, ErpPrjaRecognizeDryRun>();
builder.Services.AddSingleton<IErpCostmItemSetDryRun, ErpCostmItemSetDryRun>();
builder.Services.AddSingleton<IErpCostmTxnAddDryRun, ErpCostmTxnAddDryRun>();
builder.Services.AddSingleton<IErpCostmCloseRunDryRun, ErpCostmCloseRunDryRun>();
builder.Services.AddSingleton<IErpIntgEntitySaveDryRun, ErpIntgEntitySaveDryRun>();
builder.Services.AddSingleton<IErpIntgSubSaveDryRun, ErpIntgSubSaveDryRun>();
builder.Services.AddSingleton<IErpIntgEventRaiseDryRun, ErpIntgEventRaiseDryRun>();
builder.Services.AddSingleton<IErpFyCreateDryRun, ErpFyCreateDryRun>();
builder.Services.AddSingleton<IErpFyCloseDryRun, ErpFyCloseDryRun>();
builder.Services.AddSingleton<IErpFyReopenDryRun, ErpFyReopenDryRun>();
builder.Services.AddSingleton<IErpFyPeriodStatusDryRun, ErpFyPeriodStatusDryRun>();
builder.Services.AddSingleton<IErpPltJobSaveDryRun, ErpPltJobSaveDryRun>();
builder.Services.AddSingleton<IErpPltJobRunDryRun, ErpPltJobRunDryRun>();
builder.Services.AddSingleton<IErpPltFeatureSaveDryRun, ErpPltFeatureSaveDryRun>();
builder.Services.AddSingleton<IErpOaPartySaveDryRun, ErpOaPartySaveDryRun>();
builder.Services.AddSingleton<IErpOaAddressSaveDryRun, ErpOaAddressSaveDryRun>();
builder.Services.AddSingleton<IErpOaContactSaveDryRun, ErpOaContactSaveDryRun>();
builder.Services.AddSingleton<IErpOaCalendarSaveDryRun, ErpOaCalendarSaveDryRun>();
builder.Services.AddSingleton<IErpOaHolidayAddDryRun, ErpOaHolidayAddDryRun>();
builder.Services.AddSingleton<IErpRbacRoleSaveDryRun, ErpRbacRoleSaveDryRun>();
builder.Services.AddSingleton<IErpRbacRoleDutyDryRun, ErpRbacRoleDutyDryRun>();
builder.Services.AddSingleton<IErpRbacUserRoleDryRun, ErpRbacUserRoleDryRun>();
builder.Services.AddSingleton<IErpRtlChannelSaveDryRun, ErpRtlChannelSaveDryRun>();
builder.Services.AddSingleton<ICpCreateSitemapDryRun, CpCreateSitemapDryRun>();
builder.Services.AddSingleton<ICpLangSaveTranslationDryRun, CpLangSaveTranslationDryRun>();
builder.Services.AddSingleton<ICpLangSaveDescriptionDryRun, CpLangSaveDescriptionDryRun>();
builder.Services.AddSingleton<ICpLangCreateStringDryRun, CpLangCreateStringDryRun>();
builder.Services.AddSingleton<ICpLangDeleteNotUsedDryRun, CpLangDeleteNotUsedDryRun>();
builder.Services.AddSingleton<ICpPacksDeleteDryRun, CpPacksDeleteDryRun>();
builder.Services.AddSingleton<ICpChannelsWriteDryRun, CpChannelsWriteDryRun>();
builder.Services.AddSingleton<ICpLogisticsWriteDryRun, CpLogisticsWriteDryRun>();
builder.Services.AddSingleton<ICpPaymentsWriteDryRun, CpPaymentsWriteDryRun>();
builder.Services.AddSingleton<ICpWorkshopWriteDryRun, CpWorkshopWriteDryRun>();
builder.Services.AddSingleton<ICpTemplatesActionsDryRun, CpTemplatesActionsDryRun>();
builder.Services.AddSingleton<ICpPriceReviewWriteDryRun, CpPriceReviewWriteDryRun>();
builder.Services.AddSingleton<ICpPriceReviewCreateCsvDryRun, CpPriceReviewCreateCsvDryRun>();
builder.Services.AddSingleton<ICpAccessoriesPhotosDryRun, CpAccessoriesPhotosDryRun>();
builder.Services.AddSingleton<ICpVersionClearUpdatesDryRun, CpVersionClearUpdatesDryRun>();
builder.Services.AddSingleton<IStorefrontBulkUploadProcessDryRun, StorefrontBulkUploadProcessDryRun>();
builder.Services.AddSingleton<IStorefrontSetMyCityDryRun, StorefrontSetMyCityDryRun>();
builder.Services.AddSingleton<IStorefrontLoginSendCodeDryRun, StorefrontLoginSendCodeDryRun>();
builder.Services.AddSingleton<IStorefrontLoginCheckCodeDryRun, StorefrontLoginCheckCodeDryRun>();
builder.Services.AddSingleton<ICpReturnActionDryRun, CpReturnActionDryRun>();
builder.Services.AddSingleton<ICpSetUsersVinViewedDryRun, CpSetUsersVinViewedDryRun>();
builder.Services.AddSingleton<ICpSetUserCommentDryRun, CpSetUserCommentDryRun>();
builder.Services.AddSingleton<ICpPricesImportCsvDryRun, CpPricesImportCsvDryRun>();
builder.Services.AddSingleton<ICpPricesCompleteSessionDryRun, CpPricesCompleteSessionDryRun>();
builder.Services.AddSingleton<ISurfaceFieldParityReporter, SurfaceFieldParityReporter>();
builder.Services.AddSingleton<IUmapiUsageSummaryReporter, UmapiUsageSummaryReporter>();
builder.Services.AddSingleton<IPlatformJobsSummaryReporter, PlatformJobsSummaryReporter>();
builder.Services.AddSingleton<ISurfaceDashboardSummaryReporter, SurfaceDashboardSummaryReporter>();
builder.Services.AddSingleton<IStorefrontCartChangeCountNeedDryRun, StorefrontCartChangeCountNeedDryRun>();
builder.Services.AddSingleton<IStorefrontCartCheckForOrderDryRun, StorefrontCartCheckForOrderDryRun>();
builder.Services.AddSingleton<IStorefrontCartDeleteDryRun, StorefrontCartDeleteDryRun>();
builder.Services.AddSingleton<ICpOmsSetItemStatusDryRun, CpOmsSetItemStatusDryRun>();
builder.Services.AddSingleton<ICpOmsSetItemsStatusDryRun, CpOmsSetItemsStatusDryRun>();
builder.Services.AddSingleton<ICpOmsSendMessageDryRun, CpOmsSendMessageDryRun>();
builder.Services.AddSingleton<ICpOmsAddCommentDryRun, CpOmsAddCommentDryRun>();
builder.Services.AddSingleton<ICpOmsSetViewedDryRun, CpOmsSetViewedDryRun>();
builder.Services.AddSingleton<ICpOmsUpdateItemDryRun, CpOmsUpdateItemDryRun>();
builder.Services.AddSingleton<ICpOmsPayRefundDryRun, CpOmsPayRefundDryRun>();
builder.Services.AddSingleton<ICpOmsUpdateItemsDryRun, CpOmsUpdateItemsDryRun>();
builder.Services.AddSingleton<ICpOmsFulfillmentSetStageDryRun, CpOmsFulfillmentSetStageDryRun>();
builder.Services.AddSingleton<ICpOmsFulfillmentAdvanceDryRun, CpOmsFulfillmentAdvanceDryRun>();
builder.Services.AddSingleton<ICpOmsRefreshItemCostDryRun, CpOmsRefreshItemCostDryRun>();
builder.Services.AddSingleton<IErpPurchaseFromOrderDryRun, ErpPurchaseFromOrderDryRun>();
builder.Services.AddSingleton<IErpCcySetRateDryRun, ErpCcySetRateDryRun>();
builder.Services.AddSingleton<IErpPeriodSoftCloseDryRun, ErpPeriodSoftCloseDryRun>();
builder.Services.AddSingleton<IErpPeriodLockDryRun, ErpPeriodLockDryRun>();
builder.Services.AddSingleton<IErpCustomerSettlementDryRun, ErpCustomerSettlementDryRun>();
builder.Services.AddSingleton<IErpSupplierSettlementDryRun, ErpSupplierSettlementDryRun>();
builder.Services.AddSingleton<IErpFiscalSetLockDryRun, ErpFiscalSetLockDryRun>();
builder.Services.AddSingleton<IErpPeriodReopenDryRun, ErpPeriodReopenDryRun>();
builder.Services.AddSingleton<IErpPurchaseAdjustmentDryRun, ErpPurchaseAdjustmentDryRun>();
builder.Services.AddSingleton<IErpOrderSettlementDryRun, ErpOrderSettlementDryRun>();
builder.Services.AddSingleton<IErpSyncSuppliersDryRun, ErpSyncSuppliersDryRun>();
builder.Services.AddSingleton<IErpGlPostSalesDryRun, ErpGlPostSalesDryRun>();
builder.Services.AddSingleton<IErpGlSyncUnpostedDryRun, ErpGlSyncUnpostedDryRun>();
builder.Services.AddSingleton<IErpWorkflowStatusDryRun, ErpWorkflowStatusDryRun>();
builder.Services.AddSingleton<IErpWorkflowCreateDryRun, ErpWorkflowCreateDryRun>();
builder.Services.AddSingleton<IErpMarketingCreateDryRun, ErpMarketingCreateDryRun>();
builder.Services.AddSingleton<IErpSubscriptionSaveDryRun, ErpSubscriptionSaveDryRun>();
builder.Services.AddSingleton<IErpContractSaveDryRun, ErpContractSaveDryRun>();
builder.Services.AddSingleton<IErpWmsReceiveDryRun, ErpWmsReceiveDryRun>();
builder.Services.AddSingleton<IErpWmsLocationSaveDryRun, ErpWmsLocationSaveDryRun>();
builder.Services.AddSingleton<IErpCollectionsCaseSaveDryRun, ErpCollectionsCaseSaveDryRun>();
builder.Services.AddSingleton<IErpProcReqSaveDryRun, ErpProcReqSaveDryRun>();
builder.Services.AddSingleton<IErpFinPeriodStatusDryRun, ErpFinPeriodStatusDryRun>();
builder.Services.AddSingleton<IErpWmsWaveCreateDryRun, ErpWmsWaveCreateDryRun>();
builder.Services.AddSingleton<IErpWmsWaveReleaseDryRun, ErpWmsWaveReleaseDryRun>();
builder.Services.AddSingleton<IErpWmsWorkCompleteDryRun, ErpWmsWorkCompleteDryRun>();
builder.Services.AddSingleton<IErpSubscriptionStatusDryRun, ErpSubscriptionStatusDryRun>();
builder.Services.AddSingleton<IErpCollectionsCaseStatusDryRun, ErpCollectionsCaseStatusDryRun>();
builder.Services.AddSingleton<IErpProcReqSubmitDryRun, ErpProcReqSubmitDryRun>();
builder.Services.AddSingleton<IErpProcReqDecisionDryRun, ErpProcReqDecisionDryRun>();
builder.Services.AddSingleton<IErpWmsLocationDeleteDryRun, ErpWmsLocationDeleteDryRun>();
builder.Services.AddSingleton<ICpOmsSetCourierDryRun, CpOmsSetCourierDryRun>();
builder.Services.AddSingleton<ICpOmsDeleteOrdersDryRun, CpOmsDeleteOrdersDryRun>();
builder.Services.AddSingleton<IErpCashVoucherAmendDryRun, ErpCashVoucherAmendDryRun>();
builder.Services.AddSingleton<IErpCashVoucherVoidDryRun, ErpCashVoucherVoidDryRun>();
builder.Services.AddSingleton<IErpCashEntryCreateDryRun, ErpCashEntryCreateDryRun>();
builder.Services.AddSingleton<IErpReceiptVoucherDryRun, ErpReceiptVoucherDryRun>();
builder.Services.AddSingleton<IErpPaymentVoucherDryRun, ErpPaymentVoucherDryRun>();
builder.Services.AddSingleton<IErpSupplierCreateDryRun, ErpSupplierCreateDryRun>();
builder.Services.AddSingleton<IErpPurchaseCreateDryRun, ErpPurchaseCreateDryRun>();
builder.Services.AddSingleton<IErpPurchaseDeleteDryRun, ErpPurchaseDeleteDryRun>();
builder.Services.AddSingleton<IErpPurchaseAmendDryRun, ErpPurchaseAmendDryRun>();
builder.Services.AddSingleton<IErpInvoiceDeleteDryRun, ErpInvoiceDeleteDryRun>();
builder.Services.AddSingleton<IErpCashAccountCreateDryRun, ErpCashAccountCreateDryRun>();
builder.Services.AddSingleton<IErpCoaCreateDryRun, ErpCoaCreateDryRun>();
builder.Services.AddSingleton<IErpCustomerMasterSaveDryRun, ErpCustomerMasterSaveDryRun>();
builder.Services.AddSingleton<IErpAsRmaCreateDryRun, ErpAsRmaCreateDryRun>();

builder.Services.AddSingleton<IErpGlManualEntryDryRun, ErpGlManualEntryDryRun>();
builder.Services.AddSingleton<IErpGlReverseJournalDryRun, ErpGlReverseJournalDryRun>();
builder.Services.AddSingleton<IErpPurchaseVoidDryRun, ErpPurchaseVoidDryRun>();
builder.Services.AddSingleton<IErpInvoiceCancelDryRun, ErpInvoiceCancelDryRun>();
builder.Services.AddSingleton<IErpSalesOrderCancelDryRun, ErpSalesOrderCancelDryRun>();
builder.Services.AddSingleton<IErpSalesOrderDeleteDryRun, ErpSalesOrderDeleteDryRun>();
builder.Services.AddSingleton<IErpPoDeleteDryRun, ErpPoDeleteDryRun>();
builder.Services.AddSingleton<IStorefrontCartAddDryRun, StorefrontCartAddDryRun>();
builder.Services.AddSingleton<IStorefrontGarageNotepadAddDryRun, StorefrontGarageNotepadAddDryRun>();
builder.Services.AddSingleton<IStorefrontQuoteSubmitDryRun, StorefrontQuoteSubmitDryRun>();
builder.Services.AddSingleton<IStorefrontQuoteAcceptDryRun, StorefrontQuoteAcceptDryRun>();
builder.Services.AddSingleton<IStorefrontQuoteAddItemDryRun, StorefrontQuoteAddItemDryRun>();
builder.Services.AddSingleton<IStorefrontQuoteAddManualDryRun, StorefrontQuoteAddManualDryRun>();
builder.Services.AddSingleton<IStorefrontGarageSetActiveDryRun, StorefrontGarageSetActiveDryRun>();
builder.Services.AddSingleton<IStorefrontGarageDeleteDryRun, StorefrontGarageDeleteDryRun>();
builder.Services.AddSingleton<IStorefrontGarageCheckCarDryRun, StorefrontGarageCheckCarDryRun>();
builder.Services.AddSingleton<IStorefrontCheckoutCreateDryRun, StorefrontCheckoutCreateDryRun>();
builder.Services.AddSingleton<IStorefrontOrderSendMessageDryRun, StorefrontOrderSendMessageDryRun>();
builder.Services.AddSingleton<IPythonSidecarCatalogReporter, PythonSidecarCatalogReporter>();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
    [
        "application/json",
        "application/javascript",
        "text/css",
        "text/html",
        "text/plain",
        "text/json",
        "image/svg+xml"
    ]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseResponseCompression();
app.UseMiddleware<SecurityHeadersMiddleware>();
// Deep /CP|/ERP|/BOS|/shop product paths → ASP.NET; PHP only via /php-reference/*.
app.UseMiddleware<PhpProductPathRedirectMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();
// BOS is Super-CP / platform only — never answer /bos on tenant hosts (epartscart, …).
app.UseMiddleware<BosHostGateMiddleware>();
app.UseMiddleware<RouteCutoverDecisionMiddleware>();
// Credential POSTs on /cp|/erp|/bos|/storefront/login and /auth/login/admin — before antiforgery/Blazor.
app.UseMiddleware<LegacyLoginBridgeMiddleware>();
// Required for Blazor SSR endpoints (MapRazorComponents adds antiforgery metadata).
app.UseAntiforgery();

app.MapHealthChecks(EcomAeRoutes.Health);

app.MapGet(EcomAeRoutes.MigrationStatus, (IMigrationParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MigrationReadiness, (IMigrationReadinessReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MigrationCutoverPlan, (IMigrationCutoverPlanner planner) => Results.Ok(planner.BuildPlan()));

app.MapGet(EcomAeRoutes.MigrationProgress, (IMigrationProgressReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.ZeroPhpCompletion, (IZeroPhpCompletionReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.PhpDecommissionReadiness, (IPhpDecommissionReadinessReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.PythonSidecars, (IPythonSidecarCatalogReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MigrationRouteCutover, (HttpContext context, IMigrationRouteCutoverPolicy policy) =>
{
    var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
    return tenant is null ? Results.Problem("Tenant context was not resolved.") : Results.Ok(policy.Decide(tenant));
});

app.MapGet(EcomAeRoutes.MigrationDataParity, (IDataParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MigrationCutoverValidation, (ICutoverValidationReporter reporter) => Results.Ok(reporter.BuildReport()));
app.MapGet(EcomAeRoutes.MigrationPhpReferenceMode, (IPhpReferenceModeReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MigrationUmapiUsage, async (int? days, IUmapiUsageSummaryReporter reporter, CancellationToken cancellationToken) =>
{
    var summary = await reporter.BuildAsync(days ?? 7, cancellationToken);
    return Results.Ok(new
    {
        daily_limit = summary.DailyLimit,
        today_live = summary.TodayLive,
        today_cache = summary.TodayCache,
        today_blocked = summary.TodayBlocked,
        remaining = summary.Remaining,
        pct_used = summary.PctUsed,
        quota_exceeded = summary.QuotaExceeded,
        by_action_today = summary.ByActionToday,
        by_source_today = summary.BySourceToday,
        history = summary.History,
        recent_today = summary.RecentToday,
        source = summary.Source,
        message = summary.Message,
        note = "Internal migration diagnostic only. External catalog clients must not call PHP usage_report."
    });
});

app.MapGet(EcomAeRoutes.MigrationPlatformJobs, async (int? limit, IPlatformJobsSummaryReporter reporter, CancellationToken cancellationToken) =>
{
    var summary = await reporter.BuildAsync(limit ?? 50, cancellationToken);
    return Results.Ok(new
    {
        total = summary.Total,
        queued = summary.Queued,
        running = summary.Running,
        done = summary.Done,
        failed = summary.Failed,
        by_status = summary.ByStatus,
        by_type = summary.ByType,
        recent = summary.Recent,
        source = summary.Source,
        message = summary.Message,
        note = "Internal migration diagnostic only. Does not claim or complete jobs; PHP cron remains authoritative."
    });
});

app.MapGet(EcomAeRoutes.SurfaceParity, (ISurfaceParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.PresentationParity, (IPresentationParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.PhpModuleCatalog, () => Results.Ok(PhpModuleCatalog.BuildSummary()));

app.MapGet(EcomAeRoutes.LiveSurfaceLinks, (ILiveSurfaceLinkReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.LiveTenantPresentationLock, () => Results.Ok(LiveTenantPresentationLock.BuildSummary()));

app.MapGet(EcomAeRoutes.AspNetZeroPhpPath, (IAspNetZeroPhpPathReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MigrationOnPremisesParity, (IOnPremisesParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MarketingPresentationLock, (IMarketingPresentationLockReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.SurfaceFieldParity, (ISurfaceFieldParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.TenantContext, (HttpContext context) =>
{
    var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
    return tenant is null ? Results.Problem("Tenant context was not resolved.") : Results.Ok(tenant);
});

app.MapGet(EcomAeRoutes.TenantWorkspaceParity, (ITenantWorkspaceParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.LegacySessionProbe, async (HttpContext context, ILegacySessionValidator validator) =>
{
    var session = await validator.ValidateAsync(context, context.RequestAborted);
    return Results.Ok(new
    {
        session.Kind,
        session.UserId,
        session.IsAuthenticated,
        session.Email,
        group_ids = session.Groups,
        has_backend_access = session.HasBackendAccess,
        capabilities = session.Capabilities,
        module_acl = session.Modules,
        session.Permissions
    });
});

app.MapGet(EcomAeRoutes.LegacyApiClientParity, (ILegacyApiClientParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.LegacySessionParity, (ILegacySessionParityReporter reporter) => Results.Ok(reporter.BuildReport()));

// Browser navigations to the old POST-only URL should land on CP login, not a blank 405/500 page.
app.MapGet(EcomAeRoutes.LegacyAdminLogin, () => Results.Redirect(EcomAeRoutes.ControlPanelLogin));

// PHP-compatible logout — clears admin/customer cookies (+ best-effort sessions row delete).
async Task<IResult> PerformLegacyLogout(HttpContext context, LegacyLogoutService logout, string? surface, string? returnUrl)
{
    await logout.LogoutAsync(context, context.RequestAborted);
    var dest = LegacyLogoutService.RedirectForSurface(surface, returnUrl);
    context.Response.Headers["X-EcomAE-Logout"] = "cleared";
    return Results.Redirect(dest);
}

app.MapMethods(EcomAeRoutes.LegacyLogout, ["GET", "POST"], async (HttpContext context, LegacyLogoutService logout) =>
{
    var surface = context.Request.Query["surface"].FirstOrDefault()
        ?? context.Request.Query["s"].FirstOrDefault();
    var returnUrl = context.Request.Query["return"].FirstOrDefault()
        ?? context.Request.Query["redirect"].FirstOrDefault();
    if (context.Request.HasFormContentType)
    {
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        surface ??= form["surface"].FirstOrDefault();
        returnUrl ??= form["return"].FirstOrDefault() ?? form["redirect"].FirstOrDefault();
    }

    return await PerformLegacyLogout(context, logout, surface, returnUrl);
});
app.MapMethods(EcomAeRoutes.ControlPanelLogout, ["GET", "POST"], (HttpContext context, LegacyLogoutService logout)
    => PerformLegacyLogout(context, logout, "cp", null));
app.MapMethods(EcomAeRoutes.ErpLogout, ["GET", "POST"], (HttpContext context, LegacyLogoutService logout)
    => PerformLegacyLogout(context, logout, "erp", null));
app.MapMethods(EcomAeRoutes.BosLogout, ["GET", "POST"], (HttpContext context, LegacyLogoutService logout)
    => PerformLegacyLogout(context, logout, "bos", null));
app.MapMethods(EcomAeRoutes.StorefrontLogout, ["GET", "POST"], (HttpContext context, LegacyLogoutService logout)
    => PerformLegacyLogout(context, logout, "storefront", null));

// PHP-compatible admin/customer login bridge (exact-route). Sets cookies; does not cut over /CP/ /ERP/ /BOS/.
// Accepts JSON or application/x-www-form-urlencoded (HTML login forms).
// Prefer LegacyLoginBridgeMiddleware (runs before antiforgery). This MapPost remains as a fallback.
app.MapPost(EcomAeRoutes.LegacyAdminLogin, async (HttpContext context, ILegacyAdminLoginService login, ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("EcomAE.Auth.Login");
    var wantsHtml = context.Request.HasFormContentType
        || (context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase)
            && !context.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase));

    string contact = "", password = "", contactType = "email", surface = "cp", redirect = "";
    var remember = false;

    try
    {
        if (!login.IsConfigured)
        {
            if (wantsHtml)
            {
                var surfaceFail = "cp";
                if (context.Request.HasFormContentType)
                {
                    var earlyForm = await context.Request.ReadFormAsync(context.RequestAborted);
                    surfaceFail = string.IsNullOrWhiteSpace(earlyForm["surface"]) ? "cp" : earlyForm["surface"].ToString();
                }

                return Results.Redirect($"/{LegacyLoginSurfaceParser.Key(surfaceFail)}/login?error=bridge_not_configured");
            }

            return Results.Json(new { ok = false, code = "bridge_not_configured", message = "Set EcomAE__SecretSuccession and DB. Use PHP login." }, statusCode: 503);
        }

        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            contact = form["contact"].ToString();
            password = form["password"].ToString();
            contactType = string.IsNullOrWhiteSpace(form["contact_type"]) ? "email" : form["contact_type"].ToString();
            surface = string.IsNullOrWhiteSpace(form["surface"]) ? "cp" : form["surface"].ToString();
            redirect = form["redirect"].ToString();
            remember = form["remember_me"].Count > 0;
            wantsHtml = true;
        }
        else
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync(context.RequestAborted);
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                var root = doc.RootElement;
                contact = root.TryGetProperty("contact", out var c) ? c.GetString() ?? "" : "";
                password = root.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";
                contactType = root.TryGetProperty("contact_type", out var t) ? t.GetString() ?? "email" : "email";
                surface = root.TryGetProperty("surface", out var s) ? s.GetString() ?? "cp" : "cp";
                redirect = root.TryGetProperty("redirect", out var rd) ? rd.GetString() ?? "" : "";
                remember = root.TryGetProperty("remember_me", out var r) && r.ValueKind == System.Text.Json.JsonValueKind.True;
            }
            catch
            {
                return Results.Json(new { ok = false, code = "bad_json", message = "Expected JSON body." }, statusCode: 400);
            }
        }

        var loginSurface = LegacyLoginSurfaceParser.Parse(surface);
        var outcome = await login.LoginAsync(
            new LegacyLoginRequest(contact, password, contactType, remember, loginSurface),
            LegacySessionTokenFactory.ResolveClientIp(context.Request),
            context.Request.Headers.UserAgent.ToString(),
            context.RequestAborted);

        if (!outcome.Ok || outcome.Success is null)
        {
            if (wantsHtml)
            {
                return Results.Redirect($"/{LegacyLoginSurfaceParser.Key(surface)}/login?error={Uri.EscapeDataString(outcome.Failure?.Code ?? "invalid_credentials")}");
            }

            return Results.Json(new
            {
                ok = false,
                code = outcome.Failure?.Code ?? "invalid_credentials",
                message = outcome.Failure?.Message ?? "Incorrect login or password."
            }, statusCode: 401);
        }

        LegacyLoginCookieWriter.Apply(context.Response, outcome.Success, remember);
        var dest = string.IsNullOrWhiteSpace(redirect) ? outcome.Success.RedirectPath : redirect;
        if (!dest.StartsWith('/') || dest.StartsWith("//", StringComparison.Ordinal))
        {
            dest = outcome.Success.RedirectPath;
        }

        if (wantsHtml)
        {
            return Results.Redirect(dest);
        }

        return Results.Ok(new
        {
            ok = true,
            user_id = outcome.Success.UserId,
            email = outcome.Success.Email,
            admin_session = outcome.Success.AdminSession,
            redirect = dest,
            note = "PHP-compatible session row created. Product chrome remains PHP-authoritative."
        });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Login bridge failed for surface={Surface}", surface);
        if (wantsHtml)
        {
            return Results.Redirect($"/{LegacyLoginSurfaceParser.Key(surface)}/login?error=login_backend_error");
        }

        return Results.Json(new
        {
            ok = false,
            code = "login_backend_error",
            message = "Login bridge backend error. Check TenantRegistry DB + EcomAE__SecretSuccession, then journalctl -u ecomae-platform."
        }, statusCode: 500);
    }
}).DisableAntiforgery();

app.MapEcomAeSurfaceModules();

// Serve PHP chrome CSS/static from the monorepo so ASP.NET shells match PHP look
// even when PHP-FPM is not fronting Kestrel (local + loopback probes).
PhpLegacyAssetBridge.Map(app, app.Environment);

// Blazor SSR ops console (exact /migration/console). Interim improvement UI — not product chrome cutover.
app.MapRazorComponents<App>();

app.Run();

public partial class Program;
