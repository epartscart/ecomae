namespace EcomAE.Platform.Auth;

public interface ILegacySessionParityReporter
{
    LegacySessionParityReport BuildReport();
}
