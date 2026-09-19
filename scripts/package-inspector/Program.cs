using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

// Read PE metadata without loading or executing the inspected assemblies.
var entries = new List<object>();
foreach (var path in args.OrderBy(x => x, StringComparer.Ordinal))
{
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    if (!pe.HasMetadata) throw new InvalidDataException("managed_metadata_missing");
    var reader = pe.GetMetadataReader();
    var assembly = reader.GetAssemblyDefinition();
    string? target = null;
    foreach (var handle in assembly.GetCustomAttributes())
    {
        var attribute = reader.GetCustomAttribute(handle);
        if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
        var member = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
        if (member.Parent.Kind != HandleKind.TypeReference) continue;
        var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
        if (reader.GetString(type.Namespace) != "System.Runtime.Versioning" || reader.GetString(type.Name) != "TargetFrameworkAttribute") continue;
        var blob = reader.GetBlobReader(attribute.Value);
        if (blob.ReadUInt16() != 1) throw new InvalidDataException("target_attribute_invalid");
        target = blob.ReadSerializedString();
    }
    var references = reader.AssemblyReferences.Select(handle => {
        var reference = reader.GetAssemblyReference(handle);
        return new { name = reader.GetString(reference.Name), version = reference.Version.ToString() };
    }).OrderBy(x => x.name, StringComparer.Ordinal).ToArray();
    var publicTypes = reader.TypeDefinitions.Select(handle => reader.GetTypeDefinition(handle))
        .Where(type => (type.Attributes & System.Reflection.TypeAttributes.VisibilityMask) is System.Reflection.TypeAttributes.Public or System.Reflection.TypeAttributes.NestedPublic)
        .Select(type => reader.GetString(type.Namespace) + "." + reader.GetString(type.Name))
        .OrderBy(x => x, StringComparer.Ordinal).ToArray();
    entries.Add(new { file = Path.GetFileName(path), name = reader.GetString(assembly.Name), version = assembly.Version.ToString(), targetFramework = target,
        references, publicTypes, moduleVersionId = reader.GetGuid(reader.GetModuleDefinition().Mvid).ToString() });
}
Console.WriteLine(JsonSerializer.Serialize(entries));
