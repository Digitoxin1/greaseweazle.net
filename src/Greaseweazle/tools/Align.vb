Imports Greaseweazle.Core
Imports Greaseweazle.Codecs
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Shared

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `align` action.
    Public Class AlignOptions
        Public Property Header As String
        Public Property Format As String
        Public Property DiskDefsPath As String
        Public Property FormatDef As DiskDef
        Public Property Reads As Integer
        Public Property Revs As Integer
        ' Python align.py:64-71 keeps a fractional default_revs as a float and
        ' converts it to a tick budget at runtime (after measuring drive
        ' ticks-per-rev). When the format provides a non-integral default_revs,
        ' this property holds the raw float so the live path can reproduce
        ' Python's `ticks = drive_tpr * fractional_revs; revs = 2` collapse.
        Public Property FractionalRevs As Nullable(Of Double)
        Public Property Ticks As Integer
        Public Property TrackSet As TrackSet
        Public Property Raw As Boolean
        Public Property HardSectors As Boolean
        Public Property Reverse As Boolean
        Public Property AdjustSpeed As Nullable(Of Double)
        Public Property PllProfiles As IReadOnlyList(Of Pll)
        Public Property FakeIndexPeriod As Nullable(Of Double)
        Public Property GenTg43 As Boolean
        Public Property Densel As Nullable(Of Boolean)
        Public Property Live As Boolean = True
        Public Property Device As String
        Public Property Drive As DriveSpec
    End Class

    ' Python map: src/greaseweazle/tools/align.py (direct command-algorithm parity mapping).
    Public NotInheritable Class Align

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildTrackSpec)
        Public Shared Function BuildTrackSpec(cyl As Integer, head As Integer, physicalCyl As Integer, physicalHead As Integer) As String
            Dim tspec = String.Format("T{0}.{1}", cyl, head)
            If physicalCyl <> cyl OrElse physicalHead <> head Then
                tspec &= String.Format(" <- Drive {0}.{1}", physicalCyl, physicalHead)
            End If
            Return tspec
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildSingleTrackHeader)
        Public Shared Function BuildSingleTrackHeader(tspec As String, reads As Integer, revs As Integer) As String
            Return String.Format("Aligning {0}, reading {1} times, revs={2}", tspec, reads, revs)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildMultiTrackHeader)
        Public Shared Function BuildMultiTrackHeader(cyl As Integer, heads As IReadOnlyList(Of Integer), reads As Integer, revs As Integer) As String
            Return String.Format("Aligning T{0} (alternating heads {1}), reading {2} times, revs={3}",
                                 cyl,
                                 String.Join(",", heads),
                                 reads,
                                 revs)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration ValidateTrackCylinders)
        Public Shared Sub ValidateTrackCylinders(trackCylinderHeads As IReadOnlyList(Of Tuple(Of Integer, Integer)))
            If trackCylinderHeads Is Nothing OrElse trackCylinderHeads.Count = 0 Then
                Throw New FatalException("Align command requires at least one track (e.g., c=40:h=0)")
            End If
            Dim cyl = trackCylinderHeads(0).Item1
            For i = 1 To trackCylinderHeads.Count - 1
                If trackCylinderHeads(i).Item1 <> cyl Then
                    Throw New FatalException("All tracks must be on the same cylinder for alignment")
                End If
            Next
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveAlternatingTrackIndex)
        Public Shared Function ResolveAlternatingTrackIndex(readNumber As Integer, trackCount As Integer) As Integer
            Return (readNumber - 1) Mod trackCount
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveHardSectorReadParams)
        Public Shared Function ResolveHardSectorReadParams(hardSectors As Integer, revs As Integer) As Tuple(Of Integer, Integer)
            Return Tuple.Create((hardSectors + 1) * (revs + 1), 0)
        End Function

        ' Python map: src/greaseweazle/tools/align.py::read_and_normalise
        Public Shared Function ReadAndNormalise(usbClient As Unit,
                                                revs As Integer,
                                                ticks As Integer,
                                                driveTicksPerRev As Nullable(Of Double),
                                                reverse As Boolean,
                                                hardSectors As Boolean,
                                                raw As Boolean,
                                                adjustSpeed As Nullable(Of Double),
                                                Optional fakeIndexPeriod As Nullable(Of Double) = Nothing) As Flux
            Dim flux As Flux
            If fakeIndexPeriod.HasValue Then
                ErrorHandling.Check(driveTicksPerRev.HasValue, "fake-index requires drive ticks-per-rev")
                ' Python read.py:36 / align.py:28: `drive_tpr = int(args.drive_ticks_per_rev)`.
                ' int() truncates toward zero; Math.Round here would use banker's
                ' rounding and diverge from Python whenever the fractional part is
                ' >0.5 (or exactly .5 with an odd integer part).
                Dim fakeResult = ReadWrite.BuildFakeIndexList(revs,
                                                              ticks,
                                                              CInt(Math.Truncate(driveTicksPerRev.Value)),
                                                              usbClient.SampleFreq)
                flux = usbClient.ReadTrack(0, fakeResult.EffectiveTicks)
                flux.IndexList = fakeResult.IndexList.Select(Function(x) CDbl(x)).ToList()
            Else
                flux = usbClient.ReadTrack(revs, ticks)
            End If

            ' Python read.py:45 unconditionally assigns:
            '   flux._ticks_per_rev = args.drive_ticks_per_rev
            ' which clears the cached value to None when no explicit measurement is
            ' supplied. VB's `Flux.TicksPerRev` getter prefers the index-list-derived
            ' value over the cached field, so a missing override is automatically
            ' equivalent to Python's None case; we only assign when we have an actual
            ' override to apply.
            If driveTicksPerRev.HasValue Then
                flux.TicksPerRev = driveTicksPerRev.Value
            End If
            If reverse Then
                flux.Reverse()
            End If
            If hardSectors AndAlso Not raw Then
                flux.IdentifyHardSectors()
            End If
            If adjustSpeed.HasValue Then
                flux.Scale(adjustSpeed.Value / flux.TimePerRev)
            End If
            Return flux
        End Function

        ' Python map: src/greaseweazle/tools/align.py::align_track
        '
        ' Performs the read loop. Two callbacks expose progress without
        ' the algorithm producing any text:
        '   onStarted        (tracks, reads, revs, formatName)
        '   onReadCompleted  (AlignReadCompletedEventArgs)
        ' Returns the number of read passes that fully completed
        ' (always equal to `reads` on the success path).
        Public Shared Function AlignTrack(usbClient As Unit,
                                          tracks As IReadOnlyList(Of TrackIter),
                                          reads As Integer,
                                          revs As Integer,
                                          ticks As Integer,
                                          reverse As Boolean,
                                          hardSectors As Boolean,
                                          raw As Boolean,
                                          adjustSpeed As Nullable(Of Double),
                                          driveTicksPerRev As Nullable(Of Double),
                                          onStarted As Action(Of IReadOnlyList(Of TrackIter), Integer, Integer, String),
                                          onReadCompleted As Action(Of Greaseweazle.Actions.AlignReadCompletedEventArgs),
                                          Optional fakeIndexPeriod As Nullable(Of Double) = Nothing,
                                          Optional formatDef As DiskDef = Nothing,
                                          Optional formatName As String = Nothing,
                                          Optional pllProfiles As IReadOnlyList(Of Pll) = Nothing) As Integer
            Dim trackList = tracks.ToList()
            Dim pairs = trackList.Select(Function(t) Tuple.Create(t.Cyl, t.Head)).ToList()
            ValidateTrackCylinders(pairs)

            ' Python prints the "Aligning ..." header AFTER all revs
            ' adjustments (fractional collapse + hard-sectors multiplier),
            ' so the displayed revs reflects the *effective* read count.
            If onStarted IsNot Nothing Then
                onStarted(trackList, reads, revs, formatName)
            End If

            Dim completed = 0
            For readNum = 1 To reads
                Dim t = trackList(ResolveAlternatingTrackIndex(readNum, trackList.Count))
                usbClient.Seek(t.PhysicalCyl, t.PhysicalHead)
                Dim flux = ReadAndNormalise(usbClient, revs, ticks, driveTicksPerRev, reverse, hardSectors, raw, adjustSpeed, fakeIndexPeriod)
                Dim args As Greaseweazle.Actions.AlignReadCompletedEventArgs
                If formatDef Is Nothing Then
                    args = New Greaseweazle.Actions.AlignReadCompletedEventArgs(
                        t,
                        Greaseweazle.Actions.AlignReadOutcome.NoFormat,
                        flux.SummaryString(),
                        Nothing,
                        Nothing)
                Else
                    ' Python (align.py:126-135): `dat = fmt_cls.decode_flux(cyl, head, flux)`
                    ' creates the codec instance and runs the first decode pass; each retry
                    ' calls `dat.decode_flux(flux, pll)` on the SAME instance so subsequent
                    ' PLL profiles cumulatively merge any newly-recovered sectors via the
                    ' IBMTrack_Fixed.decode_flux raw-reconcile pass (first-good-wins).
                    Dim profiles = If(pllProfiles, Plls.Values)
                    Dim firstPll As Pll = If(profiles.Count > 0, profiles(0), Nothing)
                    Dim decoded = formatDef.DecodeFlux(t.Cyl, t.Head, flux, firstPll)
                    If decoded Is Nothing Then
                        args = New Greaseweazle.Actions.AlignReadCompletedEventArgs(
                            t,
                            Greaseweazle.Actions.AlignReadOutcome.OutOfRange,
                            flux.SummaryString(),
                            Nothing,
                            If(formatName, String.Empty))
                    Else
                        Dim nr = 1
                        While decoded.NrMissing() <> 0 AndAlso nr < profiles.Count
                            decoded.DecodeFlux(flux, profiles(nr))
                            nr += 1
                        End While
                        args = New Greaseweazle.Actions.AlignReadCompletedEventArgs(
                            t,
                            Greaseweazle.Actions.AlignReadOutcome.Decoded,
                            flux.SummaryString(),
                            decoded.SummaryString(),
                            Nothing)
                    End If
                End If
                If onReadCompleted IsNot Nothing Then
                    onReadCompleted(args)
                End If
                completed += 1
                If readNum < reads Then
                    Threading.Thread.Sleep(100)
                End If
            Next
            Return completed
        End Function

    End Class

End Namespace
