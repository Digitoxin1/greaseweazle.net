Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports System.IO

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/image.py::ImageOpts
    Public Class ImageOpts

        Public Property ReadSettings As New List(Of String)()
        Public Property WriteSettings As New List(Of String)()
        Public Property SharedSettings As New List(Of String)()

        ' Python map: src/greaseweazle/image/image.py::ImageOpts.r_set
        Public Sub RSet(fileName As String, opt As String, value As String)
            [Set](fileName, opt, value, SharedSettings.Concat(ReadSettings))
        End Sub

        ' Python map: src/greaseweazle/image/image.py::ImageOpts.w_set
        Public Sub WSet(fileName As String, opt As String, value As String)
            [Set](fileName, opt, value, SharedSettings.Concat(WriteSettings))
        End Sub

        ' Python map: src/greaseweazle/image/image.py::ImageOpts._set
        Private Sub [Set](fileName As String, opt As String, value As String, allowed As IEnumerable(Of String))
            Dim all = allowed.ToList()
            ErrorHandling.Check(all.Contains(opt),
                                String.Format("{0}: Invalid file option: {1}{2}Valid options: {3}",
                                              fileName,
                                              opt,
                                              vbLf,
                                              If(all.Count = 0, "<none>", String.Join(", ", all))))
            Values(opt) = value
        End Sub

        ' Python: ImageOpts uses setattr with case-sensitive option names; mirror with
        ' an ordinal (case-sensitive) comparer rather than OrdinalIgnoreCase.
        Public ReadOnly Property Values As New Dictionary(Of String, String)(StringComparer.Ordinal)
    End Class

    ' Python map: src/greaseweazle/image/image.py::Image
    Public MustInherit Class Image

        Public Property FileName As String
        Public Property NoClobber As Boolean
        Public Property DefaultFormat As String
        Public Property [ReadOnly] As Boolean
        Public Property WriteOnCtrlC As Boolean
        Public Property Options As ImageOpts = New ImageOpts()
        Private _openFile As FileStream

        ' Python map: src/greaseweazle/image/image.py::Image.__init__
        Protected Sub New(Optional name As String = "")
            FileName = name
        End Sub

        ' Python map: src/greaseweazle/image/image.py::Image.apply_r_opts
        Public Sub ApplyROpts(opts As IDictionary(Of String, String))
            If opts Is Nothing Then
                Return
            End If
            For Each pair In opts
                Options.RSet(FileName, pair.Key, pair.Value)
            Next
        End Sub

        ' Python map: src/greaseweazle/image/image.py::Image.apply_w_opts
        Public Sub ApplyWOpts(opts As IDictionary(Of String, String))
            If opts Is Nothing Then
                Return
            End If
            For Each pair In opts
                Options.WSet(FileName, pair.Key, pair.Value)
            Next
        End Sub

        ' Python map: src/greaseweazle/image/image.py::Image.__enter__
        Public Overridable Function Enter() As Image
            Dim mode = If(NoClobber, FileMode.CreateNew, FileMode.Create)
            _openFile = New FileStream(FileName, mode, FileAccess.Write, FileShare.None)
            Return Me
        End Function

        ' Python map: src/greaseweazle/image/image.py::Image.__exit__
        Public Overridable Sub [Exit](exceptionType As Type, value As Exception)
            Dim isKeyboardInterrupt = exceptionType IsNot Nothing AndAlso
                                      String.Equals(exceptionType.Name, "KeyboardInterruptException", StringComparison.Ordinal)
            Dim save = (exceptionType Is Nothing) OrElse
                       (isKeyboardInterrupt AndAlso WriteOnCtrlC)
            Try
                If save AndAlso _openFile IsNot Nothing Then
                    Dim bytes = GetImage()
                    _openFile.Write(bytes, 0, bytes.Length)
                End If
            Finally
                If _openFile IsNot Nothing Then
                    _openFile.Dispose()
                    _openFile = Nothing
                End If
            End Try
            If Not save AndAlso File.Exists(FileName) Then
                File.Delete(FileName)
            End If
        End Sub

        ' Python map: src/greaseweazle/image/image.py::Image.to_file
        Public Shared Function ToFile(Of T As {Image, New})(name As String,
                                                                       format As DiskDef,
                                                                       noClobber As Boolean,
                                                                       opts As IDictionary(Of String, String)) As T
            Dim image = New T()
            ErrorHandling.Check(Not image.ReadOnly,
                                String.Format("{0}: Cannot create {1} image files",
                                              name,
                                              GetType(T).Name))
            image.FileName = name
            AssignFormat(image, format)
            image.NoClobber = noClobber
            image.ApplyWOpts(opts)
            Return image
        End Function

        ' Python map: src/greaseweazle/image/image.py::Image.from_file
        Public Shared Function FromFile(Of T As {Image, New})(name As String,
                                                                      format As DiskDef,
                                                                      opts As IDictionary(Of String, String)) As T
            Dim image = New T()
            image.FileName = name
            AssignFormat(image, format)
            image.ApplyROpts(opts)
            image.FromBytes(IO.File.ReadAllBytes(name))
            Return image
        End Function

        ' Python map: src/greaseweazle/image/image.py::Image.from_file (pass fmt argument to class constructor equivalent)
        Private Shared Sub AssignFormat(image As Image, format As DiskDef)
            If image Is Nothing OrElse format Is Nothing Then
                Return
            End If

            Dim formatProp = image.GetType().GetProperty("Format",
                                                         Reflection.BindingFlags.Instance Or Reflection.BindingFlags.Public)
            If formatProp IsNot Nothing AndAlso
               formatProp.CanWrite AndAlso
               formatProp.PropertyType.IsAssignableFrom(GetType(DiskDef)) Then
                formatProp.SetValue(image, format, Nothing)
            End If
        End Sub

        ' Python map: src/greaseweazle/image/image.py::Image.max_cylinder
        Public Function MaxCylinder() As List(Of Integer)
            Dim result As New List(Of Integer)()
            For head = 0 To 1
                For cyl = 100 To -1 Step -1
                    If cyl < 0 OrElse GetTrack(cyl, head) IsNot Nothing Then
                        result.Add(cyl)
                        Exit For
                    End If
                Next
            Next
            Return result
        End Function

        ' Python map: src/greaseweazle/image/image.py::Image.from_bytes
        Public MustOverride Sub FromBytes(data As Byte())
        ' Python map: src/greaseweazle/image/image.py::Image.get_track
        Public MustOverride Function GetTrack(cyl As Integer, side As Integer) As HasFlux
        ' Python map: src/greaseweazle/image/image.py::Image.emit_track
        Public MustOverride Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
        ' Python map: src/greaseweazle/image/image.py::Image.get_image
        Public MustOverride Function GetImage() As Byte()
    End Class

End Namespace
