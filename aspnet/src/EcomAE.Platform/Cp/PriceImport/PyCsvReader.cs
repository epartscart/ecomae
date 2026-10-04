using System.Text;

namespace EcomAE.Platform.Cp.PriceImport;

/// <summary>
/// Python <c>csv.reader(file, delimiter=d)</c> (excel dialect, <c>strict=False</c>) over a file opened with
/// universal newlines, as pyprices <c>handle_text_file</c> reads TXT/CSV price files: <c>"</c> quoting with
/// doubled quotes, newlines kept inside quoted fields, a quote inside an unquoted field is literal, text after a
/// closing quote is appended, and a blank line yields an empty record.
/// </summary>
public static class PyCsvReader
{
    private enum State
    {
        StartRecord,
        StartField,
        InField,
        InQuotedField,
        QuoteInQuotedField,
    }

    public static IEnumerable<List<string>> Read(TextReader reader, char delimiter)
    {
        var state = State.StartRecord;
        var record = new List<string>();
        var field = new StringBuilder();
        var buffer = new char[8192];
        var pendingCr = false;

        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var c = buffer[i];
                if (pendingCr)
                {
                    pendingCr = false;
                    if (c == '\n')
                    {
                        continue;
                    }
                }

                if (c == '\r')
                {
                    pendingCr = true;
                    c = '\n';
                }

                if (Step(c, delimiter, ref state, record, field))
                {
                    yield return record;
                    record = new List<string>();
                }
            }
        }

        if (state != State.StartRecord)
        {
            if (state is not State.StartField || record.Count > 0)
            {
                record.Add(field.ToString());
            }

            yield return record;
        }
    }

    public static IEnumerable<List<string>> Read(string text, char delimiter)
        => Read(new StringReader(text), delimiter);

    /// <returns><c>true</c> when <paramref name="record"/> is complete.</returns>
    private static bool Step(char c, char delimiter, ref State state, List<string> record, StringBuilder field)
    {
        switch (state)
        {
            case State.StartRecord:
                if (c == '\n')
                {
                    return true;
                }

                state = State.StartField;
                goto case State.StartField;

            case State.StartField:
                if (c == '\n')
                {
                    SaveField(record, field);
                    state = State.StartRecord;
                    return true;
                }

                if (c == '"')
                {
                    state = State.InQuotedField;
                }
                else if (c == delimiter)
                {
                    SaveField(record, field);
                }
                else
                {
                    field.Append(c);
                    state = State.InField;
                }

                return false;

            case State.InField:
                if (c == '\n')
                {
                    SaveField(record, field);
                    state = State.StartRecord;
                    return true;
                }

                if (c == delimiter)
                {
                    SaveField(record, field);
                    state = State.StartField;
                }
                else
                {
                    field.Append(c);
                }

                return false;

            case State.InQuotedField:
                if (c == '"')
                {
                    state = State.QuoteInQuotedField;
                }
                else
                {
                    field.Append(c);
                }

                return false;

            case State.QuoteInQuotedField:
                if (c == '"')
                {
                    field.Append('"');
                    state = State.InQuotedField;
                    return false;
                }

                if (c == delimiter)
                {
                    SaveField(record, field);
                    state = State.StartField;
                    return false;
                }

                if (c == '\n')
                {
                    SaveField(record, field);
                    state = State.StartRecord;
                    return true;
                }

                field.Append(c);
                state = State.InField;
                return false;
        }

        return false;
    }

    private static void SaveField(List<string> record, StringBuilder field)
    {
        record.Add(field.ToString());
        field.Clear();
    }
}
