using System.Security.Cryptography;
using System.Text;

namespace Core.DemoSeeder;

/// <summary>Deterministic demo identifiers so re-runs stay stable and referential.</summary>
internal static class DemoIds
{
    public static Guid User(string key) => Create($"user:{key}");
    public static Guid Company(string key) => Create($"company:{key}");
    public static Guid Case(string key) => Create($"case:{key}");
    public static Guid Fund(string key) => Create($"fund:{key}");
    public static Guid Credit(string key) => Create($"credit:{key}");
    public static Guid Process(string key) => Create($"process:{key}");
    public static Guid Snapshot(string key) => Create($"snapshot:{key}");

    private static Guid Create(string key)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"rtf-demo-v1|{key}"));
        return new Guid(bytes);
    }
}
