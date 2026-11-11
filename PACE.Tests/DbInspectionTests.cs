using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PACE.Domain.Entities;
using Xunit;
using Xunit.Abstractions;

namespace PACE.Tests;

public class DbInspectionTests
{
    private readonly ITestOutputHelper _output;

    public DbInspectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DropRubricTables()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connStr = config.GetConnectionString("DefaultConnection") 
                      ?? "Host=ep-rapid-silence-azrlmoxx-pooler.c-3.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_rF8HIqysK0RA;SSL Mode=Require;Channel Binding=Require";

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        await using var dropCmd = new NpgsqlCommand(@"
            DROP TABLE IF EXISTS rubric_scores CASCADE;
            DROP TABLE IF EXISTS rubric_criteria CASCADE;", conn);
        await dropCmd.ExecuteNonQueryAsync();

        _output.WriteLine("Dropped rubric_scores and rubric_criteria tables successfully!");

        // Verify they are gone
        await using var checkCmd = new NpgsqlCommand(@"
            SELECT table_name FROM information_schema.tables 
            WHERE table_schema = 'public' AND table_name IN ('rubric_criteria', 'rubric_scores');", conn);
        await using (var reader = await checkCmd.ExecuteReaderAsync())
        {
            var remaining = new List<string>();
            while (await reader.ReadAsync())
            {
                remaining.Add(reader.GetString(0));
            }
            Assert.Empty(remaining);
        }

        await using var migCmd = new NpgsqlCommand(@"
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20261111125115_DropRubricTables', '9.0.4')
            ON CONFLICT (""MigrationId"") DO NOTHING;", conn);
        await migCmd.ExecuteNonQueryAsync();

        _output.WriteLine("Remaining rubric tables in DB: 0. Migration recorded successfully!");
    }
}
