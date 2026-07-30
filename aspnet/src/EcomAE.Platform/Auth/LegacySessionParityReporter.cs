namespace EcomAE.Platform.Auth;

public sealed class LegacySessionParityReporter : ILegacySessionParityReporter
{
    public LegacySessionParityReport BuildReport()
    {
        return new LegacySessionParityReport(
            "PHP CP/ERP/BOS session cookies and API authorization headers",
            "ASP.NET HttpLegacySessionValidator bridge and diagnostic probe",
            "bridge-ready-db-pending",
            ["PHPSESSID cookie", "X-API-Key header", "Bearer API key header"],
            [
                "Validate PHP session IDs against the production session store.",
                "Map PHP user roles and permissions into ASP.NET authorization claims.",
                "Replay CP, ERP, and BOS login flows in staging before traffic cutover."
            ]);
    }
}
