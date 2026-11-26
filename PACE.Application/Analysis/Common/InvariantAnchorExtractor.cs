using System.Text.RegularExpressions;
using PACE.Application.Analysis.Models;

namespace PACE.Application.Analysis.Common;

public static partial class InvariantAnchorExtractor
{
    private static readonly HashSet<string> DbWriteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Add", "AddAsync", "AddRange", "AddRangeAsync",
        "Update", "UpdateAsync", "UpdateRange", "UpdateRangeAsync",
        "Remove", "RemoveAsync", "RemoveRange", "RemoveRangeAsync",
        "Delete", "DeleteAsync", "DeleteById", "DeleteByIdAsync", "DeleteAll", "DeleteMany", "DeleteOne",
        "Insert", "InsertAsync", "InsertOne", "InsertOneAsync", "InsertMany", "InsertManyAsync",
        "Save", "SaveAsync", "SaveAll", "SaveAndFlush", "SaveChanges", "SaveChangesAsync",
        "Create", "CreateAsync", "CreateMany", "Upsert", "UpsertAsync",
        "ExecuteSqlRaw", "ExecuteSqlRawAsync", "ExecuteSqlInterpolated", "ExecuteSqlInterpolatedAsync",
        "ExecuteAsync", "Execute", "BulkInsert", "BulkUpdate", "BulkDelete",
        "FindOneAndUpdate", "FindOneAndDelete", "FindByIdAndUpdate", "FindByIdAndDelete", "UpdateOne", "UpdateMany",
        "Destroy"
    };

    private static readonly HashSet<string> DbReadMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Find", "FindAsync", "FindById", "FindByIdAsync", "FindOne", "FindOneAsync", "FindAll", "FindMany", "FindFirst", "FindUnique",
        "FirstOrDefault", "FirstOrDefaultAsync", "First", "FirstAsync",
        "SingleOrDefault", "SingleOrDefaultAsync", "Single", "SingleAsync",
        "ToList", "ToListAsync", "ToArray", "ToArrayAsync",
        "Any", "AnyAsync", "Count", "CountAsync", "LongCount", "LongCountAsync",
        "GetById", "GetByIdAsync", "GetAll", "GetAllAsync", "GetOne",
        "Query", "QueryAsync", "QueryFirst", "QueryFirstOrDefault", "QuerySingle", "QuerySingleOrDefault",
        "FromSqlRaw", "FromSqlInterpolated", "Select", "Where"
    };

    private static readonly string[] SdkPrefixes =
    [
        "JwtSecurityTokenHandler", "Jwt", "JsonWebToken", "Jwts", "TokenHandler",
        "BCrypt", "Argon2", "PBKDF2", "PasswordHasher", "PasswordEncoder", "HashVerifier", "Crypto", "SHA256", "HMAC", " Aes",
        "SignInManager", "UserManager", "RoleManager", "AuthenticationManager",
        "Stripe", "PayPal", "VnPay", "Momo", "ZaloPay",
        "SmtpClient", "MailKit", "SendGrid", "EmailService", "JavaMailSender", "Nodemailer",
        "S3Client", "BlobServiceClient", "Cloudinary", "Firebase", "Supabase",
        "Redis", "IDistributedCache", "IMemoryCache", "Upstash"
    ];

    private static readonly HashSet<string> HttpOutReceivers = new(StringComparer.OrdinalIgnoreCase)
    {
        "HttpClient", "RestTemplate", "WebClient", "RestClient", "FeignClient", "axios", "fetch", "got", "superagent"
    };

    private static readonly HashSet<string> EventPubReceivers = new(StringComparer.OrdinalIgnoreCase)
    {
        "IPublishEndpoint", "IBus", "KafkaTemplate", "RabbitTemplate", "AmqpTemplate", "EventEmitter", "EventBus", "SNS", "SQS"
    };

    private static readonly Dictionary<string, string> HttpStatusMethodMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ok"] = "200",
        ["Created"] = "201",
        ["CreatedAtAction"] = "201",
        ["CreatedAtRoute"] = "201",
        ["Accepted"] = "202",
        ["NoContent"] = "204",
        ["BadRequest"] = "400",
        ["Unauthorized"] = "401",
        ["Forbid"] = "403",
        ["NotFound"] = "404",
        ["Conflict"] = "409",
        ["UnprocessableEntity"] = "422",
        ["StatusCode"] = "HTTP_STATUS"
    };

    [GeneratedRegex(@"\b(INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM)\s+([a-zA-Z0-9_`""\[\]\.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SqlWriteRegex();

    [GeneratedRegex(@"\bSELECT\s+.+?\s+FROM\s+([a-zA-Z0-9_`""\[\]\.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SqlReadRegex();

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]{2,35}$")]
    private static partial Regex StatusConstantRegex();

    [GeneratedRegex(@"\b(100|200|201|202|204|301|302|400|401|403|404|409|422|429|500|502|503)\b")]
    private static partial Regex HttpCodeRegex();

    public static ParsedSink? ClassifyCall(
        string? receiverType,
        string? receiverExpr,
        string methodName,
        IReadOnlyList<string> arguments,
        string? condition)
    {
        var recType = ParserText.SimpleTypeName(receiverType);
        var recExpr = (receiverExpr ?? string.Empty).Trim();
        var combinedReceiver = !string.IsNullOrEmpty(recType) ? recType : recExpr;

        // 1. Check raw SQL inside arguments
        foreach (var arg in arguments)
        {
            var writeMatch = SqlWriteRegex().Match(arg);
            if (writeMatch.Success)
            {
                var table = CleanIdentifier(writeMatch.Groups[2].Value);
                return new ParsedSink(SinkKind.DbWrite, table, $"{methodName}({ParserText.OneLine(arg, 60)})", condition);
            }

            var readMatch = SqlReadRegex().Match(arg);
            if (readMatch.Success)
            {
                var table = CleanIdentifier(readMatch.Groups[1].Value);
                return new ParsedSink(SinkKind.DbRead, table, $"{methodName}({ParserText.OneLine(arg, 60)})", condition);
            }
        }

        // 2. Check SDK Calls
        if (IsSdkReceiver(recType, recExpr, methodName, out var sdkTarget))
        {
            return new ParsedSink(SinkKind.SdkCall, sdkTarget, ExtractColumnsOrArgs(arguments), condition);
        }

        // 3. Check HTTP Outbound
        if (IsHttpOut(recType, recExpr, methodName))
        {
            var target = string.IsNullOrEmpty(combinedReceiver) ? methodName : $"{combinedReceiver}.{methodName}";
            return new ParsedSink(SinkKind.HttpOut, target, ExtractColumnsOrArgs(arguments), condition);
        }

        // 4. Check Event Publish
        if (IsEventPub(recType, recExpr, methodName))
        {
            var target = string.IsNullOrEmpty(combinedReceiver) ? methodName : $"{combinedReceiver}.{methodName}";
            return new ParsedSink(SinkKind.EventPub, target, ExtractColumnsOrArgs(arguments), condition);
        }

        // 5. Check ORM / Repository DB Write or Read
        if (LooksLikeDataTarget(recType, recExpr, methodName))
        {
            var table = ExtractEntityOrTableName(recType, recExpr);
            if (IsDbWriteMethod(methodName))
            {
                var cols = ExtractColumnsOrArgs(arguments);
                var target = string.IsNullOrEmpty(cols) ? table : $"{table}({cols})";
                return new ParsedSink(SinkKind.DbWrite, target, $"{combinedReceiver}.{methodName}", condition);
            }

            if (IsDbReadMethod(methodName))
            {
                var cols = ExtractColumnsOrArgs(arguments);
                var target = string.IsNullOrEmpty(cols) ? table : $"{table}({cols})";
                return new ParsedSink(SinkKind.DbRead, target, $"{combinedReceiver}.{methodName}", condition);
            }
        }

        return null;
    }

    public static void CollectInvariantLiteral(string? literalValue, List<string> target)
    {
        if (string.IsNullOrWhiteSpace(literalValue)) return;
        var val = literalValue.Trim();
        if (StatusConstantRegex().IsMatch(val) || val.StartsWith("ROLE_", StringComparison.OrdinalIgnoreCase))
        {
            if (!target.Contains(val)) target.Add(val);
        }
    }

    public static void CollectHttpStatusFromCall(string methodName, IReadOnlyList<string> args, List<string> target)
    {
        if (HttpStatusMethodMap.TryGetValue(methodName, out var code))
        {
            var item = code == "HTTP_STATUS" && args.Count > 0 ? $"HTTP_{ParserText.OneLine(args[0], 20)}" : $"HTTP_{code}";
            if (!target.Contains(item)) target.Add(item);
            return;
        }

        if (methodName is "status" or "sendStatus" or "Status" && args.Count > 0)
        {
            var m = HttpCodeRegex().Match(args[0]);
            if (m.Success)
            {
                var item = $"HTTP_{m.Value}";
                if (!target.Contains(item)) target.Add(item);
            }
        }
    }

    private static bool IsSdkReceiver(string recType, string recExpr, string methodName, out string sdkTarget)
    {
        sdkTarget = string.Empty;
        foreach (var prefix in SdkPrefixes)
        {
            var p = prefix.Trim();
            if (recType.Contains(p, StringComparison.OrdinalIgnoreCase) || recExpr.Contains(p, StringComparison.OrdinalIgnoreCase))
            {
                var host = !string.IsNullOrEmpty(recType) ? recType : recExpr;
                sdkTarget = $"{host}.{methodName}";
                return true;
            }
        }

        if (methodName is "HashPassword" or "VerifyHashedPassword" or "VerifyPassword" or "CreateToken" or "WriteToken" or "ValidateToken" or "GenerateJwt" or "SignJwt")
        {
            var host = !string.IsNullOrEmpty(recType) ? recType : (!string.IsNullOrEmpty(recExpr) ? recExpr : "AuthSdk");
            sdkTarget = $"{host}.{methodName}";
            return true;
        }

        return false;
    }

    private static bool IsHttpOut(string recType, string recExpr, string methodName)
    {
        if (HttpOutReceivers.Contains(recType) || HttpOutReceivers.Contains(recExpr)) return true;
        if (methodName.Equals("fetch", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(recType)) return true;
        return false;
    }

    private static bool IsEventPub(string recType, string recExpr, string methodName)
    {
        if (EventPubReceivers.Contains(recType) || EventPubReceivers.Contains(recExpr)) return true;
        return methodName is "PublishEvent" or "EmitEvent" or "SendMessageAsync";
    }

    private static bool LooksLikeDataTarget(string recType, string recExpr, string methodName)
    {
        if (DbWriteMethods.Contains(methodName) || DbReadMethods.Contains(methodName))
        {
            // Check if receiver looks like DbSet, Repository, Dao, Context, Prisma, Mongoose model, Knex, etc.
            var combined = $"{recType} {recExpr}";
            if (combined.Contains("DbSet", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Repo", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Dao", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Context", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Db", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Store", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Model", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("prisma", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("uow", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("UnitOfWork", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Table", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Collection", StringComparison.OrdinalIgnoreCase)
                || combined.Contains('.'))
            {
                return true;
            }
        }

        // Spring Data JPA derived queries: findByEmail, existsByUsername, deleteByUserId, countByStatus
        if ((recType.Contains("Repo", StringComparison.OrdinalIgnoreCase) || recExpr.Contains("Repo", StringComparison.OrdinalIgnoreCase)
             || recType.Contains("Dao", StringComparison.OrdinalIgnoreCase) || recExpr.Contains("Dao", StringComparison.OrdinalIgnoreCase))
            && (methodName.StartsWith("findBy", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("existsBy", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("countBy", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("deleteBy", StringComparison.OrdinalIgnoreCase)
                || methodName.StartsWith("removeBy", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool IsDbWriteMethod(string methodName)
        => DbWriteMethods.Contains(methodName)
           || methodName.StartsWith("Insert", StringComparison.OrdinalIgnoreCase)
           || methodName.StartsWith("Update", StringComparison.OrdinalIgnoreCase)
           || methodName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)
           || methodName.StartsWith("Remove", StringComparison.OrdinalIgnoreCase)
           || methodName.StartsWith("Create", StringComparison.OrdinalIgnoreCase)
           || methodName.StartsWith("Save", StringComparison.OrdinalIgnoreCase);

    private static bool IsDbReadMethod(string methodName)
        => DbReadMethods.Contains(methodName)
           || methodName.StartsWith("Find", StringComparison.OrdinalIgnoreCase)
           || methodName.StartsWith("Get", StringComparison.OrdinalIgnoreCase)
           || methodName.StartsWith("ExistsBy", StringComparison.OrdinalIgnoreCase)
           || methodName.StartsWith("CountBy", StringComparison.OrdinalIgnoreCase)
           || methodName.StartsWith("Query", StringComparison.OrdinalIgnoreCase);

    private static string ExtractEntityOrTableName(string recType, string recExpr)
    {
        // E.g. _context.Users -> Users, prisma.refreshToken -> refreshToken, IUserRepository -> User
        if (!string.IsNullOrEmpty(recExpr))
        {
            var parts = recExpr.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var last = parts.LastOrDefault()?.Trim('_') ?? string.Empty;
            foreach (var suffix in new[] { "Repository", "Repo", "Dao", "DbSet", "Table", "Collection" })
            {
                if (last.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && last.Length > suffix.Length)
                    last = last[..^suffix.Length];
            }
            if (!string.IsNullOrEmpty(last) && !last.Equals("context", StringComparison.OrdinalIgnoreCase)
                && !last.Equals("db", StringComparison.OrdinalIgnoreCase)
                && !last.Equals("uow", StringComparison.OrdinalIgnoreCase)
                && !last.Equals("prisma", StringComparison.OrdinalIgnoreCase)
                && !last.Equals("this", StringComparison.OrdinalIgnoreCase))
            {
                return ToSnakeCase(last);
            }
        }

        if (!string.IsNullOrEmpty(recType))
        {
            var clean = recType.TrimStart('I');
            foreach (var suffix in new[] { "Repository", "Repo", "Dao", "DbSet", "Context", "DbContext" })
            {
                if (clean.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && clean.Length > suffix.Length)
                    clean = clean[..^suffix.Length];
            }
            if (!string.IsNullOrEmpty(clean)) return ToSnakeCase(clean);
        }

        return "database";
    }

    [GeneratedRegex(@"\.([a-zA-Z_][a-zA-Z0-9_]*)\b")]
    private static partial Regex MemberPropRegex();

    private static string? ExtractColumnsOrArgs(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0) return null;
        var props = new List<string>();
        foreach (var arg in arguments.Take(4))
        {
            foreach (Match m in MemberPropRegex().Matches(arg))
            {
                var prop = m.Groups[1].Value;
                if (prop is "Length" or "Count" or "Value" or "HasValue" or "ToString" or "Trim" or "ToLower" or "ToUpper" or "Id" && props.Count > 4)
                    continue;
                var snake = ToSnakeCase(prop);
                if (!props.Contains(snake)) props.Add(snake);
            }
        }

        if (props.Count > 0) return string.Join(", ", props.Take(5));
        return null;
    }

    private static string CleanIdentifier(string raw)
        => raw.Trim('`', '"', '[', ']', '\'');

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(input[i - 1]) || char.IsDigit(input[i - 1])))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
