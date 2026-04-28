Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure
Imports System.IO

Namespace Greaseweazle.Tools

    ' Python map: no-1:1 with Python symbols; this DTO captures parsed clean runtime state.
    Public Class CleanRuntimePreview
        Public Property Cyls As Integer
        Public Property PassLines As List(Of String)
        Public Property Sequences As List(Of List(Of Integer))
        Public Property LingerMs As Integer
        Public Property Live As Boolean
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
        Public Shared Function Seek(cyl As Integer, usbClient As Unit, preview As CleanRuntimePreview, output As TextWriter) As Integer
            Dim clamped = ClampSeekCylinder(cyl, preview.Cyls)
            output.Write(String.Format(Globalization.CultureInfo.InvariantCulture, "{0} ", clamped))
            usbClient.Seek(clamped, 0)
            Return clamped
        End Function

        ' Python map: src/greaseweazle/tools/clean.py::clean
        Public Shared Sub Clean(usbClient As Unit, preview As CleanRuntimePreview, output As TextWriter)
            For p = 0 To preview.Sequences.Count - 1
                output.Write(String.Format(Globalization.CultureInfo.InvariantCulture, "Pass {0}: ", p))
                For Each cyl In preview.Sequences(p)
                    Seek(cyl, usbClient, preview, output)
                    Threading.Thread.Sleep(preview.LingerMs)
                Next
                output.WriteLine()
            Next
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildRuntimePreview)
        Public Shared Function BuildRuntimePreview(args As IReadOnlyList(Of String)) As CleanRuntimePreview
            Dim cyls = 80
            Dim passes = 3
            Dim linger = 100
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
                    Case "--cyls"
                        cyls = Integer.Parse(TakeOptionValue(args, i, token, inlineValue), Globalization.CultureInfo.InvariantCulture)
                    Case "--passes"
                        passes = Integer.Parse(TakeOptionValue(args, i, token, inlineValue), Globalization.CultureInfo.InvariantCulture)
                    Case "--linger"
                        linger = Integer.Parse(TakeOptionValue(args, i, token, inlineValue), Globalization.CultureInfo.InvariantCulture)
                    Case "--device", "--drive"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--device", StringComparison.Ordinal) Then
                            device = optionValue
                        Else
                            driveToken = optionValue
                        End If
                    Case "--test"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        live = False
                    Case Else
                        If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                            Throw New FatalException(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            If positionals.Count > 0 Then
                Throw New FatalException(String.Format("unrecognized arguments: {0}", String.Join(" ", positionals)))
            End If
            ErrorHandling.Check(cyls >= 0, "--cyls must be >= 0")
            ErrorHandling.Check(passes >= 0, "--passes must be >= 0")
            ErrorHandling.Check(linger >= 0, "--linger must be >= 0")

            Dim lines As New List(Of String)()
            Dim sequences = BuildPassSequences(cyls, passes)
            For p = 0 To sequences.Count - 1
                Dim suffix = If(sequences(p).Count = 0, "", String.Join(" ", sequences(p)) & " ")
                lines.Add(String.Format("Pass {0}: {1}", p, suffix))
            Next
            Return New CleanRuntimePreview With {
                .Cyls = cyls,
                .PassLines = lines,
                .Sequences = sequences,
                .LingerMs = linger,
                .Live = live,
                .Device = device,
                .Drive = ResolveDrive(driveToken)
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

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveDrive)
        Private Shared Function ResolveDrive(token As String) As DriveSpec
            Try
                Return ToolOptions.Drive(token)
            Catch ex As ArgumentException
                Throw New FatalException(ex.Message)
            End Try
        End Function

    End Class

End Namespace
