using System.Text.Json;
using System;
using System.IO;
using SQLite;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: DesktopProbe <database-path>");
    return 2;
}

var databasePath = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
if (File.Exists(databasePath))
{
    Console.Error.WriteLine($"Refusing to overwrite existing database: {databasePath}");
    return 2;
}

const long expectedSequence = 9_007_199_254_740_993L;
const string expectedPayload = "committed-after-rollback";

using (var connection = new SQLiteConnection(databasePath))
{
    connection.Execute("CREATE TABLE probe_state (id INTEGER PRIMARY KEY, sequence INTEGER NOT NULL, payload TEXT NOT NULL)");

    connection.BeginTransaction();
    connection.Execute(
        "INSERT INTO probe_state (id, sequence, payload) VALUES (?, ?, ?)",
        1,
        expectedSequence - 1,
        "must-not-survive");
    connection.Rollback();

    connection.BeginTransaction();
    connection.Execute(
        "INSERT INTO probe_state (id, sequence, payload) VALUES (?, ?, ?)",
        1,
        expectedSequence,
        expectedPayload);
    connection.Commit();
}

long actualSequence;
string actualPayload;
int rowCount;
int sqliteVersionNumber;
using (var reopened = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadOnly))
{
    rowCount = reopened.ExecuteScalar<int>("SELECT COUNT(*) FROM probe_state");
    actualSequence = reopened.ExecuteScalar<long>("SELECT sequence FROM probe_state WHERE id = 1");
    actualPayload = reopened.ExecuteScalar<string>("SELECT payload FROM probe_state WHERE id = 1");
    sqliteVersionNumber = reopened.LibVersionNumber;
}

if (rowCount != 1 || actualSequence != expectedSequence || actualPayload != expectedPayload)
{
    Console.Error.WriteLine("Transaction/reopen verification failed.");
    return 1;
}

Console.WriteLine(JsonSerializer.Serialize(new
{
    result = "passed",
    databasePath,
    rowCount,
    sequence = actualSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
    payload = actualPayload,
    sqliteVersionNumber,
    nativeLibrary = SQLite3.LibraryPath,
    processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription
}));
return 0;
