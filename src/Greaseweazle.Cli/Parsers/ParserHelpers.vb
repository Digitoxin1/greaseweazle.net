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

        ' Action -> argparse `usage:` banner. Matches gw.exe's per-action
        ' usage line one-for-one (verified by running each action with no
        ' args / `--nope` against H:\gw\gw.exe). The two-line stderr block
        '   usage: gw-vb <action> [options] ...
        '   gw-vb <action>: error: <msg>
        ' is what the Driver's ArgparseException catch renders to match
        ' Python argparse's contract (and exit with code 2).
        Public Shared Function ArgparseUsage(action As String) As String
            Select Case action
                Case "info" : Return "usage: gw-vb info [options]"
                Case "read" : Return "usage: gw-vb read [options] file"
                Case "write" : Return "usage: gw-vb write [options] file"
                Case "convert" : Return "usage: gw-vb convert [options] in_file out_file"
                Case "erase" : Return "usage: gw-vb erase [options]"
                Case "clean" : Return "usage: gw-vb clean [options]"
                Case "seek" : Return "usage: gw-vb seek [options] cylinder"
                Case "delays" : Return "usage: gw-vb delays [options]"
                Case "update" : Return "usage: gw-vb update [options]"
                Case "pin" : Return "usage: gw-vb pin get|set [-h] ..."
                Case "reset" : Return "usage: gw-vb reset [options]"
                Case "bandwidth" : Return "usage: gw-vb bandwidth [options]"
                Case "rpm" : Return "usage: gw-vb rpm [options]"
                Case "align" : Return "usage: gw-vb align [options]"
            End Select
            ' Defensive fallback - any unknown action gets a generic banner
            ' so we never end up with an empty `usage:` line on stderr.
            Return String.Format("usage: gw-vb {0} [options]", action)
        End Function

        ' Throw an argparse-style failure: the Driver renders the per-action
        ' `usage:` / `gw-vb action: error: ...` two-line block on stderr and
        ' exits 2 (matching Python argparse). Use this for argv-validation
        ' failures only - missing positional, unknown option, type-conversion
        ' error, mutually-exclusive flags, invalid choice, malformed value
        ' (`--tracks=cyl=abc`). Reserve plain FatalException for runtime
        ' problems the library raises (file IO, USB, codec) - those keep
        ' exit code 1 and the FatalErrorFormatter rendering.
        Public Shared Sub Argparse(action As String, message As String)
            Throw New ArgparseException(action, ArgparseUsage(action), message)
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

        ' Resolve a `--tracks` (or `--out-tracks`) option into a TrackSet.
        '
        ' Mirrors Python's `read.py` / `write.py` / `convert.py` plumbing:
        '   1. `--tracks=''` is an argparse error (matches Python which
        '      forwards the empty string to `TrackSet.__init__`, which
        '      raises ValueError).
        '   2. If `format` resolves to a DiskDef with a `tracks` attribute,
        '      that becomes the default trackset (overlaid by `--tracks`).
        '   3. Otherwise fall back to `defaultRange` (typically
        '      "c=0-81:h=0-1"), again overlaid by `--tracks` if supplied.
        '   4. ArgumentException from TrackSet parsing is converted to
        '      `Argparse(... invalid TrackSet value: 'spec' ...)` so the
        '      Driver never surfaces a .NET stack trace for malformed
        '      cylinder / head segments.
        '
        ' Replaces the duplicated 25-line block in ReadOptionsParser,
        ' WriteOptionsParser, EraseOptionsParser, and AlignOptionsParser.
        ' ConvertOptionsParser uses a slightly different shape (out-tracks)
        ' and is handled in-line.
        Public Shared Function ResolveTracksOption(action As String,
                                                    optionName As String,
                                                    tracksSpec As String,
                                                    format As String,
                                                    diskDefsPath As String,
                                                    defaultRange As String) As TrackSet
            If tracksSpec IsNot Nothing AndAlso tracksSpec.Length = 0 Then
                Argparse(action, String.Format("argument {0}: invalid TrackSet value: ''", optionName))
            End If

            Dim tracks As TrackSet = Nothing
            If Not String.IsNullOrEmpty(format) Then
                Try
                    Dim fmtCls = DiskDefParser.GetDiskdef(format, diskDefsPath)
                    If fmtCls IsNot Nothing AndAlso fmtCls.Tracks IsNot Nothing Then
                        tracks = TrackResolution.ResolveDefaultTracksFromFormat(fmtCls.Tracks, tracksSpec)
                    End If
                Catch ex As ArgumentException When tracksSpec IsNot Nothing
                    Argparse(action, String.Format("argument {0}: invalid TrackSet value: '{1}'", optionName, tracksSpec))
                Catch
                    ' Format resolution failure (other than tracks parse) - fall through.
                End Try
            End If
            If tracks Is Nothing Then
                Try
                    tracks = TrackResolution.ResolveDefaultTracks(defaultRange, tracksSpec)
                Catch ex As ArgumentException When tracksSpec IsNot Nothing
                    Argparse(action, String.Format("argument {0}: invalid TrackSet value: '{1}'", optionName, tracksSpec))
                End Try
            End If
            Return tracks
        End Function

    End Class

End Namespace
