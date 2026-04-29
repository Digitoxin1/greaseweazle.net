Namespace Greaseweazle.Core

    ' Small Byte()-shaped utilities shared across the codec, image, and
    ' track layers.
    '
    ' Lives in `error/` next to HostVersion / CollectionHelpers because the
    ' folder is the de-facto home for "library-wide utility" code; the
    ' filename is just a folder convention and doesn't imply error handling.
    Public NotInheritable Class ByteArrayHelpers

        Private Sub New()
        End Sub

        ' Allocate a fresh Byte() containing source[offset..offset+length).
        ' Equivalent to source.Skip(offset).Take(length).ToArray() but uses
        ' Array.Copy (O(length)) instead of Enumerable.Skip (O(offset+length))
        ' and skips the per-element LINQ iterator overhead.
        Public Shared Function SubArray(source As Byte(), offset As Integer, length As Integer) As Byte()
            If length = 0 Then Return New Byte() {}
            Dim result(length - 1) As Byte
            Array.Copy(source, offset, result, 0, length)
            Return result
        End Function

        ' Allocate a fresh Byte() of `count` elements all set to `value`.
        ' Equivalent to Enumerable.Repeat(value, count).ToArray() with no
        ' iterator allocation. Skips the fill loop entirely when `value`
        ' is zero, since Byte() is zero-initialised by the runtime.
        Public Shared Function RepeatByte(value As Byte, count As Integer) As Byte()
            If count <= 0 Then Return New Byte() {}
            Dim result(count - 1) As Byte
            If value <> 0 Then
                For i = 0 To count - 1
                    result(i) = value
                Next
            End If
            Return result
        End Function

    End Class

End Namespace
