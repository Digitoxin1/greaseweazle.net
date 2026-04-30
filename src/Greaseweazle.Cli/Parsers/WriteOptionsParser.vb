Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports Greaseweazle.Shared
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb write`. Migrated from
    ' ReadWrite.BuildWriteOptions; preserves the precomp-spec parsing,
    ' image-class default_format fallback, the catalogue-bearing
    ' UnknownFormatException, and the --hard-sectors/--fake-index plus
    ' --gen-tg43/--densel mutex checks.
    Public NotInheritable Class WriteOptionsParser

        Private Const ActionName As String = "write"

        Private Sub New()
        End Sub

        ' See ParserHelpers.Argparse - exit code 2 + per-action `usage:`
        ' two-liner on stderr for argv-validation failures.
        Private Shared Sub Argparse(message As String)
            ParserHelpers.Argparse(ActionName, message)
        End Sub

        Public Shared Function Parse(args As IReadOnlyList(Of String),
                                     knownFormats As IEnumerable(Of String)) As WriteOptions
            Dim format As String = Nothing
            Dim diskDefsPath As String = Nothing
            Dim tracksSpec As String = Nothing
            Dim precompSpec As String = Nothing
            Dim preErase = False
            Dim eraseEmpty = False
            Dim hardSectors = False
            Dim noVerify = False
            Dim reverse = False
            Dim genTg43 = False
            Dim densel As Nullable(Of Boolean) = Nothing
            Dim fakeIndexPeriod As Nullable(Of Double) = Nothing
            Dim retries = 3
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
                    Case "--precomp"
                        precompSpec = TakeOptionValue(args, i, token, inlineValue)
                    Case "--device", "--drive", "--diskdefs", "--fake-index", "--retries", "--densel", "--dd"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--device", StringComparison.Ordinal) Then
                            device = optionValue
                        ElseIf String.Equals(token, "--drive", StringComparison.Ordinal) Then
                            driveToken = optionValue
                        ElseIf String.Equals(token, "--diskdefs", StringComparison.Ordinal) Then
                            diskDefsPath = optionValue
                        ElseIf String.Equals(token, "--fake-index", StringComparison.Ordinal) Then
                            fakeIndexPeriod = ParserHelpers.Period(optionValue)
                        ElseIf String.Equals(token, "--retries", StringComparison.Ordinal) Then
                            retries = ParseUInt(optionValue, token)
                        ElseIf String.Equals(token, "--densel", StringComparison.Ordinal) OrElse
                               String.Equals(token, "--dd", StringComparison.Ordinal) Then
                            Try
                                densel = ParserHelpers.Level(optionValue)
                            Catch ex As ArgumentException
                                Argparse(ex.Message)
                            End Try
                        End If
                    Case "--pre-erase", "--erase-empty", "--hard-sectors", "--no-verify", "--reverse", "--gen-tg43"
                        If inlineValue IsNot Nothing Then
                            Argparse(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        If String.Equals(token, "--pre-erase", StringComparison.Ordinal) Then
                            preErase = True
                        ElseIf String.Equals(token, "--erase-empty", StringComparison.Ordinal) Then
                            eraseEmpty = True
                        ElseIf String.Equals(token, "--hard-sectors", StringComparison.Ordinal) Then
                            hardSectors = True
                        ElseIf String.Equals(token, "--no-verify", StringComparison.Ordinal) Then
                            noVerify = True
                        ElseIf String.Equals(token, "--reverse", StringComparison.Ordinal) Then
                            reverse = True
                        ElseIf String.Equals(token, "--gen-tg43", StringComparison.Ordinal) Then
                            genTg43 = True
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

            If positionals.Count = 0 Then
                Argparse("the following arguments are required: file")
            ElseIf positionals.Count > 1 Then
                Argparse(String.Format("unrecognized arguments: {0}", String.Join(" ", positionals.Skip(1))))
            End If

            ' Python write.py:231-234: --fake-index / --hard-sectors mutex.
            If fakeIndexPeriod.HasValue AndAlso hardSectors Then
                Argparse("argument --hard-sectors: not allowed with argument --fake-index")
            End If
            ' Python write.py:244-251: --densel/--dd / --gen-tg43 mutex.
            If densel.HasValue AndAlso genTg43 Then
                Argparse("argument --gen-tg43: not allowed with argument --densel")
            End If

            ' Python write.py:260-261: if not args.format: args.format = image_class.default_format
            If String.IsNullOrEmpty(format) Then
                format = ImageDefaults.DefaultFormatForFile(positionals(0))
            End If
            ParserHelpers.ValidateFormatIfSpecified(format, knownFormats, diskDefsPath)

            ' --tracks is captured as user intent (TrackSetSpec); the engine
            ' folds it against format defaults inside RunFromOptions.
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

            Dim precompText As String = Nothing
            If Not String.IsNullOrEmpty(precompSpec) Then
                precompText = New PrecompSpec(precompSpec).ToString()
            End If
            Dim drive As DriveSpec = Nothing
            Try
                drive = ParserHelpers.Drive(driveToken)
            Catch ex As ArgumentException
                Argparse(ex.Message)
            End Try
            Return New WriteOptions With {
                .FileName = positionals(0),
                .Format = format,
                .DiskDefsPath = diskDefsPath,
                .Precomp = precompText,
                .PrecompSpec = precompSpec,
                .TrackSet = tracks,
                .PreErase = preErase,
                .EraseEmpty = eraseEmpty,
                .HardSectors = hardSectors,
                .NoVerify = noVerify,
                .Reverse = reverse,
                .GenTg43 = genTg43,
                .Densel = densel,
                .FakeIndexPeriod = fakeIndexPeriod,
                .Retries = retries,
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
