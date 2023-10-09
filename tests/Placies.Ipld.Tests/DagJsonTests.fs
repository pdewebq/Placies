module Placies.Ipld.Tests.DagJsonTests

open System.IO
open System.Text
open Ipfs
open Xunit
open Xunit.Abstractions
open Swensen.Unquote

open Placies
open Placies.Multiformats
open Placies.Ipld
open Placies.Ipld.DagJson

type DagJsonTests(output: ITestOutputHelper) =

    static do DagJsonCodec.AddShipyardMulticodec()

    static member GetFixtures(): TheoryData<Fixture> =
        TheoryData<_>() {
            yield! IpldFixtures.read ()
        }

    static member FixturesToSkip = readOnlyDict [
        "float--1e-323", "Too large number"
        "float--8.940696716308594e-8", "Too large number"
        "float-1e-323", "Too large number"
        "float-8.940696716308594e-8", "Too large number"
        "int--11959030306112471732", "Too large number"
        "int-11959030306112471731", "Too large number"
        "int-18446744073709551615", "Too large number"
        "string-𐅑", "Supplementary pair escaping issue"
    ]

    [<SkippableTheory>]
    [<MemberData("GetFixtures")>]
    member _.``Test fixtures reencoding``(fixture: Fixture): unit =
        ( let condition, reason = DagJsonTests.FixturesToSkip.TryGetValue(fixture.Name)
          Skip.If(condition, reason) )

        output.WriteLine($"Fixture '%s{fixture.Name}'")
        let dagJsonCodec = DagJsonCodec(MultiBaseRegistry.CreateDefault())
        for fixtureEntry in fixture.Entries do
            match fixtureEntry.CodecName with
            | "dag-json" ->
                output.WriteLine("Fixture bytes:")
                output.WriteLine(fixtureEntry.DataBytes.ToHexString())
                output.WriteLine("")
                output.WriteLine("Fixture text:")
                output.WriteLine(Encoding.UTF8.GetString(fixtureEntry.DataBytes))
                output.WriteLine("")
                output.WriteLine($"Fixture CID: {fixtureEntry.Cid}")
                output.WriteLine("")
                output.WriteLine("")

                use dataStream = new MemoryStream(fixtureEntry.DataBytes)
                let dataModelNode = (dagJsonCodec :> ICodec).Decode(dataStream) |> Result.getOk

                output.WriteLine("Decoded DataModel node:")
                output.WriteLine($"%A{dataModelNode}")
                output.WriteLine("")

                use reencodedDataStream = new MemoryStream()
                (dagJsonCodec :> ICodec).Encode(reencodedDataStream, dataModelNode)
                let reencodedDataBytes = reencodedDataStream.ToArray()

                output.WriteLine("Reencoded bytes:")
                output.WriteLine(reencodedDataBytes.ToHexString())
                output.WriteLine("")
                output.WriteLine("Reencoded text:")
                output.WriteLine(Encoding.UTF8.GetString(reencodedDataBytes))
                output.WriteLine("")

                let reencodedMultiHash = MultiHash.computeFromBytes reencodedDataBytes MultiHashInfos.Sha2_256
                let reencodedCid = Cid.create 1 (dagJsonCodec :> ICodec).CodecInfo.Code reencodedMultiHash
                output.WriteLine($"Reencoded CID: {reencodedCid}")

                test <@ Cid.encode fixtureEntry.Cid = Cid.encode reencodedCid @>
            | "dag-cbor" ->
                // TODO: Implement
                ()
            | "dag-pb" ->
                // TODO: Implement
                ()
            | _ ->
                failwith $"Not supported codec: {fixtureEntry.CodecName}"
