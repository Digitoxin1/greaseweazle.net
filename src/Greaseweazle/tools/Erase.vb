Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Shared

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `erase` action.
    Public Class EraseOptions
        Public Property Tracks As String
        Public Property TrackSet As TrackSet
        Public Property Revs As Integer
        Public Property Hfreq As Boolean
        Public Property FakeIndex As Double?
        Public Property Live As Boolean = True
        Public Property Device As String
        Public Property Drive As DriveSpec

        Public Shared Function FromArgs(args As IReadOnlyList(Of String)) As EraseOptions
            Return [Erase].BuildRuntimePreview(args)
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/erase.py (direct command-algorithm parity mapping).
    Public NotInheritable Class [Erase]

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildEraseHeader)
        Public Shared Function BuildEraseHeader(tracks As String, revs As Integer) As String
            Return String.Format("Erasing {0}, revs={1}", tracks, revs)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ComputeEraseTicks)
        Public Shared Function ComputeEraseTicks(driveTicks As Double) As Double
            Return driveTicks * 1.1
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ComputeHighFrequencyFlux)
        Public Shared Function ComputeHighFrequencyFlux(driveTicks As Double) As List(Of Integer)
            Dim eraseTicks = ComputeEraseTicks(driveTicks)
            Return New List(Of Integer) From {CInt(Math.Round(eraseTicks))}
        End Function

        ' Python map: src/greaseweazle/tools/erase.py::erase
        '
        ' Performs the per-track erase loop. `onTrackStarting` is invoked
        ' just before each TrackIter is touched so the caller (the typed
        ' EraseCommand or any other library consumer) can render progress
        ' UI without this routine generating any text itself. Returns the
        ' number of tracks visited.
        Public Shared Function [Erase](usbClient As Unit,
                                       preview As EraseOptions,
                                       tracks As IReadOnlyList(Of TrackIter),
                                       onTrackStarting As Action(Of TrackIter)) As Integer
            Dim driveTicks As Double
            If preview.FakeIndex.HasValue Then
                driveTicks = preview.FakeIndex.Value * usbClient.SampleFreq
            Else
                driveTicks = usbClient.ReadTrack(2, 0).TicksPerRev
            End If

            Dim processed = 0
            For Each track In tracks
                If onTrackStarting IsNot Nothing Then
                    onTrackStarting(track)
                End If
                usbClient.Seek(track.PhysicalCyl, track.PhysicalHead)
                For rev = 0 To preview.Revs - 1
                    If preview.Hfreq Then
                        usbClient.WriteTrack(ComputeHighFrequencyFlux(driveTicks),
                                             terminateAtIndex:=False,
                                             cueAtIndex:=False)
                    Else
                        usbClient.EraseTrack(ComputeEraseTicks(driveTicks))
                    End If
                Next
                processed += 1
            Next
            Return processed
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildRuntimePreview)
        Public Shared Function BuildRuntimePreview(args As IReadOnlyList(Of String)) As EraseOptions
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
                        fakeIndex = ToolOptions.Period(TakeOptionValue(args, i, token, inlineValue))
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
                drive = ToolOptions.Drive(driveToken)
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

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration CheckOptionValue)
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
