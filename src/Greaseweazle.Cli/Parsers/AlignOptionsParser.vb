Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports Greaseweazle.Shared
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb align`. Migrated from
    ' Align.BuildRuntimePreview; preserves the format-resolution + track-
    ' set + PLL-profile + revs-collapse logic so the resulting
    ' AlignOptions DTO is byte-for-byte equivalent to what
    ' Greaseweazle.Tools.Align used to produce.
    Public NotInheritable Class AlignOptionsParser

        Private Sub New()
        End Sub

        Public Shared Function Parse(args As IReadOnlyList(Of String),
                                     knownFormats As IEnumerable(Of String)) As AlignOptions
            Dim format As String = Nothing
            Dim diskDefsPath As String = Nothing
            Dim tracksSpec As String = Nothing
            Dim raw = False
            Dim hardSectors = False
            Dim reverse = False
            Dim genTg43 = False
            Dim live = True
            Dim device As String = Nothing
            Dim driveToken = "A"
            Dim pllOverride As Pll = Nothing
            Dim adjustSpeed As Nullable(Of Double) = Nothing
            Dim fakeIndexPeriod As Nullable(Of Double) = Nothing
            Dim densel As Nullable(Of Boolean) = Nothing
            Dim reads = 10
            Dim revs As Integer? = Nothing
            Dim positionals As New List(Of String)()

            Dim i = 0
            While i < args.Count
                Dim rawToken = args(i)
                If String.Equals(rawToken, "--", StringComparison.Ordinal) Then
                    For j = i To args.Count - 1
                        positionals.Add(args(j))
                    Next
                    Exit While
                End If
                Dim token = rawToken
                Dim inlineValue As String = Nothing
                Dim equalsIndex = rawToken.IndexOf("="c)
                If rawToken.StartsWith("--", StringComparison.Ordinal) AndAlso equalsIndex > 2 Then
                    token = rawToken.Substring(0, equalsIndex)
                    inlineValue = rawToken.Substring(equalsIndex + 1)
                End If
                Select Case token
                    Case "--format"
                        format = TakeOptionValue(args, i, token, inlineValue)
                    Case "--tracks"
                        tracksSpec = TakeOptionValue(args, i, token, inlineValue)
                    Case "--reads"
                        reads = ParseUInt(TakeOptionValue(args, i, token, inlineValue), token)
                        ErrorHandling.Check(reads >= 1, "--reads must be >= 1")
                    Case "--revs"
                        revs = ParseUInt(TakeOptionValue(args, i, token, inlineValue), token)
                        ErrorHandling.Check(revs.Value >= 1, "--revs must be >= 1")
                    Case "--device", "--drive", "--diskdefs", "--pll", "--adjust-speed", "--densel", "--dd", "--fake-index"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--device", StringComparison.Ordinal) Then
                            device = optionValue
                        ElseIf String.Equals(token, "--drive", StringComparison.Ordinal) Then
                            driveToken = optionValue
                        ElseIf String.Equals(token, "--diskdefs", StringComparison.Ordinal) Then
                            diskDefsPath = optionValue
                        ElseIf String.Equals(token, "--adjust-speed", StringComparison.Ordinal) Then
                            adjustSpeed = ParserHelpers.Period(optionValue)
                        ElseIf String.Equals(token, "--fake-index", StringComparison.Ordinal) Then
                            fakeIndexPeriod = ParserHelpers.Period(optionValue)
                        ElseIf String.Equals(token, "--pll", StringComparison.Ordinal) Then
                            Try
                                pllOverride = New Pll(optionValue)
                            Catch ex As ArgumentException
                                Throw New FatalException(ex.Message)
                            End Try
                        ElseIf String.Equals(token, "--densel", StringComparison.Ordinal) OrElse
                               String.Equals(token, "--dd", StringComparison.Ordinal) Then
                            Try
                                densel = ParserHelpers.Level(optionValue)
                            Catch ex As ArgumentException
                                Throw New FatalException(ex.Message)
                            End Try
                        End If
                    Case "--raw", "--hard-sectors", "--gen-tg43", "--reverse", "--test"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        If String.Equals(token, "--raw", StringComparison.Ordinal) Then
                            raw = True
                        ElseIf String.Equals(token, "--hard-sectors", StringComparison.Ordinal) Then
                            hardSectors = True
                        ElseIf String.Equals(token, "--gen-tg43", StringComparison.Ordinal) Then
                            genTg43 = True
                        ElseIf String.Equals(token, "--reverse", StringComparison.Ordinal) Then
                            reverse = True
                        ElseIf String.Equals(token, "--test", StringComparison.Ordinal) Then
                            live = False
                        End If
                    Case Else
                        If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                            Throw New FatalException(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            If positionals.Count > 0 Then
                Throw New FatalException(String.Format("unrecognized arguments: {0}", String.Join(" ", positionals)))
            End If
            ErrorHandling.Check(Not String.IsNullOrEmpty(tracksSpec), "align requires --tracks")
            ParserHelpers.ValidateFormatIfSpecified(format, knownFormats, diskDefsPath)
            Dim formatDef As DiskDef = Nothing
            ' Python: load DiskDef whenever --format is supplied, not only when running live.
            ' Track-only specs (e.g. "ibm.mfm") may not resolve to a DiskDef; tolerate
            ' a missing entry here so callers using --format for codec hints continue
            ' to work in --test (preview) mode.
            Dim fractionalRevs As Nullable(Of Double) = Nothing
            If Not String.IsNullOrEmpty(format) Then
                Try
                    formatDef = ParserHelpers.ResolveDiskDefinition(format, diskDefsPath)
                Catch ex As FatalException
                    formatDef = Nothing
                End Try
                ' Python align.py:201: `if args.revs is None: args.revs = fmt_cls.default_revs`
                ' Python keeps the float typing of default_revs and collapses to
                ' integer revs=2 inside align_track at runtime (see
                ' align.py:64-71). Preserve the float for the runtime collapse
                ' rather than rounding here, which would lose tick fidelity
                ' for fractional values like 1.1.
                If revs Is Nothing AndAlso formatDef IsNot Nothing AndAlso formatDef.DefaultRevs > 0 Then
                    Dim defaultRevs = formatDef.DefaultRevs
                    If defaultRevs <> Math.Floor(defaultRevs) Then
                        ' Fractional: stash float, set integer revs=2 (Python's
                        ' unconditional collapse target).
                        fractionalRevs = defaultRevs
                        revs = 2
                    Else
                        revs = CInt(defaultRevs)
                    End If
                End If
            End If
            Dim resolvedRevs = If(revs, 3)

            Dim trackSet = TrackResolution.ResolveDefaultTracksFromFormat(If(formatDef Is Nothing, Nothing, formatDef.Tracks), tracksSpec)
            Dim trackList = trackSet.IteratePhysical().ToList()
            Dim pairs = trackList.Select(Function(t) Tuple.Create(t.Cyl, t.Head)).ToList()
            Greaseweazle.Tools.Align.ValidateTrackCylinders(pairs)

            Dim drive As DriveSpec
            Try
                drive = ParserHelpers.Drive(driveToken)
            Catch ex As ArgumentException
                Throw New FatalException(ex.Message)
            End Try
            Dim pllProfiles As New List(Of Pll)()
            If pllOverride IsNot Nothing Then
                pllProfiles.Add(pllOverride)
            End If
            pllProfiles.AddRange(Plls.Values)

            Dim header As String
            If trackList.Count = 1 Then
                Dim t = trackList(0)
                header = Greaseweazle.Tools.Align.BuildSingleTrackHeader(
                    Greaseweazle.Tools.Align.BuildTrackSpec(t.Cyl, t.Head, t.PhysicalCyl, t.PhysicalHead),
                    reads,
                    resolvedRevs)
            Else
                Dim heads = trackList.Select(Function(t) t.Head).ToList()
                header = Greaseweazle.Tools.Align.BuildMultiTrackHeader(trackList(0).Cyl, heads, reads, resolvedRevs)
            End If

            Return New AlignOptions With {
                .Header = header,
                .Format = format,
                .DiskDefsPath = diskDefsPath,
                .FormatDef = formatDef,
                .Reads = reads,
                .Revs = resolvedRevs,
                .FractionalRevs = fractionalRevs,
                .Ticks = 0,
                .TrackSet = trackSet,
                .Raw = raw,
                .HardSectors = hardSectors,
                .Reverse = reverse,
                .AdjustSpeed = adjustSpeed,
                .PllProfiles = pllProfiles,
                .FakeIndexPeriod = fakeIndexPeriod,
                .GenTg43 = genTg43,
                .Densel = densel,
                .Live = live,
                .Device = device,
                .Drive = drive
            }
        End Function

        Private Shared Sub CheckOptionValue(args As IReadOnlyList(Of String), index As Integer, optionName As String)
            Dim hasValue = index < args.Count
            If hasValue Then
                Dim value = args(index)
                If value.StartsWith("--", StringComparison.Ordinal) Then
                    hasValue = False
                End If
            End If
            ErrorHandling.Check(hasValue, String.Format("missing value for option {0}", optionName))
        End Sub

        Private Shared Function TakeOptionValue(args As IReadOnlyList(Of String),
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

        Private Shared Function ParseUInt(value As String, optionName As String) As Integer
            Dim parsed As Integer
            If Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) OrElse parsed < 0 Then
                Throw New FatalException(String.Format("invalid value for {0}: {1}", optionName, value))
            End If
            Return parsed
        End Function

    End Class

End Namespace
