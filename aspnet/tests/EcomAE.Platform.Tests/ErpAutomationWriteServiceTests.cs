using System.Text.Json.Nodes;
using Xunit;

namespace EcomAE.Platform.Tests;

public class ErpAutomationWriteServiceTests
{
    [Fact]
    public void CatalogueMatchesDeactivateServiceSettingKeys()
    {
        Assert.Equal(
            EcomAE.Platform.Erp.ErpAutomationDeactivateWriteService.CatalogueSettingKeys.Count,
            EcomAE.Platform.Erp.ErpAutomationWriteService.Catalogue.Count);
        foreach (var (id, _) in EcomAE.Platform.Erp.ErpAutomationWriteService.Catalogue)
        {
            Assert.True(
                EcomAE.Platform.Erp.ErpAutomationDeactivateWriteService.CatalogueSettingKeys.ContainsKey(id),
                $"Catalogue id '{id}' missing from deactivate setting keys");
        }
    }

    [Theory]
    [InlineData("vat_reminder", "accounting", "vat_filing_reminder", false)]
    [InlineData("po_approval", "process", "po_approval_chain", true)]
    [InlineData("order_to_erp", "accounting", "", true)]
    [InlineData("goods_receipt_notify", "process", "grn_notify", false)]
    [InlineData("collections_dunning", "accounting", "", true)]
    public void CatalogueEntriesMatchPhpAutomationCatalogue(string id, string category, string template, bool defaultOn)
    {
        var entry = EcomAE.Platform.Erp.ErpAutomationWriteService.Catalogue[id];
        Assert.Equal(category, entry.Category);
        Assert.Equal(template, entry.WorkflowTemplate);
        Assert.Equal(defaultOn, entry.DefaultOn);
    }

    [Fact]
    public void WorkflowTemplatesCoverAllPhpTemplateIds()
    {
        var expected = new[]
        {
            "po_approval_chain", "invoice_auto_send", "low_stock_alert", "vat_filing_reminder",
            "overdue_escalation", "employee_onboarding", "daily_sales_summary", "aml_compliance_alert",
            "ap_payment_due", "credit_limit_gate", "grn_notify", "order_confirmation",
        };
        foreach (var id in expected)
        {
            Assert.True(
                EcomAE.Platform.Erp.ErpAutomationWriteService.WorkflowTemplates.ContainsKey(id),
                $"Missing PHP workflow template '{id}'");
        }

        Assert.Equal(12, EcomAE.Platform.Erp.ErpAutomationWriteService.WorkflowTemplates.Count);
    }

    [Fact]
    public void WorkflowTemplatesHavePhpTriggerTypesAndSteps()
    {
        foreach (var (id, tpl) in EcomAE.Platform.Erp.ErpAutomationWriteService.WorkflowTemplates)
        {
            Assert.Contains(tpl.TriggerType, new[] { "event", "schedule" });
            Assert.False(string.IsNullOrWhiteSpace(tpl.Name), $"Template '{id}' has no name");
            Assert.NotEmpty(tpl.Steps);
        }
    }

    [Theory]
    [InlineData(null, true)]          // never ran -> due
    [InlineData("", true)]            // empty last_run_at -> due
    [InlineData("not-a-date", true)]  // unparseable -> due
    [InlineData("2025-01-01 09:00:00", true)] // ran on a different date -> due
    [InlineData("2030-01-01 09:00:00", false)] // same date -> skip
    public void ScheduleDueFollowsPhpLastRunDateCompare(string? lastRunAt, bool expected)
    {
        var now = new DateTime(2030, 1, 1, 10, 0, 0);
        var due = EcomAE.Platform.Erp.ErpAutomationWriteService.ScheduleDue(new JsonObject(), lastRunAt, now);
        Assert.Equal(expected, due);
    }

    [Fact]
    public void ScheduleDueRefusesBeforeCronHour()
    {
        var cfg = new JsonObject { ["cron_expression"] = "0 20 * * *" };
        Assert.False(EcomAE.Platform.Erp.ErpAutomationWriteService.ScheduleDue(cfg, null, new DateTime(2030, 1, 1, 10, 0, 0)));
        Assert.True(EcomAE.Platform.Erp.ErpAutomationWriteService.ScheduleDue(cfg, null, new DateTime(2030, 1, 1, 21, 0, 0)));
    }

    [Fact]
    public void ScheduleDueDefaultsToNineWhenCronIsMissing()
    {
        Assert.False(EcomAE.Platform.Erp.ErpAutomationWriteService.ScheduleDue(new JsonObject(), null, new DateTime(2030, 1, 1, 8, 0, 0)));
        Assert.True(EcomAE.Platform.Erp.ErpAutomationWriteService.ScheduleDue(new JsonObject(), null, new DateTime(2030, 1, 1, 9, 0, 0)));
    }

    [Fact]
    public void DunningDefaultStepsMatchPhp()
    {
        var steps = EcomAE.Platform.Erp.ErpAutomationWriteService.DunningDefaultSteps;
        Assert.Equal(7, steps.Length);
        Assert.Equal((1, "email", "Friendly Payment Reminder"), steps[0]);
        Assert.Equal((60, "letter", "Final Notice Before Legal Action"), steps[6]);
        Assert.Equal("escalation", steps[5].Action);
    }
}
