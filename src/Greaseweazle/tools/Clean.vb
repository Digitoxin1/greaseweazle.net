Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `clean` action.
    Public Class CleanOptions
        Public Property Cyls As Integer
        Public Property PassLines As List(Of String)
        Public Property Sequences As List(Of List(Of Integer))
        Public Property LingerMs As Integer
        Public Property Live As Boolean = True
        Public Property Device As String
        Public Property Drive As DriveSpec
    End Class

    ' Python map: src/greaseweazle/tools/clean.py (direct command-algorithm parity mapping).
    Public NotInheritable Class Clean

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ComputeStep)
        Public Shared Function ComputeStep(cyls As Integer) As Integer
            Return Math.Max(cyls \ 8, 2)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ClampSeekCylinder)
        Public Shared Function ClampSeekCylinder(cyl As Integer, cyls As Integer) As Integer
            Return Math.Min(cyl, cyls - 1)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildPassSequences)
        Public Shared Function BuildPassSequences(cyls As Integer, passes As Integer) As List(Of List(Of Integer))
            Dim stepValue = ComputeStep(cyls)
            Dim output As New List(Of List(Of Integer))()
            For p = 0 To passes - 1
                Dim sequence As New List(Of Integer)()
                For cyl = 0 To cyls - 1 Step stepValue
                    sequence.Add(ClampSeekCylinder(cyl + stepValue - 1, cyls))
                    sequence.Add(ClampSeekCylinder(cyl, cyls))
                Next
                output.Add(sequence)
            Next
            Return output
        End Function

        ' Python map: src/greaseweazle/tools/clean.py::seek
        '
        ' Issues a single seek (clamped to the configured cylinder range)
        ' and returns the clamped cylinder so the caller can report it
        ' however it sees fit (the live CLI renders it inline; library
        ' consumers may push it onto a progress queue).
        Public Shared Function Seek(cyl As Integer, usbClient As Unit, preview As CleanOptions) As Integer
            Dim clamped = ClampSeekCylinder(cyl, preview.Cyls)
            usbClient.Seek(clamped, 0)
            Return clamped
        End Function

        ' Python map: src/greaseweazle/tools/clean.py::clean
        '
        ' Walks the planned pass/cylinder schedule. The three callbacks
        ' fire in pass order so a typed event subscriber can render
        ' Python's "Pass N: 0 1 2 ..." line piece-by-piece without this
        ' routine producing any text itself. Returns the total number
        ' of clamped cylinder visits performed.
        Public Shared Function Clean(usbClient As Unit,
                                     preview As CleanOptions,
                                     onPassStarted As Action(Of Integer),
                                     onCylinderSeeked As Action(Of Integer, Integer),
                                     onPassCompleted As Action(Of Integer)) As Integer
            Dim visited = 0
            For p = 0 To preview.Sequences.Count - 1
                If onPassStarted IsNot Nothing Then onPassStarted(p)
                For Each cyl In preview.Sequences(p)
                    Dim clamped = Seek(cyl, usbClient, preview)
                    If onCylinderSeeked IsNot Nothing Then onCylinderSeeked(p, clamped)
                    visited += 1
                    Threading.Thread.Sleep(preview.LingerMs)
                Next
                If onPassCompleted IsNot Nothing Then onPassCompleted(p)
            Next
            Return visited
        End Function

    End Class

End Namespace
