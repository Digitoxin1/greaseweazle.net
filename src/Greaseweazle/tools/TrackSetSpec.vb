Imports System.Text.RegularExpressions

Namespace Greaseweazle.Shared

    ' Partial / user-intent companion to TrackSet.
    '
    ' Where TrackSet always carries fully-resolved values (Step=1, Hswap=False,
    ' HOff={0,0} as concrete defaults) the moment it is constructed,
    ' TrackSetSpec uses Nothing scalars and empty Cyls/Heads lists to record
    ' which fields the user actually supplied. The user's intent is exactly
    ' the set of fields with non-Nothing / non-empty values; everything else
    ' is "let the format decide".
    '
    ' This type only exists for Convert: format defaults are not known until
    ' the input image is opened, so the user's --tracks / --out-tracks
    ' intent has to be carried in partial form across the parse → engine
    ' boundary and folded against format defaults at engine time via
    ' Resolve(...).
    '
    ' Read / Write / Erase / Align do NOT use TrackSetSpec — their format is
    ' known at parse time, so the parser eagerly produces a fully-resolved
    ' TrackSet and the DTO never sees a partial.
    '
    ' Python map: src/greaseweazle/tools/util.py::TrackSet
    ' (Python uses TrackSet itself in both roles, distinguishing user-set
    ' from default by re-applying TrackSet.trackspec onto format defaults
    ' via update_from_trackspec. VB uses two types so the partial-vs-resolved
    ' distinction is enforced by the type system rather than by convention.)
    Public Class TrackSetSpec

        Public Sub New()
            Cyls = New List(Of Integer)()
            Heads = New List(Of Integer)()
            HOff = New Integer?() {Nothing, Nothing}
            _trackspec = String.Empty
        End Sub

        Public Sub New(spec As String)
            Me.New()
            If Not String.IsNullOrEmpty(spec) Then
                UpdateFromTrackspec(spec)
            End If
        End Sub

        ' Cyls / Heads: empty list ⇒ user did not supply this key.
        Public Property Cyls As List(Of Integer)
        Public Property Heads As List(Of Integer)
        ' HOff: per-slot Nothing ⇒ user did not supply h{idx}.off.
        Public Property HOff As Integer?()
        ' [Step] / Hswap: Nothing ⇒ user did not supply this key.
        Public Property [Step] As Integer?
        Public Property Hswap As Boolean?

        ' Raw spec text accumulated across UpdateFromTrackspec calls. Useful
        ' for diagnostics; not consumed by Resolve (which works directly on
        ' the typed fields).
        Public ReadOnly Property Trackspec As String
            Get
                Return _trackspec
            End Get
        End Property
        Private _trackspec As String

        ' Folds key=value segments from `spec` into this TrackSetSpec. Only
        ' keys that appear in `spec` mutate fields; everything else is left
        ' Nothing / empty. Throws ArgumentException for malformed input,
        ' matching TrackSet's parse semantics so existing argparse-style
        ' error reporting in the CLI still works without changes.
        '
        ' Python map: src/greaseweazle/tools/util.py::TrackSet.update_from_trackspec
        Public Sub UpdateFromTrackspec(spec As String)
            If spec Is Nothing Then Return
            _trackspec &= spec
            For Each part In spec.Split(":"c)
                If part = "hswap" Then
                    Hswap = True
                    Continue For
                End If

                Dim kv = part.Split("="c)
                If kv.Length <> 2 Then
                    Throw New ArgumentException("Invalid track specification segment.")
                End If

                Dim key = kv(0)
                Dim value = kv(1)
                Select Case key
                    Case "c"
                        Cyls = ParseCylinderSet(value)
                    Case "h"
                        Heads = ParseHeadSet(value)
                    Case "h0.off", "h1.off"
                        Dim idx = If(key.StartsWith("h0", StringComparison.Ordinal), 0, 1)
                        If Not Regex.IsMatch(value, "^[+-]\d+$") Then
                            Throw New ArgumentException("Invalid head offset.")
                        End If
                        HOff(idx) = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                    Case "step"
                        Dim fractional = Regex.Match(value, "^1/(\d+)$")
                        If fractional.Success Then
                            [Step] = -Integer.Parse(fractional.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
                        Else
                            [Step] = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                        End If
                    Case Else
                        Throw New ArgumentException(String.Format("Unknown track spec key '{0}'.", key))
                End Select
            Next
        End Sub

        ' Folds the user's intent onto a fully-resolved defaults TrackSet,
        ' returning a brand-new fully-resolved TrackSet. Each field is taken
        ' from `Me` if set, otherwise from `defaults`.
        '
        ' Mirrors Python's `def_tracks.update_from_trackspec(args.tracks.trackspec)`
        ' fold without re-parsing a string — we work directly on the typed
        ' partial-intent fields.
        Public Function Resolve(defaults As TrackSet) As TrackSet
            If defaults Is Nothing Then
                Throw New ArgumentNullException(NameOf(defaults))
            End If

            ' Clone defaults via spec round-trip so the returned TrackSet's
            ' internal invariants (Trackspec accumulator, list ownership) are
            ' identical to a normally-constructed instance.
            Dim result As New TrackSet(defaults.ToString())

            If Cyls IsNot Nothing AndAlso Cyls.Count > 0 Then
                result.Cyls = New List(Of Integer)(Cyls)
            End If
            If Heads IsNot Nothing AndAlso Heads.Count > 0 Then
                result.Heads = New List(Of Integer)(Heads)
            End If
            For idx = 0 To 1
                If HOff(idx).HasValue Then
                    result.HOff(idx) = HOff(idx).Value
                End If
            Next
            If [Step].HasValue Then
                result.[Step] = [Step].Value
            End If
            If Hswap.HasValue Then
                result.Hswap = Hswap.Value
            End If
            Return result
        End Function

        ' Renders the user-supplied keys back to spec form. Unspecified fields
        ' are omitted (in contrast to TrackSet.ToString, which always emits
        ' c= and h= because they're always populated on a resolved set).
        Public Overrides Function ToString() As String
            Dim parts As New List(Of String)()
            If Cyls IsNot Nothing AndAlso Cyls.Count > 0 Then
                parts.Add(String.Format("c={0}", RangeToString(Cyls)))
            End If
            If Heads IsNot Nothing AndAlso Heads.Count > 0 Then
                parts.Add(String.Format("h={0}", RangeToString(Heads)))
            End If
            For idx = 0 To 1
                If HOff(idx).HasValue Then
                    Dim offset = HOff(idx).Value
                    parts.Add(String.Format("h{0}.off={1}{2}", idx, If(offset >= 0, "+", String.Empty), offset))
                End If
            Next
            If [Step].HasValue Then
                Dim s = [Step].Value
                parts.Add("step=" & If(s < 0, String.Format("1/{0}", -s), s.ToString(Globalization.CultureInfo.InvariantCulture)))
            End If
            If Hswap.HasValue AndAlso Hswap.Value Then
                parts.Add("hswap")
            End If
            Return String.Join(":", parts)
        End Function

        ' Parser helpers below mirror TrackSet's private helpers byte-for-byte.
        ' Duplicated rather than shared because they are tightly coupled to
        ' the parse exception messages that the CLI argparse-shim relies on.
        Private Shared Function ParseCylinderSet(value As String) As List(Of Integer)
            Dim values As New HashSet(Of Integer)()
            For Each token In value.Split(","c)
                Dim m = Regex.Match(token, "^(\d+)(-(\d+)(/(\d+))?)?$")
                If Not m.Success Then
                    Throw New ArgumentException("Invalid cylinder range.")
                End If
                Dim s = Integer.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
                Dim e = If(String.IsNullOrEmpty(m.Groups(3).Value), s, Integer.Parse(m.Groups(3).Value, Globalization.CultureInfo.InvariantCulture))
                Dim stepValue = If(String.IsNullOrEmpty(m.Groups(5).Value), 1, Integer.Parse(m.Groups(5).Value, Globalization.CultureInfo.InvariantCulture))
                For c = s To e Step stepValue
                    values.Add(c)
                Next
            Next
            Return values.OrderBy(Function(x) x).ToList()
        End Function

        Private Shared Function ParseHeadSet(value As String) As List(Of Integer)
            Dim selected = New Boolean() {False, False}
            For Each token In value.Split(","c)
                Dim m = Regex.Match(token, "^([01])(-([01]))?$")
                If Not m.Success Then
                    Throw New ArgumentException("Invalid head range.")
                End If
                Dim s = Integer.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
                Dim e = If(String.IsNullOrEmpty(m.Groups(3).Value), s, Integer.Parse(m.Groups(3).Value, Globalization.CultureInfo.InvariantCulture))
                For h = s To e
                    selected(h) = True
                Next
            Next
            Dim result As New List(Of Integer)()
            For i = 0 To selected.Length - 1
                If selected(i) Then
                    result.Add(i)
                End If
            Next
            Return result
        End Function

        Private Shared Function RangeToString(values As IList(Of Integer)) As String
            If values.Count = 0 Then
                Return "<none>"
            End If
            Dim ranges As New List(Of String)()
            Dim start = values(0)
            Dim finish = values(0)
            For i = 1 To values.Count - 1
                Dim value = values(i)
                If value = finish + 1 Then
                    finish = value
                    Continue For
                End If
                ranges.Add(If(start = finish, start.ToString(), String.Format("{0}-{1}", start, finish)))
                start = value
                finish = value
            Next
            ranges.Add(If(start = finish, start.ToString(), String.Format("{0}-{1}", start, finish)))
            Return String.Join(",", ranges)
        End Function

    End Class

End Namespace
