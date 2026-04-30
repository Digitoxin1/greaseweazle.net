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

        ' Engine-time resolution helper used by every action's RunFromOptions.
        ' Folds `spec` (user intent — may be Nothing or a partial TrackSetSpec)
        ' against `formatDefaults` (codec defaults — may be Nothing if no
        ' format applies or hasn't been resolved yet) and falls back to
        ' `fallbackBaseSpec` when neither side supplies cyls/heads.
        '
        ' Mirrors Python's `def_tracks = copy.copy(args.fmt_cls.tracks);
        ' def_tracks.update_from_trackspec(args.tracks.trackspec)` runtime
        ' fold, just expressed against typed objects rather than a spec
        ' string round-trip.
        Public Shared Function ResolveSpec(spec As TrackSetSpec,
                                           formatDefaults As TrackSet,
                                           fallbackBaseSpec As String) As TrackSet
            Dim defaults As TrackSet =
                If(formatDefaults IsNot Nothing, formatDefaults, New TrackSet(fallbackBaseSpec))
            If spec Is Nothing Then
                Return New TrackSet(defaults.ToString())
            End If
            Return spec.Resolve(defaults)
        End Function

    End Class

End Namespace
