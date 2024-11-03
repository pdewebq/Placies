module Placies.Multiformats.Tests.VarIntTests

open System
open System.Buffers
open Swensen.Unquote
open Xunit
open Placies

let varints: (uint64 * byte array) seq = seq {
    1uL, [| 0x01uy |]
    127uL, [| 0x7fuy |]
    128uL, [| 0x80uy; 0x01uy |]
    255uL, [| 0xffuy; 0x01uy |]
    300uL, [| 0xacuy; 0x02uy |]
    16384uL, [| 0x80uy; 0x80uy; 0x01uy |]
    50000000uL, [| 128uy; 225uy; 235uy; 23uy |]
    4503599627370495uL, [| 255uy; 255uy; 255uy; 255uy; 255uy; 255uy; 255uy; 7uy |] // 2^52-1
    4503599627370496uL, [| 128uy; 128uy; 128uy; 128uy; 128uy; 128uy; 128uy; 8uy |] // 2^52
    9007199254740991uL, [| 255uy; 255uy; 255uy; 255uy; 255uy; 255uy; 255uy; 15uy |] // 2^53-1
    9007199254740992uL, [| 128uy; 128uy; 128uy; 128uy; 128uy; 128uy; 128uy; 16uy |] // 2^53
}

type VarIntTests() =

    static member GetVarInts() =
        let data = TheoryData<uint64, byte array>()
        for i, bs in varints do
            data.Add(i, bs)
        data

    [<Theory>]
    [<MemberData("GetVarInts")>]
    member _.``Bytes to int``(number: uint64, bytes: byte array): unit =
        let actualVarintNumber = VarInt.parseFromSpanAsUInt64Complete (ReadOnlySpan(bytes))
        test <@ Ok number = actualVarintNumber @>

    [<Theory>]
    [<MemberData("GetVarInts")>]
    member _.``Int to bytes``(number: uint64, bytes: byte array): unit =
        let bufferWriter = ArrayBufferWriter()
        VarInt.writeToBufferWriterOfUInt64 number bufferWriter
        let actualVarintBytes = bufferWriter.WrittenSpan.ToArray()
        test <@ bytes = actualVarintBytes @>

    [<Theory>]
    [<MemberData("GetVarInts")>]
    member _.``VarInt.getSize``(number: uint64, bytes: byte array): unit =
        let expectedSize = bytes.Length
        let actualSize = VarInt.getSizeOfUInt64 number
        test <@ actualSize = expectedSize @>
