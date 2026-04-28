Imports System.Globalization
Imports System.Text.RegularExpressions
Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Shared
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Shared CLI-side helpers used by the per-tool argv parsers in this
    ' folder. These replicate the small token-level parsers that the
    ' Python `tools/util.py` exposed (period, Drive(), level()) plus the
    ' inline-value / next-argv-token plumbing every BuildRuntimePreview
    ' clone used to duplicate. They live in the CLI because they only
    ' make sense in the context of argv parsing - the library's runtime
    ' algorithm functions consume the resulting strongly-typed Options
    ' DTOs and never see flag names or `--xxx` syntax.
    Friend NotInheritable Class ParserHelpers

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/util.py::period
        ' Parses a duration suffix (rpm/ms/us/ns/scp) into seconds, or a
        ' bare numeric as 60/N (treated as RPM).
        Public Shared Function Period(arg As String) As Double
            Dim m = Regex.Match(arg, "^(\d*\.\d+|\d+)rpm")
            If m.Success Then
                Return 60.0 / Double.Parse(m.Groups(1).Value, CultureInfo.InvariantCulture)
            End If
            m = Regex.Match(arg, "^(\d*\.\d+|\d+)ms")
            If m.Success Then
                Return Double.Parse(m.Groups(1).Value, CultureInfo.InvariantCulture) / 1000.0
            End If
            m = Regex.Match(arg, "^(\d*\.\d+|\d+)us")
            If m.Success Then
                Return Double.Parse(m.Groups(1).Value, CultureInfo.InvariantCulture) / 1000000.0
            End If
            m = Regex.Match(arg, "^(\d*\.\d+|\d+)ns")
            If m.Success Then
                Return Double.Parse(m.Groups(1).Value, CultureInfo.InvariantCulture) / 1000000000.0
            End If
            m = Regex.Match(arg, "^(\d*\.\d+|\d+)scp")
            If m.Success Then
                Return Double.Parse(m.Groups(1).Value, CultureInfo.InvariantCulture) / 40000000.0
            End If
            Return 60.0 / Double.Parse(arg, CultureInfo.InvariantCulture)
        End Function

        ' Python map: src/greaseweazle/tools/util.py::Drive.__call__
        Public Shared Function Drive(token As String) As DriveSpec
            Dim map As New Dictionary(Of String, Tuple(Of UsbProtocol.BusType, Integer))(StringComparer.OrdinalIgnoreCase) From {
                {"A", Tuple.Create(UsbProtocol.BusType.IBMPC, 0)},
                {"B", Tuple.Create(UsbProtocol.BusType.IBMPC, 1)},
                {"0", Tuple.Create(UsbProtocol.BusType.Shugart, 0)},
                {"1", Tuple.Create(UsbProtocol.BusType.Shugart, 1)},
                {"2", Tuple.Create(UsbProtocol.BusType.Shugart, 2)},
                {"3", Tuple.Create(UsbProtocol.BusType.Shugart, 3)}
            }
            If Not map.ContainsKey(token) Then
                Throw New ArgumentException(String.Format("invalid drive letter: '{0}'", token))
            End If
            Dim mapped = map(token)
            Return New DriveSpec With {.Bus = mapped.Item1, .UnitId = mapped.Item2}
        End Function

        ' Python map: src/greaseweazle/tools/util.py::level
        Public Shared Function Level(token As String) As Boolean
            Dim map As New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase) From {
                {"H", True},
                {"L", False}
            }
            If Not map.ContainsKey(token) Then
                Throw New ArgumentException(String.Format("invalid pin level: '{0}'", token))
            End If
            Return map(token)
        End Function

        ' Python map: src/greaseweazle/tools/util.py::split_opts
        ' Splits an `image::opt1=val1:opt2=val2` spec into a (name, options-dict)
        ' pair. Used by the read/write/convert parsers so the runtime layer
        ' receives a clean filename plus a parsed options dictionary.
        Public Shared Function SplitOpts(input As String) As Tuple(Of String, Dictionary(Of String, String))
            Return OptionParser.SplitOpts(input)
        End Function

        ' Inline `--flag=value` / next-argv-token plumbing. Each old
        ' BuildRuntimePreview duplicated this; consolidating here so the
        ' per-tool parsers stay small.
        Public Shared Sub CheckOptionValue(args As IReadOnlyList(Of String), index As Integer, optionName As String)
            Dim hasValue = index < args.Count
            If hasValue Then
                Dim value = args(index)
                If value.StartsWith("-", StringComparison.Ordinal) Then
                    hasValue = False
                End If
            End If
            ErrorHandling.Check(hasValue, String.Format("missing value for option {0}", optionName))
        End Sub

        Public Shared Function TakeOptionValue(args As IReadOnlyList(Of String),
                                                ByRef index As Integer,
                                                optionName As String,
                                                inlineValue As String) As String
            If inlineValue IsNot Nothing Then
                Return inlineValue
            End If
            index += 1
            CheckOptionValue(args, index, optionName)
            Return args(index)
        End Function

        ' Walks args until either (a) `--` sentinel (stop, push remainder
        ' as positionals) or (b) end-of-args. Splits each token into
        ' (raw, key, inlineValue) so callers can dispatch on the key while
        ' still seeing the original `--key=value` source for diagnostic
        ' messages. The caller does the Select Case dispatch.
        Public Shared Function SplitTokenAtEquals(rawToken As String,
                                                   ByRef key As String,
                                                   ByRef inlineValue As String) As Boolean
            key = rawToken
            inlineValue = Nothing
            If Not rawToken.StartsWith("--", StringComparison.Ordinal) Then
                Return False
            End If
            Dim equalsIndex = rawToken.IndexOf("="c)
            If equalsIndex > 2 Then
                key = rawToken.Substring(0, equalsIndex)
                inlineValue = rawToken.Substring(equalsIndex + 1)
                Return True
            End If
            Return False
        End Function

        ' Convenience for parsers that reject inline values on flag-only
        ' options (e.g. `--test` shouldn't accept `--test=foo`). Mirrors
        ' Python argparse's "ignored explicit argument" error.
        Public Shared Sub RejectInlineValue(token As String, inlineValue As String)
            If inlineValue IsNot Nothing Then
                Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
            End If
        End Sub

        ' Default diskdefs.xml path resolution shared by every parser that
        ' accepts `--diskdefs`. Matching Python `tools/util.py`'s default.
        Public Shared Function ResolveDiskDefsPath(diskDefsPath As String) As String
            If Not String.IsNullOrEmpty(diskDefsPath) Then
                Return diskDefsPath
            End If
            Return "diskdefs.xml"
        End Function

        ' Resolve a diskdef name to its DiskDef record, throwing the
        ' typed UnknownFormatException (no catalogue) if the name isn't
        ' registered. The catalogue-bearing variant lives in
        ' ValidateFormatIfSpecified below.
        Public Shared Function ResolveDiskDefinition(formatName As String, diskDefsPath As String) As DiskDef
            Dim path = ResolveDiskDefsPath(diskDefsPath)
            Dim disk = DiskDefParser.GetDiskdef(formatName, path)
            If disk Is Nothing Then
                Throw New UnknownFormatException(formatName)
            End If
            Return disk
        End Function

        ' Catalogue-bearing format validator used by align/read/write/convert
        ' parsers when they want the FATAL-ERROR-banner-with-known-formats
        ' rendering on a typo. Throws UnknownFormatException carrying the
        ' enumerated format list; the CLI's FatalErrorFormatter then
        ' columnifies it. Returns silently when the format is registered
        ' (or empty/null).
        Public Shared Sub ValidateFormatIfSpecified(format As String,
                                                    knownFormats As IEnumerable(Of String),
                                                    diskDefsPath As String)
            If String.IsNullOrEmpty(format) Then
                Return
            End If
            Dim resolvedDiskDefsPath = ResolveDiskDefsPath(diskDefsPath)
            Try
                Dim parsed = DiskDefParser.GetDiskdef(format, resolvedDiskDefsPath)
                If parsed IsNot Nothing Then
                    Return
                End If
            Catch
                ' Fall through and emit the catalogue.
            End Try

            Dim formats As List(Of String)
            Try
                formats = DiskDefParser.GetAllFormats(resolvedDiskDefsPath)
            Catch
                formats = knownFormats.OrderBy(Function(x) x).ToList()
            End Try

            Throw New UnknownFormatException(format, formats)
        End Sub

    End Class

End Namespace
