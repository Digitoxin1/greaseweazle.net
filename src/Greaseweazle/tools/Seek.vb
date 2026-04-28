Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `seek` action. Pure POCO - the CLI's
    ' SeekOptionsParser produces this from argv. PromptNeeded signals the
    ' library should ask the supplied ISeekPrompter to confirm before
    ' moving the head; the prompt's wording is owned by the prompter
    ' implementation (so the library carries no user-facing text).
    Public Class SeekOptions
        Public Property Cyl As Integer
        Public Property MotorOn As Boolean
        Public Property Force As Boolean
        Public Property PromptNeeded As Boolean
        Public Property Live As Boolean = True
        Public Property Device As String
        Public Property Drive As DriveSpec
    End Class

    ' Python map: src/greaseweazle/tools/seek.py (direct command-algorithm parity mapping).
    Public NotInheritable Class Seek

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/seek.py::seek
        Public Shared Sub Seek(usbClient As Unit, cyl As Integer)
            usbClient.Seek(cyl, 0)
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration IsExtremeCylinder)
        Public Shared Function IsExtremeCylinder(cyl As Integer) As Boolean
            Return cyl < 0 OrElse cyl > 83
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ShouldPromptForExtremeCylinder)
        Public Shared Function ShouldPromptForExtremeCylinder(cyl As Integer, force As Boolean) As Boolean
            Return IsExtremeCylinder(cyl) AndAlso Not force
        End Function

    End Class

End Namespace
