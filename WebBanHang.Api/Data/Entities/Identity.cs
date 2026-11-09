using System.Text.Json.Serialization;

namespace WebBanHang.Api.Data.Entities;

/// <summary>dbo.Logons — back-office (admin/staff) accounts.</summary>
public class Logon : AuditEntity
{
    public string Username { get; set; } = "";
    [JsonIgnore] public string Password { get; set; } = "";
    [JsonIgnore] public string? ActivationCode { get; set; }
    public int? IsActived { get; set; }
    public int? IsSeo { get; set; }
}

/// <summary>dbo.Assigns — Logons ↔ Groups (1 = Admin, 2 = Nhân viên).</summary>
public class Assign : CmsEntity
{
    public int LogonsId { get; set; }
    public int GroupsId { get; set; }
}

/// <summary>dbo.Groups</summary>
public class Group : CmsEntity
{
    public string? GroupName { get; set; }
    public string? Description { get; set; }
}

/// <summary>dbo.Members — profile of a back-office account.</summary>
public class Member : CmsEntity
{
    public int LogonsId { get; set; }
    public string? Fullname { get; set; }
    public int? Sex { get; set; }
    public DateTime? Birthday { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public int? IsStatus { get; set; }
    public int? IsStaff { get; set; }
}

/// <summary>dbo.Account — storefront customer accounts.</summary>
public class Account : AuditEntity
{
    public string Username { get; set; } = "";
    [JsonIgnore] public string Password { get; set; } = "";
    [JsonIgnore] public string? ActivationCode { get; set; }
    public int? IsActived { get; set; }
    public string? Name { get; set; }
    public string Email { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Cmnd { get; set; }
}
