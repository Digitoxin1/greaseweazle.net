Namespace Greaseweazle.Core

    ' Small generic collection helpers shared between the codec, image, and
    ' track layers. Lives next to HostVersion in `error/` because that's
    ' where the namespace's other "library-wide utility" lives; the file
    ' isn't error-handling related.
    Public NotInheritable Class CollectionHelpers

        Private Sub New()
        End Sub

        ' Return a new List(Of T) containing values rotated left by `index`
        ' positions. Equivalent to `values[index:] + values[:index]` in
        ' Python, with the index normalized into [0, count).
        '
        ' Was duplicated as private helpers in HFEImage and CAPSImage; both
        ' previously implemented the same Skip/Concat-then-ToList LINQ
        ' chain, then both got rewritten to a pre-sized List build at the
        ' same time. Now there's just one home.
        Public Shared Function RotateList(Of T)(values As IList(Of T), index As Integer) As List(Of T)
            If values Is Nothing OrElse values.Count = 0 Then
                Return New List(Of T)()
            End If
            Dim n = values.Count
            Dim wrapped = ((index Mod n) + n) Mod n
            Dim result As New List(Of T)(n)
            For i = wrapped To n - 1
                result.Add(values(i))
            Next
            For i = 0 To wrapped - 1
                result.Add(values(i))
            Next
            Return result
        End Function

    End Class

End Namespace
