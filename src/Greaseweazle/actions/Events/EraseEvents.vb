Namespace Greaseweazle.Actions

    ' Raised once at the start of an erase run, before any USB activity.
    ' Mirrors Python's "Erasing {tracks}, revs={N}" header line: subscribers
    ' that want to render the header can format it from these typed fields
    ' (the DLL no longer formats the line itself).
    Public NotInheritable Class EraseStartedEventArgs
        Inherits EventArgs

        Public Sub New(tracks As String, revs As Integer)
            Me.Tracks = tracks
            Me.Revs = revs
        End Sub

        ' Compact track-set spec from EraseOptions.Tracks, e.g. "c=0-81:h=0-1".
        Public ReadOnly Property Tracks As String

        ' Number of erase revolutions per track.
        Public ReadOnly Property Revs As Integer

    End Class

    ' Raised right before a single physical track is erased — only fires
    ' in the live (non-dry-run) path, since --test stops after the header.
    Public NotInheritable Class EraseTrackEventArgs
        Inherits EventArgs

        Public Sub New(cyl As Integer, head As Integer)
            Me.Cyl = cyl
            Me.Head = head
        End Sub

        ' Logical cylinder (TrackIter.Cyl) — what Python prints in
        ' "T{cyl}.{head}: Erasing Track".
        Public ReadOnly Property Cyl As Integer

        ' Logical head (TrackIter.Head).
        Public ReadOnly Property Head As Integer

    End Class

End Namespace
