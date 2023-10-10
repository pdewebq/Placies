namespace Placies

open System
open System.IO
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks

[<AutoOpen>]
module StreamExtensions =
    type Stream with
        member this.ReadVarint64Async([<Optional>] ct: CancellationToken): Task<int64> = task {
            let mutable value: int64 = 0
            let mutable shift: int = 0
            let mutable bytesRead: int = 0
            let buffer: byte array = Array.zeroCreate 1
            let mutable doLoop = true
            while doLoop do
                do! this.ReadExactlyAsync(buffer, 0, 1, ct)
                bytesRead <- bytesRead + 1
                if bytesRead > 9 then
                    raise (InvalidDataException("Varint value is bigger than 9 bytes"))
                let b = buffer.[0]
                value <- value ||| ((int64 (b &&& 0x7Fuy)) <<< shift)
                if b < 0x80uy then
                    doLoop <- false
                else
                    shift <- shift + 7
            return value
        }

        member this.ReadVarint32(): int =
            let i = this.ReadVarint64Async().GetAwaiter().GetResult()
            int i

        member this.WriteVarintAsync(value: int64, [<Optional>] ct: CancellationToken): Task = task {
            if value < 0 then
                raise (NotSupportedException("Negative values are not allowed for a Varint"))
            let mutable value = value
            let bytes: byte array = Array.zeroCreate 10
            let mutable i = 0
            let mutable doLoop = true
            while doLoop do
                let mutable v: byte = byte (value &&& 0x7F)
                if value > 0x7F then
                    v <- v ||| 0x80uy
                bytes.[i] <- v
                i <- i + 1
                value <- value >>> 7
                if value = 0 then
                    doLoop <- false
            return! this.WriteAsync(bytes, 0, i, ct)
        }

        member this.WriteVarint(value: int64): unit =
            this.WriteVarintAsync(value).GetAwaiter().GetResult()
