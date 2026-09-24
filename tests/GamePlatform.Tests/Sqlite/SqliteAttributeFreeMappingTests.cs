using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using GamePlatform.Storage.Sqlite.Executor;

namespace GamePlatform.Tests.Sqlite
{
    public sealed class SqliteAttributeFreeMappingTests
    {
        private const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static readonly string[] RowMaterializers = { "Query", "DeferredQuery", "FindWithQuery", "Get", "Find", "Table" };
        private static readonly Assembly StorageAssembly = typeof(SqliteDatabase).Assembly;

        [Fact]
        public void SdkRowTypesDoNotDependOnSqliteNetMappingAttributes()
        {
            var offenders = StorageAssembly.GetTypes()
                .Where(type => type.Namespace != "SQLite")
                .SelectMany(type => type.GetCustomAttributesData().Select(attribute => (Member: type.ToString(), attribute))
                    .Concat(type.GetMembers(AllMembers).SelectMany(member => member.GetCustomAttributesData().Select(attribute => (Member: type + "." + member.Name, attribute)))))
                .Where(entry => entry.attribute.AttributeType.Namespace == "SQLite")
                .Select(entry => entry.Member + " [" + entry.attribute.AttributeType.Name + "]")
                .ToArray();

            Assert.Empty(offenders);
        }

        [Fact]
        public void EveryMaterializedRowTypeIsFullyPreservedByTheUnityLinkXml()
        {
            var rowTypes = FindMaterializedRowTypes();
            var preserved = XDocument.Load(FindLinkXml()).Descendants("assembly")
                .Where(assembly => (string?)assembly.Attribute("fullname") == StorageAssembly.GetName().Name)
                .Descendants("type")
                .Where(type => (string?)type.Attribute("preserve") == "all")
                .Select(type => (string?)type.Attribute("fullname"))
                .ToHashSet(StringComparer.Ordinal);

            Assert.Contains("GamePlatform.Storage.Sqlite.Sync.SqlitePrivateSyncStore+StagedProjectionRow", rowTypes);
            var unpreserved = rowTypes.Where(name => !preserved.Contains(name)).ToArray();
            Assert.Empty(unpreserved);
        }

        private static SortedSet<string> FindMaterializedRowTypes()
        {
            var rowTypes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var type in StorageAssembly.GetTypes().Where(type => type.Namespace != "SQLite"))
            {
                foreach (var method in type.GetMethods(AllMembers).Cast<MethodBase>().Concat(type.GetConstructors(AllMembers)))
                {
                    var il = method.GetMethodBody()?.GetILAsByteArray();
                    if (il == null) continue;
                    for (var i = 0; i + 4 < il.Length; i++)
                    {
                        if (il[i] != 0x28 && il[i] != 0x6F) continue;
                        var called = TryResolve(method, BitConverter.ToInt32(il, i + 1));
                        if (called == null || !called.IsGenericMethod || !RowMaterializers.Contains(called.Name) || !IsSqliteNetEntryPoint(called.DeclaringType)) continue;
                        var rowType = called.GetGenericArguments()[0];
                        if (!rowType.IsGenericParameter) rowTypes.Add(rowType.FullName!);
                    }
                }
            }

            return rowTypes;
        }

        private static bool IsSqliteNetEntryPoint(Type? type) => type != null && (type.Namespace == "SQLite" || type == typeof(SqliteTransactionSession));

        private static MethodBase? TryResolve(MethodBase caller, int token)
        {
            try
            {
                return caller.Module.ResolveMethod(token,
                    caller.DeclaringType!.IsGenericType ? caller.DeclaringType.GetGenericArguments() : null,
                    caller.IsGenericMethod ? caller.GetGenericArguments() : null);
            }
            catch (Exception error) when (error is ArgumentException || error is BadImageFormatException)
            {
                return null;
            }
        }

        private static string FindLinkXml()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "integration", "unity", "package", "link.xml");
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException("integration/unity/package/link.xml was not found above the test output.");
        }
    }
}
