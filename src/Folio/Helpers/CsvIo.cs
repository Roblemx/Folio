using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Folio.Models;
using Folio.Services.Persistence;

namespace Folio.Helpers;

/// <summary>CSV/JSON import &amp; export for transactions and the workspace.</summary>
public static class CsvIo
{
    private const string Header = "Id,CoinId,Type,Amount,PricePerCoin,Fee,Timestamp,Note";

    public static string ExportTransactions(IEnumerable<Transaction> transactions)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        foreach (var t in transactions.OrderBy(t => t.Timestamp))
        {
            sb.AppendLine(string.Join(",",
                Q(t.Id),
                Q(t.CoinId),
                Q(t.Type.ToString()),
                t.Amount.ToString(CultureInfo.InvariantCulture),
                t.PricePerCoin.ToString(CultureInfo.InvariantCulture),
                t.Fee.ToString(CultureInfo.InvariantCulture),
                Q(t.Timestamp.ToString("o", CultureInfo.InvariantCulture)),
                Q(t.Note ?? string.Empty)));
        }

        return sb.ToString();
    }

    public static List<Transaction> ImportTransactions(string csv)
    {
        var result = new List<Transaction>();
        var lines = csv.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var start = lines.Length > 0 && lines[0].StartsWith("Id", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        for (var i = start; i < lines.Length; i++)
        {
            var f = ParseLine(lines[i]);
            if (f.Count < 7)
            {
                continue;
            }

            if (!decimal.TryParse(f[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                continue;
            }

            decimal.TryParse(f[4], NumberStyles.Number, CultureInfo.InvariantCulture, out var price);
            decimal.TryParse(f[5], NumberStyles.Number, CultureInfo.InvariantCulture, out var fee);
            var type = Enum.TryParse<TransactionType>(f[2], true, out var tt) ? tt : TransactionType.Buy;
            var ts = DateTimeOffset.TryParse(f[6], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
                ? dt
                : DateTimeOffset.Now;
            var id = string.IsNullOrWhiteSpace(f[0]) ? Guid.NewGuid().ToString("N") : f[0];
            var note = f.Count > 7 ? f[7] : null;

            result.Add(new Transaction(id, f[1], type, amount, price, fee, ts,
                string.IsNullOrEmpty(note) ? null : note));
        }

        return result;
    }

    public static string ExportWorkspaceJson(Workspace workspace) =>
        JsonSerializer.Serialize(StorageMapper.ToStored(workspace), new JsonSerializerOptions { WriteIndented = true });

    private static string Q(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }
}
