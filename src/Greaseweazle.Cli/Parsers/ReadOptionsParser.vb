Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports Greaseweazle.Images
Imports Greaseweazle.Shared
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb read`. Migrated from
    ' ReadWrite.BuildReadOptions; preserves the Python read.py
    ' parity nuances: fractional default_revs collapse,
    ' image-class default_format fallback, and the catalogue-bearing
    ' UnknownFormatException for an unrecognised --format.
    Public NotInheritable Class ReadOptionsParser

        Private Const ActionName As String = "read"

        Private Sub New()
        End Sub

        ' Local shim for ParserHelpers.Argparse - argparse-style failures
        ' here surface as exit 2 with the `usage: gw-vb read [options] file`
        ' two-liner, matching gw.exe's argparse contract.
        Private Shared Sub Argparse(message As String)
            ParserHelpers.Argparse(ActionName, message)
        End Sub

        Public Shared Function Parse(args As IReadOnlyList(Of String),
                                     knownFormats As IEnumerable(Of String)) As ReadOptions
            Dim format As String = Nothing
            Dim diskDefsPath As String = Nothing
            Dim tracksSpec As String = Nothing
            Dim revs As Integer? = Nothing
            Dim raw = False
            Dim hardSectors = False
            Dim reverse = False
            Dim genTg43 = False
            Dim noClobber = False
            Dim adjustSpeed As Nullable(Of Double) = Nothing
            Dim fakeIndexPeriod As Nullable(Of Double) = Nothing
            Dim densel As Nullable(Of Boolean) = Nothing
            Dim retries = 3
            Dim seekRetries = 0
            Dim pllOverride As Pll = Nothing
            Dim live = True
            Dim device As String = Nothing
            Dim driveToken = "A"
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
                    Case "--revs"
                        revs = ParseUInt(TakeOptionValue(args, i, token, inlineValue), token)
                        ErrorHandling.Check(revs.Value >= 1, "--revs must be >= 1")
                    Case "--device", "--drive", "--diskdefs", "--fake-index", "--adjust-speed",
                         "--retries", "--seek-retries", "--pll", "--densel", "--dd"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--device", StringComparison.Ordinal) Then
                            device = optionValue
                        ElseIf String.Equals(token, "--drive", StringComparison.Ordinal) Then
                            driveToken = optionValue
                        ElseIf String.Equals(token, "--diskdefs", StringComparison.Ordinal) Then
                            diskDefsPath = optionValue
                        ElseIf String.Equals(token, "--fake-index", StringComparison.Ordinal) Then
                            fakeIndexPeriod = ParserHelpers.Period(optionValue)
                        ElseIf String.Equals(token, "--adjust-speed", StringComparison.Ordinal) Then
                            adjustSpeed = ParserHelpers.Period(optionValue)
                        ElseIf String.Equals(token, "--retries", StringComparison.Ordinal) Then
                            retries = ParseUInt(optionValue, token)
                        ElseIf String.Equals(token, "--seek-retries", StringComparison.Ordinal) Then
                            seekRetries = ParseUInt(optionValue, token)
                        ElseIf String.Equals(token, "--pll", StringComparison.Ordinal) Then
                            Try
                                pllOverride = New Pll(optionValue)
                            Catch ex As ArgumentException
                                Argparse(ex.Message)
                            End Try
                        ElseIf String.Equals(token, "--densel", StringComparison.Ordinal) OrElse
                               String.Equals(token, "--dd", StringComparison.Ordinal) Then
                            Try
                                densel = ParserHelpers.Level(optionValue)
                            Catch ex As ArgumentException
                                Argparse(ex.Message)
                            End Try
                        End If
                    Case "--raw", "--hard-sectors", "--no-clobber", "-n", "--gen-tg43", "--reverse"
                        If inlineValue IsNot Nothing Then
                            Argparse(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        If String.Equals(token, "--raw", StringComparison.Ordinal) Then
                            raw = True
                        ElseIf String.Equals(token, "--hard-sectors", StringComparison.Ordinal) Then
                            hardSectors = True
                        ElseIf String.Equals(token, "--gen-tg43", StringComparison.Ordinal) Then
                            genTg43 = True
                        ElseIf String.Equals(token, "--reverse", StringComparison.Ordinal) Then
                            reverse = True
                        ElseIf String.Equals(token, "--no-clobber", StringComparison.Ordinal) OrElse
                               String.Equals(token, "-n", StringComparison.Ordinal) Then
                            noClobber = True
                        End If
                    Case "--test"
                        If inlineValue IsNot Nothing Then
                            Argparse(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        live = False
                    Case Else
                        If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                            Argparse(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            ' Mirror Python argparse's positional-arg error wording.
            If positionals.Count = 0 Then
                Argparse("the following arguments are required: file")
            ElseIf positionals.Count > 1 Then
                Argparse(String.Format("unrecognized arguments: {0}", String.Join(" ", positionals.Skip(1))))
            End If
            ' Python read.py:231-234: --fake-index and --hard-sectors are mutex.
            If fakeIndexPeriod.HasValue AndAlso hardSectors Then
                Argparse("argument --hard-sectors: not allowed with argument --fake-index")
            End If
            ' Python read.py:247-251: --densel/--dd and --gen-tg43 are mutex.
            If densel.HasValue AndAlso genTg43 Then
                Argparse("argument --gen-tg43: not allowed with argument --densel")
            End If

            ' Python read.py:267-268: if not args.format: args.format = image_class.default_format
            ' Apply the per-image-class default before validating the format string.
            If String.IsNullOrEmpty(format) Then
                format = ImageDefaults.DefaultFormatForFile(positionals(0))
            End If
            ParserHelpers.ValidateFormatIfSpecified(format, knownFormats, diskDefsPath)

            ' Python read.py:278-279: when --format is given, fmt_cls.default_revs becomes
            ' the default if --revs is omitted.
            Dim fractionalRevs As Nullable(Of Double) = Nothing
            If revs Is Nothing AndAlso Not String.IsNullOrEmpty(format) Then
                Try
                    Dim fmtCls = DiskDefParser.GetDiskdef(format, diskDefsPath)
                    If fmtCls IsNot Nothing Then
                        Dim defaultRevs = fmtCls.DefaultRevs
                        If defaultRevs > 0 Then
                            If defaultRevs <> Math.Floor(defaultRevs) Then
                                fractionalRevs = defaultRevs
                                revs = 2
                            Else
                                revs = CInt(defaultRevs)
                            End If
                        End If
                    End If
                Catch
                End Try
            End If

            Dim resolvedRevs = If(revs, 3)
            Dim revsDisplay As String
            If fractionalRevs.HasValue Then
                revsDisplay = fractionalRevs.Value.ToString(Globalization.CultureInfo.InvariantCulture)
            Else
                revsDisplay = resolvedRevs.ToString(Globalization.CultureInfo.InvariantCulture)
            End If

            ' --tracks is captured as user intent (TrackSetSpec); the engine
            ' folds it against format defaults inside RunFromOptions. The
            ' partial constructor still throws ArgumentException for malformed
            ' input, so parse-time error reporting is unchanged.
            Dim tracks As TrackSetSpec = Nothing
            If tracksSpec IsNot Nothing AndAlso tracksSpec.Length = 0 Then
                Argparse("argument --tracks: invalid TrackSet value: ''")
            End If
            If tracksSpec IsNot Nothing Then
                Try
                    tracks = New TrackSetSpec(tracksSpec)
                Catch ex As ArgumentException
                    Argparse(String.Format("argument --tracks: invalid TrackSet value: '{0}'", tracksSpec))
                End Try
            End If
            Dim drive As DriveSpec = Nothing
            Try
                drive = ParserHelpers.Drive(driveToken)
            Catch ex As ArgumentException
                Argparse(ex.Message)
            End Try
            Dim pllProfiles As New List(Of Pll)()
            If pllOverride IsNot Nothing Then
                pllProfiles.Add(pllOverride)
            End If
            pllProfiles.AddRange(Plls.Values)
            Return New ReadOptions With {
                .FileName = positionals(0),
                .Format = format,
                .DiskDefsPath = diskDefsPath,
                .TrackSet = tracks,
                .Revs = resolvedRevs,
                .FractionalRevs = fractionalRevs,
                .RevsDisplay = revsDisplay,
                .Raw = raw,
                .HardSectors = hardSectors,
                .Reverse = reverse,
                .NoClobber = noClobber,
                .AdjustSpeed = adjustSpeed,
                .PllProfiles = pllProfiles,
                .FakeIndexPeriod = fakeIndexPeriod,
                .GenTg43 = genTg43,
                .Densel = densel,
                .Retries = retries,
                .SeekRetries = seekRetries,
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
                Argparse(String.Format("invalid value for {0}: {1}", optionName, value))
            End If
            Return parsed
        End Function

    End Class

End Namespace
