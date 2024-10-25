namespace Placies

open System
open System.Buffers
open System.IO
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling


type VarIntParser =

    static let rec tryParseFromSpanAsUInt64Core (buffer: ReadOnlySpan<byte>) (value: uint64) (shift: int) (offset: int) (bytesConsumed: int outref) =
        if shift >= 64 then
            Error (InvalidDataException("Varint value is bigger than 9 bytes") :> exn)
        elif offset >= buffer.Length then
            Error (InvalidDataException("Unexpected end of data") :> exn)
        else
            let b = buffer.[offset]
            let offset = offset + 1
            let value = value ||| ((uint64 (b &&& 0x7Fuy)) <<< shift)
            if b < 0x80uy then
                bytesConsumed <- offset
                Ok value
            else
                let shift = shift + 7
                tryParseFromSpanAsUInt64Core buffer value shift offset &bytesConsumed

    static member TryParseFromSpanAsUInt64(buffer: ReadOnlySpan<byte>, bytesConsumed: int outref): Result<uint64, exn> =
        tryParseFromSpanAsUInt64Core buffer 0uL 0 0 &bytesConsumed

    static member TryParseFromSpanAsInt32(buffer: ReadOnlySpan<byte>, bytesConsumed: int outref): Result<int32, exn> =
        VarIntParser.TryParseFromSpanAsUInt64(buffer, &bytesConsumed) |> Result.map int32<uint64>

[<RequireQualifiedAccess>]
module VarInt =

    let private len8tab = [|
        0; 1; 2; 2; 3; 3; 3; 3; 4; 4; 4; 4; 4; 4; 4; 4;
        5; 5; 5; 5; 5; 5; 5; 5; 5; 5; 5; 5; 5; 5; 5; 5;
        6; 6; 6; 6; 6; 6; 6; 6; 6; 6; 6; 6; 6; 6; 6; 6;
        6; 6; 6; 6; 6; 6; 6; 6; 6; 6; 6; 6; 6; 6; 6; 6;
        7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7;
        7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7;
        7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7;
        7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7; 7;
        8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8;
        8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8;
        8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8;
        8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8;
        8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8;
        8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8;
        8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8;
        8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8; 8
    |]

    // Copied from https://github.com/ipld/js-dag-pb/blob/27ed1722e3d1788e40f03186a1dc9b1ba69fd1d2/src/pb-encode.js#L179
    /// golang math/bits, how many bits does it take to represent this integer?
    let private len64 (x: uint64) : int =
        let mutable x = x
        let mutable n = 0
        if x >= uint64 Int32.MaxValue then
            x <- x / uint64 Int32.MaxValue
            n <- 32
        if x >= (1uL <<< 16) then
            x <- x >>> 16
            n <- n + 16
        if x >= (1uL <<< 8) then
            x <- x >>> 8
            n <- n + 8
        n + len8tab.[int x]

    /// Size of varint in bits
    let getSizeOfUInt64 (x: uint64) : int =
        let x = if x % 2uL = 0uL then x + 1uL else x
        (len64 x + 6) / 7

    let getSizeOfInt32 (x: int32) : int =
        getSizeOfUInt64 (uint64<int32> x)

    // ----
    // Read

    let parseFromSpanAsUInt64 (buffer: ReadOnlySpan<byte> byref) : Result<uint64, exn> =
        let res, bytesConsumed = VarIntParser.TryParseFromSpanAsUInt64(buffer)
        buffer <- buffer.Slice(bytesConsumed)
        res

    let parseFromSpanAsInt32 (buffer: ReadOnlySpan<byte> byref) : Result<int32, exn> =
        parseFromSpanAsUInt64 &buffer |> Result.map int32<uint64>

    let parseFromSpanAsUInt64Complete (buffer: ReadOnlySpan<byte>) : Result<uint64, exn> =
        let res, bytesConsumed = VarIntParser.TryParseFromSpanAsUInt64(buffer)
        if bytesConsumed > buffer.Length then
            Error (exn $"Unexpected leftover %i{buffer.Length - bytesConsumed} bytes")
        else
            res

    let parseFromSpanAsInt32Complete (buffer: ReadOnlySpan<byte>) : Result<int32, exn> =
        parseFromSpanAsUInt64Complete buffer |> Result.map int32<uint64>

    let parseFromMemoryAsUInt64 (buffer: ReadOnlyMemory<byte>) : Result<struct(ReadOnlyMemory<byte> * uint64), exn> = result {
        let res, bytesConsumed = VarIntParser.TryParseFromSpanAsUInt64(buffer.Span)
        let! value = res
        let buffer = buffer.Slice(bytesConsumed)
        return struct(buffer, value)
    }

    let parseFromMemoryAsInt32 (buffer: ReadOnlyMemory<byte>) : Result<struct(ReadOnlyMemory<byte> * int32), exn> =
        parseFromMemoryAsUInt64 buffer |> Result.map (fun struct(buffer, value) -> struct(buffer, int32<uint64> value))

    // TODO: Use System.Buffers and/or System.IO.Pipelines
    let readFromStreamAsUInt64Async (stream: Stream) (ct: CancellationToken) : Task<uint64> = task {
        let mutable value: uint64 = 0uL
        let mutable shift: int = 0
        let mutable bytesRead: int = 0
        let buffer: byte array = Array.zeroCreate 1
        let mutable doLoop = true
        while doLoop do
            do! stream.ReadExactlyAsync(buffer, 0, 1, ct)
            bytesRead <- bytesRead + 1
            if bytesRead > 9 then
                raise (InvalidDataException("Varint value is bigger than 9 bytes"))
            let b = buffer.[0]
            value <- value ||| ((uint64 (b &&& 0x7Fuy)) <<< shift)
            if b < 0x80uy then
                doLoop <- false
            else
                shift <- shift + 7
        return value
    }

    // ----
    // Write

    let rec private writeToSpanOfUInt64Core (value: uint64) (buffer: Span<byte>) (i: int) =
        buffer.[i] <-
            if value > 0x7FuL
            then byte (value &&& 0x7FuL) ||| 0x80uy
            else byte (value &&& 0x7FuL)
        let i = i + 1
        let value = value >>> 7
        if value = 0uL then
            i
        else
            writeToSpanOfUInt64Core value buffer i

    let writeToSpanOfUInt64 (value: uint64) (buffer: Span<byte>) : int =
        writeToSpanOfUInt64Core value buffer 0

    let writeToSpanOfInt32 (value: int32) (buffer: Span<byte>) : int =
        writeToSpanOfUInt64 (uint64<int32> value) buffer

    let writeToBufferWriterOfUInt64 (value: uint64) (bufferWriter: IBufferWriter<byte>) : unit =
        let buffer = bufferWriter.GetSpan(10)
        let written = writeToSpanOfUInt64 value buffer
        bufferWriter.Advance(written)

    let writeToBufferWriterOfInt32 (value: int32) (bufferWriter: IBufferWriter<byte>) : unit =
        writeToBufferWriterOfUInt64 (uint64<int32> value) bufferWriter

    // TODO: Use System.Buffers and/or System.IO.Pipelines
    let writeToStreamOfUInt64Async (value: uint64) (stream: Stream) (ct: CancellationToken) : Task = task {
        let mutable value = value
        let bytes: byte array = Array.zeroCreate 10
        let mutable i = 0
        let mutable doLoop = true
        while doLoop do
            let mutable v: byte = byte (value &&& 0x7FuL)
            if value > 0x7FuL then
                v <- v ||| 0x80uy
            bytes.[i] <- v
            i <- i + 1
            value <- value >>> 7
            if value = 0uL then
                doLoop <- false
        return! stream.WriteAsync(bytes, 0, i, ct)
    }

    // ----


[<AutoOpen>]
module StreamExtensions =
    type Stream with
        member this.ReadVarIntAsUInt64Async([<Optional>] ct: CancellationToken): Task<uint64> = task {
            return! VarInt.readFromStreamAsUInt64Async this ct
        }

        member this.ReadVarIntAsInt32(): int32 =
            let i = this.ReadVarIntAsUInt64Async().GetAwaiter().GetResult()
            int32<uint64> i

        member this.WriteVarIntAsync(value: uint64, [<Optional>] ct: CancellationToken): Task = task {
            return! VarInt.writeToStreamOfUInt64Async value this ct
        }

        member this.WriteVarInt(value: int32): unit =
            this.WriteVarIntAsync(uint64<int32> value).GetAwaiter().GetResult()
