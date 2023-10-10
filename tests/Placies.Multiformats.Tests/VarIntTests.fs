module Placies.Multiformats.Tests.VarIntTests

open System.IO
open Swensen.Unquote
open Xunit
open Placies

let varints: (int * byte array) seq = seq {
    1, [| 0x01uy |]
    127, [| 0x7fuy |]
    128, [| 0x80uy; 0x01uy |]
    255, [| 0xffuy; 0x01uy |]
    300, [| 0xacuy; 0x02uy |]
    16384, [| 0x80uy; 0x80uy; 0x01uy |]
}

type VarIntTests() =

    static member GetVarInts() =
        let data = TheoryData<int, byte array>()
        for i, bs in varints do
            data.Add(i, bs)
        data

    [<Theory>]
    [<MemberData("GetVarInts")>]
    member _.``Bytes to int``(number: int, bytes: byte array): unit =
        use stream = new MemoryStream(bytes)
        let actualVarintNumber = stream.ReadVarint32()
        test <@ number = actualVarintNumber @>

    [<Theory>]
    [<MemberData("GetVarInts")>]
    member _.``Int to bytes``(number: int, bytes: byte array): unit =
        use stream = new MemoryStream()
        stream.WriteVarint(number)
        let actualVarintBytes = stream.ToArray()
        test <@ bytes = actualVarintBytes @>
