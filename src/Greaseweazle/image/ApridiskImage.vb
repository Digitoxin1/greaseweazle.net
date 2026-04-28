Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports System.Linq

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/apridisk.py::Apridisk
    Public Class Apridisk
        Inherits Img

        Private Const SectorType As UInteger = &HE31D0001UI
        Private Const CompressionRaw As UShort = &H9E90US
        Private Const CompressionRle As UShort = &H3E5AUS

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New(format As DiskDef)
            MyBase.New(format)
        End Sub

        ' Python map: src/greaseweazle/image/apridisk.py::Apridisk.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 128, "Apridisk file missing 128 byte header")
            EnsureFormatBounds()

            Dim payload = data.Skip(128).ToArray()
            Dim records As New List(Of ApridiskRecord)()
            Dim pos = 0
            While pos < payload.Length
                Dim rec = ParseRecord(payload, pos)
                records.Add(rec)
                pos += rec.RecordSize
            End While

            records.Sort(Function(a, b) a.OrderKey.CompareTo(b.OrderKey))
            Dim expanded As New List(Of Byte)()
            For Each rec In records
                expanded.AddRange(rec.ExpandRecord())
            Next

            MyBase.FromBytes(expanded.ToArray())
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration EnsureFormatBounds)
        Private Sub EnsureFormatBounds()
            ErrorHandling.Check(Format IsNot Nothing, "Sector image requires a disk format to be specified")
            ErrorHandling.Check(Format.Tracks IsNot Nothing, "Disk format has no track map")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ParseRecord)
        Private Function ParseRecord(fileData As Byte(), offset As Integer) As ApridiskRecord
            ErrorHandling.Check(offset + 16 <= fileData.Length, "Apridisk record contains no section header")
            ' Python: ApridiskSecType.from_bytes raises Fatal on unknown enum values;
            ' validate first via FromBytes so we surface that error before length checks.
            Dim header = New Byte(3) {}
            Array.Copy(fileData, offset, header, 0, 4)
            Dim secType = ApridiskSecTypeFunctions.FromBytes(header)
            Dim typeValue = BitConverter.ToUInt32(fileData, offset)
            Dim compression = BitConverter.ToUInt16(fileData, offset + 4)
            Dim headerSize = CInt(BitConverter.ToUInt16(fileData, offset + 6))
            Dim dataSize = CInt(BitConverter.ToUInt32(fileData, offset + 8))
            Dim recordSize = headerSize + dataSize
            ErrorHandling.Check(recordSize >= 16, "Apridisk file ends before end of record.")
            ErrorHandling.Check(offset + recordSize <= fileData.Length, "Apridisk file ends before end of record.")

            ErrorHandling.Check(compression = CompressionRaw OrElse compression = CompressionRle,
                                String.Format("Apridisk unknown compression scheme: {0}", compression))

            Dim head = CInt(fileData(offset + 12))
            Dim sector = CInt(fileData(offset + 13))
            Dim cyl = CInt(BitConverter.ToUInt16(fileData, offset + 14))
            ValidatePosition(typeValue, cyl, head, sector)

            Dim dataOffset = offset + headerSize
            Dim dataLen = Math.Max(0, recordSize - headerSize)
            ' Python: self.data = data[self.header_size : self.record_size] (sliced from
            ' the raw record bytes inside the file). Mirror that exactly.
            Dim payload As Byte()
            If dataLen <= 0 Then
                payload = Array.Empty(Of Byte)()
            Else
                payload = New Byte(dataLen - 1) {}
                Array.Copy(fileData, dataOffset, payload, 0, dataLen)
            End If
            Return New ApridiskRecord(typeValue, compression, payload, recordSize, sector + (head * 20) + (cyl * 100))
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration ValidatePosition)
        Private Sub ValidatePosition(typeValue As UInteger, cyl As Integer, head As Integer, sector As Integer)
            Dim maxHeads = If(Format.Tracks.Heads.Count > 0, Format.Tracks.Heads.Max() + 1, 2)
            ErrorHandling.Check(head >= 0 AndAlso head < maxHeads,
                                String.Format("Apridisk heads out of range (max {0}): {1}", maxHeads, "0x" & head.ToString("x")))

            If typeValue = SectorType Then
                ErrorHandling.Check(sector >= 1 AndAlso sector <= 18,
                                    String.Format("Apridisk sector out of range (must be 1-9): {0}", sector))
            End If

            Dim maxCyl = If(Format.Tracks.Cyls.Count > 0, Format.Tracks.Cyls.Max() + 1, 80)
            ErrorHandling.Check(cyl >= 0 AndAlso cyl < maxCyl,
                                String.Format("Apridisk cylinder out of range for chosen format (max {0}): {1}", maxCyl, cyl))
        End Sub

        ' Python map: src/greaseweazle/image/apridisk.py::ApridiskRecord
        Public NotInheritable Class ApridiskRecord
            Public ReadOnly TypeValue As UInteger
            Public ReadOnly Compression As UShort
            Public ReadOnly Payload As Byte()
            Public ReadOnly RecordSize As Integer
            Public ReadOnly OrderKey As Integer

            Private Const SectorTypeValue As UInteger = &HE31D0001UI
            Private Const CompressionRawValue As UShort = &H9E90US
            Private Const CompressionRleValue As UShort = &H3E5AUS

            ' Python map: src/greaseweazle/image/apridisk.py::ApridiskRecord.__init__
            Public Sub New(typeValue As UInteger, compression As UShort, payload As Byte(), recordSize As Integer, orderKey As Integer)
                Me.TypeValue = typeValue
                Me.Compression = compression
                Me.Payload = payload
                Me.RecordSize = recordSize
                Me.OrderKey = orderKey
            End Sub

            ' Python map: src/greaseweazle/image/apridisk.py::ApridiskRecord.split_apridisk_file
            Public Shared Function SplitApridiskFile(fileData As Byte()) As List(Of ApridiskRecord)
                Dim records As New List(Of ApridiskRecord)()
                Dim offset = 0
                Dim src = If(fileData, Array.Empty(Of Byte)())
                While offset + 16 <= src.Length
                    Dim typeValue = BitConverter.ToUInt32(src, offset)
                    Dim compression = BitConverter.ToUInt16(src, offset + 4)
                    Dim headerSize = CInt(BitConverter.ToUInt16(src, offset + 6))
                    Dim dataSize = CInt(BitConverter.ToUInt32(src, offset + 8))
                    Dim recordSize = headerSize + dataSize
                    If recordSize < 16 OrElse offset + recordSize > src.Length Then
                        Exit While
                    End If
                    Dim dataOffset = offset + headerSize
                    Dim dataLen = Math.Max(0, recordSize - headerSize)
                    Dim payload = src.Skip(dataOffset).Take(dataLen).ToArray()
                    records.Add(New ApridiskRecord(typeValue, compression, payload, recordSize, records.Count))
                    offset += recordSize
                End While
                Return records
            End Function

            ' Python map: src/greaseweazle/image/apridisk.py::ApridiskRecord.expand_record
            Public Function ExpandRecord() As IEnumerable(Of Byte)
                If TypeValue <> SectorTypeValue Then
                    Return Enumerable.Empty(Of Byte)()
                End If
                If Compression = CompressionRleValue Then
                    ErrorHandling.Check(Payload.Length = 3,
                                        String.Format("Apridisk compressed section length must be 3, length: {0}", Payload.Length))
                    Dim count = CInt(BitConverter.ToUInt16(Payload, 0))
                    Dim value = Payload(2)
                    Return Enumerable.Repeat(value, count)
                End If
                Return Payload
            End Function

            ' Python map: src/greaseweazle/image/apridisk.py::ApridiskRecord._match_compression
            Private Shared Function MatchCompression(compression As UShort) As Boolean
                Return compression = CompressionRawValue OrElse compression = CompressionRleValue
            End Function

            ' Python map: src/greaseweazle/image/apridisk.py::ApridiskRecord._match_pos
            Private Shared Function MatchPos(cyl As Integer, head As Integer, sector As Integer) As Boolean
                Return cyl >= 0 AndAlso head >= 0 AndAlso sector >= 0
            End Function
        End Class

    End Class

    ' Python map: src/greaseweazle/image/apridisk.py::ApridiskSecType
    Public Enum ApridiskSecType As UInteger
        Deleted = &HE31D0000UI
        Sector = &HE31D0001UI
        Comment = &HE31D0002UI
        Creator = &HE31D0003UI
    End Enum

    Public Module ApridiskSecTypeFunctions
        ' Python map: src/greaseweazle/image/apridisk.py::ApridiskSecType.from_bytes
        Public Function FromBytes(data As Byte()) As ApridiskSecType
            Dim src = If(data, Array.Empty(Of Byte)())
            ErrorHandling.Check(src.Length >= 4,
                                String.Format("Unknown Apridisk sector type: 0x{0:x}", 0))
            Dim typeValue = BitConverter.ToUInt32(src, 0)
            If typeValue = CUInt(ApridiskSecType.Deleted) Then
                Return ApridiskSecType.Deleted
            ElseIf typeValue = CUInt(ApridiskSecType.Sector) Then
                Return ApridiskSecType.Sector
            ElseIf typeValue = CUInt(ApridiskSecType.Comment) Then
                Return ApridiskSecType.Comment
            ElseIf typeValue = CUInt(ApridiskSecType.Creator) Then
                Return ApridiskSecType.Creator
            End If
            Throw New FatalException(String.Format("Unknown Apridisk sector type: 0x{0:x}", typeValue))
        End Function
    End Module

End Namespace
