Imports System.Text.RegularExpressions

Namespace Greaseweazle.Shared

    ' Python map: src/greaseweazle/tools/util.py::TrackSet
    Public Class TrackSet
        Implements IEnumerable(Of TrackIter)

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.__init__
        Public Sub New(trackSpecValue As String)
            Cyls = New List(Of Integer)()
            Heads = New List(Of Integer)()
            HOff = New Integer() {0, 0}
            [Step] = 1
            Hswap = False
            Me.Trackspec = String.Empty
            UpdateFromTrackspec(trackSpecValue)
        End Sub

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.cyls
        Public Property Cyls As List(Of Integer)
        ' Python map: src/greaseweazle/tools/util.py::TrackSet.heads
        Public Property Heads As List(Of Integer)
        ' Python map: src/greaseweazle/tools/util.py::TrackSet.h_off
        Public Property HOff As Integer()
        ' Python map: src/greaseweazle/tools/util.py::TrackSet.step
        Public Property [Step] As Integer
        ' Python map: src/greaseweazle/tools/util.py::TrackSet.hswap
        Public Property Hswap As Boolean
        ' Python map: src/greaseweazle/tools/util.py::TrackSet.trackspec
        Public Property Trackspec As String

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.ch_to_pch
        Public Function ChToPch(cyl As Integer, head As Integer) As Tuple(Of Integer, Integer)
            ' Python uses // (floor division). VB '\' truncates toward zero, which
            ' diverges from Python for negative cylinders. Use floor-division parity.
            Dim physicalCyl As Integer
            If [Step] < 0 Then
                physicalCyl = FloorDiv(cyl, -[Step])
            Else
                physicalCyl = cyl * [Step]
            End If
            physicalCyl += HOff(head)
            Dim physicalHead = If(Hswap, 1 - head, head)
            Return Tuple.Create(physicalCyl, physicalHead)
        End Function

        ' Python map: src/greaseweazle/tools/util.py::(no direct 1:1 symbol; VB helper for Python `//` semantics)
        Private Shared Function FloorDiv(numerator As Integer, denominator As Integer) As Integer
            Dim q = numerator \ denominator
            Dim r = numerator Mod denominator
            If (r <> 0) AndAlso ((r < 0) Xor (denominator < 0)) Then
                q -= 1
            End If
            Return q
        End Function

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.update_from_trackspec
        Public Sub UpdateFromTrackspec(trackSpecValue As String)
            Me.Trackspec &= trackSpecValue
            For Each part In trackSpecValue.Split(":"c)
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

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.__contains__
        Public Function Contains(cyl As Integer, head As Integer) As Boolean
            Return Cyls.Contains(cyl) AndAlso Heads.Contains(head)
        End Function

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.__contains__
        Public Function Contains(key As Tuple(Of Integer, Integer)) As Boolean
            If key Is Nothing Then
                Throw New ArgumentNullException(NameOf(key))
            End If
            Return Contains(key.Item1, key.Item2)
        End Function

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.TrackIter.__next__
        Public Iterator Function IteratePhysical() As IEnumerable(Of TrackIter)
            Dim all As New List(Of TrackIter)()
            For Each c In Cyls
                For Each h In Heads
                    Dim mapped = ChToPch(c, h)
                    all.Add(New TrackIter(mapped.Item1, mapped.Item2, c, h))
                Next
            Next

            For Each entry In all.OrderBy(Function(x) x.PhysicalCyl).ThenBy(Function(x) x.PhysicalHead)
                Yield entry
            Next
        End Function

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.__iter__
        Public Function GetEnumerator() As IEnumerator(Of TrackIter) Implements IEnumerable(Of TrackIter).GetEnumerator
            Return IteratePhysical().GetEnumerator()
        End Function

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.__iter__
        Private Function GetEnumeratorNonGeneric() As IEnumerator Implements IEnumerable.GetEnumerator
            Return IteratePhysical().GetEnumerator()
        End Function

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.__str__
        Public Overrides Function ToString() As String
            Dim value = String.Format("c={0}:h={1}",
                                      RangeToString(Cyls),
                                      RangeToString(Heads))
            For idx = 0 To HOff.Length - 1
                Dim offset = HOff(idx)
                If offset <> 0 Then
                    value &= String.Format(":h{0}.off={1}{2}", idx, If(offset >= 0, "+", String.Empty), offset)
                End If
            Next
            If [Step] <> 1 Then
                value &= ":step="
                value &= If([Step] < 0, String.Format("1/{0}", -[Step]), [Step].ToString(Globalization.CultureInfo.InvariantCulture))
            End If
            If Hswap Then
                value &= ":hswap"
            End If
            Return value
        End Function

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.update_from_trackspec
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

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.update_from_trackspec
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

        ' Python map: src/greaseweazle/tools/util.py::range_str
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

    ' Python map: src/greaseweazle/tools/util.py::TrackSet.TrackIter
    Public Class TrackIter
        ' Python map: src/greaseweazle/tools/util.py::TrackSet.TrackIter.__init__
        Public Sub New(physicalCyl As Integer, physicalHead As Integer, cyl As Integer, head As Integer)
            Me.PhysicalCyl = physicalCyl
            Me.PhysicalHead = physicalHead
            Me.Cyl = cyl
            Me.Head = head
        End Sub

        ' Python map: src/greaseweazle/tools/util.py::(no direct 1:1 symbol; VB default constructor for serializer/initializer support)
        Public Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/util.py::TrackSet.TrackIter.__next__.physical_cyl
        Public Property PhysicalCyl As Integer
        ' Python map: src/greaseweazle/tools/util.py::TrackSet.TrackIter.__next__ (no direct 1:1 symbol; VB projected physical head)
        Public Property PhysicalHead As Integer
        ' Python map: src/greaseweazle/tools/util.py::TrackSet.TrackIter.__next__.cyl
        Public Property Cyl As Integer
        ' Python map: src/greaseweazle/tools/util.py::TrackSet.TrackIter.__next__ (no direct 1:1 symbol; VB logical head)
        Public Property Head As Integer
    End Class

End Namespace
