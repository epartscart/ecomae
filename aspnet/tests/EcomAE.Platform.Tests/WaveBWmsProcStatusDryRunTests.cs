using EcomAE.Platform.Migration;
using Xunit;
namespace EcomAE.Platform.Tests;
public sealed class WaveBWmsProcStatusDryRunTests
{
    [Fact] public void WaveCreateValidated(){ var r=new ErpWmsWaveCreateDryRun().Evaluate(new("SKU",2,"W1")); Assert.Equal("dry-run-validated", r.Status); Assert.False(r.CutoverAllowed);} 
    [Fact] public void WaveReleaseRequiresId(){ var r=new ErpWmsWaveReleaseDryRun().Evaluate(new(0)); Assert.Equal("invalid_request", r.ValidationCode);} 
    [Fact] public void WorkCompleteValidated(){ var r=new ErpWmsWorkCompleteDryRun().Evaluate(new(3)); Assert.Equal("dry-run-validated", r.Status); Assert.Equal(0,r.Writes);} 
    [Fact] public void SubStatusValidated(){ var r=new ErpSubscriptionStatusDryRun().Evaluate(new(1,"paused")); Assert.Equal("dry-run-validated", r.Status);} 
    [Fact] public void CollStatusValidated(){ var r=new ErpCollectionsCaseStatusDryRun().Evaluate(new(1,"open")); Assert.Equal("dry-run-validated", r.Status);} 
    [Fact] public void ProcSubmitValidated(){ var r=new ErpProcReqSubmitDryRun().Evaluate(new(9)); Assert.Equal("dry-run-validated", r.Status);} 
    [Fact] public void ProcDecisionValidated(){ var r=new ErpProcReqDecisionDryRun().Evaluate(new(9,false,"no")); Assert.Equal("dry-run-validated", r.Status);} 
    [Fact] public void LocationDeleteValidated(){ var r=new ErpWmsLocationDeleteDryRun().Evaluate(new(4)); Assert.Equal("dry-run-validated", r.Status);} 
}
