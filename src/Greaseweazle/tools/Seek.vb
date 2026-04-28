Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `seek` action.
    Public Class SeekOptions
        Public Property Cyl As Integer
        Public Property MotorOn As Boolean
        Public Property Force As Boolean
        Public Property PromptNeeded As Boolean
        Public Property PromptText As String
        Public Property Live As Boolean = True
        Public Property Device As String
        Public Property Drive As DriveSpec

        Public Shared Function FromArgs(args As IReadOnlyList(Of String)) As SeekOptions
            Return Seek.BuildRuntimePreview(args)
        End Function
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

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetExtremeCylinderPrompt)
        Public Shared Function GetExtremeCylinderPrompt(cyl As Integer) As String
            Return String.Format("Seek to extreme cylinder {0}, Yes/No? ", cyl)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildRuntimePreview)
        Public Shared Function BuildRuntimePreview(args As IReadOnlyList(Of String)) As SeekOptions
            Dim force = False
            Dim motorOn = False
            Dim live = True
            Dim device As String = Nothing
            Dim driveToken = "A"
            Dim positionals As New List(Of String)()

            Dim i = 0
            While i < args.Count
                Dim rawToken = args(i)
                If String.Equals(rawToken, "--", StringComparison.Ordinal) Then
                    For j = i To args.Count - 1
                        positionals.Add(args(j))
                    Next
                    Exit While
                End If
                Dim token = rawToken
                Dim inlineValue As String = Nothing
                Dim equalsIndex = rawToken.IndexOf("="c)
                If rawToken.StartsWith("--", StringComparison.Ordinal) AndAlso equalsIndex > 2 Then
                    token = rawToken.Substring(0, equalsIndex)
                    inlineValue = rawToken.Substring(equalsIndex + 1)
                End If
                Select Case token
                    Case "--force"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        force = True
                    Case "--motor-on"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        motorOn = True
                    Case "--test"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        live = False
                    Case "--device", "--drive"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--device", StringComparison.Ordinal) Then
                            device = optionValue
                        Else
                            driveToken = optionValue
                        End If
                    Case Else
                        If rawToken.StartsWith("-", StringComparison.Ordinal) AndAlso
                           Not IsSignedIntegerToken(rawToken) Then
                            Throw New FatalException(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            ErrorHandling.Check(positionals.Count >= 1, "seek requires cylinder argument")
            ErrorHandling.Check(positionals.Count = 1, "seek takes exactly one cylinder argument")
            Dim cyl = ParseUint(positionals(0), "cylinder")
            Dim promptNeeded = ShouldPromptForExtremeCylinder(cyl, force)
            Dim drive As DriveSpec
            Try
                drive = ToolOptions.Drive(driveToken)
            Catch ex As ArgumentException
                Throw New FatalException(ex.Message)
            End Try

            Return New SeekOptions With {
                .Cyl = cyl,
                .Force = force,
                .MotorOn = motorOn,
                .PromptNeeded = promptNeeded,
                .PromptText = If(promptNeeded, GetExtremeCylinderPrompt(cyl), Nothing),
                .Live = live,
                .Device = device,
                .Drive = drive
            }
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration CheckOptionValue)
        Private Shared Sub CheckOptionValue(args As IReadOnlyList(Of String), index As Integer, optionName As String)
            Dim hasValue = index < args.Count
            If hasValue Then
                Dim value = args(index)
                If value.StartsWith("--", StringComparison.Ordinal) Then
                    hasValue = False
                End If
            End If
            ErrorHandling.Check(hasValue, String.Format("missing value for option {0}", optionName))
        End Sub

        Private Shared Function TakeOptionValue(args As IReadOnlyList(Of String),
                                                ByRef index As Integer,
                                                optionName As String,
                                                inlineValue As String) As String
            If inlineValue IsNot Nothing Then
                Return inlineValue
            End If
            index += 1
            CheckOptionValue(args, index, optionName)
            Return args(index)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ParseUint)
        Private Shared Function ParseUint(value As String, fieldName As String) As Integer
            Dim parsed As Integer
            If Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) OrElse parsed < 0 Then
                Throw New FatalException(String.Format("invalid {0}: {1}", fieldName, value))
            End If
            Return parsed
        End Function

        Private Shared Function IsSignedIntegerToken(value As String) As Boolean
            Dim parsed As Integer
            Return Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed)
        End Function

    End Class

End Namespace
