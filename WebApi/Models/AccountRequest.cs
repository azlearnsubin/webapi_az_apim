namespace WebApi.Models;

public class AccountRequest
{
    public bool OnHold { get; set; }
    public string? _Address { get; set; }
    public string? _CRNumber { get; set; }
    public string? _Fax { get; set; }
    public string? _GUID { get; set; }
    public string? _NameAR { get; set; }
    public string? _NameEN { get; set; }
    public string? _Phone1 { get; set; }
    public string? _Phone2 { get; set; }
    public string? _PostalCode { get; set; }
    public string? _email { get; set; }
}