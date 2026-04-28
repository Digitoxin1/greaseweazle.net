Imports System.IO

Namespace Greaseweazle.Tools

    ' Python map: src/greaseweazle/image/image.py::Image.default_format (per-class attribute).
    ' Python sets `default_format` on each image subclass; `read.py:267-268` and
    ' `write.py:260-261` consult it via `image_class.default_format` after the user
    ' filename is mapped to its image class. We collapse that lookup into an extension
    ' table because the VB image-class lookup is currently fan-out across multiple
    ' tools rather than a single registry.
    Public NotInheritable Class ImageDefaults

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/read.py:267-268 and write.py:260-261
        ' (`if not args.format: args.format = image_class.default_format`).
        Public Shared Function DefaultFormatForFile(fileName As String) As String
            If String.IsNullOrEmpty(fileName) Then
                Return Nothing
            End If
            Return DefaultFormatForExtension(Path.GetExtension(fileName))
        End Function

        ' Per-extension mapping mirroring each Python image module's `default_format`.
        Public Shared Function DefaultFormatForExtension(ext As String) As String
            If String.IsNullOrEmpty(ext) Then
                Return Nothing
            End If
            Select Case ext.ToLowerInvariant()
                Case ".adf"
                    Return "amiga.amigados"             ' image/adf.py
                Case ".ssd"
                    Return "acorn.dfs.ss"               ' image/acorn.py
                Case ".dsd"
                    Return "acorn.dfs.ds"               ' image/acorn.py
                Case ".ads"
                    Return "acorn.adfs.160"             ' image/acorn.py
                Case ".adm"
                    Return "acorn.adfs.320"             ' image/acorn.py
                Case ".adl"
                    Return "acorn.adfs.640"             ' image/acorn.py
                Case ".do"
                    Return "apple2.appledos.140"        ' image/apple2.py
                Case ".po"
                    Return "apple2.prodos.140"          ' image/apple2.py
                Case ".d64"
                    Return "commodore.1541"             ' image/d64.py
                Case ".d71"
                    Return "commodore.1571"             ' image/d64.py
                Case ".d81"
                    Return "commodore.1581"             ' image/d81.py
                Case ".d1m"
                    Return "commodore.cmd.fd2000.dd"    ' image/d81.py
                Case ".d2m"
                    Return "commodore.cmd.fd2000.hd"    ' image/d81.py
                Case ".d4m"
                    Return "commodore.cmd.fd4000.ed"    ' image/d81.py
                Case ".2d"
                    Return "sharp.2d"                   ' image/sharp2d.py
                Case ".fd"
                    Return "thomson.1s320"              ' image/fd.py
                Case ".fdi"
                    Return "pc98.2hd"                   ' image/fdi.py
                Case ".hdm"
                    Return "pc98.2hd"                   ' image/hdm.py
                Case ".xdf"
                    Return "pc98.2hd"                   ' image/xdf.py
                Case ".mgt"
                    Return "ibm.800"                    ' image/mgt.py
                Case ".sf7"
                    Return "sega.sf7000"                ' image/sf7.py
                Case Else
                    Return Nothing
            End Select
        End Function

    End Class

End Namespace
