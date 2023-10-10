module Placies.Ipld.Tests.DagCborTests

open System.IO
open Xunit
open Xunit.Abstractions
open Swensen.Unquote
open Placies
open Placies.Multiformats
open Placies.Ipld
open Placies.Ipld.DagCbor

type DagCborTests(output: ITestOutputHelper) =

    static member GetDagCborFixtures(): TheoryData<CodecFixture> =
        TheoryData<_>() {
            yield! IpldFixtures.readCodecFixtures MultiCodecInfos.DagCbor.Name
        }

    static member FixturesToSkip = readOnlyDict [
        "int--11959030306112471732", "Too large number"
        "int-11959030306112471731", "Too large number"
        "int-18446744073709551615", "Too large number"
    ]

    [<SkippableTheory>]
    [<MemberData("GetDagCborFixtures")>]
    member _.``Test fixtures reencoding``(fixture: CodecFixture): unit =
        output.WriteLine($"Fixture '%s{fixture.Name}'")
        ( let condition, reason = DagCborTests.FixturesToSkip.TryGetValue(fixture.Name)
          Skip.If(condition, reason) )

        let dagCborCodec = DagCborCodec()

        output.WriteLine("Fixture bytes:")
        output.WriteLine(fixture.DataBytes.ToHexString())
        output.WriteLine("")
        output.WriteLine($"Fixture CID: {fixture.Cid}")
        output.WriteLine("")
        output.WriteLine("")

        use dataStream = new MemoryStream(fixture.DataBytes)
        let dataModelNode = (dagCborCodec :> ICodec).Decode(dataStream) |> Result.getOk

        output.WriteLine("Decoded DataModel node:")
        output.WriteLine($"%A{dataModelNode}")
        output.WriteLine("")

        use reencodedDataStream = new MemoryStream()
        let reencodedCid = Codec.encodeWithCid dagCborCodec 1 MultiHashInfos.Sha2_256 dataModelNode reencodedDataStream |> ResultExn.getOk
        let reencodedDataBytes = reencodedDataStream.ToArray()

        output.WriteLine("Reencoded bytes:")
        output.WriteLine(reencodedDataBytes.ToHexString())
        output.WriteLine("")
        output.WriteLine($"Reencoded CID: {reencodedCid}")

        test <@ Cid.encode fixture.Cid = Cid.encode reencodedCid @>
