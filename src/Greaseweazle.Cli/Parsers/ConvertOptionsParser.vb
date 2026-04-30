Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports Greaseweazle.Shared
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb convert`. Builds ConvertOptions, including
    ' TrackSetSpec partials for --tracks / --out-tracks. Format-default
    ' folding is deferred to BasicActions.RunFromOptions (after the input
    ' image has been opened and its format resolved), mirroring Python
    ' convert.py's late-binding of args.fmt_cls.tracks.
    Public NotInheritable Class ConvertOptionsParser

        Private Const ActionName As String = "convert"

        Private Sub New()
        End Sub

        ' Local shim that forwards to ParserHelpers.Argparse. Keeps call
        ' sites in this file short (no need to repeat ActionName at every
        ' throw point) while delegating the actual ArgparseException
        ' construction + usage-line lookup to the shared helper.
        Private Shared Sub Argparse(message As String)
            ParserHelpers.Argparse(ActionName, message)
        End Sub

        Public Shared Function Parse(args As IReadOnlyList(Of String),
                                     knownFormats As IEnumerable(Of String)) As ConvertOptions
            Dim format As String = Nothing
            Dim tracksSpec As String = Nothing
            Dim outTracksSpec As String = Nothing
            Dim diskDefsPath As String = Nothing
            Dim noClobber = False
            Dim hardSectors = False
            Dim reverse = False
            Dim adjustSpeed As Nullable(Of Double) = Nothing
            Dim pllOverride As Pll = Nothing
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
                    Case "--out-tracks"
                        outTracksSpec = TakeOptionValue(args, i, token, inlineValue)
                    Case "--diskdefs", "--pll", "--adjust-speed"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--diskdefs", StringComparison.Ordinal) Then
                            diskDefsPath = optionValue
                        ElseIf String.Equals(token, "--adjust-speed", StringComparison.Ordinal) Then
                            adjustSpeed = ParserHelpers.Period(optionValue)
                        ElseIf String.Equals(token, "--pll", StringComparison.Ordinal) Then
                            Try
                                pllOverride = New Pll(optionValue)
                            Catch ex As ArgumentException
                                Argparse(ex.Message)
                            End Try
                        End If
                    Case "--hard-sectors", "--reverse", "--no-clobber", "-n"
                        If inlineValue IsNot Nothing Then
                            Argparse(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        If String.Equals(token, "--no-clobber", StringComparison.Ordinal) OrElse
                           String.Equals(token, "-n", StringComparison.Ordinal) Then
                            noClobber = True
                        ElseIf String.Equals(token, "--hard-sectors", StringComparison.Ordinal) Then
                            hardSectors = True
                        ElseIf String.Equals(token, "--reverse", StringComparison.Ordinal) Then
                            reverse = True
                        End If
                    Case Else
                        If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                            Argparse(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            ' Mirror Python argparse: positional-argument errors cite the
            ' missing parameter names. This is structurally important so
            ' tooling parsing the error stream can pattern-match.
            If positionals.Count = 0 Then
                Argparse("the following arguments are required: in_file, out_file")
            ElseIf positionals.Count = 1 Then
                Argparse("the following arguments are required: out_file")
            ElseIf positionals.Count > 2 Then
                Argparse(String.Format("unrecognized arguments: {0}", String.Join(" ", positionals.Skip(2))))
            End If

            ' Pass the raw `path::opt1=val1:opt2=val2` strings through to the
            ' action layer untouched — ConvertAction.OpenImageForRead/Write
            ' splits them itself and applies the parsed options via
            ' Image.ApplyROpts/ApplyWOpts. (Earlier we extracted .Item1 here
            ' which silently dropped per-image options like
            ' `out.hfe::bitrate=500`.) Use SplitOpts only to validate that
            ' each positional has a non-empty path component.
            Dim inFile = positionals(0)
            Dim outFile = positionals(1)
            If String.IsNullOrEmpty(ParserHelpers.SplitOpts(inFile).Item1) Then
                Argparse("the following arguments are required: in_file")
            End If
            If String.IsNullOrEmpty(ParserHelpers.SplitOpts(outFile).Item1) Then
                Argparse("the following arguments are required: out_file")
            End If

            ParserHelpers.ValidateFormatIfSpecified(format, knownFormats, diskDefsPath)

            ' Mirror Python argparse: empty `--tracks=''` and `--out-tracks=''` are
            ' rejected with a TrackSet-parse error rather than silently treated as
            ' "no spec" (which is what String.IsNullOrEmpty would do below).
            If tracksSpec IsNot Nothing AndAlso tracksSpec.Length = 0 Then
                Argparse("argument --tracks: invalid TrackSet value: ''")
            End If
            If outTracksSpec IsNot Nothing AndAlso outTracksSpec.Length = 0 Then
                Argparse("argument --out-tracks: invalid TrackSet value: ''")
            End If

            ' Build TrackSetSpec partials directly from the user's --tracks /
            ' --out-tracks strings. The actual format-default fold happens
            ' later in BasicActions.RunFromOptions (after open_input_image
            ' has determined the format), mirroring Python convert.py. The
            ' partial constructors throw ArgumentException for malformed
            ' input, which we convert to argparse-style errors so the Driver
            ' never surfaces a .NET stack trace.
            Dim trackSetSpec As TrackSetSpec = Nothing
            If tracksSpec IsNot Nothing Then
                Try
                    trackSetSpec = New TrackSetSpec(tracksSpec)
                Catch ex As ArgumentException
                    Argparse(String.Format("argument --tracks: invalid TrackSet value: '{0}'", tracksSpec))
                End Try
            End If
            Dim outTrackSetSpec As TrackSetSpec = Nothing
            If outTracksSpec IsNot Nothing Then
                Try
                    outTrackSetSpec = New TrackSetSpec(outTracksSpec)
                Catch ex As ArgumentException
                    Argparse(String.Format("argument --out-tracks: invalid TrackSet value: '{0}'", outTracksSpec))
                End Try
            End If
            Dim pllProfiles As New List(Of Pll)()
            If pllOverride IsNot Nothing Then
                pllProfiles.Add(pllOverride)
            End If
            pllProfiles.AddRange(Plls.Values)
            Return New ConvertOptions With {
                .InputFile = inFile,
                .OutputFile = outFile,
                .Format = format,
                .DiskDefsPath = diskDefsPath,
                .TrackSet = trackSetSpec,
                .OutTrackSet = outTrackSetSpec,
                .NoClobber = noClobber,
                .HardSectors = hardSectors,
                .Reverse = reverse,
                .AdjustSpeed = adjustSpeed,
                .PllProfiles = pllProfiles
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

    End Class

End Namespace
