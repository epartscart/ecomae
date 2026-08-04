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

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents();

builder.Services.Configure<EcomAeOptions>(builder.Configuration.GetSection(EcomAeOptions.SectionName));
builder.Services.Configure<MigrationRouteCutoverOptions>(builder.Configuration.GetSection(MigrationRouteCutoverOptions.SectionName));
builder.Services.Configure<PriceLookupOptions>(builder.Configuration.GetSection(PriceLookupOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITenantRegistry, ConfigurationTenantRegistry>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<ILegacySessionStore>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    return connections.IsConfigured
        ? ActivatorUtilities.CreateInstance<DbLegacySessionStore>(sp)
        : new MigrationLegacySessionStore();
});
builder.Services.AddSingleton<ILegacySessionValidator, DbBackedLegacySessionValidator>();
builder.Services.AddSingleton<ILegacySessionParityReporter, LegacySessionParityReporter>();
builder.Services.AddSingleton<ILegacyAdminLoginService>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EcomAeOptions>>();
    return connections.IsConfigured && !string.IsNullOrWhiteSpace(options.Value.SecretSuccession)
        ? ActivatorUtilities.CreateInstance<DbLegacyAdminLoginService>(sp)
        : new UnconfiguredLegacyAdminLoginService();
});
builder.Services.AddSingleton<ITenantDbConnectionFactory, MySqlTenantDbConnectionFactory>();

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
builder.Services.AddSingleton<IMigrationProgressReporter, MigrationProgressReporter>();
builder.Services.AddSingleton<ISurfaceParityReporter, SurfaceParityReporter>();
builder.Services.AddSingleton<IZeroPhpCompletionReporter, ZeroPhpCompletionReporter>();
builder.Services.AddSingleton<IPhpDecommissionReadinessReporter, PhpDecommissionReadinessReporter>();
builder.Services.AddSingleton<ILiveSurfaceLinkReporter, LiveSurfaceLinkReporter>();
builder.Services.AddSingleton<IMarketingPresentationLockReporter, MarketingPresentationLockReporter>();
builder.Services.AddSingleton<IAspNetZeroPhpPathReporter, AspNetZeroPhpPathReporter>();
builder.Services.AddSingleton<IOnPremisesParityReporter, OnPremisesParityReporter>();
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
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<RouteCutoverDecisionMiddleware>();
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

// PHP-compatible admin/customer login bridge (exact-route). Sets cookies; does not cut over /CP/ /ERP/ /BOS/.
// Accepts JSON or application/x-www-form-urlencoded (HTML login forms).
app.MapPost(EcomAeRoutes.LegacyAdminLogin, async (HttpContext context, ILegacyAdminLoginService login) =>
{
    var wantsHtml = context.Request.HasFormContentType
        || (context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase)
            && !context.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase));

    if (!login.IsConfigured)
    {
        if (wantsHtml)
        {
            var surfaceFail = context.Request.HasFormContentType
                ? context.Request.Form["surface"].ToString()
                : "cp";
            return Results.Redirect($"/{LegacyLoginSurfaceParser.Key(surfaceFail)}/login?error=bridge_not_configured");
        }

        return Results.Json(new { ok = false, code = "bridge_not_configured", message = "Set EcomAE__SecretSuccession and DB. Use PHP login." }, statusCode: 503);
    }

    string contact = "", password = "", contactType = "email", surface = "cp", redirect = "";
    var remember = false;

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
}).DisableAntiforgery();

app.MapEcomAeSurfaceModules();


// Blazor SSR ops console (exact /migration/console). Interim improvement UI — not product chrome cutover.
app.MapRazorComponents<App>();

app.Run();

public partial class Program;
