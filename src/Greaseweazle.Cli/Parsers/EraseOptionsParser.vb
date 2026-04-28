Imports Greaseweazle.Core
Imports Greaseweazle.Shared
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb erase`. Migrated from
    ' [Erase].BuildRuntimePreview; preserves the bespoke single-dash
    ' handling for `--revs <signed-int>` so `gw-vb erase --revs -1`
    ' surfaces a domain-language error rather than "missing value".
    Public NotInheritable Class EraseOptionsParser

        Private Sub New()
        End Sub

        Public Shared Function Parse(args As IReadOnlyList(Of String)) As EraseOptions
            Dim tracksSpec As String = Nothing
            Dim revs = 1
            Dim hfreq = False
            Dim fakeIndex As Double? = Nothing
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
                    Case "--tracks"
                        tracksSpec = TakeOptionValue(args, i, token, inlineValue)
                    Case "--revs"
                        Dim revsText = TakeOptionValue(args, i, token, inlineValue, allowSingleDashValue:=True)
                        If inlineValue Is Nothing AndAlso
                           revsText.StartsWith("-", StringComparison.Ordinal) AndAlso
                           Not revsText.StartsWith("--", StringComparison.Ordinal) AndAlso
                           revsText.Length > 1 Then
                            Dim parsedRevs As Integer
                            ErrorHandling.Check(
                                Integer.TryParse(revsText, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsedRevs),
                                String.Format("missing value for option {0}", token))
                        End If
                        Dim parsedRevsValue As Integer
                        ErrorHandling.Check(
                            Integer.TryParse(revsText, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsedRevsValue),
                            String.Format("invalid value for option {0}: {1}", token, revsText))
                        revs = parsedRevsValue
                        ErrorHandling.Check(revs >= 1, "--revs must be >= 1")
                    Case "--fake-index"
                        fakeIndex = ParserHelpers.Period(TakeOptionValue(args, i, token, inlineValue))
                    Case "--hfreq"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        hfreq = True
                    Case "--test"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        live = False
                    Case "--device", "--drive"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--device", StringComparison.Ordinal) Then
                            device = optionValue
                        Else
                            driveToken = optionValue
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
            If tracksSpec IsNot Nothing AndAlso tracksSpec.Length = 0 Then
                Throw New FatalException("invalid value for option --tracks: ''")
            End If

            Dim resolvedTracks As TrackSet
            Try
                resolvedTracks = TrackResolution.ResolveDefaultTracks("c=0-81:h=0-1", tracksSpec)
            Catch ex As Exception When tracksSpec IsNot Nothing
                Throw New FatalException(String.Format("invalid value for option --tracks: '{0}'", tracksSpec))
            End Try
            Dim drive As DriveSpec
            Try
                drive = ParserHelpers.Drive(driveToken)
            Catch ex As ArgumentException
                Throw New FatalException(ex.Message)
            End Try
            Return New EraseOptions With {
                .Tracks = resolvedTracks.ToString(),
                .TrackSet = resolvedTracks,
                .Revs = revs,
                .Hfreq = hfreq,
                .FakeIndex = fakeIndex,
                .Live = live,
                .Device = device,
                .Drive = drive
            }
        End Function

        Private Shared Sub CheckOptionValue(args As IReadOnlyList(Of String),
                                            index As Integer,
                                            optionName As String,
                                            Optional allowSingleDashValue As Boolean = False)
            Dim hasValue = index < args.Count
            If hasValue Then
                Dim value = args(index)
                If value.StartsWith("--", StringComparison.Ordinal) Then
                    hasValue = False
                ElseIf value.StartsWith("-", StringComparison.Ordinal) AndAlso Not allowSingleDashValue Then
                    hasValue = False
                End If
            End If
            ErrorHandling.Check(hasValue,
                                String.Format("missing value for option {0}", optionName))
        End Sub

        Private Shared Function TakeOptionValue(args As IReadOnlyList(Of String),
                                                ByRef index As Integer,
                                                optionName As String,
                                                inlineValue As String,
                                                Optional allowSingleDashValue As Boolean = False) As String
            If inlineValue IsNot Nothing Then
                Return inlineValue
            End If
            index += 1
            CheckOptionValue(args, index, optionName, allowSingleDashValue)
            Return args(index)
        End Function

    End Class

End Namespace
