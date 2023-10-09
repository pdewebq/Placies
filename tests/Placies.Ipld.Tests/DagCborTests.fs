module Placies.Ipld.Tests.DagCborTests

open System.IO
open Ipfs
open Xunit
open Xunit.Abstractions
open Swensen.Unquote
open Placies
open Placies.Multiformats
open Placies.Ipld
open Placies.Ipld.DagCbor

type DagCborTests(output: ITestOutputHelper) =

    static member GetFixtures(): TheoryData<Fixture> =
        TheoryData<_>() {
            yield! IpldFixtures.read ()
        }

    static member FixturesToSkip = readOnlyDict [
        "int--11959030306112471732", "Too large number"
        "int-11959030306112471731", "Too large number"
        "int-18446744073709551615", "Too large number"
    ]

    [<SkippableTheory>]
    [<MemberData("GetFixtures")>]
    member _.``Test fixtures reencoding``(fixture: Fixture): unit =
        output.WriteLine($"Fixture '%s{fixture.Name}'")
        ( let condition, reason = DagCborTests.FixturesToSkip.TryGetValue(fixture.Name)
          Skip.If(condition, reason) )

        let dagCborCodec = DagCborCodec()
        for fixtureEntry in fixture.Entries do
            match fixtureEntry.CodecName with
            | "dag-cbor" ->
                output.WriteLine("Fixture bytes:")
                output.WriteLine(fixtureEntry.DataBytes.ToHexString())
                output.WriteLine("")
                output.WriteLine($"Fixture CID: {fixtureEntry.Cid}")
                output.WriteLine("")
                output.WriteLine("")

                use dataStream = new MemoryStream(fixtureEntry.DataBytes)
                let dataModelNode = (dagCborCodec :> ICodec).Decode(dataStream) |> Result.getOk

                output.WriteLine("Decoded DataModel node:")
                output.WriteLine($"%A{dataModelNode}")
                output.WriteLine("")

                use reencodedDataStream = new MemoryStream()
                (dagCborCodec :> ICodec).Encode(reencodedDataStream, dataModelNode)
                let reencodedDataBytes = reencodedDataStream.ToArray()

                output.WriteLine("Reencoded bytes:")
                output.WriteLine(reencodedDataBytes.ToHexString())
                output.WriteLine("")

                let reencodedMultiHash = MultiHash.computeFromBytes reencodedDataBytes MultiHashInfos.Sha2_256
                let reencodedCid = Cid.create 1 (dagCborCodec :> ICodec).CodecInfo.Code reencodedMultiHash
                output.WriteLine($"Reencoded CID: {reencodedCid}")

                test <@ Cid.encode fixtureEntry.Cid = Cid.encode reencodedCid @>
            | "dag-json" ->
                // TODO: Implement
                ()
            | "dag-pb" ->
                // TODO: Implement
                ()
            | _ ->
                failwith $"Not supported codec: {fixtureEntry.CodecName}"
