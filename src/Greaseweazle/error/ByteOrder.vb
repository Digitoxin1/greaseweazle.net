Namespace Greaseweazle.Core

    ' Endian-explicit byte-array readers / writers.
    '
    ' BitConverter.ToUInt16/ToUInt32 are LITTLE-endian on every platform
    ' .NET targets that we care about. The codebase has dozens of call
    ' sites that either:
    '   - call BitConverter directly when the input is little-endian, or
    '   - inline `(b1 << 8) | b2` (BE) / `b1 | (b2 << 8)` (LE) byte-ops
    '     where endianness is not obvious without local context.
    '
    ' Centralising into one module makes the intended endianness a property
    ' of the call (Read*BE vs Read*LE) rather than something the reader has
    ' to infer. Existing per-file helpers (e.g. MSAImage.ReadUInt16BE,
    ' AmigaDosCodec.BytesToUInt32BE / UInt32ToBytesBE) delegate here.
    Public NotInheritable Class ByteOrder

        Private Sub New()
        End Sub

        ' --- Big-endian reads --------------------------------------------------

        Public Shared Function ReadU16BE(data As Byte(), offset As Integer) As UShort
            Return CUShort((CInt(data(offset)) << 8) Or CInt(data(offset + 1)))
        End Function

        Public Shared Function ReadU32BE(data As Byte(), offset As Integer) As UInteger
            Return (CUInt(data(offset)) << 24) Or
                   (CUInt(data(offset + 1)) << 16) Or
                   (CUInt(data(offset + 2)) << 8) Or
                   CUInt(data(offset + 3))
        End Function

        ' --- Big-endian writes -------------------------------------------------

        Public Shared Function WriteU16BE(value As UShort) As Byte()
            Return New Byte() {
                CByte((value >> 8) And &HFFUS),
                CByte(value And &HFFUS)
            }
        End Function

        Public Shared Sub WriteU16BE(value As UShort, dest As Byte(), offset As Integer)
            dest(offset) = CByte((value >> 8) And &HFFUS)
            dest(offset + 1) = CByte(value And &HFFUS)
        End Sub

        Public Shared Function WriteU32BE(value As UInteger) As Byte()
            Return New Byte() {
                CByte((value >> 24) And &HFFUI),
                CByte((value >> 16) And &HFFUI),
                CByte((value >> 8) And &HFFUI),
                CByte(value And &HFFUI)
            }
        End Function

        Public Shared Sub WriteU32BE(value As UInteger, dest As Byte(), offset As Integer)
            dest(offset) = CByte((value >> 24) And &HFFUI)
            dest(offset + 1) = CByte((value >> 16) And &HFFUI)
            dest(offset + 2) = CByte((value >> 8) And &HFFUI)
            dest(offset + 3) = CByte(value And &HFFUI)
        End Sub

        ' --- Little-endian reads / writes -------------------------------------
        '
        ' Provided as named-endianness alternatives to BitConverter for sites
        ' where the surrounding code mixes BE and LE accesses (e.g. SCP file
        ' format) and the explicit name is clearer than ToUInt16/ToUInt32.

        Public Shared Function ReadU16LE(data As Byte(), offset As Integer) As UShort
            Return CUShort(CInt(data(offset)) Or (CInt(data(offset + 1)) << 8))
        End Function

        Public Shared Function ReadU32LE(data As Byte(), offset As Integer) As UInteger
            Return CUInt(data(offset)) Or
                   (CUInt(data(offset + 1)) << 8) Or
                   (CUInt(data(offset + 2)) << 16) Or
                   (CUInt(data(offset + 3)) << 24)
        End Function

        Public Shared Function WriteU16LE(value As UShort) As Byte()
            Return New Byte() {
                CByte(value And &HFFUS),
                CByte((value >> 8) And &HFFUS)
            }
        End Function

        Public Shared Function WriteU32LE(value As UInteger) As Byte()
            Return New Byte() {
                CByte(value And &HFFUI),
                CByte((value >> 8) And &HFFUI),
                CByte((value >> 16) And &HFFUI),
                CByte((value >> 24) And &HFFUI)
            }
        End Function

    End Class

End Namespace
