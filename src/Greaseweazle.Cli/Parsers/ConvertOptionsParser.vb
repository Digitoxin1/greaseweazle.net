Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports Greaseweazle.Shared
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb convert`. Migrated from
    ' Convert.BuildRuntimePreview; preserves the format -> formatTracks
    ' resolution and the input/output trackset clone-and-merge logic so
    ' the resulting ConvertOptions DTO is byte-for-byte equivalent to
    ' what Greaseweazle.Tools.Convert used to produce.
    Public NotInheritable Class ConvertOptionsParser

        Private Sub New()
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
                                Throw New FatalException(ex.Message)
                            End Try
                        End If
                    Case "--hard-sectors", "--reverse", "--no-clobber", "-n"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
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
                            Throw New FatalException(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            ErrorHandling.Check(positionals.Count = 2, "convert requires input and output files")

            Dim inFile = ParserHelpers.SplitOpts(positionals(0)).Item1
            Dim outFile = ParserHelpers.SplitOpts(positionals(1)).Item1
            If String.IsNullOrEmpty(inFile) OrElse String.IsNullOrEmpty(outFile) Then
                Throw New FatalException("convert requires input and output files")
            End If

            ParserHelpers.ValidateFormatIfSpecified(format, knownFormats, diskDefsPath)

            Dim formatTracks As TrackSet = Nothing
            If Not String.IsNullOrEmpty(format) Then
                Dim resolvedDiskDefsPath = ParserHelpers.ResolveDiskDefsPath(diskDefsPath)
                Dim disk = DiskDefParser.GetDiskdef(format, resolvedDiskDefsPath)
                If disk IsNot Nothing Then
                    formatTracks = disk.Tracks
                End If
            End If

            Dim resolved = Greaseweazle.Tools.Convert.ResolveTrackSets(formatTracks, tracksSpec, outTracksSpec)
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
                .TracksSpec = tracksSpec,
                .OutTracksSpec = outTracksSpec,
                .Tracks = resolved.Item1.ToString(),
                .OutTracks = resolved.Item2.ToString(),
                .TrackSet = resolved.Item1,
                .OutTrackSet = resolved.Item2,
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
