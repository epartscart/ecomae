namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/users/ajax_set_user_comment.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpSetUserCommentDryRun { CpSetUserCommentDryRunResult Evaluate(CpSetUserCommentRequest request); }
public sealed class CpSetUserCommentDryRun : ICpSetUserCommentDryRun
{
    public CpSetUserCommentDryRunResult Evaluate(CpSetUserCommentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/users/ajax_set_user_comment.php remains authoritative.", request);
        if (request.UserId <= 0)
            return Refuse("dry-run-invalid","invalid_request","UserId must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.UserId, request.Comment,
            ["cp/content/users/ajax_set_user_comment.php (NOT executed)"],
            "CpSetUserComment payload validated; UPDATE blocked.",
            "cp/content/users/ajax_set_user_comment.php");
    }
    private static CpSetUserCommentDryRunResult Refuse(string s,string c,string d,CpSetUserCommentRequest r)=>
        new(s,0,true,false,true,c,false,r.UserId, r.Comment,[],d,"cp/content/users/ajax_set_user_comment.php");
}
public sealed record CpSetUserCommentRequest(long UserId, string? Comment, bool ConfirmWrites = false);
public sealed record CpSetUserCommentDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long UserId, string? Comment,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{userId=UserId,comment=Comment},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
