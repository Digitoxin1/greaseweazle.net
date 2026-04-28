Namespace Greaseweazle.Core

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration ErrorHandling)
    Public NotInheritable Class ErrorHandling

        ' Python map: src/greaseweazle/error.py::(no direct 1:1 symbol; VB utility class constructor)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/error.py::check
        Public Shared Sub Check(predicate As Boolean, description As String)
            If Not predicate Then
                Throw New FatalException(description)
            End If
        End Sub

    End Class

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration FatalException)
    Public Class FatalException
        Inherits Exception

        ' Python map: src/greaseweazle/error.py::Fatal.__init__
        Public Sub New(message As String)
            MyBase.New(message)
        End Sub
    End Class

    ' Python map: src/greaseweazle/error.py::Fatal
    Public Class Fatal
        Inherits FatalException

        ' Python map: src/greaseweazle/error.py::Fatal.__init__
        Public Sub New(message As String)
            MyBase.New(message)
        End Sub
    End Class

End Namespace
