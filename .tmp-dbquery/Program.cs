using Npgsql;
var env = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env"));
var connString = env.First(x => x.StartsWith("ConnectionStrings__ScanNowDB="))[(env.First(x => x.StartsWith("ConnectionStrings__ScanNowDB=")).IndexOf('=') + 1)..];
await using var conn = new NpgsqlConnection(connString);
await conn.OpenAsync();
await using var cmd = new NpgsqlCommand("""
select bs."UserId", bs."BranchId", b."Name"
from "BranchStaff" bs join "Branches" b on b."Id" = bs."BranchId"
where bs."UserId" = '019e8368-abb0-7f77-a76c-90cda31c209e'
""", conn);
await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync()) Console.WriteLine($"{reader[0]} | {reader[1]} | {reader[2]}");
