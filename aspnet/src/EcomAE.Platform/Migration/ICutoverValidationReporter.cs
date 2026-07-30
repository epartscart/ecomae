namespace EcomAE.Platform.Migration;

public interface ICutoverValidationReporter
{
    CutoverValidationReport BuildReport();
}
