Namespace Greaseweazle.Actions

    ' Raised at the start of each clean pass, before any cylinder is
    ' visited in that pass. Subscribers render Python's "Pass {N}: "
    ' prefix from PassIndex.
    Public NotInheritable Class CleanPassStartedEventArgs
        Inherits EventArgs

        Public Sub New(passIndex As Integer)
            Me.PassIndex = passIndex
        End Sub

        Public ReadOnly Property PassIndex As Integer

    End Class

    ' Raised after each cylinder is reached during a pass — in live mode
    ' this is right after the USB seek, in dry-run mode it fires
    ' synchronously per planned cylinder. Cylinder is the *clamped*
    ' cylinder actually visited (matches Python's behaviour where
    ' `min(cyl, cyls-1)` is logged, not the requested target).
    Public NotInheritable Class CleanCylinderEventArgs
        Inherits EventArgs

        Public Sub New(passIndex As Integer, cylinder As Integer)
            Me.PassIndex = passIndex
            Me.Cylinder = cylinder
        End Sub

        Public ReadOnly Property PassIndex As Integer
        Public ReadOnly Property Cylinder As Integer

    End Class

    ' Raised at the end of each clean pass, once every planned cylinder
    ' has been visited. Subscribers render the trailing newline.
    Public NotInheritable Class CleanPassCompletedEventArgs
        Inherits EventArgs

        Public Sub New(passIndex As Integer)
            Me.PassIndex = passIndex
        End Sub

        Public ReadOnly Property PassIndex As Integer

    End Class

End Namespace
