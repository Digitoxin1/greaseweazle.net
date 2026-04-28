Imports Greaseweazle.Shared

Namespace Greaseweazle.Tools

    ' Python map: no-1:1 with Python symbols; this helper class centralizes TrackSet defaulting shared across read/write/convert/erase/align.
    Public NotInheritable Class TrackResolution

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveDefaultTracks)
        Public Shared Function ResolveDefaultTracks(defaultTrackSpec As String, requestedTrackSpec As String) As TrackSet
            Dim resolved As New TrackSet(defaultTrackSpec)
            If Not String.IsNullOrEmpty(requestedTrackSpec) Then
                resolved.UpdateFromTrackspec(requestedTrackSpec)
            End If
            Return resolved
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveDefaultTracksFromFormat)
        Public Shared Function ResolveDefaultTracksFromFormat(formatTracks As TrackSet, requestedTrackSpec As String) As TrackSet
            Dim baseTracks As TrackSet =
                If(formatTracks Is Nothing, New TrackSet("c=0-81:h=0-1"), New TrackSet(formatTracks.ToString()))
            If Not String.IsNullOrEmpty(requestedTrackSpec) Then
                baseTracks.UpdateFromTrackspec(requestedTrackSpec)
            End If
            Return baseTracks
        End Function

    End Class

End Namespace
