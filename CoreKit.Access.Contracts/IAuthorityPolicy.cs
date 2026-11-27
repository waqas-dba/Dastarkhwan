namespace CoreKit.Access.Contracts;

public interface IAuthorityPolicy
{
    bool RequiresFreshAuthority(string permission);
}

public interface IAuthorityPolicyContributor
{
    IEnumerable<string> GetFreshAuthorityPermissions();
}