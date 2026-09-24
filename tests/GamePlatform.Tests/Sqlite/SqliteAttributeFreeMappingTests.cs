using System.Linq;
using System.Reflection;
using GamePlatform.Storage.Sqlite.Executor;

namespace GamePlatform.Tests.Sqlite
{
    public sealed class SqliteAttributeFreeMappingTests
    {
        private const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [Fact]
        public void SdkRowTypesDoNotDependOnSqliteNetMappingAttributes()
        {
            var assembly = typeof(SqliteDatabase).Assembly;
            var offenders = assembly.GetTypes()
                .Where(type => type.Namespace != "SQLite")
                .SelectMany(type => type.GetCustomAttributesData().Select(attribute => (Member: type.ToString(), attribute))
                    .Concat(type.GetMembers(AllMembers).SelectMany(member => member.GetCustomAttributesData().Select(attribute => (Member: type + "." + member.Name, attribute)))))
                .Where(entry => entry.attribute.AttributeType.Namespace == "SQLite")
                .Select(entry => entry.Member + " [" + entry.attribute.AttributeType.Name + "]")
                .ToArray();

            Assert.Empty(offenders);
        }
    }
}
